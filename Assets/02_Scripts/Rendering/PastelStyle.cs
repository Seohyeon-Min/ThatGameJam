using UnityEngine;

// Scene-wide look for ThatGameJam/SoftPastel materials (a soft pastel / chalk test next to the watercolor look):
// soft form gradients whose light-to-shade transition is grainy like pastel on toothed paper, a warm glow on the lit
// edges, and silhouettes that dissolve into grain. Materials keep only colour and
// texture; this component pushes the style as shader globals (_PS_*). Values apply immediately.
[ExecuteAlways]
public class PastelStyle : MonoBehaviour
{
    [Header("Colour")]
    [Tooltip("Extra saturation for the pastel colours. Pastel pigment is vivid even when light.")]
    [Range(-1f, 1f)] public float saturation = 0.6f;
    [Tooltip("How blotchy the colour is, from the baked watercolor pattern (floors: world-space noise). Denser blotches turn toward blue instead of darker, so nothing goes muddy.")]
    [Range(0f, 1f)] public float mottle = 0.4f;
    [Tooltip("How much the blotches change brightness: denser ones darker (and a little richer, never greyer), lighter ones brighter. 0 = hue only.")]
    [Range(0f, 1f)] public float mottleBrightness = 0.3f;
    [Tooltip("Patches of a different colour where the baked colour drops are, as a hue turn in degrees. Uses the watercolor Colour Chaos bake.")]
    [Range(0f, 180f)] public float colourPatches = 40f;
    [Tooltip("Size of a floor blotch in world units (floors are not baked).")]
    [Range(0.2f, 10f)] public float floorMottleSize = 2f;
    [Tooltip("Pale pastel chips (pink, mint, sky, lavender) on the lit side, with soft blurry edges. Never on white.")]
    [Range(0f, 1f)] public float pastelChips = 0.7f;
    [Tooltip("Smallest chip, in % of screen height.")]
    [Range(0.5f, 20f)] public float chipSizeMin = 2f;
    [Tooltip("Largest chip, in % of screen height.")]
    [Range(0.5f, 20f)] public float chipSizeMax = 6f;
    [Tooltip("Chance a chip appears in each spot.")]
    [Range(0f, 1f)] public float chipChance = 0.5f;

    [Header("Water Bleed (watercolor mixed in)")]
    [Tooltip("Wet-in-wet watercolor mixed into the pastel: neighbouring colours seep into each other, blooms fade toward the paper, and their edges gather pigment like cauliflower rims. Uses the watercolor bake (floors: noise).")]
    public bool waterBleed = false;
    [Tooltip("How far the inside of a bloom fades toward the paper.")]
    [Range(0f, 1f)] public float bleedBloom = 0.6f;
    [Tooltip("How far colours seep toward their neighbours on the colour wheel, in degrees (e.g. teal and blue mixing).")]
    [Range(0f, 90f)] public float bleedHueRange = 25f;
    [Tooltip("How much pigment gathers on a bloom's edge (richer and a little bluer, never greyer).")]
    [Range(0f, 1f)] public float bleedRim = 0.6f;

    [Header("Light and Shade")]
    [Tooltip("Warm colour the lit side brightens toward.")]
    public Color lightTint = new Color(1f, 0.95f, 0.78f);
    [Tooltip("How far the lit side brightens toward Light Tint.")]
    [Range(0f, 1f)] public float lightStrength = 0.25f;
    [Tooltip("Colour the shade side and cast shadows turn toward (hue), keeping their saturation so they never go muddy.")]
    public Color shadeTint = new Color(0.55f, 0.55f, 1f);
    [Tooltip("How strongly the shade side takes on Shade Tint.")]
    [Range(0f, 1f)] public float shadeStrength = 0.6f;
    [Tooltip("How far the shade side's hue turns toward Shade Tint. 0 = keeps its own hue (only darker), 1 = all the way.")]
    [Range(0f, 1f)] public float shadeHueTurn = 0.6f;
    [Tooltip("How much colours near the opposite of Shade Tint still turn (e.g. yellow under a violet shade). 0 = they barely turn and get a little darker instead (yellow -> ochre); 1 = they turn like any other colour (yellow -> pink).")]
    [Range(0f, 1f)] public float complementTurn = 0f;
    [Tooltip("Where the light-to-shade transition sits. Higher = more of each object in shade.")]
    [Range(0f, 1f)] public float shadeThreshold = 0.55f;
    [Tooltip("How wide the transition is.")]
    [Range(0.02f, 1f)] public float shadeSoftness = 0.35f;

