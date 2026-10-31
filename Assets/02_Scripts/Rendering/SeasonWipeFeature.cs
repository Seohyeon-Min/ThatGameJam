using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

// Lets a new season's look spread out from the spring ring (WatercolorSeasonLooks). Before the look changes, a temporary camera
// renders the background alone, without the baby and the bird, and its opaque picture is kept as a photo. While the ring plays,
// that photo covers everything outside it, so the look only seems to change inside the ring; the baby and the bird are drawn
// again on top, live. Costs one extra scene render at the start and two draws per frame during the transition, nothing otherwise.
public class SeasonWipeFeature : ScriptableRendererFeature
{
    const string ShaderName = "Hidden/ThatGameJam/SeasonWipe";

    [SerializeField, Tooltip("Hidden/ThatGameJam/SeasonWipe. Referenced here so it is included in builds.")]
    Shader shader;
    [SerializeField, Tooltip("Softness of the ring's edge between the two looks, in world units.")]
    float edgeSoftness = 0.6f;
    [SerializeField, Tooltip("How far the edge pushes in and out into lumps, in world units.")]
    float lumpHeight = 3f;
    [SerializeField, Tooltip("Size of the lumps, in world units.")]
    float lumpSize = 2.5f;
    [SerializeField, Tooltip("Ring radius where whatever is left of the old look starts fading out.")]
    float fadeStart = 26f;
    [SerializeField, Tooltip("Ring radius where the old look is gone. Keep it inside the ring's Max Radius.")]
    float fadeEnd = 38f;

    Material m_Material;
    SeasonWipePass m_Pass;

    public override void Create()
    {
        if (shader == null)
            shader = Shader.Find(ShaderName);
        if (shader == null)
            return;

        m_Material = CoreUtils.CreateEngineMaterial(shader);
        m_Pass = new SeasonWipePass(m_Material)
        {
            // After the opaques and the sky, before snow, wisps and the ring.
            renderPassEvent = RenderPassEvent.BeforeRenderingTransparents,
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (m_Pass == null || !SeasonWipe.Busy)
            return;
        m_Pass.Setup(new Vector4(edgeSoftness, lumpHeight, lumpSize, 0f), new Vector4(fadeStart, fadeEnd, 0f, 0f));
        renderer.EnqueuePass(m_Pass);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(m_Material);
        m_Material = null;
        m_Pass = null;
        SeasonWipe.Release();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (shader == null)
            shader = Shader.Find(ShaderName);
    }
#endif
}

// What the look switcher and the render pass share.
public static class SeasonWipe
{
    static readonly int SpringRingId = Shader.PropertyToID("_SpringRing");

    public static RTHandle Photo;
    public static Matrix4x4 PhotoViewProjection;
    public static Camera CaptureCamera;
    public static bool Captured;
    // Drawn again over the photo. Set by Capture.
    public static Renderer[] Live = { };

    public static bool RingPlaying => Shader.GetGlobalVector(SpringRingId).w > 0f;
    public static bool Busy => CaptureCamera != null || (Captured && RingPlaying);

    // Renders the background now, in the current look, with these renderers hidden. False if no photo came, which means the
    // renderer has no SeasonWipeFeature.
    public static bool Capture(Camera view, Renderer[] hidden)
    {
        Release();
        Live = hidden;
        var wasEnabled = new bool[hidden.Length];
        for (int i = 0; i < hidden.Length; i++)
        {
            wasEnabled[i] = hidden[i].enabled;
            hidden[i].enabled = false;
        }

        var cameraObject = new GameObject("SeasonWipeCamera") { hideFlags = HideFlags.HideAndDontSave };
        var capture = cameraObject.AddComponent<Camera>();
        capture.CopyFrom(view);
        capture.enabled = false;
        capture.GetUniversalAdditionalCameraData().renderPostProcessing = false;
        var target = RenderTexture.GetTemporary(view.pixelWidth, view.pixelHeight, 24, RenderTextureFormat.DefaultHDR);
        capture.targetTexture = target;
        CaptureCamera = capture;
        capture.Render();
        CaptureCamera = null;
        capture.targetTexture = null;
        RenderTexture.ReleaseTemporary(target);
        Object.Destroy(cameraObject);

        for (int i = 0; i < hidden.Length; i++)
            hidden[i].enabled = wasEnabled[i];
        return Captured;
    }

