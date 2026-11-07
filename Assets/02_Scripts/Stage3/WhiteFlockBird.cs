using UnityEngine;

// One of the white birds that sweep over the screen at the very end (EndingFlock). The bird model's Flap clip calls Flap() on
// every wingbeat (for the player bird's sound); dozens of these beating at once stay silent, so it is taken here and dropped.
[RequireComponent(typeof(Animator))]
public class WhiteFlockBird : MonoBehaviour
{
    public void Flap() { }
}
