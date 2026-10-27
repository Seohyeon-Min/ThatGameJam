using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public enum WatercolorQualityPreset { Low, Medium, High, Custom }
public enum WatercolorResolution { Full, Half }
public enum WatercolorSampling { Low, Medium, High }
public enum WatercolorSectors { Four = 4, Eight = 8 }

// Screen-space watercolor filter (Anisotropic Kuwahara + watercolor stylisation) for URP Render Graph.
// Art controls come from the WatercolorKuwahara Volume component; this feature only holds performance settings,
// so each renderer (PC / Mobile-for-WebGL) can pick its own cost.
public class WatercolorKuwaharaFeature : ScriptableRendererFeature
{
    const string ShaderName = "Hidden/ThatGameJam/WatercolorKuwahara";

    [Serializable]
    public class Settings
    {
        [Tooltip("All presets run at half resolution; full resolution costs about 16x more (11.8 ms vs 0.7 ms at 1080p on an RTX 3050 Ti Laptop). Low: 2 px sampling, 4 sectors. Medium: 1.5 px sampling, 8 sectors. High: 1 px sampling, 8 sectors. Custom: use the fields below.")]
        public WatercolorQualityPreset quality = WatercolorQualityPreset.Medium;

        [Tooltip("Custom only. Resolution the structure tensor and Kuwahara passes run at. Full is about 16x the cost of Half, because the radius in working pixels doubles as well as the pixel count.")]
        public WatercolorResolution renderResolution = WatercolorResolution.Half;

        [Tooltip("Custom only. Spacing of the kernel sample grid. High = 1 px, Medium = 1.5 px, Low = 2 px. Wider spacing reads fewer texels.")]
        public WatercolorSampling samplingQuality = WatercolorSampling.High;

        [Tooltip("Custom only. Directional sectors in the anisotropic kernel. 8 follows curves better; 4 is about half the ALU cost.")]
        public WatercolorSectors sectorCount = WatercolorSectors.Eight;

        [Tooltip("Also detect edges from the normals texture. Catches creases at the same depth (folds, faces) but forces a depth-normals prepass.")]
        public bool useNormalsForEdges = false;
    }

    public Settings settings = new Settings();

    [SerializeField, Tooltip("Hidden/ThatGameJam/WatercolorKuwahara. Referenced here so it is included in builds.")]
    Shader shader;

    Material m_Material;
    Texture2D m_Noise;
    WatercolorKuwaharaPass m_Pass;

    public override void Create()
    {
        if (shader == null)
            shader = Shader.Find(ShaderName);
        if (shader == null)
            return;

        m_Material = CoreUtils.CreateEngineMaterial(shader);
        m_Noise = CreateNoiseTexture();
        m_Pass = new WatercolorKuwaharaPass(m_Material, m_Noise)
        {
            // After tonemapping, so the variance test sees display colours, and before FXAA/final blit.
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing,
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (m_Pass == null)
            return;

        var volume = VolumeManager.instance.stack.GetComponent<WatercolorKuwahara>();
        if (volume == null || !volume.IsActive())
            return;

        m_Pass.Setup(settings, volume);
        renderer.EnqueuePass(m_Pass);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(m_Material);
        CoreUtils.Destroy(m_Noise);
        m_Material = null;
        m_Noise = null;
        m_Pass = null;
    }

    // Independent random value per texel in each channel. Sampled bilinearly with repeat wrapping it is tileable
    // value noise whose lattice cell is one texel. Fixed seed, so the pattern is identical every run.
    static Texture2D CreateNoiseTexture()
    {
        const int size = 256;
        var random = new System.Random(1842);
        var pixels = new Color32[size * size];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new Color32((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256));

        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
        {
            name = "WatercolorNoise",
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear,
        };
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (shader == null)
            shader = Shader.Find(ShaderName);
    }
#endif
}

public class WatercolorKuwaharaPass : ScriptableRenderPass
{
    // Pass indices in WatercolorKuwahara.shader.
    const int PassTensor = 0;
    const int PassTensorNormals = 1;
    const int PassBlurH = 2;
    const int PassBlurV = 3;
    const int PassClassic = 4;
    const int PassAnisotropic8 = 5;
    const int PassAnisotropic4 = 6;
    const int PassComposite = 7;

