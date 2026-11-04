using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif

// One generator for the trees of the whole world. The areas are its child TreeZones: trees grow on a jittered grid over the whole terrain
// (Cover Ground) or inside the Trees zones, and outside the Clear zones except in the top and bottom paddings, which close off the road, each turned and sized at random, neighbours never sharing a kind if it can be helped. Inside a Pines zone a
// share of those same trees are pines, so pines replace trees rather than add to them. Generate and Clear are inspector buttons
// (TreeGeneratorEditor); the same seed and settings give the same trees.
// Leaves: every tree draws its own number from 0 to 1 when it is made, and has leaves at a season level whose share is above that number.
// So the trees that leaf out first keep their leaves as the share grows. A tree that can ever leaf out is made twice, bare and leafy, and
// the season shows one of them: nothing is made while playing. Pines always have their leaves.
// Oaks: the birches turn into oaks as the seasons go by, the same way: each draws its own number, and is an oak at a level whose share is
// above it. An oak is the same model repainted brown without the birch marks (Oak Colour), still varying in colour as the birches do.
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
        public float oakNumber;
    }

    [Tooltip("The tree kinds without leaves")]
    [FormerlySerializedAs("prefabs")]
    [SerializeField] GameObject[] bareTrees = { };
    [Tooltip("The same kinds with leaves, in the same order as Bare Trees. Empty: the trees never leaf out")]
    [SerializeField] GameObject[] leafyTrees = { };
    [Tooltip("Pines, for the Pines zones. They always have their leaves")]
    [SerializeField] GameObject[] pines = { };
    [Tooltip("Share of the trees inside a Pines zone that are pines")]
    [SerializeField, Range(0f, 1f)] float pineShare = 0.05f;
    [Tooltip("Share of the birches that have turned into oaks at each season level, from level 0. Past the end, the last value. Keep it rising, so an oak never turns back")]
    [SerializeField] float[] oakShareByLevel = { 0f, 0.1f, 0.5f, 0.9f };
    [Tooltip("Bark of the oaks: the birches' colour, as it would look before the watercolor tints and per-tree variation")]
    [SerializeField] Color oakColour = new(0.45f, 0.33f, 0.24f);
    [Tooltip("The birches' leaves, brighter than the oaks' by this much (their leaf material's Brightness). 1 = the same")]
    [SerializeField, Range(0.5f, 2f)] float birchLeafBrightness = 1.15f;
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
    [Tooltip("Trees grow over the whole terrain, as if a Trees zone covered it. Clear and Pines zones still count")]
    [SerializeField] bool coverGround = true;
    [Tooltip("Cover Ground: metres left bare along every edge of the terrain, the same on all four sides")]
    [SerializeField, Min(0f)] float edgePadding = 0f;
    [Tooltip("Top end (+Z): the last metres of the planted area where Clear zones don't count, so trees close off the road there")]
    [SerializeField, Min(0f)] float topPadding = 0f;
    [Tooltip("Bottom end (-Z): the first metres of the planted area where Clear zones don't count, so trees close off the road there")]
    [SerializeField, Min(0f)] float bottomPadding = 0f;
    [Tooltip("Paint the bare and leafy trees in watercolor after making them. A kind's bake is shared, so this also bakes again every other tree of that kind in the open scenes")]
    [SerializeField] bool paint = true;
    // Everything Generate made, so Clear removes only these.
    [SerializeField, HideInInspector] List<GameObject> trees = new();
    [SerializeField, HideInInspector] List<Planted> planted = new();
    // Each birch bark material and its oak version, by index, so a season can swap one for the other while playing.
    [SerializeField, HideInInspector] List<Material> birchBark = new();
    [SerializeField, HideInInspector] List<Material> oakBark = new();

    Season season;
    int pendingLevel = -1;

    public int TreeCount => planted.Count;
    public int PineCount => planted.FindAll(p => p.pine).Count;
    public int OakCount => planted.FindAll(p => !p.pine && p.oakNumber < MostOaks).Count;
    float MostOaks => oakShareByLevel.Length == 0 ? 0f : Mathf.Max(oakShareByLevel);
    public int CanLeafCount => planted.FindAll(p => p.leafy != null).Count;

    void Start()
    {
        season = FindAnyObjectByType<Season>();
        if (season == null)
            return;
        ShowSeason(season.Level);
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
        ShowSeason(pendingLevel);
        pendingLevel = -1;
    }

