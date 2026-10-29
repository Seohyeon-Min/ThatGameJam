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

        [Tooltip("How much blotches and backrun rims gain saturation as they get darker, in proportion to how much darker. 0 = only the small built-in boost. Applies immediately.")]
        [Range(0f, 2f)] public float blotchSaturation = 0.5f;

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

        [Header("Pencil")]
        [Tooltip("How visible the coloured-pencil strokes are. 0 = off. Applies immediately.")]
        [Range(0f, 1f)] public float pencilStrength = 0.6f;

        [Tooltip("How much darker (richer, bluer, never greyer) the pencil lead is than the paint under it. Applies immediately.")]
        [Range(0f, 2f)] public float pencilDarkness = 0.9f;

        [Tooltip("Length of one pencil stroke on screen, in % of screen height. Same size near and far. Strokes stay where they are and only grow or shrink; long strokes can run into the next ones in their row. Applies immediately (rebuilds the stroke texture once).")]
        [Range(0.5f, 10f)] public float strokeLengthPercent = 3f;

        [Tooltip("How much smaller pieces on screen get shorter strokes. 0 = same length everywhere; 1 = length shrinks with the piece's size on screen (a piece a quarter of the screen high or bigger keeps the full length). Only the length changes, not the width or spacing. Applies immediately.")]
        [Range(0f, 1f)] public float strokeSizeInfluence = 0.5f;

        [Tooltip("Width of a stroke on screen, in pixels at 1080p. Strokes stay where they are; only their width changes. Applies immediately.")]
        [Range(0.5f, 8f)] public float strokeWidth = 2.5f;

        [Tooltip("How much stroke widths differ from each other. 0 = all the same. Applies immediately.")]
        [Range(0f, 1f)] public float strokeWidthVariation = 0.2f;

        [Tooltip("How tightly strokes are packed side by side and how few gaps they leave. Above 1 the lines nearly touch and the gaps between stacks close up. Strokes never pile up into a smudge. Applies immediately.")]
        [Range(0f, 2f)] public float strokeDensity = 0.7f;

        [Tooltip("How neatly neighbouring strokes line up into stacks, starting and ending at the same place like a ladder. 0 = scattered. Applies immediately.")]
        [Range(0f, 1f)] public float strokeAlignment = 0.85f;

        [Tooltip("Direction of the strokes on screen, in degrees (0 = horizontal). Applies immediately.")]
        [Range(-90f, 90f)] public float strokeAngle = 35f;

        [Tooltip("How much each stack of strokes turns away from that direction, in degrees. Every stack gets its own turn. Applies immediately (rebuilds the stroke texture once).")]
        [Range(0f, 90f)] public float strokeAngleJitter = 10f;

        [Tooltip("How much the strokes gather on the form-shadow side (the side of a mass turned away from the light). 0 = anywhere in a patch, 1 = only on the shadow side. Applies immediately.")]
        [FormerlySerializedAs("pencilEdgeFocus")]
        [Range(0f, 1f)] public float pencilShadowFocus = 0.8f;

        [Tooltip("How softly the strokes fade out across the line where a mass turns from the light, in % of screen height. Applies immediately.")]
        [FormerlySerializedAs("pencilEdgeWidthPercent")]
        [Range(0.5f, 20f)] public float pencilShadowFadePercent = 4f;

        [Tooltip("Size of a pencil patch, in % of screen height. Used when baking.")]
        [Range(1f, 50f)] public float pencilPatchSizePercent = 10f;

        [Tooltip("Chance that a chunk of the surface gets a pencil patch. Used when baking.")]
        [Range(0f, 1f)] public float pencilPatchChance = 0.4f;

        [Header("Light and Shadow")]
        [Tooltip("Light for the form: which side of each mass stays paper white, the cream and boundary bands, and the side the pencil strokes gather on. Uses this object's facing direction (blue arrow) and rotating it updates the view immediately. Only the Transform is used: use an empty object, or turn off the Light component if it is a copy of a light, otherwise that light also lights and casts shadows. Empty = the scene's main light. Cast shadows always come from the main light.")]
        public Transform formLight;

        // No longer used: the light side now fades toward Light Cream instead of turning yellow. Kept so saved
        // values survive.
        [HideInInspector] public float lightYellowPercent = 30f;

        [Tooltip("How far the light zone next to the paper reaches, in % of screen height. In it the colour fades toward Light Cream, more the closer it is to the paper.")]
        [Range(0.1f, 20f)] public float lightYellowWidthPercent = 4f;

        [Tooltip("Pale cream the light side fades toward: brighter, softer, only faintly warm. Not used on floors.")]
        public Color lightCream = new Color(1f, 0.96f, 0.86f);

        [Tooltip("How far the light side fades toward Light Cream at the paper's edge. 0 = keeps the paint colour. Not used on floors.")]
        [Range(0f, 1f)] public float lightFade = 0.6f;

        [Tooltip("How strongly the light side shows scattered pastel chips (pink, mint, sky, lavender) from the baked colour drops. Never on the paper or on white paint. Not used on floors.")]
        [Range(0f, 1f)] public float lightPastelChips = 0.5f;

        [Tooltip("On: cast shadows become clear mid-tone shadow hues (Shadow Darkening, Shadow Hue From/To). Off: the earlier cast shadows, darker and richer in the paint's own colour turning toward blue (Shadow Depth, Dark Pigment Blue %).")]
        public bool pastelShadows = false;

        [Tooltip("Saturation of cast shadows, both kinds. 0 = as they come out, below 0 = softer, -1 = grey. Applies immediately.")]
        [Range(-1f, 1f)] public float shadowSaturation = 0f;

        [Tooltip("Pastel Shadows only. How much cast shadows lower the brightness. Shadows keep their saturation and turn toward the shadow hues, so a low value gives clear mid-tone blues rather than dark ones.")]
        [Range(0f, 1f)] public float shadowDarkening = 0.25f;

        [Tooltip("Pastel Shadows only. Shadow hue range, in degrees on the colour wheel (180 teal, 220 blue, 270 violet). Shadows mix between the two with the baked noise.")]
        [Range(0f, 360f)] public float shadowHueFrom = 185f;

        [Range(0f, 360f)] public float shadowHueTo = 270f;

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

        [Header("Scene Colour")]
        [Tooltip("Colour of the light and shade drifting across the scene: a very large world-space noise moves the lit side between Light Tint A and B and the shade side between Shade Tint A and B. Tints, never darkens; paper white takes the light colour too. 0 = off.")]
        [Range(0f, 1f)] public float sceneColour = 0.5f;
        [Tooltip("Size of one colour zone, in world units.")]
        [Range(1f, 200f)] public float sceneColourSize = 10f;
        public Color lightTintA = new Color(1f, 0.95f, 0.78f);
        public Color lightTintB = new Color(1f, 0.86f, 0.88f);
        public Color shadeTintA = new Color(0.72f, 0.82f, 1f);
        public Color shadeTintB = new Color(0.84f, 0.76f, 1f);

        [Tooltip("Shade facing up takes the sky's colour, shade facing down or sideways the warm ground colour. 0 = off.")]
        [Range(0f, 1f)] public float bounce = 0.4f;
        public Color skyBounce = new Color(0.75f, 0.85f, 1f);
        public Color groundBounce = new Color(1f, 0.86f, 0.86f);

        [Tooltip("A thin warm complementary accent on the boundary band next to the paper (like orange at the edge of blue ice). 0 = off.")]
        [Range(0f, 1f)] public float edgeAccent = 0.3f;
        public Color edgeAccentColour = new Color(1f, 0.62f, 0.4f);

        [Tooltip("Distance haze: far things fade into the air colour (Haze Low toward the horizon, Haze High looking up). Match it to the sky. 0 = off.")]
        [Range(0f, 1f)] public float haze = 0.5f;
        [Tooltip("Distance where the haze starts, in world units.")]
        [Range(0f, 200f)] public float hazeStart = 12f;
        [Tooltip("Distance over which it reaches full strength, in world units.")]
        [Range(1f, 300f)] public float hazeRange = 40f;
        public Color hazeLow = new Color(1f, 0.9f, 0.92f);
        public Color hazeHigh = new Color(0.82f, 0.9f, 1f);

        [Header("Floor")]
        [Tooltip("Size of one blotch on floors (surfaces with Floor on), in % of screen height. Floors keep this size on screen near and far, like a flat painting, while the pattern stays attached to the floor. Applies immediately.")]
        [Range(1f, 30f)] public float floorBlotchSizePercent = 6f;

        [Header("Colour")]
        [Tooltip("Colour difference between models sharing a bake (e.g. the trees of one kind): each one's hue turns by up to this many degrees either way. Applies immediately.")]
        [Range(0f, 60f)] public float instanceHueRange = 12f;
        [Tooltip("Brightness difference between models sharing a bake, in % either way.")]
        [Range(0f, 50f)] public float instanceBrightnessRange = 10f;
        [Tooltip("Saturation difference between models sharing a bake, in % either way.")]
        [Range(0f, 100f)] public float instanceSaturationRange = 40f;

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
        Shader.SetGlobalFloat("_WC_BlotchSaturation", s.blotchSaturation);
        Shader.SetGlobalVector("_WC_InstanceColour", new Vector4(s.instanceHueRange / 360f, s.instanceBrightnessRange / 100f, s.instanceSaturationRange / 100f, 0f));
        // sRGB values as they are (not converted to linear), because the shader mixes colours in sRGB.
        Shader.SetGlobalVector("_WC_LightCream", new Vector4(s.lightCream.r, s.lightCream.g, s.lightCream.b, s.lightFade));
        Shader.SetGlobalVector("_WC_LightShade", new Vector4(s.lightPastelChips, s.shadowDarkening, s.shadowHueFrom / 360f, s.shadowHueTo / 360f));
        Shader.SetGlobalFloat("_WC_PastelShadows", s.pastelShadows ? 1f : 0f);
        // Scene colours: sRGB values as they are, like the other colours the shader mixes in sRGB.
        Shader.SetGlobalVector("_WC_SceneColour", new Vector4(s.sceneColour, s.sceneColourSize, s.bounce, s.edgeAccent));
        Shader.SetGlobalVector("_WC_LightTintA", (Vector4)s.lightTintA);
        Shader.SetGlobalVector("_WC_LightTintB", (Vector4)s.lightTintB);
        Shader.SetGlobalVector("_WC_ShadeTintA", (Vector4)s.shadeTintA);
        Shader.SetGlobalVector("_WC_ShadeTintB", (Vector4)s.shadeTintB);
        Shader.SetGlobalVector("_WC_SkyBounce", (Vector4)s.skyBounce);
        Shader.SetGlobalVector("_WC_GroundBounce", (Vector4)s.groundBounce);
        Shader.SetGlobalVector("_WC_EdgeAccent", (Vector4)s.edgeAccentColour);
        Shader.SetGlobalVector("_WC_Haze", new Vector4(s.haze, s.hazeStart, s.hazeRange, 0f));
        Shader.SetGlobalVector("_WC_HazeLow", (Vector4)s.hazeLow);
        Shader.SetGlobalVector("_WC_HazeHigh", (Vector4)s.hazeHigh);
        Shader.SetGlobalFloat("_WC_ShadowSaturation", s.shadowSaturation);
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
        Shader.SetGlobalTexture("_WC_PencilStrokes", PencilStrokesTexture(s));
        Shader.SetGlobalVector("_WC_Pencil", new Vector4(s.pencilStrength, s.pencilDarkness,
            s.strokeAngle * Mathf.Deg2Rad, s.strokeWidth));
        Shader.SetGlobalVector("_WC_PencilZone", new Vector4(s.pencilShadowFadePercent / 100f, s.pencilShadowFocus, s.strokeSizeInfluence, 0f));
        s_Active = s;

    }

    // Scene view without the watercolor look (plain shading, no post effect), so the editor view stays easy on the
    // eyes. Game view and builds are unaffected. Toggled from Tools > Watercolor > Plain Scene View (per user).
    public static bool PlainSceneView;

    static readonly int PlainId = Shader.PropertyToID("_WC_Plain");

    // Tells WatercolorFlat which camera is about to render, so the Scene view can be drawn plain.

    static readonly int FloorDetailId = Shader.PropertyToID("_WC_FloorDetail");
    static readonly int FormLightId = Shader.PropertyToID("_WC_FormLight");
    static Settings s_Active = new Settings();

    static void OnBeginCamera(ScriptableRenderContext context, Camera camera)
    {
        Shader.SetGlobalFloat(PlainId, PlainSceneView && camera.cameraType == CameraType.SceneView ? 1f : 0f);

        // Read every frame, so rotating the form light shows at once. w = 0 means use the main light.
        Transform form = s_Active.formLight;
        Shader.SetGlobalVector(FormLightId, form != null ? (Vector4)(-form.forward) + new Vector4(0f, 0f, 0f, 1f) : Vector4.zero);

        // World size of one floor blotch at distance d is x * d + y, so it covers the same share of this camera's
        // screen everywhere (perspective: grows with distance; orthographic: constant).
        float share = s_Active.floorBlotchSizePercent / 100f;
        Vector4 detail = camera.orthographic
            ? new Vector4(0f, share * camera.orthographicSize * 2f, 0f, 0f)
            : new Vector4(share * 2f * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad), 0f, 0f, 0f);
        Shader.SetGlobalVector(FloorDetailId, detail);

        // Same idea for pencil strokes: world size of one stroke-texture tile at distance d. One texel is one pixel
        // at 1080p, so strokes stay crisp.
        float tile = PencilTexels / 1080f;
        Vector4 pencil = camera.orthographic
            ? new Vector4(0f, tile * camera.orthographicSize * 2f, 0f, 0f)
            : new Vector4(tile * 2f * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad), 0f, 0f, 0f);
        Shader.SetGlobalVector(PencilScaleId, pencil);
    }

    static readonly int PencilScaleId = Shader.PropertyToID("_WC_PencilScale");

    // Coloured-pencil strokes: hatching. Strokes lie end to end on parallel lanes, so they stay separate lines and
    // never pile up into a smudge. Horizontal here; the shader rotates them.
    // r = distance to the nearest stroke's centre line (0..PencilMaxDistance texels, already divided by that stroke's
    // width variation and end taper), g = pressure. The shader turns the distance into a line of the set width, so
    // changing the width keeps every stroke in place.
    // Built once and rebuilt only when width variation, density, length, alignment or angle jitter change.
    const int PencilTexels = 512;
    const float PencilMaxDistance = 8f;
    // Lane spacing is measured in this fixed width, not the stroke width, so the layout does not move with the width.
    const float PencilLaneWidth = 3f;
    // Likewise the layout is measured in this fixed stroke length (3% of 1080p), not the set length.
    const float PencilLayoutLength = 32f;
    static Texture2D s_PencilStrokes;
    static (float, float, float, float, float) s_PencilKey = (-1f, 0f, 0f, 0f, 0f);

    static Texture2D PencilStrokesTexture(Settings st)
    {
        var key = (st.strokeWidthVariation, st.strokeDensity, st.strokeLengthPercent, st.strokeAlignment, st.strokeAngleJitter);
        if (s_PencilStrokes != null && key == s_PencilKey)
            return s_PencilStrokes;
        s_PencilKey = key;

        const int size = PencilTexels;
        // One texel is one pixel at 1080p, so the sizes in the settings are texels here. Strokes are laid out at a
        // fixed length and then drawn longer or shorter around their own centres, so changing the length keeps every
        // stroke in place.
        float grow = Mathf.Min(st.strokeLengthPercent / 100f * 1080f, size * 0.8f) / PencilLayoutLength;
        // Density 0-1: lanes go from three and a half lane widths apart to just under two, and more stacks are kept.
        // Density 1-2: lanes close in to 1.1 lane widths, and the gaps between stacks shrink. The lane count is a whole
        // number so the texture tiles.
        float dense = Mathf.Clamp01(st.strokeDensity - 1f);
        float laneStep = st.strokeDensity <= 1f ? Mathf.Lerp(3.5f, 1.8f, st.strokeDensity) : Mathf.Lerp(1.8f, 1.1f, dense);
        int lanes = Mathf.Max(1, Mathf.RoundToInt(size / (PencilLaneWidth * laneStep)));
        float spacing = (float)size / lanes;
        float keep = st.strokeDensity <= 1f ? Mathf.Lerp(0.45f, 0.9f, st.strokeDensity) : Mathf.Lerp(0.9f, 1f, dense);
        float gapScale = Mathf.Lerp(1f, 0.35f, dense);
        float turn = st.strokeAngleJitter * Mathf.Deg2Rad;

        var distance = new float[size * size];
        for (int i = 0; i < distance.Length; i++)
            distance[i] = PencilMaxDistance;
        var pressureMap = new float[size * size];
        var random = new System.Random(20231);
        float R(float a, float b) => a + (float)random.NextDouble() * (b - a);
        // Strokes come in stacks: a run of 4-10 neighbouring lanes shares where each stroke starts and ends, so the
        // strokes line up like a ladder. Alignment sets how closely the ends match. Each block of a stack (one column)
        // turns as a whole by its own angle, within the angle jitter.
        float jitter = Mathf.Lerp(0.5f, 0.04f, st.strokeAlignment);
        var columns = new System.Collections.Generic.List<Vector3>();
        for (int lane = 0; lane < lanes;)
        {
            int stack = Mathf.Min(lanes - lane, random.Next(4, 11));
            float stackCenterY = (lane + stack * 0.5f) * spacing;
            columns.Clear();
            float first = R(0f, PencilLayoutLength), x = first;
            while (true)
            {
                float length = PencilLayoutLength * R(0.7f, 1.3f);
                // Stop before wrapping onto the first column.
                if (x + length > size + first - PencilLayoutLength * 0.25f)
                    break;
                if (random.NextDouble() < keep)
                    columns.Add(new Vector3(x, length, R(-turn, turn)));
                x += length + PencilLayoutLength * R(0.25f, 0.6f) * gapScale;
            }
            for (int k = 0; k < stack; k++, lane++)
            {
                float laneY = (lane + 0.5f) * spacing;
                foreach (var column in columns)
                {
                    // Now and then a line in the stack is missing.
                    if (random.NextDouble() < 0.08)
                        continue;
                    float start = column.x + R(-jitter, jitter) * column.y;
                    float length = column.y * (1f + R(-jitter, jitter));
                    // Turn the stroke's centre around the block's centre, so the block keeps its ladder shape.
                    float ox = start + length * 0.5f - (column.x + column.y * 0.5f);
                    float oy = laneY + R(-0.08f, 0.08f) * spacing - stackCenterY;
                    float cos = Mathf.Cos(column.z), sin = Mathf.Sin(column.z);
                    DrawStroke(column.x + column.y * 0.5f + ox * cos - oy * sin, stackCenterY + ox * sin + oy * cos, length * grow, column.z + R(-0.008f, 0.008f),
                        1f + R(-1f, 1f) * st.strokeWidthVariation * 0.6f, R(0.7f, 1f));
                }
            }
        }

        void DrawStroke(float cx, float cy, float length, float angle, float widthScale, float pressure)
        {
            float dx = Mathf.Cos(angle), dy = Mathf.Sin(angle);
            float reach = PencilMaxDistance * widthScale + 1f;
            int x0 = Mathf.FloorToInt(cx - length * 0.5f - reach), x1 = Mathf.CeilToInt(cx + length * 0.5f + reach);
            int y0 = Mathf.FloorToInt(cy - length * 0.5f * Mathf.Abs(dy) - reach), y1 = Mathf.CeilToInt(cy + length * 0.5f * Mathf.Abs(dy) + reach);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float px = x + 0.5f - cx, py = y + 0.5f - cy;
                    float along = px * dx + py * dy;
                    float across = -px * dy + py * dx;
                    // Distance to the segment (round ends). Even width along the stroke; only the very ends taper.
                    float beyond = Mathf.Max(Mathf.Abs(along) - length * 0.5f, 0f);
                    float taper = Mathf.Clamp01((1f - Mathf.Abs(along) / (length * 0.5f)) * 6f);
                    float d = Mathf.Sqrt(beyond * beyond + across * across) / (widthScale * (0.4f + 0.6f * taper));
                    int i = ((y % size + size) % size) * size + ((x % size + size) % size);
                    if (d < distance[i])
                    {
                        distance[i] = d;
                        pressureMap[i] = pressure * (0.7f + 0.3f * taper);
                    }
                }
        }

        var pixels = new Color32[size * size];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = new Color32((byte)(Mathf.Min(distance[i] / PencilMaxDistance, 1f) * 255f), (byte)(pressureMap[i] * 255f), 0, 255);
        }
        if (s_PencilStrokes == null)
            s_PencilStrokes = new Texture2D(size, size, TextureFormat.RGBA32, true, true)
            {
                name = "WatercolorPencilStrokes",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
            };
        s_PencilStrokes.SetPixels32(pixels);
        // Stays readable: it is rebuilt in place when the settings change.
        s_PencilStrokes.Apply(true, false);
        return s_PencilStrokes;
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