    // Upper bound on the half-width of the sample grid, so a large radius at full resolution cannot stall the GPU.
    const int MaxGridExtent = 16;

    // Public so profiling tools can read GPU time per stage.
    public static readonly ProfilingSampler TensorSampler = new ProfilingSampler("Watercolor Structure Tensor");
    public static readonly ProfilingSampler BlurHSampler = new ProfilingSampler("Watercolor Tensor Blur H");
    public static readonly ProfilingSampler BlurVSampler = new ProfilingSampler("Watercolor Tensor Blur V");
    public static readonly ProfilingSampler KuwaharaSampler = new ProfilingSampler("Watercolor Kuwahara");
    public static readonly ProfilingSampler CompositeSampler = new ProfilingSampler("Watercolor Composite");

    static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");
    static readonly int BlitScaleBiasId = Shader.PropertyToID("_BlitScaleBias");
    static readonly int OrientTexId = Shader.PropertyToID("_OrientTex");
    static readonly int FilteredTexId = Shader.PropertyToID("_FilteredTex");
    static readonly int PaperTexId = Shader.PropertyToID("_PaperTex");
    static readonly int NoiseTexId = Shader.PropertyToID("_NoiseTex");
    static readonly int WorkSizeId = Shader.PropertyToID("_WorkSize");
    static readonly int SourceSizeId = Shader.PropertyToID("_SourceSize");
    static readonly int KuwaharaId = Shader.PropertyToID("_Kuwahara");
    static readonly int Kuwahara2Id = Shader.PropertyToID("_Kuwahara2");
    static readonly int StyleId = Shader.PropertyToID("_Style");
    static readonly int Style2Id = Shader.PropertyToID("_Style2");
    static readonly int Style3Id = Shader.PropertyToID("_Style3");

    // Property blocks are copied when the draw is recorded, so each draw keeps its own values even though
    // the material is shared by every camera.
    static readonly MaterialPropertyBlock s_Props = new MaterialPropertyBlock();

    struct ShaderParams
    {
        public Vector4 workSize, sourceSize, kuwahara, kuwahara2, style, style2, style3;
        public Texture paper;
        public Texture noise;
    }

    class PassData
    {
        public Material material;
        public int pass;
        public ShaderParams p;
        public TextureHandle input;
        public TextureHandle orient;
        public TextureHandle filtered;
    }

    readonly Material m_Material;
    readonly Texture2D m_Noise;
    WatercolorKuwaharaFeature.Settings m_Settings;
    WatercolorKuwahara m_Volume;

    public WatercolorKuwaharaPass(Material material, Texture2D noise)
    {
        m_Material = material;
        m_Noise = noise;
        // The pass reads the camera colour, which is impossible if it is the back buffer.
        requiresIntermediateTexture = true;
    }

    public void Setup(WatercolorKuwaharaFeature.Settings settings, WatercolorKuwahara volume)
    {
        m_Settings = settings;
        m_Volume = volume;
        var input = ScriptableRenderPassInput.Depth;
        if (settings.useNormalsForEdges)
            input |= ScriptableRenderPassInput.Normal;
        ConfigureInput(input);
    }

