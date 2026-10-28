using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

// Master controls for every ThatGameJam/WatercolorFlat material. Materials only hold colour; the painting style
// (pigment thickness, warm light / cool shadow, paper area) is set here once and sent to the shader as globals.
// Put one in a scene. Without one (or while it is disabled), the defaults below apply.
[ExecuteAlways]
public class WatercolorManager : MonoBehaviour
{
    // Every size here is a share of the screen height, so a small flower and a large tree get the same amount of
    // detail on screen. Line widths are converted per pixel in the shader; blotch sizes are converted to world size
    // at bake time from each model's distance to the camera (the view is fixed, so this stays right).
    [Serializable]
    public class Settings
    {
        [Header("Pigment")]
        [Tooltip("Overall thickness of accumulated pigment (boundary bands, outlines, blotches, backrun rims). 1 = default.")]
        [Range(0f, 3f)] public float pigmentThickness = 1f;

        [Tooltip("Pigment band along the boundary between the light (paper) and the colour.")]
        [Range(0f, 1f)] public float boundaryPigment = 0.6f;

        [Tooltip("Width of that band, in % of screen height.")]
        [Range(0.05f, 5f)] public float boundaryWidthPercent = 0.6f;

        [Tooltip("Pigment pooled along each mass's outline.")]
        [Range(0f, 1f)] public float outlinePigment = 0.35f;

        [Tooltip("Width of the outline pigment, in % of screen height.")]
        [Range(0.05f, 3f)] public float outlineWidthPercent = 0.4f;

        [Tooltip("Baked blotches (uneven pigment density). Needs baked models.")]
        [Range(0f, 1f)] public float blotches = 0.5f;

        [Tooltip("Baked backrun blooms (pale inside, dark rim). Needs baked models.")]
        [Range(0f, 1f)] public float backruns = 0.6f;

        [Tooltip("Size of a blotch on screen, in % of screen height. Used when baking; re-bake after changing.")]
        [Range(0.5f, 30f)] public float blotchSizePercent = 5f;

        [Tooltip("Size of a backrun bloom on screen, in % of screen height. Used when baking; re-bake after changing.")]
        [Range(0.5f, 30f)] public float backrunSizePercent = 7f;

        [Header("Colour Chaos")]
        [Tooltip("Hue range of the colour drops, in degrees on the colour wheel. 0 = off. Applies immediately.")]
        [Range(0f, 90f)] public float chaosHueRange = 25f;

        [Tooltip("Size of the chunks a colour area is split into, in % of screen height. Used when baking.")]
        [Range(1f, 50f)] public float chaosChunkSizePercent = 12f;

        [Tooltip("Chance that a chunk gets a colour drop (0 = never, 1 = every chunk). Used when baking.")]
        [Range(0f, 1f)] public float chaosSplitChance = 0.5f;

        [Tooltip("Smallest colour drop, in % of screen height. Used when baking.")]
        [Range(0.5f, 50f)] public float chaosDropSizeMinPercent = 3f;

        [Tooltip("Largest colour drop, in % of screen height. Used when baking.")]
        [Range(0.5f, 50f)] public float chaosDropSizeMaxPercent = 9f;

        [Header("Light and Shadow")]
        [Tooltip("How far the zone next to the light (paper) turns toward yellow on the colour wheel, strongest at the light and fading with distance. 100% = up to 30 degrees.")]
        [Range(0f, 100f)] public float lightYellowPercent = 30f;

        [Tooltip("How far that yellow zone reaches from the light, in % of screen height.")]
        [Range(0.1f, 20f)] public float lightYellowWidthPercent = 4f;

        [Tooltip("How far areas darkened by pigment (blotches, backrun rims, boundary bands, outlines, cast shadows) turn toward blue on the colour wheel, in proportion to how dark they get. 100% = up to 30 degrees.")]
        [FormerlySerializedAs("shadowBluePercent")]
        [Range(0f, 100f)] public float darkPigmentBluePercent = 60f;

        [Tooltip("How much darker (and richer, never greyer) cast shadows are.")]
        [Range(0f, 1.5f)] public float shadowDepth = 0.4f;

        [Tooltip("How strongly cast shadows show. 0 = no shadows.")]
        [Range(0f, 1f)] public float receiveShadows = 1f;

        [Tooltip("Pigment pooled along the edge of cast shadows, like a dried wash.")]
        [Range(0f, 1f)] public float shadowEdgePigment = 0.6f;

        [Tooltip("How ragged (brush-like) the edge of cast shadows is.")]
        [Range(0f, 1f)] public float shadowRaggedness = 0.5f;