    [Header("Grain")]
    [Tooltip("Size of a grain speck on screen, in pixels at 1080p.")]
    [Range(0.5f, 6f)] public float grainSize = 1.5f;
    [Tooltip("How grainy (speckled rather than smooth) the light-to-shade transition is.")]
    [Range(0f, 1f)] public float graininess = 0.8f;
    [Tooltip("Paper tooth over everything: how much the grain lightens and darkens flat areas.")]
    [Range(0f, 0.5f)] public float paperTooth = 0.12f;

    [Header("Edges")]
    [Tooltip("Warm glow on the edges that face the light.")]
    public Color rimColor = new Color(1f, 0.92f, 0.45f);
    [Range(0f, 1f)] public float rimStrength = 0.6f;
    [Tooltip("How far in from the silhouette objects dissolve into grain, in pixels at 1080p. 0 = hard edges.")]
    [Range(0f, 20f)] public float edgeDissolve = 4f;

    [Header("Haze")]
    [Tooltip("Distance haze: far things fade into the air colour (Haze Low toward the horizon, Haze High looking up). The WatercolorSky skybox follows it while this component is on. 0 = off.")]
    [Range(0f, 1f)] public float haze = 0.5f;
    [Tooltip("Distance where the haze starts, in world units.")]
    [Range(0f, 200f)] public float hazeStart = 12f;
    [Tooltip("Distance over which it reaches full strength, in world units.")]
    [Range(1f, 300f)] public float hazeRange = 40f;
    public Color hazeLow = new Color(1f, 0.86f, 0.92f);
    public Color hazeHigh = new Color(0.8f, 0.88f, 1f);

    void OnEnable() => Push();
    // The sky goes back to the watercolor haze when this is off.
    void OnDisable() => Shader.SetGlobalVector("_PS_Haze", Vector4.zero);
    void OnValidate() => Push();

    void Push()
    {
        // sRGB values as they are, because the shader mixes colours in sRGB.
        Shader.SetGlobalVector("_PS_Light", new Vector4(lightTint.r, lightTint.g, lightTint.b, lightStrength));
        Shader.SetGlobalVector("_PS_Shade", new Vector4(shadeTint.r, shadeTint.g, shadeTint.b, shadeStrength));
        Shader.SetGlobalVector("_PS_Tone", new Vector4(shadeThreshold, shadeSoftness, saturation, 0f));
        Shader.SetGlobalVector("_PS_Grain", new Vector4(grainSize, graininess, paperTooth, edgeDissolve));
        Shader.SetGlobalVector("_PS_Rim", new Vector4(rimColor.r, rimColor.g, rimColor.b, rimStrength));
        Shader.SetGlobalVector("_PS_Chips", new Vector4(Mathf.Min(chipSizeMin, chipSizeMax) / 100f, Mathf.Max(chipSizeMin, chipSizeMax) / 100f, chipChance, mottleBrightness));
        Shader.SetGlobalVector("_PS_Haze", new Vector4(haze, hazeStart, hazeRange, 1f));
        Shader.SetGlobalVector("_PS_HazeLow", (Vector4)hazeLow);
        Shader.SetGlobalVector("_PS_HazeHigh", (Vector4)hazeHigh);
        Shader.SetGlobalVector("_PS_Turn", new Vector4(shadeHueTurn, complementTurn, 0f, 0f));
        Shader.SetGlobalVector("_PS_Water", new Vector4(waterBleed ? 1f : 0f, bleedBloom, bleedHueRange / 360f, bleedRim));
        Shader.SetGlobalVector("_PS_Mottle", new Vector4(mottle, colourPatches / 360f, floorMottleSize, pastelChips));
    }
}
