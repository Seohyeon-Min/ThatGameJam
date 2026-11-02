using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif

// One generator for the trees of the whole world. The areas are its child TreeZones: trees grow on a jittered grid inside the Trees zones
// and outside the Clear zones, each turned and sized at random, neighbours never sharing a kind if it can be helped. Inside a Pines zone a
// share of those same trees are pines, so pines replace trees rather than add to them. Generate and Clear are inspector buttons
// (TreeGeneratorEditor); the same seed and settings give the same trees.
// Leaves: every tree draws its own number from 0 to 1 when it is made, and has leaves at a season level whose share is above that number.
// So the trees that leaf out first keep their leaves as the share grows. A tree that can ever leaf out is made twice, bare and leafy, and
// the season shows one of them: nothing is made while playing. Pines always have their leaves.
public class TreeGenerator : MonoBehaviour
{
    // Trees closer than spacing times this are next to each other: the grid neighbours along X and Z, not the diagonal ones.
    const float NeighbourFactor = 1.3f;

    [System.Serializable]
    class Planted
    {
        public GameObject bare;
        public GameObject leafy;
        public float leafNumber;
        public bool pine;
    }

    [Tooltip("The tree kinds without leaves")]
    [FormerlySerializedAs("prefabs")]
    [SerializeField] GameObject[] bareTrees = { };
    [Tooltip("The same kinds with leaves, in the same order as Bare Trees. Empty: the trees never leaf out")]
    [SerializeField] GameObject[] leafyTrees = { };
    [Tooltip("Pines, for the Pines zones. They always have their leaves")]
    [SerializeField] GameObject[] pines = { };
    [Tooltip("Share of the trees inside a Pines zone that are pines")]
    [SerializeField, Range(0f, 1f)] float pineShare = 0.1f;
    [Tooltip("Share of the trees with leaves at each season level, from level 0. Past the end, the last value. Keep it rising, so a tree never loses its leaves")]
    [SerializeField] float[] leafShareByLevel = { 0f, 0f, 0.1f, 0.7f, 1f };
    [Tooltip("Average distance between neighbouring trees")]
    [SerializeField, Min(0.1f)] float spacing = 2.4f;
    [Tooltip("How far a tree moves off its grid point, as a fraction of the spacing")]
    [SerializeField, Range(0f, 0.5f)] float jitter = 0.3f;
    [SerializeField] Vector2 scaleRange = new(0.7f, 1.6f);
    [SerializeField] bool randomRotation = true;
    [SerializeField] int seed = 2;
    [Tooltip("Trees stand on this terrain's surface. Empty: the scene's active terrain")]
    [SerializeField] Terrain ground;
    [Tooltip("Paint the bare trees in watercolor after making them. A kind's bake is shared, so this also bakes again every other tree of that kind in the open scenes")]
    [SerializeField] bool paint = true;
    // Everything Generate made, so Clear removes only these.
    [SerializeField, HideInInspector] List<GameObject> trees = new();
    [SerializeField, HideInInspector] List<Planted> planted = new();

    Season season;
    int pendingLevel = -1;

    public int TreeCount => planted.Count;
    public int PineCount => planted.FindAll(p => p.pine).Count;
    public int CanLeafCount => planted.FindAll(p => p.leafy != null).Count;

    void Start()
    {
        season = FindAnyObjectByType<Season>();
        if (season == null)
            return;
        ShowLeaves(season.Level);
        season.OnSeasonChanged.AddListener(OnSeasonChanged);
    }

    void OnDestroy()
    {
        if (season != null)
            season.OnSeasonChanged.RemoveListener(OnSeasonChanged);
    }

    // Not at once: WatercolorSeasonLooks photographs the old look in this same event, and the leaves must come out after it,
    // so they appear inside the spring ring as it spreads.
    void OnSeasonChanged(int level) => pendingLevel = level;

    void LateUpdate()
    {
        if (pendingLevel < 0)
            return;
        ShowLeaves(pendingLevel);
        pendingLevel = -1;
    }

    float LeafShare(int level) =>
        leafShareByLevel.Length == 0 ? 0f : leafShareByLevel[Mathf.Clamp(level, 0, leafShareByLevel.Length - 1)];

