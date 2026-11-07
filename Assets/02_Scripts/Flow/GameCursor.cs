using UnityEngine;

// The mouse cursor: a plain white arrow with a soft dark blur under it, so it reads on the bright snow too. While the girl still
// sleeps at the start (the intro's dive over, the bird the player's), it is the right-click mouse instead, blinking: a cry wakes her.
// Anything else that wants its own cursor for a while (the bird's perch cursor) asks for it with Override.
public class GameCursor : MonoBehaviour
{
    [SerializeField] Texture2D arrow;
    [Tooltip("The arrow's point, in its pixels from the top left.")]
    [SerializeField] Vector2 arrowHotspot = new(6f, 4f);
    [Tooltip("The right-click mouse, bright and dim: it blinks between the two while she sleeps at the start.")]
    [SerializeField] Texture2D wakeHint;
    [SerializeField] Texture2D wakeHintDim;
    [SerializeField] Vector2 wakeHintHotspot = new(32f, 32f);
    [Tooltip("Seconds it stays bright, then dim, in each blink.")]
    [SerializeField, Min(0.05f)] float brightSeconds = 0.55f;
    [SerializeField, Min(0.05f)] float dimSeconds = 0.35f;

    static Texture2D overrideTexture;
    static Vector2 overrideHotspot;

    Texture2D shown;

    // Another cursor until Override(null): it goes over both the arrow and the hint.
    public static void Override(Texture2D texture, Vector2 hotspot)
    {
        overrideTexture = texture;
        overrideHotspot = hotspot;
    }

    void Update()
    {
        Texture2D texture = arrow;
        Vector2 hotspot = arrowHotspot;
        if (overrideTexture != null)
        {
            texture = overrideTexture;
            hotspot = overrideHotspot;
        }
        else if (WakeHintWanted && wakeHint != null)
        {
            bool bright = Time.unscaledTime % (brightSeconds + dimSeconds) < brightSeconds;
            texture = bright || wakeHintDim == null ? wakeHint : wakeHintDim;
            hotspot = wakeHintHotspot;
        }
        if (texture == shown)
            return;
        shown = texture;
        Cursor.SetCursor(texture, hotspot, CursorMode.Auto);
    }

    void OnDisable()
    {
        shown = null;
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }

    // She sleeps where the game starts, and the bird is the player's (the intro is over), with nothing open over the game.
    static bool WakeHintWanted
    {
        get
        {
            if (IntroSequence.IsPlaying || Encyclopedia.IsOpen)
                return false;
            Baby baby = Baby.Instance;
            GameFlow flow = GameFlow.Instance;
            return baby != null && baby.State == BabyState.Sleeping
                && flow != null && flow.Stages.Count > 0 && flow.CurrentStage == flow.Stages[0];
        }
    }
}
