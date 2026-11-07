using UnityEngine;

// How the flowers that bloom with the spring ring spring up (a ScatterGenerator area's Bloom With Ring): as the ring's edge, as it shows
// on the screen with its lumps (SeasonWipe), comes to each one, it grows up out of nothing white and a little too big, then settles to
// its own size and colour, a little after or before its neighbours, so a field of them comes up in a rush along the edge. Behind the
// edge they are as they always are; ahead of it, under the old look's photo, they are not drawn yet. With no ring playing they are as
// they always are. One in the scene is enough; WatercolorFlat reads it from a shader global (_WC_RingBloom).
[ExecuteAlways]
public class RingBloom : MonoBehaviour
{
    static readonly int RingBloomId = Shader.PropertyToID("_WC_RingBloom");
    static readonly int RingBloomGlowId = Shader.PropertyToID("_WC_RingBloomGlow");

    [Tooltip("Metres behind the ring's edge over which each one comes up and settles. Larger is slower and softer.")]
    [SerializeField, Min(0.1f)] float band = 3f;
    [Tooltip("How much bigger than its own size it gets on the way, at the most (0.4 = 40% bigger).")]
    [SerializeField, Range(0f, 1.5f)] float overshoot = 0.45f;
    [Tooltip("How white it starts (0 = its own colour, 1 = white), fading to its own colour as it settles.")]
    [SerializeField, Range(0f, 1f)] float white = 0.9f;
    [Tooltip("Metres each one comes up after or before the edge, at random, so they don't come up in a clean line.")]
    [SerializeField, Min(0f)] float stagger = 1.5f;
    [Tooltip("The light it gives off as it comes up, fading as it settles, added over its colour. HDR: above 1 the Bloom post effect spreads it into a glow. Black: no glow, only White")]
    [SerializeField, ColorUsage(false, true)] Color glow = new(4f, 3.8f, 3.2f);

    void OnEnable() => Apply();
    void OnValidate()
    {
        if (isActiveAndEnabled)
            Apply();
    }
    void OnDisable()
    {
        Shader.SetGlobalVector(RingBloomId, Vector4.zero);
        Shader.SetGlobalVector(RingBloomGlowId, Vector4.zero);
    }

    void Apply()
    {
        Shader.SetGlobalVector(RingBloomId, new Vector4(band, overshoot, white, stagger));
        // As typed: the shader adds it to its linear colour.
        Shader.SetGlobalVector(RingBloomGlowId, glow);
    }
}
