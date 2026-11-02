using UnityEngine;
using UnityEngine.Serialization;

// This instance's own turn of its WatercolorFlat colour: its hue moved a little, and faded towards white, such as grass still pale from
// the snow. Each instance carries its own (ScatterGenerator sets them per area); the material stays shared, so the SRP Batcher still
// batches them all. They reach the shader in the renderers' shader user value, beside WatercolorPaintedModel's variation
// (WatercolorPaintedModel.ApplyUserValue).
[ExecuteAlways]
public class WatercolorTint : MonoBehaviour
{
    // The most the hue can move either way, in turns of the colour wheel. The shader decodes the same.
    public const float MostHueShift = 0.25f;

    [Tooltip("0 = its own colour, 1 = white")]
    [FormerlySerializedAs("amount")]
    [Range(0f, 1f)] public float whiten;
    [Tooltip("How far its hue turns, in turns of the colour wheel (0.1 = 36 degrees). 0 = its own hue")]
    [Range(-MostHueShift, MostHueShift)] public float hueShift;

    void OnEnable() => WatercolorPaintedModel.ApplyUserValue(gameObject);
    void OnValidate() => WatercolorPaintedModel.ApplyUserValue(gameObject);
}