        [Tooltip("How uneven the darkness inside cast shadows is.")]
        [Range(0f, 1f)] public float shadowUnevenness = 0.4f;

        [Header("Floor")]
        [Tooltip("Size of one blotch on floors (surfaces with Floor on), in % of screen height. Floors keep this size on screen near and far, like a flat painting, while the pattern stays attached to the floor. Applies immediately.")]
        [Range(1f, 30f)] public float floorBlotchSizePercent = 6f;

        [Header("Colour")]
        [Tooltip("Saturation of the paint colours. Negative = calmer, positive = more vivid. Paper white is not affected.")]
        [Range(-100f, 100f)] public float saturation = 0f;

        [Tooltip("Brightness of the paint colours. Paper white is not affected.")]
        [Range(-100f, 100f)] public float brightness = 0f;

        [Tooltip("Contrast of the paint colours around mid grey. Paper white is not affected.")]
        [Range(-100f, 100f)] public float contrast = 0f;

        [Tooltip("How much of URP's Neutral tonemapping to apply, over the whole screen. 100% = the usual soft, calm look but white becomes light grey; 0% = untouched colours and pure white. Applied by the WatercolorBlur pass, so turn URP's own Tonemapping off in the watercolor Volume.")]
        [Range(0f, 100f)] public float tonemappingPercent = 100f;

        [Header("Paper")]
        [Tooltip("How directly a surface must face the light before it is left as unpainted paper. Lower = larger paper area.")]
        [Range(0f, 1f)] public float paperThreshold = 0.8f;

        [Tooltip("Softness of the paper edge, in % of screen height.")]
        [Range(0.01f, 3f)] public float paperSoftnessPercent = 0.25f;
    }

    public Settings settings = new Settings();

    // The scene's manager (or defaults when there is none). WatercolorBaker reads the blotch sizes from here.
    public static Settings Current
    {
        get
        {
            var manager = FindAnyObjectByType<WatercolorManager>();
            return manager != null && manager.isActiveAndEnabled ? manager.settings : new Settings();
        }
    }

    void OnEnable() => Push(settings);
    void OnValidate()
    {
        if (isActiveAndEnabled)
            Push(settings);
    }

    // Back to defaults when the scene's manager goes away, so materials never fall to zeroed globals.
    void OnDisable() => Push(new Settings());

    public static void Push(Settings s)
    {
        Shader.SetGlobalFloat("_WC_PigmentThickness", s.pigmentThickness);
        Shader.SetGlobalFloat("_WC_BoundaryPigment", s.boundaryPigment);
        Shader.SetGlobalFloat("_WC_BoundaryWidth", s.boundaryWidthPercent / 100f);
        Shader.SetGlobalFloat("_WC_OutlinePigment", s.outlinePigment);
        Shader.SetGlobalFloat("_WC_OutlineWidth", s.outlineWidthPercent / 100f);
        Shader.SetGlobalFloat("_WC_Blotches", s.blotches);
        Shader.SetGlobalFloat("_WC_Backruns", s.backruns);
        Shader.SetGlobalFloat("_WC_WarmShift", s.lightYellowPercent / 100f);
        Shader.SetGlobalFloat("_WC_WarmWidth", s.lightYellowWidthPercent / 100f);
        Shader.SetGlobalFloat("_WC_CoolShift", s.darkPigmentBluePercent / 100f);
        Shader.SetGlobalFloat("_WC_ShadowDepth", s.shadowDepth);
        Shader.SetGlobalFloat("_WC_ReceiveShadows", s.receiveShadows);
        Shader.SetGlobalFloat("_WC_PaperThreshold", s.paperThreshold);
        Shader.SetGlobalFloat("_WC_PaperSoftness", s.paperSoftnessPercent / 100f);
        Shader.SetGlobalFloat("_WC_Saturation", s.saturation / 100f);
        Shader.SetGlobalFloat("_WC_Brightness", s.brightness / 100f);
        Shader.SetGlobalFloat("_WC_Contrast", s.contrast / 100f);
        Shader.SetGlobalFloat("_WC_Tonemapping", s.tonemappingPercent / 100f);
        Shader.SetGlobalFloat("_WC_ChaosHue", s.chaosHueRange / 360f);
        Shader.SetGlobalFloat("_WC_ShadowEdge", s.shadowEdgePigment);
        Shader.SetGlobalFloat("_WC_ShadowRagged", s.shadowRaggedness);
        Shader.SetGlobalFloat("_WC_ShadowUneven", s.shadowUnevenness);
        Shader.SetGlobalTexture("_WC_FloorPattern", FloorPatternTexture);
        s_Active = s;

    }

