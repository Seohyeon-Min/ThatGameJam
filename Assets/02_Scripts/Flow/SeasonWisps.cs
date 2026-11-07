using System;
using UnityEngine;

// The swirl of wind that rides the spring ring's front (the SnowWisps shader) at every turn of the season, in five steps:
// step 1 faint and slow to build up, each one stronger, and step 5 the most special (the ending's), a glowing swirl. Which step
// each season level plays is set below (Step By Level). It comes up and goes by the seconds since the ring began, not by the
// ring's size, since the ring bursts out at first. The quad it is drawn on is a child, disabled until a ring plays; it is laid
// over the ring's centre while the ring spreads and switched off when it ends.
public class SeasonWisps : MonoBehaviour
{
    static readonly int SpringRingId = Shader.PropertyToID("_SpringRing");
    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int StrokesId = Shader.PropertyToID("_Strokes");
    static readonly int WidthId = Shader.PropertyToID("_Width");
    static readonly int SpiralId = Shader.PropertyToID("_Spiral");
    static readonly int SwirlId = Shader.PropertyToID("_Swirl");
    static readonly int DryBrushId = Shader.PropertyToID("_DryBrush");
    static readonly int FadeRadiusId = Shader.PropertyToID("_FadeRadius");
    static readonly int AppearId = Shader.PropertyToID("_Appear");

    [Serializable]
    public class Step
    {
        [Tooltip("Colour and strength (alpha). HDR: above 1 the Bloom post effect makes it glow.")]
        [ColorUsage(true, true)] public Color color = new(1f, 1f, 1f, 0.95f);
        [Range(1, 24)] public int strokes = 14;
        [Tooltip("Thickest stroke, in metres.")]
        [Min(0f)] public float width = 1.1f;
        [Tooltip("How far each stroke's spiral opens up along it.")]
        [Range(0f, 1f)] public float spiral = 0.35f;
        [Tooltip("How far the strokes turn as the ring grows: small is a slow swirl.")]
        [Min(0f)] public float swirl = 0.05f;
        [Range(0f, 1f)] public float dryBrush = 0.8f;
        [Tooltip("Seconds from the ring's start to full strength: large is a slow build-up from nothing.")]
        [Min(0f)] public float buildUp = 1f;
        [Tooltip("Seconds from the ring's start by which it has gone; it fades over the last 40% of them.")]
        [Min(0.1f)] public float goneBy = 8f;
    }

    [SerializeField] Season season;
    [Tooltip("The quad it is drawn on (SnowWisps material), flat, under this object. Must reach past where the strokes go.")]
    [SerializeField] MeshRenderer quad;
    [Tooltip("Height above the ring's centre, in metres.")]
    [SerializeField] float height = 2f;
    [Tooltip("Step 1 to 5, in order.")]
    [SerializeField] Step[] steps = new Step[5];
    [Tooltip("The step each season level plays when the season turns to it (index = level, 0 = none). Level 4 is the ending.")]
    [SerializeField] int[] stepByLevel = { 0, 1, 2, 3, 5 };

    MaterialPropertyBlock block;
    Step playing;
    float startedAt;

    void OnDisable() => Hide();

    void LateUpdate()
    {
        Vector4 ring = Shader.GetGlobalVector(SpringRingId);
        if (ring.w <= 0f)
        {
            Hide();
            return;
        }
        if (playing == null && !Show())
            return;
        quad.transform.position = (Vector3)ring + Vector3.up * height;
        float t = Time.time - startedAt;
        float up = playing.buildUp > 0f ? Mathf.SmoothStep(0f, 1f, t / playing.buildUp) : 1f;
        float down = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(playing.goneBy * 0.6f, playing.goneBy, t));
        block.SetFloat(AppearId, up * down);
        quad.SetPropertyBlock(block);
    }

    // As a ring starts: the step for the season it turned to. False when this level has none.
    bool Show()
    {
        int level = season != null ? season.Level : 0;
        int number = level >= 0 && level < stepByLevel.Length ? stepByLevel[level] : 0;
        if (number < 1 || number > steps.Length || steps[number - 1] == null || quad == null)
            return false;
        playing = steps[number - 1];
        startedAt = Time.time;
        block ??= new MaterialPropertyBlock();
        block.SetColor(ColorId, playing.color);
        block.SetFloat(StrokesId, playing.strokes);
        block.SetFloat(WidthId, playing.width);
        block.SetFloat(SpiralId, playing.spiral);
        block.SetFloat(SwirlId, playing.swirl);
        block.SetFloat(DryBrushId, playing.dryBrush);
        // Gone by time (Gone By), not by the ring's size.
        block.SetFloat(FadeRadiusId, 10000f);
        block.SetFloat(AppearId, 0f);
        quad.SetPropertyBlock(block);
        quad.gameObject.SetActive(true);
        return true;
    }

    void Hide()
    {
        if (playing == null)
            return;
        playing = null;
        if (quad != null)
            quad.gameObject.SetActive(false);
    }
}
