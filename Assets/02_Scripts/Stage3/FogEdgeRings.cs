using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// While the baby is in the fog (FogMaze's FogField), faint cry rings bubble up at the edges of the screen, as if cries were heard
// from somewhere in the fog: the squirrel hollow's rings (CryHint), a little cluster at a time, each a little higher and to one side
// of the last, but stuck to the screen like a frame instead of to a place. Each cluster comes up at a new random place along the
// screen's edge: a big ring mostly half off the edge, a small one just inside it. Fades in as she enters the fog and out as she
// leaves it; rings already up finish on their own.
// The ring system is a child, simulated in the camera's own space (Custom simulation space, the main camera), so the rings stay put
// on the screen however the camera moves. On the AboveFog layer, so the fog doesn't cover them.
// Preview In Editor plays them outside Play Mode too, fog or not, so the values can be tuned while watching.
[ExecuteAlways]
public class FogEdgeRings : MonoBehaviour
{
    class Cluster
    {
        public Vector2 at;      // on the screen, in screen heights from its middle
        public Vector2 size;
        public int left, count;
        public float nextRing;
        public Vector2 last;
    }

    [Tooltip("The fog: the rings show while the baby is inside it.")]
    [SerializeField] FogField field;
    [Tooltip("Outside Play Mode: plays the rings as if she were in the fog, to tune them while watching.")]
    [SerializeField] bool previewInEditor;
    [Tooltip("The ring system (a child, simulated in the main camera's space, AboveFog layer).")]
    [SerializeField] ParticleSystem ring;
    [Tooltip("How far in front of the camera the rings are drawn, in metres. Only their size on the screen matters.")]
    [SerializeField, Min(0.5f)] float distance = 5f;
    [Tooltip("How faint: the ring's own colour's alpha times this.")]
    [SerializeField, Range(0f, 1f)] float opacity = 0.3f;
    [SerializeField, Min(0.01f)] float fadeSeconds = 1.5f;

    [Header("Clusters")]
    [Tooltip("Seconds between one cluster starting and the next, at random between the two (they may overlap).")]
    [SerializeField] Vector2 every = new(0.8f, 1.8f);
    [Tooltip("Rings in one cluster, at random between the two.")]
    [SerializeField] Vector2Int perCluster = new(2, 3);
    [Tooltip("Seconds between the rings of a cluster.")]
    [SerializeField, Min(0.01f)] float between = 0.16f;
    [Tooltip("How far each ring may be to the side of the last, and how far up a whole cluster goes, as shares of the screen's height.")]
    [SerializeField, Min(0f)] float spread = 0.03f;
    [SerializeField, Min(0f)] float rise = 0.05f;

    [Header("Where And How Big")]
    [Tooltip("Share of clusters that are big rings; the rest are small ones.")]
    [SerializeField, Range(0f, 1f)] float bigShare = 0.45f;
    [Tooltip("A big ring's size, as a share of the screen's height, at random between the two.")]
    [SerializeField] Vector2 bigSize = new(0.14f, 0.32f);
    [Tooltip("A small ring's size, as a share of the screen's height.")]
    [SerializeField] Vector2 smallSize = new(0.015f, 0.035f);
    [Tooltip("Where a big ring's middle is, across the edge, as a share of the screen's height: below 0 is off the screen.")]
    [SerializeField] Vector2 bigInset = new(-0.06f, 0.04f);
    [Tooltip("Where a small ring is, inside the edge, as a share of the screen's height.")]
    [SerializeField] Vector2 smallInset = new(0.01f, 0.05f);

    readonly List<Cluster> clusters = new();
    Color colour;
    float weight;
    float nextCluster;
    float now;

    void OnEnable() => colour = ring != null ? ring.main.startColor.color : Color.white;

    void LateUpdate()
    {
        Camera view = Camera.main;
        if (view == null || ring == null)
            return;
        bool preview = !Application.isPlaying;
        float deltaTime = Time.deltaTime;
#if UNITY_EDITOR
        if (preview)
        {
            if (!previewInEditor)
            {
                if (ring.particleCount > 0)
                    ring.Clear();
                clusters.Clear();
                return;
            }
            // Outside Play Mode nothing ticks by itself: its own clock, the particles stepped by hand, and the next frame asked for.
            float clock = (float)EditorApplication.timeSinceStartup;
            deltaTime = Mathf.Clamp(clock - now, 0f, 0.1f);
            colour = ring.main.startColor.color;
            EditorApplication.QueuePlayerLoopUpdate();
        }
#endif
        now = preview ? now + deltaTime : Time.time;
        bool inside = preview || (Baby.Instance != null && field != null && field.Contains(Baby.Instance.transform.position));
        weight = Mathf.MoveTowards(weight, inside ? 1f : 0f, deltaTime / fadeSeconds);
        if (preview)
            ring.Simulate(deltaTime, true, false, false);
        if (weight <= 0f)
        {
            clusters.Clear();
            return;
        }

        float height = 2f * distance * Mathf.Tan(view.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float aspect = view.aspect;
        if (now >= nextCluster)
        {
            nextCluster = now + Random.Range(every.x, every.y);
            clusters.Add(NewCluster(aspect));
        }
        for (int i = clusters.Count - 1; i >= 0; i--)
            if (!Pop(clusters[i], height))
                clusters.RemoveAt(i);
    }

    // A random place along the screen's edge (a side picked by its length), and its rings' size.
    Cluster NewCluster(float aspect)
    {
        bool big = Random.value < bigShare;
        float inset = big ? Random.Range(bigInset.x, bigInset.y) : Random.Range(smallInset.x, smallInset.y);
        float halfWidth = aspect * 0.5f;
        float pick = Random.value * (aspect + 1f);
        Vector2 at;
        if (pick < aspect * 0.5f)
            at = new Vector2(Random.Range(-halfWidth, halfWidth), 0.5f - inset);        // top
        else if (pick < aspect)
            at = new Vector2(Random.Range(-halfWidth, halfWidth), -0.5f + inset);       // bottom
        else if (pick < aspect + 0.5f)
            at = new Vector2(-halfWidth + inset, Random.Range(-0.5f, 0.5f));            // left
        else
            at = new Vector2(halfWidth - inset, Random.Range(-0.5f, 0.5f));             // right
        int count = Random.Range(perCluster.x, perCluster.y + 1);
        return new Cluster
        {
            at = at,
            size = big ? bigSize : smallSize,
            count = count,
            left = count,
            nextRing = now,
        };
    }

    // Emits the cluster's next ring when it is due. False once it has emitted them all.
    bool Pop(Cluster cluster, float height)
    {
        if (cluster.left == 0)
            return false;
        if (now < cluster.nextRing)
            return true;
        cluster.nextRing += between;
        int index = cluster.count - cluster.left;
        cluster.left--;
        Vector2 side = Random.insideUnitCircle * spread;
        cluster.last = new Vector2(Mathf.Clamp(cluster.last.x + side.x, -spread, spread),
            rise * index / Mathf.Max(1, cluster.count - 1) + side.y * 0.3f);
        Vector2 onScreen = cluster.at + cluster.last;
        Color c = colour;
        c.a *= opacity * weight;
        ring.Emit(new ParticleSystem.EmitParams
        {
            position = new Vector3(onScreen.x * height, onScreen.y * height, distance),
            startSize = Random.Range(cluster.size.x, cluster.size.y) * height,
            startColor = c,
            applyShapeToPosition = false,
        }, 1);
        return true;
    }
}
