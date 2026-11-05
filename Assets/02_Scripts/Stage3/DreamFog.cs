using UnityEngine;

// The dream fog over a fog section (DreamFogFeature draws it): a warm pink-to-orange glowing fog filling the whole picture, everything
// but the bird a shadow in it, nearer ones darker. It comes in as the bird goes into the fog, along the field's own ramp (the first
// straight stretch). It lies evenly over everything: the field's cells (the maze's ways, the spots cleared) don't show through, so the
// way through the maze can't be read off the screen. Its colours are in the Look material; the other values are set as shader globals every frame. Nothing is made or
// changed in the scene.
public class DreamFog : MonoBehaviour
{
    static readonly int WeightId = Shader.PropertyToID("_DreamFogWeight");
    static readonly int SunId = Shader.PropertyToID("_DreamSun");
    static readonly int RaysId = Shader.PropertyToID("_DreamRays");
    static readonly int SilkId = Shader.PropertyToID("_DreamSilk");
    static readonly int SilkWindId = Shader.PropertyToID("_DreamSilkWind");
    static readonly int SparkleId = Shader.PropertyToID("_DreamSparkle");
    static readonly int SilkPlanesId = Shader.PropertyToID("_DreamSilkPlanes");
    static readonly int SilkFloatId = Shader.PropertyToID("_DreamSilkFloat");
    static readonly int MaskId = Shader.PropertyToID("_DreamFogMask");
    static readonly int AreaId = Shader.PropertyToID("_DreamFogArea");
    static readonly int FocusDepthId = Shader.PropertyToID("_DreamFocusDepth");
    static readonly int BirdId = Shader.PropertyToID("_DreamBird");
    static readonly int BabyId = Shader.PropertyToID("_DreamBaby");
    static readonly int PrismAmountId = Shader.PropertyToID("_PrismAmount");

    // The layer the baby's body is on: PC_Renderer's BabyPrism feature draws it again over the fog, glowing white (PrismOverlay).
    public const string BabyLayer = "Baby";

    // How far in the fog is now, for DreamFogFeature: 0 = not drawn at all.
    public static float Weight { get; private set; }
    // The material DreamFogFeature draws with: this one's Look, while it is on.
    public static Material Look { get; private set; }

    // What shows through the dream fog besides the bird: the cries (the bird's and the animals' pulses) are on this layer, which
    // PC_Renderer leaves out of its usual drawing and draws by itself after the fog (its AboveFog Render Objects feature).
    public const string AboveFogLayer = "AboveFog";

    [SerializeField] FogField field;
    [Tooltip("How strong the dream fog is once fully in, 0 to 1.")]
    [SerializeField, Range(0f, 1f)] float strength = 1f;
    [Tooltip("Seconds it takes to follow the bird in or out of the fog.")]
    [SerializeField, Min(0.01f)] float ease = 1.5f;

    [Tooltip("The fog's colours (Hidden/ThatGameJam/DreamFog): the fog top and bottom, the drifting patches, the light, and the shadows: their colour, how dark, how far they reach, how soft. Edit it in Play Mode and the change shows at once, and stays.")]
    [SerializeField] Material look;
    [Tooltip("Height over the baby's feet where the shade behind her centres, in metres.")]
    [SerializeField, Min(0f)] float babyMiddleHeight = 0.45f;

    [Header("Light")]
    [Tooltip("Where the light glows on the screen, 0 to 1 (0, 0 is the bottom left).")]
    [SerializeField] Vector2 lightOnScreen = new(0.22f, 0.92f);
    [Tooltip("Size of the light's bright core and of its wide halo, as shares of the screen's height.")]
    [SerializeField] Vector2 lightSize = new(0.18f, 0.45f);
    [Tooltip("How much light streams between the shadows, toward the light.")]
    [SerializeField, Range(0f, 2f)] float rays = 0.8f;

    [Header("Silk")]
    [Tooltip("How strong the sheer silk veils drifting through the fog are: the shine on their folds.")]
    [SerializeField, Range(0f, 2f)] float silk = 0.35f;
    [Tooltip("Folds per metre of the silk sheets, which hang in the world. Higher is finer silk.")]
    [SerializeField, Min(0.01f)] float silkFoldsPerMetre = 0.35f;
    [Tooltip("Which way the wind carries the silk in the world (X, Z), and how fast in metres a second.")]
    [SerializeField] Vector2 silkWindDirection = new(0.3f, -1f);
    [SerializeField, Min(0f)] float silkWindMetresPerSecond = 0.6f;
    [Tooltip("Where the silk floats: from this many metres over the fog field's ground up to this many, fading out below and above.")]
    [SerializeField] Vector2 silkFloats = new(1.5f, 9f);
    [Tooltip("How far from the camera the nearest of the three silk slices is read, and the metres between slices.")]
    [SerializeField] Vector2 silkDepths = new(5f, 4.5f);
    [Tooltip("How close the silk's folds lie together. Lower spreads them apart (fewer, looser folds); higher packs them into tight stripes.")]
    [SerializeField, Range(0.2f, 4f)] float silkFoldCloseness = 1f;
    [Tooltip("How much the folds shimmer in rainbow colours, 0 to 1.")]
    [SerializeField, Range(0f, 1f)] float iridescence = 0.5f;

