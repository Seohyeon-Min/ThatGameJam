using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Scatters flower prefabs on a terrain inside rectangular areas: random spots kept apart by a minimum spacing, gathered into
// patches by a large noise, each flower turned, sized and leaning with the slope at random. Generate and Clear are inspector
// buttons (FlowerGeneratorEditor). The same seed and settings give the same flowers.
public class FlowerGenerator : MonoBehaviour
{
    // Tries per flower to find a spot clear of the others: once the area is full, a spot keeps landing too close.
    const int TriesPerFlower = 30;
    // Above this many in one Generate, it stops and warns: likely a density typed too high.
    const int MaxFlowers = 20000;

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
    [Tooltip("Flowers per square metre, before patches thin them out")]
    [SerializeField, Min(0f)] float density = 2f;
    [Tooltip("No two flowers closer than this, in metres")]
    [SerializeField, Min(0.01f)] float minSpacing = 0.25f;
    [Tooltip("How much the flowers gather into patches. 0 = spread evenly, 1 = only in patches with bare ground between")]
    [SerializeField, Range(0f, 1f)] float clustering = 0.9f;
    [Tooltip("Size of a patch, in metres")]
    [SerializeField, Min(0.1f)] float patchSize = 4f;
    [SerializeField] Vector2 scaleRange = new(0.8f, 1.2f);
    [Tooltip("How much a flower leans with the slope of the ground. 0 = straight up")]
    [SerializeField, Range(0f, 1f)] float followSlope = 0.5f;
    [Tooltip("No flowers where the ground is steeper than this, in degrees")]
    [SerializeField, Range(0f, 90f)] float maxSlope = 35f;
    [Tooltip("Paths kept clear of flowers, such as the stages' routes: fewer flowers the nearer they are, fading in away from them")]
    [SerializeField] BabyRoute[] paths = { };
    [Tooltip("How strongly the paths are kept clear. 0 = paths ignored, 1 = no flowers at all on them")]
    [SerializeField, Range(0f, 1f)] float pathClearing = 1f;
    [Tooltip("Distance from a path's line that is cleared fully, in metres")]
    [SerializeField, Min(0f)] float pathWidth = 1.5f;
    [Tooltip("Distance beyond Path Width over which the flowers come back to full, in metres")]
    [SerializeField, Min(0.01f)] float pathFade = 3f;
    [SerializeField] int seed = 1;
    [Tooltip("Flowers stand on this terrain's surface. Empty: the scene's active terrain")]
    [SerializeField] Terrain ground;
    // What Generate made, so Clear removes only these.
    [SerializeField, HideInInspector] List<GameObject> flowers = new();

    public int FlowerCount => flowers.Count;

#if UNITY_EDITOR
    // Replaces this generator's flowers with new ones, as one undo step.
    public void Generate()
    {
        if (prefabs.Length == 0 || !System.Array.TrueForAll(prefabs, p => p != null && PrefabUtility.IsPartOfPrefabAsset(p)))
        {
            Debug.LogWarning("FlowerGenerator: fill every flower prefab slot with a prefab asset first.", this);
            return;
        }
        Terrain terrain = ground != null ? ground : Terrain.activeTerrain;
        if (terrain == null)
        {
            Debug.LogWarning("FlowerGenerator: no terrain to stand on. Set Ground.", this);
            return;
        }
        int group = Undo.GetCurrentGroup();
        Clear();

        var random = new System.Random(seed);
        // Where the patches are also follows the seed.
        var patchOffset = new Vector2(random.Next(10000), random.Next(10000));
        var spots = new Dictionary<Vector2Int, List<Vector2>>();
        var made = new List<GameObject>();
        foreach (Area area in areas)
        {
            Vector2 size = area.max - area.min;
            int wanted = Mathf.RoundToInt(Mathf.Abs(size.x * size.y) * density);
            if (made.Count + wanted > MaxFlowers)
            {
                Debug.LogWarning($"FlowerGenerator: over {MaxFlowers} flowers asked for. Lower the density or the areas.", this);
                break;
            }
            for (int i = 0; i < wanted; i++)
            {
                // Each flower looks for a spot clear of the others; if the area is full, it is left out.
                Vector2 local = Vector2.zero;
                bool found = false;
                for (int tries = 0; tries < TriesPerFlower && !found; tries++)
                {
                    local = new Vector2(Mathf.Lerp(area.min.x, area.max.x, (float)random.NextDouble()), Mathf.Lerp(area.min.y, area.max.y, (float)random.NextDouble()));
                    found = !TooClose(spots, local);
                }
                // Drawn even when unused, so a setting that drops a flower doesn't shift the ones after it.
                float keep = (float)random.NextDouble();
                int kind = random.Next(prefabs.Length);
                float yaw = (float)random.NextDouble() * 360f;
                float scale = Mathf.Lerp(scaleRange.x, scaleRange.y, (float)random.NextDouble());
                if (!found)
                    continue;

                // Outside the patches most flowers are dropped, not moved, so the bare ground stays bare.
                float patch = Mathf.PerlinNoise(local.x / patchSize + patchOffset.x, local.y / patchSize + patchOffset.y);
                float chance = Mathf.Lerp(1f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.4f, 0.65f, patch)), clustering);
                Vector3 point = transform.TransformPoint(new Vector3(local.x, 0f, local.y));
                chance *= PathFactor(point);
                if (keep > chance)
                    continue;
                if (!OnTerrain(terrain, point, out Vector3 world, out Vector3 normal))
                    continue;
                if (Vector3.Angle(Vector3.up, normal) > maxSlope)
                    continue;

                Remember(spots, local);
                Quaternion lean = Quaternion.FromToRotation(Vector3.up, Vector3.Slerp(Vector3.up, normal, followSlope));
                var flower = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[kind], transform);
                flower.transform.SetPositionAndRotation(world, lean * Quaternion.Euler(0f, yaw, 0f));
                flower.transform.localScale = Vector3.one * scale;
                Undo.RegisterCreatedObjectUndo(flower, "Generate Flowers");
                made.Add(flower);
            }
        }
        // After the flowers: registering a created object flushes a pending change to the list.
        Undo.RecordObject(this, "Generate Flowers");
        flowers.AddRange(made);
        Undo.SetCurrentGroupName("Generate Flowers");
        Undo.CollapseUndoOperations(group);
    }

    // Removes only the flowers this generator made.
    public void Clear()
    {
        // The list changes before the flowers go: destroying with Undo flushes the recorded change.
        GameObject[] old = flowers.ToArray();
        Undo.SetCurrentGroupName("Clear Flowers");
        Undo.RecordObject(this, "Clear Flowers");
        flowers.Clear();
        foreach (GameObject flower in old)
            if (flower != null)
                Undo.DestroyObjectImmediate(flower);
    }