    void ResolveQuality(out bool half, out float spacing, out int sectors)
    {
        switch (m_Settings.quality)
        {
            case WatercolorQualityPreset.Low:
                half = true; spacing = 2f; sectors = 4;
                return;
            case WatercolorQualityPreset.Medium:
                half = true; spacing = 1.5f; sectors = 8;
                return;
            case WatercolorQualityPreset.High:
                half = true; spacing = 1f; sectors = 8;
                return;
            default:
                half = m_Settings.renderResolution == WatercolorResolution.Half;
                spacing = m_Settings.samplingQuality switch
                {
                    WatercolorSampling.Low => 2f,
                    WatercolorSampling.Medium => 1.5f,
                    _ => 1f,
                };
                sectors = (int)m_Settings.sectorCount;
                return;
        }
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        var resourceData = frameData.Get<UniversalResourceData>();
        var cameraData = frameData.Get<UniversalCameraData>();

        if (cameraData.cameraType == CameraType.Preview || cameraData.cameraType == CameraType.Reflection)
            return;
        if (!cameraData.postProcessEnabled || resourceData.isActiveTargetBackBuffer)
            return;

        ResolveQuality(out bool half, out float spacing, out int sectors);

        int sourceWidth = cameraData.cameraTargetDescriptor.width;
        int sourceHeight = cameraData.cameraTargetDescriptor.height;
        int workWidth = half ? (sourceWidth + 1) / 2 : sourceWidth;
        int workHeight = half ? (sourceHeight + 1) / 2 : sourceHeight;

        ShaderParams p = BuildParams(sourceWidth, sourceHeight, workWidth, workHeight, spacing, sectors);

        TextureHandle source = resourceData.activeColorTexture;
        TextureDesc sourceDesc = renderGraph.GetTextureDesc(source);

        // Render Graph pools these by descriptor, so nothing is allocated per frame once sizes settle.
        TextureHandle tensorRaw = renderGraph.CreateTexture(WorkDesc(workWidth, workHeight, GraphicsFormat.R16G16B16A16_SFloat, "_WatercolorTensor"));
        TextureHandle tensorBlur = renderGraph.CreateTexture(WorkDesc(workWidth, workHeight, GraphicsFormat.R16G16B16A16_SFloat, "_WatercolorTensorBlur"));
        TextureHandle orient = renderGraph.CreateTexture(WorkDesc(workWidth, workHeight, GraphicsFormat.R16G16B16A16_SFloat, "_WatercolorOrient"));
        TextureHandle filtered = renderGraph.CreateTexture(WorkDesc(workWidth, workHeight, sourceDesc.format, "_WatercolorFiltered"));

        var destinationDesc = sourceDesc;
        destinationDesc.name = "_WatercolorOutput";
        destinationDesc.clearBuffer = false;
        TextureHandle destination = renderGraph.CreateTexture(destinationDesc);

        bool useNormals = m_Settings.useNormalsForEdges && resourceData.cameraNormalsTexture.IsValid();
        int kuwaharaPass = m_Volume.filterMode.value == WatercolorFilterMode.Classic ? PassClassic
            : sectors == 4 ? PassAnisotropic4 : PassAnisotropic8;

        // Structure tensor and geometric edges, computed once and shared by the Kuwahara and composite passes.
        using (var builder = renderGraph.AddRasterRenderPass<PassData>(TensorSampler.name, out var data, TensorSampler))
        {
            Fill(data, useNormals ? PassTensorNormals : PassTensor, p, source);
            builder.UseTexture(source);
            // The shader reads these through URP's global bindings; declaring them keeps the graph ordering right.
            if (resourceData.cameraDepthTexture.IsValid())
                builder.UseTexture(resourceData.cameraDepthTexture);
            if (useNormals)
                builder.UseTexture(resourceData.cameraNormalsTexture);
            builder.SetRenderAttachment(tensorRaw, 0, AccessFlags.WriteAll);
            builder.SetRenderFunc(static (PassData d, RasterGraphContext ctx) => Draw(d, ctx.cmd));
        }

        AddSimplePass(renderGraph, BlurHSampler, PassBlurH, p, tensorRaw, tensorBlur);
        AddSimplePass(renderGraph, BlurVSampler, PassBlurV, p, tensorBlur, orient);

        using (var builder = renderGraph.AddRasterRenderPass<PassData>(KuwaharaSampler.name, out var data, KuwaharaSampler))
        {
            Fill(data, kuwaharaPass, p, source);
            data.orient = orient;
            builder.UseTexture(source);
            builder.UseTexture(orient);
            builder.SetRenderAttachment(filtered, 0, AccessFlags.WriteAll);
            builder.SetRenderFunc(static (PassData d, RasterGraphContext ctx) => Draw(d, ctx.cmd));
        }

        using (var builder = renderGraph.AddRasterRenderPass<PassData>(CompositeSampler.name, out var data, CompositeSampler))
        {
            Fill(data, PassComposite, p, source);
            data.orient = orient;
            data.filtered = filtered;
            builder.UseTexture(source);
            builder.UseTexture(orient);
            builder.UseTexture(filtered);
            builder.SetRenderAttachment(destination, 0, AccessFlags.WriteAll);
            builder.SetRenderFunc(static (PassData d, RasterGraphContext ctx) => Draw(d, ctx.cmd));
        }

        // Later passes (FXAA, final blit) read our output instead of copying it back.
        resourceData.cameraColor = destination;
    }

