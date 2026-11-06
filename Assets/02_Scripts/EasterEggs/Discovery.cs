using UnityEngine;

// The encyclopedia entry an easter egg fills in when it shows itself. Reveal takes the photo at that moment (Encyclopedia.Discover).
public class Discovery : MonoBehaviour
{
    [SerializeField] EncyclopediaEntry entry;
    [Tooltip("What the photo frames. Empty: this object.")]
    [SerializeField] Transform subject;
    [Tooltip("Optional. An off camera placed where the photo should be taken from. Empty: framed from the game camera's angle.")]
    [SerializeField] Camera photoView;

    public void Reveal()
    {
        // A test scene without the encyclopedia.
        if (Encyclopedia.Instance == null)
            return;
        Encyclopedia.Instance.Discover(entry, subject != null ? subject : transform, photoView);
    }
}
