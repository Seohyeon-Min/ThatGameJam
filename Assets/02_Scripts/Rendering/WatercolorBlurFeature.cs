using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

// Single full-screen pass for the WatercolorBlur Volume component. Reads the camera colour, writes a pooled
// Render Graph texture and makes it the new camera colour, so there is no copy back.
public class WatercolorBlurFeature : ScriptableRendererFeature
{
    const string ShaderName = "Hidden/ThatGameJam/WatercolorBlur";

    [SerializeField, Tooltip("Hidden/ThatGameJam/WatercolorBlur. Referenced here so it is included in builds.")]
    Shader shader;

    Material m_Material;
    WatercolorBlurPass m_Pass;

    public override void Create()
    {
        if (shader == null)
            shader = Shader.Find(ShaderName);
        if (shader == null)
            return;

        m_Material = CoreUtils.CreateEngineMaterial(shader);
        m_Pass = new WatercolorBlurPass(m_Material)
        {
            // After tonemapping, so the colour threshold works on display colours, and before FXAA/final blit.
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing,
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (m_Pass == null)
            return;

        var volume = VolumeManager.instance.stack.GetComponent<WatercolorBlur>();
        if (volume == null || !volume.IsActive())
            return;

        m_Pass.Setup(volume);
        renderer.EnqueuePass(m_Pass);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(m_Material);
        m_Material = null;
        m_Pass = null;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (shader == null)
            shader = Shader.Find(ShaderName);
    }
#endif
}

public class WatercolorBlurPass : ScriptableRenderPass
{
    const int PassBlur = 0;
    const int PassBlurDepth = 1;

    public static readonly ProfilingSampler Sampler = new ProfilingSampler("Watercolor Bilateral Blur");

    static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");
    static readonly int BlitScaleBiasId = Shader.PropertyToID("_BlitScaleBias");
    static readonly int BlurParamsId = Shader.PropertyToID("_BlurParams");
    static readonly int BlurDepthId = Shader.PropertyToID("_BlurDepth");

    // Copied when the draw is recorded, so each camera keeps its own values on the shared material.
    static readonly MaterialPropertyBlock s_Props = new MaterialPropertyBlock();

    class PassData
    {
        public Material material;
        public int pass;
        public TextureHandle source;
        public Vector4 blurParams;
        public Vector4 blurDepth;
    }

    readonly Material m_Material;
    WatercolorBlur m_Volume;

    public WatercolorBlurPass(Material material)
    {
        m_Material = material;
        // The pass reads the camera colour, which is impossible if it is the back buffer.
        requiresIntermediateTexture = true;
    }

    public void Setup(WatercolorBlur volume)
    {
        m_Volume = volume;
        ConfigureInput(volume.depthAware.value ? ScriptableRenderPassInput.Depth : ScriptableRenderPassInput.None);
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        var resourceData = frameData.Get<UniversalResourceData>();
        var cameraData = frameData.Get<UniversalCameraData>();

        if (cameraData.cameraType == CameraType.Preview || cameraData.cameraType == CameraType.Reflection)
            return;
        if (!cameraData.postProcessEnabled || resourceData.isActiveTargetBackBuffer)
            return;

        int width = cameraData.cameraTargetDescriptor.width;
        int height = cameraData.cameraTargetDescriptor.height;
        // Radius is authored in 1080p pixels so the look matches at any resolution.
        float stepPixels = m_Volume.blurRadius.value * height / 1080f;
        bool useDepth = m_Volume.depthAware.value && resourceData.cameraDepthTexture.IsValid();

        TextureHandle source = resourceData.activeColorTexture;
        var destinationDesc = renderGraph.GetTextureDesc(source);
        destinationDesc.name = "_WatercolorBlurOutput";
        destinationDesc.clearBuffer = false;
        TextureHandle destination = renderGraph.CreateTexture(destinationDesc);

        using (var builder = renderGraph.AddRasterRenderPass<PassData>(Sampler.name, out var data, Sampler))
        {
            data.material = m_Material;
            data.pass = useDepth ? PassBlurDepth : PassBlur;
            data.source = source;
            data.blurParams = new Vector4(stepPixels / width, stepPixels / height, 1f / m_Volume.colorThreshold.value, m_Volume.blurIntensity.value);
            data.blurDepth = new Vector4(m_Volume.depthThreshold.value, 0f, 0f, 0f);

            builder.UseTexture(source);
            // The shader reads the depth through URP's global binding; declaring it keeps the graph ordering right.
            if (useDepth)
                builder.UseTexture(resourceData.cameraDepthTexture);
            builder.SetRenderAttachment(destination, 0, AccessFlags.WriteAll);
            builder.SetRenderFunc(static (PassData d, RasterGraphContext ctx) =>
            {
                var props = s_Props;
                props.Clear();
                props.SetTexture(BlitTextureId, (Texture)d.source);
                props.SetVector(BlitScaleBiasId, new Vector4(1f, 1f, 0f, 0f));
                props.SetVector(BlurParamsId, d.blurParams);
                props.SetVector(BlurDepthId, d.blurDepth);
                ctx.cmd.DrawProcedural(Matrix4x4.identity, d.material, d.pass, MeshTopology.Triangles, 3, 1, props);
            });
        }

        // Later passes (FXAA, final blit) read the blurred image instead of copying it back.
        resourceData.cameraColor = destination;
    }
}
