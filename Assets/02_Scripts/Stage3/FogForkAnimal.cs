using UnityEngine;

// The animal at one fork of the fog maze (ADR 0028): it stays where it is, on the right way. FogMaze has it answer a cry, which
// plays the same cry the bird gives (a BirdCryPulse at its middle), drawn over the fog. Once it has answered, the girl knows the right
// way at its fork.
public class FogForkAnimal : MonoBehaviour
{
    // The animal's mesh, a placeholder for now: the ring is centred on it and about its size.
    [SerializeField] Renderer body;
    // The old placeholder ring, no longer played.
    [SerializeField] CryRing ring;
    [Tooltip("The cry it answers with: the BirdCryPulse under it (the bird's own cry), on the AboveFog layer so it shows over the dream fog. Every particle system in it plays.")]
    [SerializeField] GameObject cry;

    public bool HasAnswered { get; private set; }

    public void Answer()
    {
        HasAnswered = true;
        Bounds bounds = body.bounds;
        if (cry == null)
        {
            Debug.LogWarning("FogForkAnimal: no Cry set, so it answers with nothing. Set it to the BirdCryPulse under it.", this);
            return;
        }
        cry.transform.position = bounds.center;
        // The pulse's ring and sparkles are separate systems on its children.
        foreach (ParticleSystem system in cry.GetComponentsInChildren<ParticleSystem>(true))
            system.Play(false);
    }

}
