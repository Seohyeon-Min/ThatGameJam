using UnityEngine;

// On an object the player clicks for itself (an easter egg): a click on its Interactable colliders that is not a landing on an open perch
// does not send the bird on to the ground behind. Without it, such a click passes through, as on a branch that is off screen.
public class ClickCatcher : MonoBehaviour
{
}
