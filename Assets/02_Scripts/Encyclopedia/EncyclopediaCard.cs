using UnityEngine;
using TMPro;
using UnityEngine.UI;

// One slot of the encyclopedia page: the name on one line, the photo below filling its frame. Until found, the number and "???"
// and no photo.
public class EncyclopediaCard : MonoBehaviour
{
    [SerializeField] TMP_Text label;
    [SerializeField] RawImage photo;

    public void Show(int entryNumber, EncyclopediaEntry entry, Texture foundPhoto)
    {
        bool found = foundPhoto != null;
        // Found: just its name. Not yet: its number, and "???".
        label.text = found ? entry.title : $"No. {entryNumber:00}  ???";
        photo.texture = foundPhoto;
        photo.enabled = found;
    }
}