    public static void Release()
    {
        Photo?.Release();
        Photo = null;
        Captured = false;
        Live = System.Array.Empty<Renderer>();
    }
}

public class SeasonWipePass : ScriptableRenderPass
{
    static readonly ProfilingSampler Sampler = new ProfilingSampler("Season Wipe");
    static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");
    static readonly int BlitScaleBiasId = Shader.PropertyToID("_BlitScaleBias");
    static readonly int ViewProjectionId = Shader.PropertyToID("_SeasonWipeVP");
    static readonly int WipeId = Shader.PropertyToID("_SeasonWipe");
    static readonly int FadeId = Shader.PropertyToID("_SeasonWipeFade");
    static readonly MaterialPropertyBlock s_Props = new MaterialPropertyBlock();

    class PassData
    {
        public Material material;
        public TextureHandle photo;
        public Matrix4x4 viewProjection;
        public Vector4 wipe;
        public Vector4 fade;
        public Renderer[] live;
    }

    readonly Material m_Material;
    Vector4 m_Wipe;
    Vector4 m_Fade;

    public SeasonWipePass(Material material)
    {
        m_Material = material;
        // The photo is read from the camera colour, which is impossible if it is the back buffer.
        requiresIntermediateTexture = true;
    }

    public void Setup(Vector4 wipe, Vector4 fade)
    {
        m_Wipe = wipe;
        m_Fade = fade;
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        var resourceData = frameData.Get<UniversalResourceData>();
        var cameraData = frameData.Get<UniversalCameraData>();
        if (resourceData.isActiveTargetBackBuffer)
            return;

        TextureHandle color = resourceData.activeColorTexture;
        if (cameraData.camera == SeasonWipe.CaptureCamera)
        {
            RenderTextureDescriptor descriptor = cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;
            RenderingUtils.ReAllocateHandleIfNeeded(ref SeasonWipe.Photo, descriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_SeasonWipePhoto");
            renderGraph.AddBlitPass(color, renderGraph.ImportTexture(SeasonWipe.Photo), Vector2.one, Vector2.zero, passName: "Season Wipe Photo");
            // True: this pass only runs on an intermediate texture, never the back buffer.
            SeasonWipe.PhotoViewProjection = GL.GetGPUProjectionMatrix(cameraData.GetProjectionMatrix(), true) * cameraData.GetViewMatrix();
            SeasonWipe.Captured = true;
            return;
        }

        if (SeasonWipe.Photo == null || cameraData.camera != Camera.main)
            return;

        using var builder = renderGraph.AddRasterRenderPass<PassData>(Sampler.name, out var data, Sampler);
        data.material = m_Material;
        data.photo = renderGraph.ImportTexture(SeasonWipe.Photo);
        data.viewProjection = SeasonWipe.PhotoViewProjection;
        data.wipe = m_Wipe;
        data.fade = m_Fade;
        data.live = SeasonWipe.Live;
        builder.UseTexture(data.photo);
        builder.SetRenderAttachment(color, 0, AccessFlags.ReadWrite);
        builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.ReadWrite);
        // The live characters read the shadow map and the other globals their materials use.
        builder.UseAllGlobalTextures(true);
        builder.SetRenderFunc(static (PassData d, RasterGraphContext ctx) =>
        {
            s_Props.Clear();
            s_Props.SetTexture(BlitTextureId, (Texture)d.photo);
            s_Props.SetVector(BlitScaleBiasId, new Vector4(1f, 1f, 0f, 0f));
            s_Props.SetMatrix(ViewProjectionId, d.viewProjection);
            s_Props.SetVector(WipeId, d.wipe);
            s_Props.SetVector(FadeId, d.fade);
            ctx.cmd.DrawProcedural(Matrix4x4.identity, d.material, 0, MeshTopology.Triangles, 3, 1, s_Props);
            DrawLive(ctx.cmd, d.live);
        });
    }

    // Particles are left out: they're transparents, drawn later over the photo anyway.
    static void DrawLive(RasterCommandBuffer cmd, Renderer[] live)
    {
        foreach (Renderer renderer in live)
        {
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                continue;
            if (renderer is not MeshRenderer && renderer is not SkinnedMeshRenderer)
                continue;
            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] == null)
                    continue;
                int pass = materials[i].FindPass("ForwardLit");
                cmd.DrawRenderer(renderer, materials[i], i, pass >= 0 ? pass : 0);
            }
        }
    }
}
