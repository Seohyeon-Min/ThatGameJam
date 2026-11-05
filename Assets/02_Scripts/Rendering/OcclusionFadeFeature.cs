using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

// Smooths the camera occlusion fade: trees standing in front of the bird or the baby drop pixels in an even pattern
// (WatercolorFlat's CameraFadeClip), and this blends that window before the post-processing, so they read as see-through instead
// of speckled. Only where pixels were actually dropped: the flowers, grass and ground inside the window don't fade, so they stay
// sharp. The bird (stencil bit 64) and the baby (bit 128) are left as they are. Game and Scene view both.
public class OcclusionFadeFeature : ScriptableRendererFeature
{
    const string ShaderName = "Hidden/ThatGameJam/OcclusionFade";

    [SerializeField, Tooltip("Hidden/ThatGameJam/OcclusionFade. Referenced here so it is included in builds.")]
    Shader shader;
    [SerializeField, Tooltip("The trees' Fade Radius (WatercolorFlat), in metres: where the window is fully see-through.")]
    float fadeRadius = 2f;
    [SerializeField, Tooltip("The trees' Fade Softness (WatercolorFlat), in metres: how far past it the window eases back.")]
    float fadeSoftness = 1.5f;

    Material m_Material;
    OcclusionFadePass m_Pass;

    public override void Create()
    {
        if (shader == null)
            shader = Shader.Find(ShaderName);
        if (shader == null)
            return;

        m_Material = CoreUtils.CreateEngineMaterial(shader);
        m_Pass = new OcclusionFadePass(m_Material) { renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (m_Pass == null)
            return;
        CameraType type = renderingData.cameraData.cameraType;
        if (type != CameraType.Game && type != CameraType.SceneView)
            return;
        m_Pass.Setup(new Vector4(fadeRadius, fadeSoftness, 0f, 0f));
        m_Pass.ConfigureInput(ScriptableRenderPassInput.Depth);
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

public class OcclusionFadePass : ScriptableRenderPass
{
    public static readonly ProfilingSampler Sampler = new ProfilingSampler("Occlusion Fade");

    const int PassCopy = 0;
    const int PassResolve = 1;
    const int PassDropped = 2;

    static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");
    static readonly int BlitScaleBiasId = Shader.PropertyToID("_BlitScaleBias");
    static readonly int OcclusionFadeId = Shader.PropertyToID("_OcclusionFade");
    static readonly int Target0Id = Shader.PropertyToID("_WC_OcclusionTarget0");
    static readonly int Target1Id = Shader.PropertyToID("_WC_OcclusionTarget1");
    static readonly int DepthBufferId = Shader.PropertyToID("_OcclusionDepthBuffer");
    static readonly int DroppedId = Shader.PropertyToID("_OcclusionDropped");

    // Copied when the draw is recorded, so each camera keeps its own values on the shared material.
    static readonly MaterialPropertyBlock s_Props = new MaterialPropertyBlock();

    class PassData
    {
        public Material material;
        public TextureHandle source;
        public TextureHandle dropped;
        public Vector4 fade;
    }

    class DroppedData
    {
        public Material material;
        public TextureHandle depthBuffer;
    }

    readonly Material m_Material;
    Vector4 m_Fade;

    public OcclusionFadePass(Material material)
    {
        m_Material = material;
        // The pass reads the camera colour, which is impossible if it is the back buffer.
        requiresIntermediateTexture = true;
    }

    public void Setup(Vector4 fade) => m_Fade = fade;

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        var resourceData = frameData.Get<UniversalResourceData>();
        if (resourceData.isActiveTargetBackBuffer || !resourceData.cameraDepthTexture.IsValid() || !resourceData.activeDepthTexture.IsValid())
            return;
        // Nothing to smooth while neither the bird nor the baby is there.
        if (Shader.GetGlobalVector(Target0Id).w < 0.5f && Shader.GetGlobalVector(Target1Id).w < 0.5f)
            return;

        TextureHandle source = resourceData.activeColorTexture;

        // Where the trees' pixels were dropped: the camera's depth buffer (cut) lies farther than the depth texture (whole trees).
        var droppedDesc = renderGraph.GetTextureDesc(source);
        droppedDesc.name = "_OcclusionDropped";
        droppedDesc.format = UnityEngine.Experimental.Rendering.GraphicsFormat.R8_UNorm;
        droppedDesc.clearBuffer = false;
        TextureHandle dropped = renderGraph.CreateTexture(droppedDesc);
        using (var droppedBuilder = renderGraph.AddRasterRenderPass<DroppedData>("Occlusion Fade Dropped", out var droppedData))
        {
            droppedData.material = m_Material;
            droppedData.depthBuffer = resourceData.activeDepthTexture;
            droppedBuilder.UseTexture(resourceData.activeDepthTexture);
            // Without a depth prepass the two are the same texture: nothing reads as dropped then.
            if (!resourceData.cameraDepthTexture.Equals(resourceData.activeDepthTexture))
                droppedBuilder.UseTexture(resourceData.cameraDepthTexture);
            droppedBuilder.SetRenderAttachment(dropped, 0, AccessFlags.WriteAll);
            droppedBuilder.SetRenderFunc(static (DroppedData d, RasterGraphContext ctx) =>
            {
                var props = s_Props;
                props.Clear();
                props.SetTexture(DepthBufferId, (Texture)d.depthBuffer);
                props.SetVector(BlitScaleBiasId, new Vector4(1f, 1f, 0f, 0f));
                ctx.cmd.DrawProcedural(Matrix4x4.identity, d.material, PassDropped, MeshTopology.Triangles, 3, 1, props);
            });
        }

        var destinationDesc = renderGraph.GetTextureDesc(source);
        destinationDesc.name = "_OcclusionFadeOutput";
        destinationDesc.clearBuffer = false;
        TextureHandle destination = renderGraph.CreateTexture(destinationDesc);

        using (var builder = renderGraph.AddRasterRenderPass<PassData>(Sampler.name, out var data, Sampler))
        {
            data.material = m_Material;
            data.source = source;
            data.dropped = dropped;
            data.fade = m_Fade;
            builder.UseTexture(source);
            builder.UseTexture(dropped);
            // The shader reads the depth through URP's global binding; declaring it keeps the graph ordering right.
            builder.UseTexture(resourceData.cameraDepthTexture);
            builder.SetRenderAttachment(destination, 0, AccessFlags.WriteAll);
            // The camera depth-stencil, read only, so the bird and the baby are left out.
            builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.Read);
            builder.SetRenderFunc(static (PassData d, RasterGraphContext ctx) =>
            {
                var props = s_Props;
                props.Clear();
                props.SetTexture(BlitTextureId, (Texture)d.source);
                props.SetTexture(DroppedId, (Texture)d.dropped);
                props.SetVector(BlitScaleBiasId, new Vector4(1f, 1f, 0f, 0f));
                props.SetVector(OcclusionFadeId, d.fade);
                ctx.cmd.DrawProcedural(Matrix4x4.identity, d.material, PassCopy, MeshTopology.Triangles, 3, 1, props);
                ctx.cmd.DrawProcedural(Matrix4x4.identity, d.material, PassResolve, MeshTopology.Triangles, 3, 1, props);
            });
        }

        // Later passes read the smoothed picture instead of copying it back.
        resourceData.cameraColor = destination;
    }
}
