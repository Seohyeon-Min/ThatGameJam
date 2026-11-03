using UnityEngine;

// One interaction the player can discover. The Encyclopedia lists these in order; the number shown is that order.
[CreateAssetMenu(menuName = "ThatGameJam/Encyclopedia Entry")]
public class EncyclopediaEntry : ScriptableObject
{
    public string title;
    [TextArea] public string description;
}
