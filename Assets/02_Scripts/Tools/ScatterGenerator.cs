using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Scatters prefabs on a terrain inside rectangular areas: random spots kept apart by a minimum spacing, gathered into patches by a large
// noise, each one turned, sized and leaning with the slope at random. Generate and Clear are inspector buttons (ScatterGeneratorEditor).
// The same seed and settings give the same result. FlowerGenerator and GrassGenerator are this; a subclass can thin out spots by Keep.
public abstract class ScatterGenerator : MonoBehaviour
{
    // Tries per object to find a spot clear of the others: once the area is full, a spot keeps landing too close.
    const int TriesPerObject = 30;
    // Above this many in one Generate, it stops and warns: likely a density typed too high.
    const int MaxObjects = 20000;

    [System.Serializable]
    public struct Area
    {
        [Tooltip("Covers this stage's Area (the orange box) and ignores Min and Max. Empty: Min and Max")]
        public Stage stage;
        [Tooltip("Corner (X, Z) in this object's local space")]
        public Vector2 min;
        [Tooltip("Opposite corner (X, Z) in this object's local space")]
        public Vector2 max;
        [Tooltip("Added to Density in this area only, per square metre: more in a later stage. 0 = the same as everywhere. Min Spacing still caps it")]
        public float extraDensity;
    }

    [SerializeField] protected GameObject[] prefabs = { };
    [SerializeField] protected Area[] areas = { };
    [Tooltip("Objects per square metre, before patches thin them out")]
    [SerializeField, Min(0f)] protected float density = 2f;
    [Tooltip("No two objects closer than this, in metres")]
    [SerializeField, Min(0.01f)] protected float minSpacing = 0.25f;
    [Tooltip("How much they gather into patches. 0 = spread evenly, 1 = only in patches with bare ground between")]
    [SerializeField, Range(0f, 1f)] protected float clustering = 0.9f;
    [Tooltip("Size of a patch, in metres")]
    [SerializeField, Min(0.1f)] protected float patchSize = 4f;
    [SerializeField] protected Vector2 scaleRange = new(0.8f, 1.2f);
    [Tooltip("How much each one leans with the slope of the ground. 0 = straight up")]
    [SerializeField, Range(0f, 1f)] protected float followSlope = 0.5f;
    [Tooltip("None where the ground is steeper than this, in degrees")]
    [SerializeField, Range(0f, 90f)] protected float maxSlope = 35f;
    [SerializeField] protected int seed = 1;
    [Tooltip("They stand on this terrain's surface. Empty: the scene's active terrain")]
    [SerializeField] protected Terrain ground;
    [Tooltip("On Generate, turns on Wind in the prefabs' watercolor materials that have it off, sized to each prefab's height. Materials already on keep their values")]
    [SerializeField] protected bool swayInWind = true;
    // What Generate made, so Clear removes only these.
    [FormerlySerializedAs("flowers")]
    [SerializeField, HideInInspector] List<GameObject> made = new();

    public int MadeCount => made.Count;

