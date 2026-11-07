using UnityEngine;

// Where a cry would be answered (the rabbit burrow, the squirrel's hollow): tiny, faint cry rings bubbling up there. Every so often a
// little cluster of them, one after another, each a little higher and to one side of the last, overlapping like bubbles. The rings
// are the BirdCryPulse's Ring system (in the CryHint prefab, simulated in world space so each stays where it popped), emitted one by
// one at their own size and faintness. Whoever owns the place turns it on and off with Playing.
public class CryHint : MonoBehaviour
{
    [Tooltip("The ring system (world simulation space): each ring is emitted from it.")]
    [SerializeField] ParticleSystem ring;
    [Tooltip("Pops while this is on, from the start.")]
    [SerializeField] bool playing = true;
    [Tooltip("Rings in one cluster, at random between the two.")]
    [SerializeField] Vector2Int perCluster = new(2, 4);
    [Tooltip("Seconds between the rings of a cluster.")]
    [SerializeField, Min(0.01f)] float between = 0.14f;
    [Tooltip("Seconds after a cluster before the next, at random between the two.")]
    [SerializeField] Vector2 pause = new(0.8f, 1.4f);
    [Tooltip("Each ring's size (m), at random between the two.")]
    [SerializeField] Vector2 size = new(0.06f, 0.12f);
    [Tooltip("How faint: the ring's own colour's alpha times this.")]
    [SerializeField, Range(0f, 1f)] float opacity = 0.35f;
    [Tooltip("How far (m) to the side each ring may be from the last, and how far up a whole cluster goes.")]
    [SerializeField, Min(0f)] float spread = 0.05f;
    [SerializeField, Min(0f)] float rise = 0.12f;

    Color colour;
    int left;
    int count;
    float nextRing;
    float nextCluster;
    Vector3 last;

    public bool Playing
    {
        get => playing;
        set => playing = value;
    }

    void Awake() => colour = ring.main.startColor.color;

    void Update()
    {
        if (left == 0)
        {
            if (!playing || Time.time < nextCluster)
                return;
            count = left = Random.Range(perCluster.x, perCluster.y + 1);
            nextRing = Time.time;
            nextCluster = Time.time + count * between + Random.Range(pause.x, pause.y);
            last = Vector3.zero;
        }
        if (Time.time < nextRing)
            return;
        nextRing += between;
        int index = count - left;
        left--;
        Vector2 side = Random.insideUnitCircle * spread;
        last = new Vector3(Mathf.Clamp(last.x + side.x, -spread, spread), rise * index / Mathf.Max(1, count - 1),
            Mathf.Clamp(last.z + side.y, -spread, spread));
        Color c = colour;
        c.a *= opacity;
        ring.Emit(new ParticleSystem.EmitParams
        {
            position = transform.position + last,
            startSize = Random.Range(size.x, size.y),
            startColor = c,
            applyShapeToPosition = false,
        }, 1);
    }
}
