using UnityEngine;

// The animal at one fork of the fog maze (ADR 0028): it stays where it is, on the right way. FogMaze has it answer a cry, which
// plays the same cry the bird gives (a BirdCryPulse at its middle), drawn over the fog. Off the screen, the cry shows on the screen's
// edge toward it, centred on the edge so half of it shows, as long as it plays (ADR 0031). Once it has answered, the girl knows the
// right way at its fork.
public class FogForkAnimal : MonoBehaviour
{
    // The animal's mesh, a placeholder for now: the ring is centred on it and about its size.
    [SerializeField] Renderer body;
    // The old placeholder ring, no longer played.
    [SerializeField] CryRing ring;
    [Tooltip("The cry it answers with: the BirdCryPulse under it (the bird's own cry), on the AboveFog layer so it shows over the dream fog. Every particle system in it plays.")]
    [SerializeField] GameObject cry;
    [Tooltip("On the screen's edge the cry is this far from the camera, nearer than the trees, which would hide it. It is scaled down to look as big as the bird's own cry.")]
    [SerializeField, Min(0.5f)] float edgeDepth = 4f;

    ParticleSystem[] systems;

    public bool HasAnswered { get; private set; }

    void Awake()
    {
        if (cry != null)
            systems = cry.GetComponentsInChildren<ParticleSystem>(true);
    }

    public void Answer()
    {
        HasAnswered = true;
        if (cry == null)
        {
            Debug.LogWarning("FogForkAnimal: no Cry set, so it answers with nothing. Set it to the BirdCryPulse under it.", this);
            return;
        }
        PlaceCry();
        // The pulse's ring and sparkles are separate systems on its children.
        foreach (ParticleSystem system in systems)
            system.Play(false);
    }

    // Its particles move with it (local simulation), so it follows the screen's edge while the camera moves.
    void LateUpdate()
    {
        if (systems != null && System.Array.Exists(systems, system => system.IsAlive(false)))
            PlaceCry();
    }

    // On the animal, or if it is off the screen, on the screen's edge toward it.
    void PlaceCry()
    {
        Vector3 center = body.bounds.center;
        Camera view = Camera.main;
        Vector3 viewport = view.WorldToViewportPoint(center);
        float scale = 1f;
        if (viewport.z <= 0f || viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f)
        {
            // Behind the camera the projection comes out mirrored.
            if (viewport.z < 0f)
                viewport = new Vector3(1f - viewport.x, 1f - viewport.y, -viewport.z);
            // Out from the middle toward it, in screen proportions, to the first edge.
            var toward = new Vector2((viewport.x - 0.5f) * view.aspect, viewport.y - 0.5f);
            if (toward.sqrMagnitude < 1e-6f)
                toward = Vector2.up;
            toward *= Mathf.Min(0.5f * view.aspect / Mathf.Max(Mathf.Abs(toward.x), 1e-6f), 0.5f / Mathf.Max(Mathf.Abs(toward.y), 1e-6f));
            center = view.ViewportToWorldPoint(new Vector3(0.5f + toward.x / view.aspect, 0.5f + toward.y, edgeDepth));
            scale = edgeDepth / view.WorldToViewportPoint(BirdController.Instance.transform.position).z;
        }
        cry.transform.position = center;
        // Each system scales by its own transform only (Local scaling).
        foreach (ParticleSystem system in systems)
            system.transform.localScale = Vector3.one * scale;
    }
}