    ShaderParams BuildParams(int sourceWidth, int sourceHeight, int workWidth, int workHeight, float spacing, int sectors)
    {
        var v = m_Volume;

        // Radius is authored in 1080p pixels; convert to working-resolution pixels.
        float radius = Mathf.Max(1f, v.filterRadius.value * workHeight / 1080f);
        int extent = Mathf.Clamp(Mathf.CeilToInt(radius / spacing), 1, MaxGridExtent);

        // Polynomial sector weights (Kyprianidis et al. 2010), same constants as Blender's compositor.
        float zeta = 2f / radius;
        float envelope = 1.5f * Mathf.PI / sectors;
        float sin = Mathf.Sin(envelope);
        float eta = (zeta + Mathf.Cos(envelope)) / (sin * sin);

        float sourceScale = sourceHeight / 1080f;
        float bleedPixels = v.pigmentBleeding.value * v.filterRadius.value * 0.6f;

        Texture paper = v.paperTexture.value;
        return new ShaderParams
        {
            workSize = new Vector4(workWidth, workHeight, 1f / workWidth, 1f / workHeight),
            sourceSize = new Vector4(sourceWidth, sourceHeight, 1f / sourceWidth, 1f / sourceHeight),
            kuwahara = new Vector4(radius, spacing, v.anisotropyStrength.value, v.sectorSharpness.value),
            kuwahara2 = new Vector4(zeta, eta, extent, v.edgePreservation.value),
            style = new Vector4(v.filterIntensity.value, v.colorSimplification.value, v.pigmentVariation.value, bleedPixels),
            style2 = new Vector4(v.edgeDarkening.value, v.paperTextureStrength.value, v.paperTextureScale.value, v.brushTextureStrength.value),
            style3 = new Vector4(paper != null ? 1f : 0f, sourceScale, 0f, 0f),
            paper = paper,
            noise = m_Noise,
        };
    }

    static TextureDesc WorkDesc(int width, int height, GraphicsFormat format, string name)
    {
        return new TextureDesc(width, height)
        {
            format = format,
            name = name,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            clearBuffer = false,
        };
    }

    void Fill(PassData data, int pass, in ShaderParams p, TextureHandle input)
    {
        data.material = m_Material;
        data.pass = pass;
        data.p = p;
        data.input = input;
        data.orient = TextureHandle.nullHandle;
        data.filtered = TextureHandle.nullHandle;
    }

    void AddSimplePass(RenderGraph renderGraph, ProfilingSampler sampler, int pass, in ShaderParams p, TextureHandle input, TextureHandle output)
    {
        using var builder = renderGraph.AddRasterRenderPass<PassData>(sampler.name, out var data, sampler);
        Fill(data, pass, p, input);
        builder.UseTexture(input);
        builder.SetRenderAttachment(output, 0, AccessFlags.WriteAll);
        builder.SetRenderFunc(static (PassData d, RasterGraphContext ctx) => Draw(d, ctx.cmd));
    }

    static void Draw(PassData d, RasterCommandBuffer cmd)
    {
        var props = s_Props;
        props.Clear();
        props.SetTexture(BlitTextureId, (Texture)d.input);
        props.SetVector(BlitScaleBiasId, new Vector4(1f, 1f, 0f, 0f));
        if (d.orient.IsValid())
            props.SetTexture(OrientTexId, (Texture)d.orient);
        if (d.filtered.IsValid())
            props.SetTexture(FilteredTexId, (Texture)d.filtered);
        props.SetTexture(PaperTexId, d.p.paper != null ? d.p.paper : Texture2D.grayTexture);
        props.SetTexture(NoiseTexId, d.p.noise);
        props.SetVector(WorkSizeId, d.p.workSize);
        props.SetVector(SourceSizeId, d.p.sourceSize);
        props.SetVector(KuwaharaId, d.p.kuwahara);
        props.SetVector(Kuwahara2Id, d.p.kuwahara2);
        props.SetVector(StyleId, d.p.style);
        props.SetVector(Style2Id, d.p.style2);
        props.SetVector(Style3Id, d.p.style3);
        cmd.DrawProcedural(Matrix4x4.identity, d.material, d.pass, MeshTopology.Triangles, 3, 1, props);
    }
}