#if UNITY_EDITOR
    // Season preview in the Editor (SeasonPreview).
    public void PreviewSeason(int level) => ShowSeason(level);
    public void RestoreBark(int level) => ShowOaks(level);
#endif

    float LeafShare(int level) =>
        leafShareByLevel.Length == 0 ? 0f : leafShareByLevel[Mathf.Clamp(level, 0, leafShareByLevel.Length - 1)];

    void ShowSeason(int level)
    {
        ShowLeaves(level);
        ShowOaks(level);
    }

    float OakShare(int level) =>
        oakShareByLevel.Length == 0 ? 0f : oakShareByLevel[Mathf.Clamp(level, 0, oakShareByLevel.Length - 1)];

    void ShowOaks(int level)
    {
        float share = OakShare(level);
        foreach (Planted tree in planted)
        {
            if (tree.pine)
                continue;
            bool oak = tree.oakNumber < share;
            SetBark(tree.bare, oak);
            SetBark(tree.leafy, oak);
        }
    }

    // Swaps the tree's bark between birch and oak. Its leaves keep their own material.
    void SetBark(GameObject tree, bool oak)
    {
        if (tree == null)
            return;
        List<Material> from = oak ? birchBark : oakBark, to = oak ? oakBark : birchBark;
        foreach (Renderer renderer in tree.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < materials.Length; i++)
            {
                int index = from.IndexOf(materials[i]);
                if (index < 0 || to[index] == null)
                    continue;
                materials[i] = to[index];
                changed = true;
            }
            if (changed)
                renderer.sharedMaterials = materials;
        }
    }

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
        if (coverGround && terrain == null)
        {
            Debug.LogWarning("TreeGenerator: Cover Ground is on but there is no terrain. Set Ground.", this);
            return;
        }
        if (!coverGround && !zones.Exists(z => z.kind == TreeZone.Kind.Trees))
        {
            Debug.LogWarning("TreeGenerator: turn on Cover Ground, or add a child with a TreeZone set to Trees and size its box, first.", this);
            return;
        }
        int group = Undo.GetCurrentGroup();
        Clear();

        // The grid covers everything planted together, so trees keep one spacing across the joins between zones.
        Vector2 min = new(float.MaxValue, float.MaxValue), max = new(float.MinValue, float.MinValue);
        if (coverGround)
        {
            Vector3 corner = terrain.transform.position;
            min = new Vector2(corner.x, corner.z) + Vector2.one * edgePadding;
            max = new Vector2(corner.x + terrain.terrainData.size.x, corner.z + terrain.terrainData.size.z) - Vector2.one * edgePadding;
        }
        foreach (TreeZone zone in zones)
            if (zone.kind == TreeZone.Kind.Trees)
                foreach (Vector3 corner in zone.Corners())
                {
                    min = Vector2.Min(min, new Vector2(corner.x, corner.z));
                    max = Vector2.Max(max, new Vector2(corner.x, corner.z));
                }
        // Inside the paddings the Clear zones don't count: the road is closed off at both ends.
        float clearFrom = min.y + bottomPadding, clearTo = max.y - topPadding;

        var random = new System.Random(seed);
        // Its own sequence, so adding oaks didn't move a single tree.
        var oakRandom = new System.Random(seed + 7919);
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
                if (!coverGround && !zones.Exists(zone => zone.kind == TreeZone.Kind.Trees && zone.Contains(position)))
                    continue;
                if (position.z >= clearFrom && position.z <= clearTo && zones.Exists(zone => zone.kind == TreeZone.Kind.Clear && zone.Contains(position)))
                    continue;
                if (terrain != null)
                    position.y = terrain.transform.position.y + terrain.SampleHeight(position);
                Quaternion rotation = Quaternion.Euler(0f, randomRotation ? yaw : 0f, 0f);

                float oakNumber = (float)oakRandom.NextDouble();
                bool pine = pines.Length > 0 && pineRoll < pineShare && zones.Exists(zone => zone.kind == TreeZone.Kind.Pines && zone.Contains(position));
                var tree = new Planted { leafNumber = leafNumber, pine = pine, oakNumber = oakNumber };
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
        {
            WatercolorBaker.Bake(model);
            TreeLook(model);
        }
        // After the bake, which gives every tree of a kind the same materials: an oak version of every bark the birches use.
        birchBark.Clear();
        oakBark.Clear();
        foreach (Planted tree in plantedNow)
            if (!tree.pine)
            {
                PairBark(tree.bare);
                PairBark(tree.leafy);
            }
        AssetDatabase.SaveAssets();

        // After the trees: registering a created object flushes a pending change to the lists.
        Undo.RecordObject(this, "Generate Trees");
        trees.AddRange(made);
        planted.AddRange(plantedNow);
        Season sceneSeason = FindAnyObjectByType<Season>();
        ShowSeason(sceneSeason != null ? sceneSeason.Level : 0);
        Undo.SetCurrentGroupName("Generate Trees");
        Undo.CollapseUndoOperations(group);
    }

    GameObject Place(GameObject prefab, Vector3 position, Quaternion rotation, float scale, List<GameObject> made, Dictionary<GameObject, WatercolorPaintedModel> painted)
    {
        var tree = (GameObject)PrefabUtility.InstantiatePrefab(prefab, transform);
        tree.transform.SetPositionAndRotation(position, rotation);
        tree.transform.localScale = Vector3.one * scale;
        Undo.RegisterCreatedObjectUndo(tree, "Generate Trees");
        // Painted like the bare trees placed before: each carries its own watercolor settings, its kind shares one bake. Leafy trees too:
        // their LeafCard leaves become watercolor leaf cards (WatercolorFlat's Leaf Card). Pines stay as they are: their needles are still
        // on the old wind graph.
        if (paint && (System.Array.IndexOf(bareTrees, prefab) >= 0 || System.Array.IndexOf(leafyTrees, prefab) >= 0))
        {
            if (!tree.TryGetComponent(out WatercolorPaintedModel model))
                model = tree.AddComponent<WatercolorPaintedModel>();
            painted.TryAdd(prefab, model);
        }
        made.Add(tree);
        return tree;
    }

    // Notes an oak version for each bark material on the tree that doesn't have one yet. The leaves go the other way: the birches get a
    // brighter copy of them, and the oaks keep the leaf material as it is.
    void PairBark(GameObject tree)
    {
        if (tree == null)
            return;
        foreach (Renderer renderer in tree.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                if (material == null || birchBark.Contains(material))
                    continue;
                if (material.HasProperty("_Brightness"))
                {
                    int known = oakBark.IndexOf(material);
                    Material birchLeaves = known >= 0 ? birchBark[known] : BirchLeaves(material);
                    if (known < 0)
                    {
                        birchBark.Add(birchLeaves);
                        oakBark.Add(material);
                    }
                    materials[i] = birchLeaves;
                    changed = true;
                    continue;
                }
                Material oak = OakMaterial(material);
                if (oak == material)
                    continue;
                birchBark.Add(material);
                oakBark.Add(oak);
            }
            if (!changed)
                continue;
            Undo.RecordObject(renderer, "Generate Trees");
            renderer.sharedMaterials = materials;
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        }
    }

    // A copy of a leaf material next to it, with Birch Leaf Brightness, made again from it on every Generate.
    Material BirchLeaves(Material leaves)
    {
        string leavesPath = AssetDatabase.GetAssetPath(leaves);
        string path = $"{leavesPath.Substring(0, leavesPath.LastIndexOf('/'))}/{leaves.name}_birch.mat";
        var birch = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (birch == null)
        {
            birch = new Material(leaves);
            AssetDatabase.CreateAsset(birch, path);
        }
        else
        {
            birch.shader = leaves.shader;
            birch.CopyPropertiesFromMaterial(leaves);
        }
        birch.SetFloat("_Brightness", leaves.GetFloat("_Brightness") * birchLeafBrightness);
        EditorUtility.SetDirty(birch);
        return birch;
    }

    // The oak version of a bark material, next to it (watercolor, from the bake) or under Materials/Oak (a model's own, unpainted),
    // made again from it on every Generate so it follows the bake and Oak Colour. Anything else (leaves) is returned as it is.
    Material OakMaterial(Material bark)
    {
        string path;
        if (bark.HasProperty("_Birch") && bark.GetFloat("_Birch") > 0.5f && !bark.name.EndsWith("_oak_watercolor"))
        {
            string barkPath = AssetDatabase.GetAssetPath(bark);
            string folder = barkPath.Substring(0, barkPath.LastIndexOf('/'));
            path = $"{folder}/{bark.name.Replace("_watercolor", "")}_oak_watercolor.mat";
        }
        else if (bark.HasProperty("_BASE_COLOR") && !bark.name.EndsWith("_oak"))
        {
            const string oakFolder = "Assets/01_Arts/Materials/Oak";
            if (!AssetDatabase.IsValidFolder(oakFolder))
                AssetDatabase.CreateFolder("Assets/01_Arts/Materials", "Oak");
            path = $"{oakFolder}/{bark.name}_oak.mat";
        }
        else
            return bark;

        var oak = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (oak == null)
        {
            oak = new Material(bark);
            AssetDatabase.CreateAsset(oak, path);
        }
        else
        {
            oak.shader = bark.shader;
            oak.CopyPropertiesFromMaterial(bark);
        }
        if (oak.HasProperty("_Birch"))
        {
            oak.SetFloat("_Birch", 0f);
            oak.SetColor("_BaseColor", oakColour);
        }
        else
            oak.SetColor("_BASE_COLOR", oakColour);
        EditorUtility.SetDirty(oak);
        return oak;
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

    // A stage gap opened at joinZ (WorldGap): the trees at or past it move along with the ground, and so do the zones.
    public void ShiftBeyond(float joinZ, float size)
    {
        WorldGap.ShiftAll(trees, joinZ, size);
        ShiftZonesBeyond(joinZ, size);
    }

    // Zones past the join move; a zone across it grows by the gap, so a cleared road stays cleared all the way.
    public void ShiftZonesBeyond(float joinZ, float size)
    {
        foreach (TreeZone zone in GetComponentsInChildren<TreeZone>(true))
            zone.ShiftBeyond(joinZ, size);
    }

    // What every painted tree's materials need, bark and leaves alike, which the bake makes otherwise: a tree between the camera and the
    // bird or the baby turns see-through (Camera Fade on), and trees take no shadows from each other (Receive Cast Shadows off). Taken,
    // they paint the leaves in deep blue shade, and a leaf card's shadow, cast from the card as it was modelled, lands as a big square.
    // Leaves also keep their colour on the lit side (Leave Paper off): the crown shades as one mass, and whole cards went paper white.
    static void TreeLook(WatercolorPaintedModel model)
    {
        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            foreach (Material material in renderer.sharedMaterials)
            {
                if (material == null || !material.HasProperty("_CameraFade"))
                    continue;
                material.SetFloat("_CameraFade", 1f);
                material.SetFloat("_ReceiveCastShadows", 0f);
                if (material.HasProperty("_LeafCard") && material.GetFloat("_LeafCard") > 0.5f)
                    material.SetFloat("_LeavePaper", 0f);
                EditorUtility.SetDirty(material);
            }
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
