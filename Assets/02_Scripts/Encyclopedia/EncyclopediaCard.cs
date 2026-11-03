using UnityEngine;
using UnityEngine.UI;

// One slot of the encyclopedia page. Shows "???" until its entry is found.
public class EncyclopediaCard : MonoBehaviour
{
    [SerializeField] Text number;
    [SerializeField] Text title;
    [SerializeField] Text description;
    [SerializeField] Image background;
    [SerializeField] Color foundColor = new Color(0.36f, 0.3f, 0.2f, 1f);
    [SerializeField] Color lockedColor = new Color(0.17f, 0.19f, 0.23f, 1f);

    public void Show(int entryNumber, EncyclopediaEntry entry, bool found)
    {
        number.text = $"No. {entryNumber:00}";
        title.text = found ? entry.title : "???";
        description.text = found ? entry.description : "";
        background.color = found ? foundColor : lockedColor;
    }
}
