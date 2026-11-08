using UnityEngine;

// The encyclopedia entry an easter egg fills in when it shows itself. Reveal takes the photo at that moment (Encyclopedia.Discover).
public class Discovery : MonoBehaviour
{
    [SerializeField] EncyclopediaEntry entry;
    [Tooltip("What the photo frames. Empty: this object.")]
    [SerializeField] Transform subject;
    [Tooltip("Optional. An off camera placed where the photo should be taken from. Empty: framed from the game camera's angle.")]
    [SerializeField] Camera photoView;
    [Tooltip("The player's bird shows in the photo. Off: it is left out, so it does not cover what was found.")]
    [SerializeField] bool showBird;

    public void Reveal() => Reveal(null);

    // The photo framing this instead of the Subject, such as the bird that set it off. Null: the Subject.
    public void Reveal(Transform framing)
    {
        // A test scene without the encyclopedia.
        if (Encyclopedia.Instance == null)
            return;
        Encyclopedia.Instance.Discover(entry, framing != null ? framing : subject != null ? subject : transform, photoView, showBird);
    }
}