    [Header("Sparkles")]
    [Tooltip("Share of the air's cells that hold a star, 0 to 1, before the drifts thin them out. Brighter on the silk and in the light.")]
    [SerializeField, Range(0f, 1f)] float sparkles = 0.25f;
    [Tooltip("Size of the 3D cells the stars hang in, in metres: one star at most in each, somewhere inside. Smaller is a denser sky, and reaches less far (24 cells along each line of sight).")]
    [SerializeField, Min(0.05f)] float sparkleCellSize = 0.8f;
    [Tooltip("How fast sparkles flash and fade.")]
    [SerializeField, Min(0f)] float sparkleSpeed = 2.5f;
    [SerializeField, Min(0f)] float sparkleBrightness = 1.5f;

    [Header("Grain and clearing")]
    [SerializeField, Range(0f, 0.2f)] float grain = 0.04f;
    [Tooltip("Thin the dream fog where the field's fog is thin. Off: even everywhere. Leave it off in a maze: the field holds its ways thinner, and they'd show as paths.")]
    [SerializeField] bool showClearing;
    [Tooltip("With Show Clearing: where the fog is cleared, how much dream fog is left there, 0 to 1.")]
    [SerializeField, Range(0f, 1f)] float clearedLeft = 0.45f;

    float weight;

    void OnEnable() => Look = look;

    void OnDisable()
    {
        Shader.SetGlobalFloat(PrismAmountId, 0f);
        if (Look == look)
            Look = null;
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
        // The baby's white glow comes in with the fog.
        Shader.SetGlobalFloat(PrismAmountId, Weight);
        Shader.SetGlobalVector(SunId, new Vector4(lightOnScreen.x, lightOnScreen.y, lightSize.x, lightSize.y));
        // A cleared floor of 1 leaves the dream fog full everywhere, whatever the cells say.
        Shader.SetGlobalVector(RaysId, new Vector4(rays, grain, showClearing ? clearedLeft : 1f, 0f));
        Shader.SetGlobalVector(SilkId, new Vector4(silk, silkFoldsPerMetre, silkWindMetresPerSecond, iridescence));
        Shader.SetGlobalVector(SilkWindId, new Vector4(silkWindDirection.x, silkWindDirection.y, 0f, 0f));
        float ground = field != null ? field.transform.position.y : 0f;
        Shader.SetGlobalVector(SilkPlanesId, new Vector4(ground, silkDepths.x, silkFoldCloseness, silkDepths.y));
        Shader.SetGlobalVector(SilkFloatId, new Vector4(silkFloats.x, silkFloats.y, 0f, 0f));
        // The bird's depth from the camera: the shadows' perspective is measured past it (DreamFog.mat), whatever the zoom.
        Camera view = Camera.main;
        if (view != null && BirdController.Instance != null)
        {
            Transform eye = view.transform;
            Vector3 bird = BirdController.Instance.transform.position;
            float depth = Vector3.Dot(bird - eye.position, eye.forward);
            Shader.SetGlobalFloat(FocusDepthId, depth);
            Vector3 onScreen = view.WorldToViewportPoint(bird);
            float screenHeight = 2f * Mathf.Max(depth, 0.01f) * Mathf.Tan(view.fieldOfView * 0.5f * Mathf.Deg2Rad);
            Shader.SetGlobalVector(BirdId, new Vector4(onScreen.x, onScreen.y, 1f / screenHeight, 0f));
            if (Baby.Instance != null)
            {
                Vector3 baby = Baby.Instance.transform.position + Vector3.up * babyMiddleHeight;
                float babyDepth = Vector3.Dot(baby - eye.position, eye.forward);
                Vector3 babyOnScreen = view.WorldToViewportPoint(baby);
                float babyScreenHeight = 2f * Mathf.Max(babyDepth, 0.01f) * Mathf.Tan(view.fieldOfView * 0.5f * Mathf.Deg2Rad);
                // w: 0 when she is behind the camera.
                Shader.SetGlobalVector(BabyId, new Vector4(babyOnScreen.x, babyOnScreen.y, 1f / babyScreenHeight, babyDepth > 0f ? 1f : 0f));
            }
        }
        Shader.SetGlobalVector(SparkleId, new Vector4(sparkles, sparkleCellSize, sparkleSpeed, sparkleBrightness));
        if (field != null && field.Mask != null)
        {
            Shader.SetGlobalTexture(MaskId, field.Mask);
            Shader.SetGlobalVector(AreaId, field.Area);
        }
    }
}
