using UnityEngine;

// Pins the Strokes filter (WatercolorKuwahara, Filter Mode = Strokes) to a character: its strokes are laid out around Follow on
// the screen, sized to it and turned with it, so they stay put on the character like a texture as it or the camera moves,
// instead of the character sliding under strokes fixed to the screen. One in the scene, on the baby.
// Runs in Edit Mode too, so the strokes look the same when previewing a cutscene there.
[ExecuteAlways]
public class PaintStrokeAnchor : MonoBehaviour
{
    [Tooltip("What the strokes stay on: the baby's head bone, so they turn as the head tilts. Empty uses this object")]
    [SerializeField] Transform follow;

    public static PaintStrokeAnchor Current { get; private set; }

    public Transform Follow => follow != null ? follow : transform;

    void OnEnable() => Current = this;

    void OnDisable()
    {
        if (Current == this)
            Current = null;
    }
}