#endif

    // 1 away from every path, falling to 1 - pathClearing on one, smoothly in between.
    float PathFactor(Vector3 point)
    {
        if (pathClearing <= 0f || paths.Length == 0)
            return 1f;
        float nearest = float.MaxValue;
        var flat = new Vector2(point.x, point.z);
        foreach (BabyRoute path in paths)
        {
            if (path == null || path.Count == 0)
                continue;
            for (int i = 0; i < path.Count; i++)
            {
                Vector3 a = path[i].position, b = path[Mathf.Min(i + 1, path.Count - 1)].position;
                nearest = Mathf.Min(nearest, DistanceToSegment(flat, new Vector2(a.x, a.z), new Vector2(b.x, b.z)));
            }
        }
        float away = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(pathWidth, pathWidth + pathFade, nearest));
        return Mathf.Lerp(1f, away, pathClearing);
    }

    static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float along = ab.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector2.Dot(point - a, ab) / ab.sqrMagnitude) : 0f;
        return Vector2.Distance(point, a + ab * along);
    }

    // Spots are kept in cells of minSpacing, so only the 3 x 3 cells around a spot need checking.
    Vector2Int Cell(Vector2 local) => new(Mathf.FloorToInt(local.x / minSpacing), Mathf.FloorToInt(local.y / minSpacing));

    bool TooClose(Dictionary<Vector2Int, List<Vector2>> spots, Vector2 local)
    {
        Vector2Int cell = Cell(local);
        for (int x = -1; x <= 1; x++)
            for (int z = -1; z <= 1; z++)
                if (spots.TryGetValue(cell + new Vector2Int(x, z), out List<Vector2> near))
                    foreach (Vector2 other in near)
                        if ((other - local).sqrMagnitude < minSpacing * minSpacing)
                            return true;
        return false;
    }

    void Remember(Dictionary<Vector2Int, List<Vector2>> spots, Vector2 local)
    {
        Vector2Int cell = Cell(local);
        if (!spots.TryGetValue(cell, out List<Vector2> list))
            spots[cell] = list = new List<Vector2>();
        list.Add(local);
    }

    // The point on the terrain's surface straight above or below, and the surface's up there. False off the terrain.
    static bool OnTerrain(Terrain terrain, Vector3 point, out Vector3 world, out Vector3 normal)
    {
        Vector3 local = point - terrain.transform.position;
        Vector3 size = terrain.terrainData.size;
        float u = local.x / size.x, v = local.z / size.z;
        world = point;
        normal = Vector3.up;
        if (u < 0f || u > 1f || v < 0f || v > 1f)
            return false;
        world.y = terrain.transform.position.y + terrain.SampleHeight(point);
        normal = terrain.terrainData.GetInterpolatedNormal(u, v);
        return true;
    }

    void OnDrawGizmos()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(1f, 0.55f, 0.75f);
        foreach (Area area in areas)
        {
            Vector2 center = (area.min + area.max) / 2f;
            Vector2 size = area.max - area.min;
            Gizmos.DrawWireCube(new Vector3(center.x, 0f, center.y), new Vector3(size.x, 0f, size.y));
        }
    }
}
