using UnityEngine;

// The animal at one fork of the fog maze (ADR 0028): it stays where it is, on the right way. FogMaze has it answer a cry, which
// plays the same cry the bird gives (a BirdCryPulse at its middle), drawn over the fog. Off the screen, the cry shows cut by the screen's
// edge toward it, smaller and less of it the farther the animal is from the bird, as long as it plays (ADR 0031, ADR 0032).
// Once it has answered, the girl knows the right way at its fork.
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
    [Tooltip("The ring's full radius at scale 1, in metres: half the Ring particle's largest size (BirdCryPulse's size curve, 3.6).")]
    [SerializeField, Min(0.01f)] float ringRadius = 1.8f;
    [Tooltip("On the screen's edge the cry shows by how far the animal is from the bird: as big as the bird's cry and Near Shown of it on the screen from Near Distance in (about where an animal goes off the screen), down to Far Scale of that size and Far Shown at Far Distance and beyond (metres; shown as a share of the ring's width).")]
    [SerializeField, Min(0f)] float nearDistance = 25f;
    [SerializeField, Min(0f)] float farDistance = 70f;
    [SerializeField, Range(0f, 1f)] float nearShown = 2f / 3f;
    [SerializeField, Range(0f, 1f)] float farShown = 0.2f;
    [SerializeField, Range(0f, 1f)] float farScale = 0.4f;

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

    // On the animal, or if it is off the screen, cut by the screen's edge toward it: the farther it is from the bird, the smaller and
    // the less of it shows.
    void PlaceCry()
    {
        Vector3 center = body.bounds.center;
        Camera view = Camera.main;
        Vector3 viewport = view.WorldToViewportPoint(center);
        float scale = 1f;
        if (viewport.z <= 0f || viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f)
        {
            Vector3 bird = BirdController.Instance.transform.position;
            float near = Mathf.InverseLerp(farDistance, nearDistance, Vector2.Distance(new Vector2(center.x, center.z), new Vector2(bird.x, bird.z)));
            // As big as the bird's cry: at the edge depth by as much smaller as it is nearer the camera than the bird.
            scale = edgeDepth / view.WorldToViewportPoint(bird).z * Mathf.Lerp(farScale, 1f, near);

            // Behind the camera the projection comes out mirrored.
            if (viewport.z < 0f)
                viewport = new Vector3(1f - viewport.x, 1f - viewport.y, -viewport.z);
            // In from the screen's edges, so this share of the ring's width is on the screen (half: centred on the edge), in units of
            // the screen's height.
            float screenHeight = 2f * edgeDepth * Mathf.Tan(view.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float inward = (2f * Mathf.Lerp(farShown, nearShown, near) - 1f) * ringRadius * scale / screenHeight;
            // Out from the middle toward it, in screen proportions, to the first side of the screen moved in by that much.
            var toward = new Vector2((viewport.x - 0.5f) * view.aspect, viewport.y - 0.5f);
            if (toward.sqrMagnitude < 1e-6f)
                toward = Vector2.up;
            toward *= Mathf.Min((0.5f * view.aspect - inward) / Mathf.Max(Mathf.Abs(toward.x), 1e-6f),
                (0.5f - inward) / Mathf.Max(Mathf.Abs(toward.y), 1e-6f));
            center = view.ViewportToWorldPoint(new Vector3(0.5f + toward.x / view.aspect, 0.5f + toward.y, edgeDepth));
        }
        cry.transform.position = center;
        // Each system scales by its own transform only (Local scaling).
        foreach (ParticleSystem system in systems)
            system.transform.localScale = Vector3.one * scale;
    }
}
