using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

// Draws the dream fog (Hidden/ThatGameJam/DreamFog) over the whole picture after the transparents, while a DreamFog in the scene says
// the fog is in (DreamFog.Weight). Everything but the bird turns into shadows in a warm, glowing fog; the bird is left as it is by the
// stencil BirdVisible writes. Costs nothing while the fog is out.
public class DreamFogFeature : ScriptableRendererFeature
{
    const string ShaderName = "Hidden/ThatGameJam/DreamFog";

    [SerializeField, Tooltip("Hidden/ThatGameJam/DreamFog. Referenced here so it is included in builds.")]
    Shader shader;

    Material m_Material;
    DreamFogPass m_Pass;

    public override void Create()
    {
        if (shader == null)
            shader = Shader.Find(ShaderName);
        if (shader == null)
            return;
        m_Material = CoreUtils.CreateEngineMaterial(shader);
        m_Pass = new DreamFogPass(m_Material) { renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (m_Pass == null || DreamFog.Weight <= 0.001f || renderingData.cameraData.camera != Camera.main)
            return;
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

public class DreamFogPass : ScriptableRenderPass
{
    static readonly ProfilingSampler Sampler = new ProfilingSampler("Dream Fog");
    static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");
    static readonly int BlitScaleBiasId = Shader.PropertyToID("_BlitScaleBias");
    static readonly MaterialPropertyBlock s_Props = new MaterialPropertyBlock();

    class PassData
    {
        public Material material;
        public TextureHandle source;
    }

    readonly Material m_Material;

    public DreamFogPass(Material material)
    {
        m_Material = material;
        // It reads the picture so far, which is impossible from the back buffer.
        requiresIntermediateTexture = true;
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        var resourceData = frameData.Get<UniversalResourceData>();
        if (resourceData.isActiveTargetBackBuffer)
            return;
        TextureHandle color = resourceData.activeColorTexture;

        // A copy of the picture so far, read while the fog is drawn over it.
        TextureDesc desc = renderGraph.GetTextureDesc(color);
        desc.name = "_DreamFogSource";
        desc.clearBuffer = false;
        desc.depthBufferBits = 0;
        desc.msaaSamples = MSAASamples.None;
        TextureHandle source = renderGraph.CreateTexture(desc);
        renderGraph.AddBlitPass(color, source, Vector2.one, Vector2.zero, passName: "Dream Fog Copy");

        using var builder = renderGraph.AddRasterRenderPass<PassData>(Sampler.name, out var data, Sampler);
        data.material = m_Material;
        data.source = source;
        builder.UseTexture(source);
        builder.SetRenderAttachment(color, 0, AccessFlags.Write);
        // For the stencil BirdVisible wrote: the bird's pixels are skipped.
        builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.Read);
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