    // The share of spots kept at this world point, 0 to 1. All by default.
    protected virtual float Keep(Vector3 point) => 1f;

#if UNITY_EDITOR
    // Replaces this generator's objects with new ones, as one undo step.
    public void Generate()
    {
        string kind = GetType().Name;
        if (prefabs.Length == 0 || !System.Array.TrueForAll(prefabs, p => p != null && PrefabUtility.IsPartOfPrefabAsset(p)))
        {
            Debug.LogWarning($"{kind}: fill every prefab slot with a prefab asset first.", this);
            return;
        }
        Terrain terrain = ground != null ? ground : Terrain.activeTerrain;
        if (terrain == null)
        {
            Debug.LogWarning($"{kind}: no terrain to stand on. Set Ground.", this);
            return;
        }
        int group = Undo.GetCurrentGroup();
        Clear();
        if (swayInWind)
            foreach (GameObject prefab in prefabs)
                TurnOnWind(prefab);

        var random = new System.Random(seed);
        // Where the patches are also follows the seed.
        var patchOffset = new Vector2(random.Next(10000), random.Next(10000));
        var spots = new Dictionary<Vector2Int, List<Vector2>>();
        var placed = new List<GameObject>();
        foreach (Area given in areas)
        {
            Area area = Resolve(given);
            Vector2 size = area.max - area.min;
            int wanted = Mathf.RoundToInt(Mathf.Abs(size.x * size.y) * Mathf.Max(0f, density + area.extraDensity));
            if (placed.Count + wanted > MaxObjects)
            {
                Debug.LogWarning($"{kind}: over {MaxObjects} asked for. Lower the density or the areas.", this);
                break;
            }
            for (int i = 0; i < wanted; i++)
            {
                // Each one looks for a spot clear of the others; if the area is full, it is left out.
                Vector2 local = Vector2.zero;
                bool found = false;
                for (int tries = 0; tries < TriesPerObject && !found; tries++)
                {
                    local = new Vector2(Mathf.Lerp(area.min.x, area.max.x, (float)random.NextDouble()), Mathf.Lerp(area.min.y, area.max.y, (float)random.NextDouble()));
                    found = !TooClose(spots, local);
                }
                // Drawn even when unused, so a setting that drops one doesn't shift the ones after it.
                float keep = (float)random.NextDouble();
                int which = random.Next(prefabs.Length);
                float yaw = (float)random.NextDouble() * 360f;
                float scale = Mathf.Lerp(scaleRange.x, scaleRange.y, (float)random.NextDouble());
                if (!found)
                    continue;

                // Outside the patches most are dropped, not moved, so the bare ground stays bare.
                float patch = Mathf.PerlinNoise(local.x / patchSize + patchOffset.x, local.y / patchSize + patchOffset.y);
                float chance = Mathf.Lerp(1f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.4f, 0.65f, patch)), clustering);
                Vector3 point = transform.TransformPoint(new Vector3(local.x, 0f, local.y));
                chance *= Keep(point);
                if (keep > chance)
                    continue;
                if (!OnTerrain(terrain, point, out Vector3 world, out Vector3 normal))
                    continue;
                if (Vector3.Angle(Vector3.up, normal) > maxSlope)
                    continue;

                Remember(spots, local);
                Quaternion lean = Quaternion.FromToRotation(Vector3.up, Vector3.Slerp(Vector3.up, normal, followSlope));
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[which], transform);
                instance.transform.SetPositionAndRotation(world, lean * Quaternion.Euler(0f, yaw, 0f));
                instance.transform.localScale = Vector3.one * scale;
                Undo.RegisterCreatedObjectUndo(instance, $"Generate {kind}");
                placed.Add(instance);
            }
        }
        // After the objects: registering a created object flushes a pending change to the list.
        Undo.RecordObject(this, $"Generate {kind}");
        made.AddRange(placed);
        Undo.SetCurrentGroupName($"Generate {kind}");
        Undo.CollapseUndoOperations(group);
    }

    // Wind on in the prefab's WatercolorFlat materials that have it off: Top Height is the prefab's height, the sway a share of it.
    void TurnOnWind(GameObject prefab)
    {
        const float SwayShareOfHeight = 0.12f;
        float top = 0f;
        foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            if (filter.sharedMesh != null)
                top = Mathf.Max(top, filter.transform.TransformPoint(filter.sharedMesh.bounds.max).y - prefab.transform.position.y);
        if (top <= 0f)
            return;
        foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
            foreach (Material material in renderer.sharedMaterials)
            {
                if (material == null || material.shader.name != "ThatGameJam/WatercolorFlat")
                {
                    Debug.LogWarning($"{GetType().Name}: {prefab.name}/{renderer.name} isn't WatercolorFlat, so it can't sway. Bake it first.", prefab);
                    continue;
                }
                if (material.GetFloat("_Wind") > 0.5f)
                    continue;
                Undo.RecordObject(material, "Turn On Wind");
                material.SetFloat("_Wind", 1f);
                material.SetFloat("_WindHeight", top);
                material.SetFloat("_WindStrength", top * SwayShareOfHeight);
                EditorUtility.SetDirty(material);
                Debug.Log($"{GetType().Name}: Wind on in {material.name}: Top Height {top:0.###} m, Sway {top * SwayShareOfHeight:0.###} m.", material);
            }
    }

    // Removes only the objects this generator made.
    public void Clear()
    {
        // The list changes before the objects go: destroying with Undo flushes the recorded change.
        GameObject[] old = made.ToArray();
        Undo.SetCurrentGroupName($"Clear {GetType().Name}");
        Undo.RecordObject(this, $"Clear {GetType().Name}");
        made.Clear();
        foreach (GameObject instance in old)
            if (instance != null)
                Undo.DestroyObjectImmediate(instance);
    }
#endif

    // A stage's area as Min and Max in this object's local space; otherwise the area as typed.
    Area Resolve(Area area)
    {
        if (area.stage == null)
            return area;
        Bounds box = area.stage.area;
        Vector3 a = transform.InverseTransformPoint(box.min), b = transform.InverseTransformPoint(box.max);
        area.min = new Vector2(Mathf.Min(a.x, b.x), Mathf.Min(a.z, b.z));
        area.max = new Vector2(Mathf.Max(a.x, b.x), Mathf.Max(a.z, b.z));
        return area;
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

    protected abstract Color GizmoColor { get; }

    void OnDrawGizmos()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = GizmoColor;
        foreach (Area given in areas)
        {
            Area area = Resolve(given);
            Vector2 center = (area.min + area.max) / 2f;
            Vector2 size = area.max - area.min;
            Gizmos.DrawWireCube(new Vector3(center.x, 0f, center.y), new Vector3(size.x, 0f, size.y));
        }
    }
}