    void ShowLeaves(int level)
    {
        float share = LeafShare(level);
        foreach (Planted tree in planted)
        {
            if (tree.leafy == null || tree.bare == null)
                continue;
            bool leafy = tree.leafNumber < share;
            if (tree.leafy.activeSelf == leafy)
                continue;
            // A bird perched in the tree that goes away takes off.
            if (Application.isPlaying)
                foreach (Perchable perch in (leafy ? tree.bare : tree.leafy).GetComponentsInChildren<Perchable>())
                    perch.Release();
            tree.bare.SetActive(!leafy);
            tree.leafy.SetActive(leafy);
        }
    }

#if UNITY_EDITOR
    // Replaces this generator's trees with new ones and paints them in watercolor, as one undo step.
    public void Generate()
    {
        if (bareTrees.Length == 0 || !AllPrefabs(bareTrees) || !AllPrefabs(leafyTrees) || !AllPrefabs(pines))
        {
            Debug.LogWarning("TreeGenerator: fill every tree slot with a prefab asset, and at least one Bare Tree.", this);
            return;
        }
        if (leafyTrees.Length != 0 && leafyTrees.Length != bareTrees.Length)
        {
            Debug.LogWarning("TreeGenerator: Leafy Trees must be empty or hold one leafy kind per Bare Tree, in the same order.", this);
            return;
        }
        // Min on the field guards only the Inspector; a smaller spacing could loop forever.
        if (spacing < 0.1f)
        {
            Debug.LogWarning("TreeGenerator: spacing must be at least 0.1.", this);
            return;
        }
        Terrain terrain = ground != null ? ground : Terrain.activeTerrain;
        var zones = new List<TreeZone>(GetComponentsInChildren<TreeZone>());
        if (!zones.Exists(z => z.kind == TreeZone.Kind.Trees))
        {
            Debug.LogWarning("TreeGenerator: add a child with a TreeZone set to Trees, and size its box, first.", this);
            return;
        }
        int group = Undo.GetCurrentGroup();
        Clear();

        // The grid covers all the Trees zones together, so trees keep one spacing across the joins between zones.
        Vector2 min = new(float.MaxValue, float.MaxValue), max = new(float.MinValue, float.MinValue);
        foreach (TreeZone zone in zones)
            if (zone.kind == TreeZone.Kind.Trees)
                foreach (Vector3 corner in zone.Corners())
                {
                    min = Vector2.Min(min, new Vector2(corner.x, corner.z));
                    max = Vector2.Max(max, new Vector2(corner.x, corner.z));
                }

        var random = new System.Random(seed);
        var made = new List<GameObject>();
        var plantedNow = new List<Planted>();
        var painted = new Dictionary<GameObject, WatercolorPaintedModel>();
        var placed = new List<(Vector3 position, int kind)>();
        var freeKinds = new List<int>();
        float amount = spacing * jitter;
        float neighbourDistance = spacing * NeighbourFactor;
        float mostLeaves = 0f;
        foreach (float share in leafShareByLevel)
            mostLeaves = Mathf.Max(mostLeaves, share);
        for (float x = min.x + amount; x <= max.x - amount; x += spacing)
            for (float z = min.y + amount; z <= max.y - amount; z += spacing)
            {
                // Every number is drawn for every grid point, so a zone or a setting that skips a tree doesn't shift the others.
                var position = new Vector3(x + Jitter(random, amount), 0f, z + Jitter(random, amount));
                float yaw = (float)random.NextDouble() * 360f;
                float scale = Mathf.Lerp(scaleRange.x, scaleRange.y, (float)random.NextDouble());
                float leafNumber = (float)random.NextDouble();
                float pineRoll = (float)random.NextDouble();
                int pineKind = random.Next(Mathf.Max(pines.Length, 1));
                int kindRoll = random.Next(1000);
                if (!zones.Exists(zone => zone.kind == TreeZone.Kind.Trees && zone.Contains(position))
                    || zones.Exists(zone => zone.kind == TreeZone.Kind.Clear && zone.Contains(position)))
                    continue;
                if (terrain != null)
                    position.y = terrain.transform.position.y + terrain.SampleHeight(position);
                Quaternion rotation = Quaternion.Euler(0f, randomRotation ? yaw : 0f, 0f);

                bool pine = pines.Length > 0 && pineRoll < pineShare && zones.Exists(zone => zone.kind == TreeZone.Kind.Pines && zone.Contains(position));
                var tree = new Planted { leafNumber = leafNumber, pine = pine };
                if (pine)
                    tree.bare = Place(pines[pineKind], position, rotation, scale, made, painted);
                else
                {
                    int kind = ChooseKind(position, placed, freeKinds, neighbourDistance, kindRoll);
                    placed.Add((position, kind));
                    tree.bare = Place(bareTrees[kind], position, rotation, scale, made, painted);
                    if (leafyTrees.Length > 0 && leafNumber < mostLeaves)
                        tree.leafy = Place(leafyTrees[kind], position, rotation, scale, made, painted);
                }
                plantedNow.Add(tree);
            }

        // One bake per kind paints every tree of that kind in the open scenes (they share it). Before ShowLeaves, while every
        // tree is still shown: the bake only finds models that are active.
        foreach (WatercolorPaintedModel model in painted.Values)
            WatercolorBaker.Bake(model);

        // After the trees: registering a created object flushes a pending change to the lists.
        Undo.RecordObject(this, "Generate Trees");
        trees.AddRange(made);
        planted.AddRange(plantedNow);
        Season sceneSeason = FindAnyObjectByType<Season>();
        ShowLeaves(sceneSeason != null ? sceneSeason.Level : 0);
        Undo.SetCurrentGroupName("Generate Trees");
        Undo.CollapseUndoOperations(group);
    }

