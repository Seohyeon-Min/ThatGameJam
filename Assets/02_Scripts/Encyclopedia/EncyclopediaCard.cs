using UnityEngine;
using UnityEngine.UI;

// One slot of the encyclopedia page: number and name on one line, the photo below. "???" and no photo until found.
public class EncyclopediaCard : MonoBehaviour
{
    [SerializeField] Text label;
    [SerializeField] RawImage photo;

    public void Show(int entryNumber, EncyclopediaEntry entry, Texture foundPhoto)
    {
        bool found = foundPhoto != null;
        label.text = $"No. {entryNumber:00}  {(found ? entry.title : "???")}";
        photo.texture = foundPhoto;
        photo.enabled = found;
    }
}
