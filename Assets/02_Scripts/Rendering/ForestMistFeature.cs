using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

// Draws the forest mist (Hidden/ThatGameJam/ForestMist) over the picture before the post-processing, while a ForestMist in the scene says
// the fog is in (ForestMist.Weight). Costs nothing while the fog is out.
public class ForestMistFeature : ScriptableRendererFeature
{
    const string ShaderName = "Hidden/ThatGameJam/ForestMist";

    [SerializeField, Tooltip("Hidden/ThatGameJam/ForestMist. Referenced here so it is included in builds.")]
    Shader shader;

    Material m_Material;
    ForestMistPass m_Pass;

    public override void Create()
    {
        if (shader == null)
            shader = Shader.Find(ShaderName);
        if (shader == null)
            return;
        m_Material = CoreUtils.CreateEngineMaterial(shader);
        m_Pass = new ForestMistPass { renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        Camera camera = renderingData.cameraData.camera;
        bool shown = ForestMist.Weight > 0.001f && camera == Camera.main;
        if (m_Pass == null || !(shown || ShaderPrewarm.Warms(camera)))
            return;
        m_Pass.unseen = !shown;
        // The scene's ForestMist Look holds the values; without one, the shader's defaults.
        m_Pass.material = ForestMist.Look != null ? ForestMist.Look : m_Material;
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

public class ForestMistPass : ScriptableRenderPass
{
    static readonly ProfilingSampler Sampler = new ProfilingSampler("Forest Mist");
    static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");
    static readonly int BlitScaleBiasId = Shader.PropertyToID("_BlitScaleBias");
    static readonly MaterialPropertyBlock s_Props = new MaterialPropertyBlock();

    class PassData
    {
        public Material material;
        public TextureHandle source;
    }

    public Material material;
    // Drawn into a texture nothing shows, only so its shader is ready before the mist first comes in (ShaderPrewarm).
    public bool unseen;

    public ForestMistPass()
    {
        // It reads the picture so far, which is impossible from the back buffer.
        requiresIntermediateTexture = true;
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        var resourceData = frameData.Get<UniversalResourceData>();
        if (resourceData.isActiveTargetBackBuffer || material == null)
            return;
        TextureHandle color = resourceData.activeColorTexture;

        // A copy of the picture so far, read while the mist is drawn over it.
        TextureDesc desc = renderGraph.GetTextureDesc(color);
        desc.name = "_ForestMistSource";
        desc.clearBuffer = false;
        desc.depthBufferBits = 0;
        desc.msaaSamples = MSAASamples.None;
        TextureHandle source = renderGraph.CreateTexture(desc);
        renderGraph.AddBlitPass(color, source, Vector2.one, Vector2.zero, passName: "Forest Mist Copy");

        using var builder = renderGraph.AddRasterRenderPass<PassData>(Sampler.name, out var data, Sampler);
        data.material = material;
        data.source = source;
        builder.UseTexture(source);
        if (unseen)
        {
            desc.name = "_ForestMistUnseen";
            builder.SetRenderAttachment(renderGraph.CreateTexture(desc), 0, AccessFlags.Write);
            builder.AllowPassCulling(false);
        }
        else
            builder.SetRenderAttachment(color, 0, AccessFlags.Write);
        if (resourceData.cameraDepthTexture.IsValid())
            builder.UseTexture(resourceData.cameraDepthTexture);
        builder.UseAllGlobalTextures(true);
        builder.SetRenderFunc(static (PassData d, RasterGraphContext ctx) =>
        {
            s_Props.Clear();
            s_Props.SetTexture(BlitTextureId, (Texture)d.source);
            s_Props.SetVector(BlitScaleBiasId, new Vector4(1f, 1f, 0f, 0f));
            ctx.cmd.DrawProcedural(Matrix4x4.identity, d.material, 0, MeshTopology.Triangles, 3, 1, s_Props);
        });
    }
}
