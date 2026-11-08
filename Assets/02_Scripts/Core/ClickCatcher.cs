using FMODUnity;
using UnityEngine;

// On an object the player clicks for itself (an easter egg): a click on its Interactable colliders that is not a landing on an open perch
// does not send the bird on to the ground behind. Without it, such a click passes through, as on a branch that is off screen.
public class ClickCatcher : MonoBehaviour
{
    // On the same object, when this is an easter egg with an encyclopedia entry.
    Discovery discovery;

    void Awake() => discovery = GetComponent<Discovery>();

    // The click itself, whether or not it lands on an open perch - distinct from the discovery popup's own sound,
    // which plays later when the encyclopedia entry actually shows itself. Silent once already discovered, so it
    // doesn't keep clacking every time the player revisits a found egg.
    public void PlayInteractSound()
    {
        if (discovery != null && discovery.IsFound)
            return;
        RuntimeManager.PlayOneShot("event:/SFX/eastereggInteract", transform.position);
    }
}
