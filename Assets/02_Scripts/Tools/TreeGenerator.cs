using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Scatters tree prefabs over rectangular areas: a jittered grid, each tree turned and sized at random,
// and neighbours never share a kind if it can be helped. Generate and Clear are inspector buttons (TreeGeneratorEditor).
// The same seed and settings give the same trees.
public class TreeGenerator : MonoBehaviour
{
    // Trees closer than spacing times this are next to each other: the grid neighbours along X and Z, not the diagonal ones.
    const float NeighbourFactor = 1.3f;

    [System.Serializable]
    public struct Area
    {
        [Tooltip("Corner (X, Z) in this object's local space")]
        public Vector2 min;
        [Tooltip("Opposite corner (X, Z) in this object's local space")]
        public Vector2 max;
    }

    [SerializeField] GameObject[] prefabs = { };
    [SerializeField] Area[] areas = { };
    [Tooltip("Average distance between neighbouring trees")]
    [SerializeField, Min(0.1f)] float spacing = 1.7f;
    [Tooltip("How far a tree moves off its grid point, as a fraction of the spacing")]
    [SerializeField, Range(0f, 0.5f)] float jitter = 0.3f;
    [SerializeField] Vector2 scaleRange = new(0.85f, 1.15f);
    [SerializeField] bool randomRotation = true;
    [SerializeField] int seed = 1;
    [Tooltip("Trees stand on this terrain's surface. Empty: at this object's height")]
    [SerializeField] Terrain ground;
    // What Generate made, so Clear removes only these.
    [SerializeField, HideInInspector] List<GameObject> trees = new();

    public int TreeCount => trees.Count;

#if UNITY_EDITOR
    // Replaces this generator's trees with new ones, as one undo step.
    public void Generate()
    {
        if (prefabs.Length == 0 || !System.Array.TrueForAll(prefabs, p => p != null && PrefabUtility.IsPartOfPrefabAsset(p)))
        {
            Debug.LogWarning("TreeGenerator: fill every tree prefab slot with a prefab asset first.", this);
            return;
        }
        // Min on the field guards only the Inspector; a smaller spacing could loop forever.
        if (spacing < 0.1f)
        {
            Debug.LogWarning("TreeGenerator: spacing must be at least 0.1.", this);
            return;
        }
        int group = Undo.GetCurrentGroup();
        Clear();

        var random = new System.Random(seed);
        var made = new List<GameObject>();
        var placed = new List<(Vector3 position, int kind)>();
        var freeKinds = new List<int>();
        float amount = spacing * jitter;
        float neighbourDistance = spacing * NeighbourFactor;
        foreach (Area area in areas)
            for (float x = area.min.x + amount; x <= area.max.x - amount; x += spacing)
                for (float z = area.min.y + amount; z <= area.max.y - amount; z += spacing)
                {
                    var position = new Vector3(x + Jitter(random, amount), 0f, z + Jitter(random, amount));
                    // A kind no tree next to this one has; if every kind is taken, the kind whose nearest tree is farthest.
                    freeKinds.Clear();
                    int farthestKind = 0;
                    float farthest = -1f;
                    for (int kind = 0; kind < prefabs.Length; kind++)
                    {
                        float nearest = float.MaxValue;
                        foreach (var t in placed)
                            if (t.kind == kind)
                                nearest = Mathf.Min(nearest, Vector3.Distance(t.position, position));
                        if (nearest >= neighbourDistance)
                            freeKinds.Add(kind);
                        if (nearest > farthest)
                            (farthest, farthestKind) = (nearest, kind);
                    }
                    int chosen = freeKinds.Count > 0 ? freeKinds[random.Next(freeKinds.Count)] : farthestKind;
                    placed.Add((position, chosen));

                    // Both numbers are drawn even when unused, so switching rotation off or fixing the scale keeps the layout.
                    float yaw = (float)random.NextDouble() * 360f;
                    float scale = Mathf.Lerp(scaleRange.x, scaleRange.y, (float)random.NextDouble());
                    var tree = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[chosen], transform);
                    tree.transform.SetLocalPositionAndRotation(ground != null ? OnGround(position) : position, Quaternion.Euler(0f, randomRotation ? yaw : 0f, 0f));
                    tree.transform.localScale = Vector3.one * scale;
                    Undo.RegisterCreatedObjectUndo(tree, "Generate Trees");
                    made.Add(tree);
                }
        // After the trees: registering a created object flushes a pending change to the list.
        Undo.RecordObject(this, "Generate Trees");
        trees.AddRange(made);
        Undo.SetCurrentGroupName("Generate Trees");
        Undo.CollapseUndoOperations(group);
    }

    // Removes only the trees this generator made.
    public void Clear()
    {
        // The list changes before the trees go: destroying with Undo flushes the recorded change.
        GameObject[] old = trees.ToArray();
        Undo.SetCurrentGroupName("Clear Trees");
        Undo.RecordObject(this, "Clear Trees");
        trees.Clear();
        foreach (GameObject tree in old)
            if (tree != null)
                Undo.DestroyObjectImmediate(tree);
    }
#endif

    // The local position moved down or up onto the ground terrain's surface.
    Vector3 OnGround(Vector3 local)
    {
        Vector3 world = transform.TransformPoint(local);
        world.y = ground.transform.position.y + ground.SampleHeight(world);
        return transform.InverseTransformPoint(world);
    }

    static float Jitter(System.Random random, float amount) => ((float)random.NextDouble() * 2f - 1f) * amount;

    void OnDrawGizmos()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.3f, 0.9f, 0.4f);
        foreach (Area area in areas)
        {
            Vector2 center = (area.min + area.max) / 2f;
            Vector2 size = area.max - area.min;
            Gizmos.DrawWireCube(new Vector3(center.x, 0f, center.y), new Vector3(size.x, 0f, size.y));
        }
    }
}
