using UnityEngine;

// The animal at one fork of the fog maze (ADR 0028): it stays where it is, on the right way. FogMaze has it answer a cry, which
// shows a ring of sound around it. Once it has answered, the girl knows the right way at its fork.
public class FogForkAnimal : MonoBehaviour
{
    // The animal's mesh, a placeholder for now: the ring is centred on it and about its size.
    [SerializeField] Renderer body;
    [SerializeField] CryRing ring;

    public bool HasAnswered { get; private set; }

    public void Answer()
    {
        HasAnswered = true;
        Bounds bounds = body.bounds;
        ring.Play(bounds.center, bounds.extents.magnitude);
    }
}