    // Scene view without the watercolor look (plain shading, no post effect), so the editor view stays easy on the
    // eyes. Game view and builds are unaffected. Toggled from Tools > Watercolor > Plain Scene View (per user).
    public static bool PlainSceneView;

    static readonly int PlainId = Shader.PropertyToID("_WC_Plain");

    // Tells WatercolorFlat which camera is about to render, so the Scene view can be drawn plain.

    static readonly int FloorDetailId = Shader.PropertyToID("_WC_FloorDetail");
    static Settings s_Active = new Settings();

    static void OnBeginCamera(ScriptableRenderContext context, Camera camera)
    {
        Shader.SetGlobalFloat(PlainId, PlainSceneView && camera.cameraType == CameraType.SceneView ? 1f : 0f);

        // World size of one floor blotch at distance d is x * d + y, so it covers the same share of this camera's
        // screen everywhere (perspective: grows with distance; orthographic: constant).
        float share = s_Active.floorBlotchSizePercent / 100f;
        Vector4 detail = camera.orthographic
            ? new Vector4(0f, share * camera.orthographicSize * 2f, 0f, 0f)
            : new Vector4(share * 2f * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad), 0f, 0f, 0f);
        Shader.SetGlobalVector(FloorDetailId, detail);
    }

    // Floor pattern texture: FloorCells x FloorCells blotches per tile, so a repeat is many screens apart.
    // Same channels as a baked pattern: r blotches, g backrun inside, b backrun rim, a smooth noise.
    // Built once (not per frame). Periodic lattice, so it tiles seamlessly.
    public const int FloorCells = 24;
    static Texture2D s_FloorPattern;

    static Texture2D FloorPatternTexture
    {
        get
        {
            if (s_FloorPattern != null)
                return s_FloorPattern;
            const int size = 512;
            var pigment = new float[size * size];
            var field = new float[size * size];
            var smooth = new float[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size, v = (float)y / size;
                    int i = y * size + x;
                    // One lattice cell = one blotch. Only two octaves, so it stays soft (no speckle).
                    pigment[i] = PeriodicNoise(u, v, FloorCells) * 0.7f + PeriodicNoise(u + 0.5f, v, FloorCells * 2) * 0.3f;
                    // Backrun blooms a bit larger than a blotch, with a slightly ragged edge.
                    field[i] = PeriodicNoise(u + 0.25f, v + 0.5f, FloorCells / 2) + (PeriodicNoise(u, v + 0.25f, FloorCells * 4) - 0.5f) * 0.12f;
                    smooth[i] = PeriodicNoise(u + 0.37f, v + 0.11f, FloorCells / 3);
                }
            // Same coverage rule as baked models: blooms cover about 30%.
            var sorted = (float[])field.Clone();
            Array.Sort(sorted);
            float threshold = sorted[(int)(0.7f * (sorted.Length - 1))];
            var pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++)
            {
                float f = field[i];
                float inside = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(threshold - 0.01f, threshold + 0.01f, f));
                float rim = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(threshold - 0.015f, threshold + 0.005f, f))
                    * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(threshold + 0.005f, threshold + 0.035f, f)));
                pixels[i] = new Color32((byte)(pigment[i] * 255f), (byte)(inside * 255f), (byte)(rim * 255f), (byte)(smooth[i] * 255f));
            }
            s_FloorPattern = new Texture2D(size, size, TextureFormat.RGBA32, true, true)
            {
                name = "WatercolorFloorPattern",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
            };
            s_FloorPattern.SetPixels32(pixels);
            s_FloorPattern.Apply(true, true);
            return s_FloorPattern;
        }
    }

    // Value noise on a `period` x `period` lattice that wraps, so the texture tiles.
    static float PeriodicNoise(float u, float v, int period)
    {
        float x = u * period, y = v * period;
        int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
        float fx = x - x0, fy = y - y0;
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);
        float Corner(int cx, int cy)
        {
            cx = ((cx % period) + period) % period;
            cy = ((cy % period) + period) % period;
            unchecked
            {
                uint h = (uint)(cx * 374761393 + cy * 668265263 + period * 1274126177);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xFFFFFF) / 16777215f;
            }
        }
        return Mathf.Lerp(Mathf.Lerp(Corner(x0, y0), Corner(x0 + 1, y0), fx), Mathf.Lerp(Corner(x0, y0 + 1), Corner(x0 + 1, y0 + 1), fx), fy);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadMethod]
#endif
    static void PushDefaults()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
        RenderPipelineManager.beginCameraRendering += OnBeginCamera;
        // After a script reload this can run after the scene manager's OnEnable; keep its values if there is one.
        Push(Current);
    }
}