    GameObject Place(GameObject prefab, Vector3 position, Quaternion rotation, float scale, List<GameObject> made, Dictionary<GameObject, WatercolorPaintedModel> painted)
    {
        var tree = (GameObject)PrefabUtility.InstantiatePrefab(prefab, transform);
        tree.transform.SetPositionAndRotation(position, rotation);
        tree.transform.localScale = Vector3.one * scale;
        Undo.RegisterCreatedObjectUndo(tree, "Generate Trees");
        // Painted like the bare trees placed before: each carries its own watercolor settings, its kind shares one bake. Leafy trees and
        // pines stay as they are, as the leafy ones placed by hand do: the bake can't read their leaves' wind shader yet (grey squares).
        if (paint && System.Array.IndexOf(bareTrees, prefab) >= 0)
        {
            if (!tree.TryGetComponent(out WatercolorPaintedModel model))
                model = tree.AddComponent<WatercolorPaintedModel>();
            painted.TryAdd(prefab, model);
        }
        made.Add(tree);
        return tree;
    }

    // A kind no tree next to this one has; if every kind is taken, the kind whose nearest tree is farthest.
    int ChooseKind(Vector3 position, List<(Vector3 position, int kind)> placed, List<int> freeKinds, float neighbourDistance, int roll)
    {
        freeKinds.Clear();
        int farthestKind = 0;
        float farthest = -1f;
        for (int kind = 0; kind < bareTrees.Length; kind++)
        {
            float nearest = float.MaxValue;
            // The grid runs along Z inside each X column, so the neighbours are among the last trees placed.
            for (int i = placed.Count - 1; i >= 0 && i >= placed.Count - 400; i--)
                if (placed[i].kind == kind)
                    nearest = Mathf.Min(nearest, Vector3.Distance(placed[i].position, position));
            if (nearest >= neighbourDistance)
                freeKinds.Add(kind);
            if (nearest > farthest)
                (farthest, farthestKind) = (nearest, kind);
        }
        return freeKinds.Count > 0 ? freeKinds[roll % freeKinds.Count] : farthestKind;
    }

    static bool AllPrefabs(GameObject[] prefabs) =>
        System.Array.TrueForAll(prefabs, p => p != null && PrefabUtility.IsPartOfPrefabAsset(p));

    // Removes only the trees this generator made.
    public void Clear()
    {
        // The lists change before the trees go: destroying with Undo flushes the recorded change.
        GameObject[] old = trees.ToArray();
        Undo.SetCurrentGroupName("Clear Trees");
        Undo.RecordObject(this, "Clear Trees");
        trees.Clear();
        planted.Clear();
        foreach (GameObject tree in old)
            if (tree != null)
                Undo.DestroyObjectImmediate(tree);
    }
#endif

    static float Jitter(System.Random random, float amount) => ((float)random.NextDouble() * 2f - 1f) * amount;
}
