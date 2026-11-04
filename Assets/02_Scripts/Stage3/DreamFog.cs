using UnityEngine;

// The dream fog over a fog section (DreamFogFeature draws it): a warm pink-to-orange glowing fog filling the whole picture, everything
// but the bird a shadow in it, nearer ones darker. It comes in as the bird goes into the fog, along the field's own ramp (the first
// straight stretch). It lies evenly over everything: the field's cells (the maze's ways, the spots cleared) don't show through, so the
// way through the maze can't be read off the screen. Only these values are set, as shader globals, every frame; nothing is made or
// changed in the scene.
public class DreamFog : MonoBehaviour
{
    static readonly int WeightId = Shader.PropertyToID("_DreamFogWeight");
    static readonly int LowId = Shader.PropertyToID("_DreamLow");
    static readonly int HighId = Shader.PropertyToID("_DreamHigh");
    static readonly int SunColorId = Shader.PropertyToID("_DreamSunColor");
    static readonly int SunId = Shader.PropertyToID("_DreamSun");
    static readonly int BlobId = Shader.PropertyToID("_DreamBlob");
    static readonly int ShadowId = Shader.PropertyToID("_DreamShadow");
    static readonly int DepthId = Shader.PropertyToID("_DreamDepth");
    static readonly int RaysId = Shader.PropertyToID("_DreamRays");
    static readonly int MaskId = Shader.PropertyToID("_DreamFogMask");
    static readonly int AreaId = Shader.PropertyToID("_DreamFogArea");

    // How far in the fog is now, for DreamFogFeature: 0 = not drawn at all.
    public static float Weight { get; private set; }

    // What shows through the dream fog besides the bird: the cries (the bird's and the animals' pulses) are on this layer, which
    // PC_Renderer leaves out of its usual drawing and draws by itself after the fog (its AboveFog Render Objects feature).
    public const string AboveFogLayer = "AboveFog";

    [SerializeField] FogField field;
    [Tooltip("How strong the dream fog is once fully in, 0 to 1.")]
    [SerializeField, Range(0f, 1f)] float strength = 1f;
    [Tooltip("Seconds it takes to follow the bird in or out of the fog.")]
    [SerializeField, Min(0.01f)] float ease = 1.5f;

    [Header("Fog")]
    [Tooltip("Fog colour at the bottom of the picture.")]
    [SerializeField] Color low = new(1f, 0.72f, 0.5f);
    [Tooltip("Fog colour at the top of the picture.")]
    [SerializeField] Color high = new(1f, 0.66f, 0.68f);
    [Tooltip("Colour of the soft patches that drift through the fog, and how strong they are (alpha).")]
    [SerializeField] Color patches = new(1f, 0.55f, 0.6f, 0.35f);

    [Header("Light")]
    [Tooltip("Where the light glows on the screen, 0 to 1 (0, 0 is the bottom left).")]
    [SerializeField] Vector2 lightOnScreen = new(0.22f, 0.92f);
    [Tooltip("Colour of the light's glow, and how bright (alpha above 1 is brighter).")]
    [SerializeField, ColorUsage(false, true)] Color lightColor = new(1f, 0.93f, 0.72f, 1f);
    [SerializeField, Min(0f)] float lightBrightness = 1f;
    [Tooltip("Size of the light's bright core and of its wide halo, as shares of the screen's height.")]
    [SerializeField] Vector2 lightSize = new(0.18f, 0.45f);
    [Tooltip("How much light streams between the shadows, toward the light.")]
    [SerializeField, Range(0f, 2f)] float rays = 0.8f;
    [Tooltip("How much the light eats the shadows near it.")]
    [SerializeField, Range(0f, 2f)] float lightBleed = 1f;

    [Header("Shadows")]
    [Tooltip("Colour of the shadows everything but the bird becomes, and how dark they get up close (alpha).")]
    [SerializeField] Color shadow = new(0.55f, 0.27f, 0.25f, 0.9f);
    [Tooltip("Metres from the camera that stay full shadow, then the metres over which shadows melt into the fog.")]
    [SerializeField] Vector2 shadowDepth = new(8f, 14f);
    [Tooltip("How soft the shadows' edges are, in pixels.")]
    [SerializeField, Min(0f)] float edgeSoftness = 3f;

    [Header("Grain and clearing")]
    [SerializeField, Range(0f, 0.2f)] float grain = 0.04f;
    [Tooltip("Thin the dream fog where the field's fog is thin. Off: even everywhere. Leave it off in a maze: the field holds its ways thinner, and they'd show as paths.")]
    [SerializeField] bool showClearing;
    [Tooltip("With Show Clearing: where the fog is cleared, how much dream fog is left there, 0 to 1.")]
    [SerializeField, Range(0f, 1f)] float clearedLeft = 0.45f;

    float weight;

    void OnDisable()
    {
        weight = 0f;
        Weight = 0f;
        Shader.SetGlobalFloat(WeightId, 0f);
    }

    void LateUpdate()
    {
        float goal = 0f;
        if (field != null && BirdController.Instance != null)
            goal = field.RampAt(BirdController.Instance.transform.position) * strength;
        weight = Mathf.MoveTowards(weight, goal, Time.deltaTime / ease);
        Weight = Mathf.SmoothStep(0f, 1f, weight);

        Shader.SetGlobalFloat(WeightId, Weight);
        Shader.SetGlobalVector(LowId, low.linear);
        Shader.SetGlobalVector(HighId, high.linear);
        Shader.SetGlobalVector(BlobId, new Vector4(patches.linear.r, patches.linear.g, patches.linear.b, patches.a));
        Color glow = lightColor.linear;
        Shader.SetGlobalVector(SunColorId, new Vector4(glow.r, glow.g, glow.b, lightBrightness));
        Shader.SetGlobalVector(SunId, new Vector4(lightOnScreen.x, lightOnScreen.y, lightSize.x, lightSize.y));
        Color dark = shadow.linear;
        Shader.SetGlobalVector(ShadowId, new Vector4(dark.r, dark.g, dark.b, shadow.a));
        Shader.SetGlobalVector(DepthId, new Vector4(shadowDepth.x, shadowDepth.y, edgeSoftness, lightBleed));
        // A cleared floor of 1 leaves the dream fog full everywhere, whatever the cells say.
        Shader.SetGlobalVector(RaysId, new Vector4(rays, grain, showClearing ? clearedLeft : 1f, 0f));
        if (field != null && field.Mask != null)
        {
            Shader.SetGlobalTexture(MaskId, field.Mask);
            Shader.SetGlobalVector(AreaId, field.Area);
        }
    }
}
