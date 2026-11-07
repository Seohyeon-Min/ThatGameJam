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
        [Tooltip("Each one here fades to white by an amount between these (0 = its own colour, 1 = white), more of them near Whitest. Both 0: no fading")]
        public Vector2 whiten;
        [Tooltip("Each one here turns its hue by up to this either way, in turns of the colour wheel (0.05 = 18 degrees). 0: its own hue")]
        [Range(0f, WatercolorTint.MostHueShift)] public float hueRange;
        [Tooltip("Paints the petals of each one here the Petal Hue (0.14 = yellow; Hue Range then varies it). Off: their own colours")]
        public bool paintPetals;
        [Range(0f, 1f)] public float petalHue;
        [Tooltip("Hidden until the season reaches this level, then shown as it rises (0 = always). Intro 0, Stage1 1, Stage2 2, Stage3 3")]
        public int fromSeason;
        [Tooltip("As the spring ring brings them out, each one here springs up at the ring's edge, white and a little too big, then settles to its own size and colour (RingBloom sets how). Off: they are just there inside the ring")]
        public bool bloomWithRing;
        [Tooltip("Grows here even inside the generator's clear areas (FlowerGenerator's red boxes), such as the ending's field by the cliff. Paths still keep clear")]
        public bool ignoreClearAreas;
        [Tooltip("Grows here even where an earlier area in the list covers the same ground, as one more layer over it, such as the ending's field inside the last stage. Off: where areas overlap, the earlier one fills it alone")]
        public bool layOverEarlier;
        [Tooltip("Covers the ground: no patches and no paths kept clear here, only Min Spacing between them, such as the ending's field. Off: patches and paths as everywhere")]
        public bool coverGround;
        [Tooltip("This area's own Scale Range. 0 and 0: the generator's")]
        public Vector2 ownScale;
        [Tooltip("A round area: the ellipse inside Min and Max instead of the whole box")]
        public bool round;
        [Tooltip("Metres in from the area's edge over which they thin out to none, so the edge doesn't show as a line. 0: a hard edge")]
        [Min(0f)] public float edgeFade;
    }

    // Above 1, more of the amounts land near the whitest end of an area's range.
    const float WhiteBias = 2.5f;

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
    [Tooltip("How many grow together at each spot, as a bunch: a number between these, picked for each spot. 1 and 1 = each one alone. Density counts the spots, so a bunch of 3 makes three times as many")]
    [SerializeField] protected Vector2Int clusterCount = new(1, 1);
    [Tooltip("How far the rest of a bunch stands from its first one, at most, in metres")]
    [SerializeField, Min(0f)] protected float clusterRadius = 0.3f;
    [Tooltip("Every one in a bunch is the same prefab, as a bunch of one kind of flower. Off: each picks its own")]
    [SerializeField] protected bool clusterSameKind = true;
    [Tooltip("How much each one leans with the slope of the ground. 0 = straight up")]
    [SerializeField, Range(0f, 1f)] protected float followSlope = 0.5f;
    [Tooltip("None where the ground is steeper than this, in degrees")]
    [SerializeField, Range(0f, 90f)] protected float maxSlope = 35f;
    [SerializeField] protected int seed = 1;
    [Tooltip("They stand on this terrain's surface. Empty: the scene's active terrain")]
    [SerializeField] protected Terrain ground;
    [Tooltip("On Generate, turns on Wind in the prefabs' watercolor materials that have it off, sized to each prefab's height. Materials already on keep their values")]
    [SerializeField] protected bool swayInWind = true;
    [Tooltip("Metres in from the cliff (the terrain's holes and its far +Z end) over which they thin out to none, so they don't run up to the brink. Cover Ground areas (the ending's field) are left as they are. 0: up to the edge")]
    [SerializeField, Min(0f)] protected float cliffFade;
    // What Generate made, so Clear removes only these.
    [FormerlySerializedAs("flowers")]
    [SerializeField, HideInInspector] List<GameObject> made = new();
    // One child per area holding what was made there, so an area can stay hidden until its season (fromSeason).
    [SerializeField, HideInInspector] List<GameObject> groups = new();

    Season season;
    int pendingLevel = -1;

    public int MadeCount => made.Count;

    Vector2 ScaleFor(Area area) => area.ownScale != Vector2.zero ? area.ownScale : scaleRange;

    // The share kept at this spot by the area's shape: none outside a round area, thinning out over Edge Fade towards its edge. Not
    // towards the terrain's edge: a cliff there should flower to its brink.
    float ShapeKeep(Area area, Vector2 local, Terrain terrain, Vector3 point)
    {
        if (!area.round && area.edgeFade <= 0f)
            return 1f;
        Vector2 lo = Vector2.Min(area.min, area.max), hi = Vector2.Max(area.min, area.max);
        Vector2 centre = (lo + hi) * 0.5f, half = Vector2.Max((hi - lo) * 0.5f, Vector2.one * 0.01f);
        Vector2 off = local - centre;
        float inset;
        if (area.round)
        {
            float r = new Vector2(off.x / half.x, off.y / half.y).magnitude;
            if (r > 1f)
                return 0f;
            inset = (1f - r) * Mathf.Min(half.x, half.y);
        }
        else
            inset = Mathf.Min(half.x - Mathf.Abs(off.x), half.y - Mathf.Abs(off.y));
        if (area.edgeFade <= 0f)
            return 1f;
        return Mathf.SmoothStep(0f, 1f, inset / area.edgeFade);
    }

    // The share kept at this point by Cliff Fade: none at the cliff's edge, all from Cliff Fade in from it.
    float CliffKeep(Terrain terrain, Vector3 point)
    {
        if (cliffFade <= 0f || terrain == null)
            return 1f;
        return Mathf.SmoothStep(0f, 1f, CliffDistance(terrain, point) / cliffFade);
    }

    // Metres from this point to the cliff's edge: the nearest hole or the terrain's far end, seen from above, along the terrain's +Z
    // end (each column's edge is where its holes start, coming in from that end). 0 over a hole or past the end.
    float CliffDistance(Terrain terrain, Vector3 point)
    {
        TerrainData data = terrain.terrainData;
        Vector3 origin = terrain.transform.position, size = data.size;
        int res = data.holesResolution;
        if (cliffEdges == null || cliffEdgesFor != data)
        {
            bool[,] solid = data.GetHoles(0, 0, res, res);
            cliffEdges = new float[res];
            for (int column = 0; column < res; column++)
            {
                int row = res - 1;
                while (row >= 0 && !solid[row, column])
                    row--;
                cliffEdges[column] = origin.z + (row + 1f) / res * size.z;
            }
            cliffEdgesFor = data;
        }
        float cell = size.x / res;
        int at = Mathf.FloorToInt((point.x - origin.x) / cell);
        int reach = Mathf.CeilToInt(cliffFade / cell) + 1;
        float best = float.MaxValue;
        for (int column = Mathf.Max(0, at - reach); column <= Mathf.Min(res - 1, at + reach); column++)
        {
            float dx = Mathf.Max(0f, Mathf.Abs(origin.x + (column + 0.5f) * cell - point.x) - cell * 0.5f);
            float dz = Mathf.Max(0f, cliffEdges[column] - point.z);
            if (column == at && dz <= 0f)
                return 0f;
            best = Mathf.Min(best, Mathf.Sqrt(dx * dx + dz * dz));
        }
        return best;
    }

    // Each column's edge Z, worked out once per Generate (CliffDistance).
    float[] cliffEdges;
    TerrainData cliffEdgesFor;

#if UNITY_EDITOR
    // Thins out what is already made by Cliff Fade, as Generate would now, without making it again: each one is dropped by a draw
    // from its position. Cover Ground areas are left. Returns how many were removed.
    public int ThinTowardCliff()
    {
        Terrain terrain = ground != null ? ground : Terrain.activeTerrain;
        // The holes may have changed since the last time.
        cliffEdges = null;
        int removed = 0;
        for (int a = 0; a < groups.Count && a < areas.Length; a++)
        {
            if (groups[a] == null || areas[a].coverGround)
                continue;
            Transform holder = groups[a].transform;
            for (int i = holder.childCount - 1; i >= 0; i--)
            {
                Transform one = holder.GetChild(i);
                Vector3 p = one.position;
                float draw = Mathf.Abs(Mathf.Sin(p.x * 12.9898f + p.z * 78.233f) * 43758.5453f) % 1f;
                if (draw <= CliffKeep(terrain, p))
                    continue;
                made.Remove(one.gameObject);
                Undo.DestroyObjectImmediate(one.gameObject);
                removed++;
            }
        }
        return removed;
    }
#endif

    // Whether this one, made by this generator, stands in an area whose ones bloom with the ring (WatercolorPaintedModel.ApplyUserValue).
    public bool BloomsWithRing(Transform thing)
    {
        if (thing.parent == null)
            return false;
        int area = groups.IndexOf(thing.parent.gameObject);
        return area >= 0 && area < areas.Length && areas[area].bloomWithRing;
    }

    // The share of spots kept at this world point, 0 to 1. All by default.
    protected virtual float Keep(Vector3 point) => 1f;

    // The same, for a spot in this area; by default the area makes no difference.
    protected virtual float Keep(Vector3 point, Area area) => Keep(point);

    void Start()
    {
        season = FindAnyObjectByType<Season>();
        if (season == null)
            return;
        ShowForSeason(season.Level);
        season.OnSeasonChanged.AddListener(OnSeasonChanged);
    }

    void OnDestroy()
    {
        if (season != null)
            season.OnSeasonChanged.RemoveListener(OnSeasonChanged);
    }

    // Not at once: WatercolorSeasonLooks photographs the old look in this same event, and these must come out after it, so they appear
    // inside the spring ring as it spreads (as TreeGenerator's leaves do).
    void OnSeasonChanged(int level) => pendingLevel = level;

    void LateUpdate()
    {
        if (pendingLevel < 0)
            return;
        ShowForSeason(pendingLevel);
        pendingLevel = -1;
    }

#if UNITY_EDITOR
    // Season preview in the Editor (SeasonPreview).
    public void PreviewSeason(int level) => ShowForSeason(level);
#endif

    void ShowForSeason(int level)
    {
        for (int i = 0; i < groups.Count && i < areas.Length; i++)
            if (groups[i] != null)
                groups[i].SetActive(level >= areas[i].fromSeason);
    }

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
        // The holes may have changed since the last time.
        cliffEdges = null;
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
        List<GameObject> areaGroups = MakeGroups("Generate " + kind);
        for (int a = 0; a < areas.Length; a++)
        {
            Area area = Resolve(areas[a]);
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
                float scale = Mathf.Lerp(ScaleFor(area).x, ScaleFor(area).y, (float)random.NextDouble());
                float white = 1f - Mathf.Pow(1f - (float)random.NextDouble(), WhiteBias);
                float hue = (float)random.NextDouble() * 2f - 1f;
                if (!found)
                    continue;
                // Where areas overlap, the earlier one in the list fills it alone, so the overlap isn't twice as dense.
                if (InEarlierArea(local, a))
                    continue;

                // Outside the patches most are dropped, not moved, so the bare ground stays bare.
                float patch = Mathf.PerlinNoise(local.x / patchSize + patchOffset.x, local.y / patchSize + patchOffset.y);
                float chance = Mathf.Lerp(1f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.4f, 0.65f, patch)), clustering);
                Vector3 point = transform.TransformPoint(new Vector3(local.x, 0f, local.y));
                chance *= Keep(point, area);
                if (area.coverGround)
                    chance = 1f;
                chance *= ShapeKeep(area, local, terrain, point);
            if (!area.coverGround)
                chance *= CliffKeep(terrain, point);
                if (keep > chance)
                    continue;
                if (!OnTerrain(terrain, point, out Vector3 world, out Vector3 normal))
                    continue;
                if (Vector3.Angle(Vector3.up, normal) > maxSlope)
                    continue;

                Remember(spots, local);
                placed.Add(Spawn(areaGroups[a].transform, area, which, world, normal, yaw, scale, white, hue));

                // The rest of the bunch, around the first one. Its numbers are drawn whether or not each stands, so the next spot
                // doesn't shift with the ground under this one.
                // Left at 1 and 1, nothing more is drawn, so a generator without bunches makes what it made before.
                int fewest = Mathf.Max(1, Mathf.Min(clusterCount.x, clusterCount.y)), most = Mathf.Max(1, Mathf.Max(clusterCount.x, clusterCount.y));
                int members = most > 1 ? random.Next(fewest, most + 1) : 1;
                for (int m = 1; m < members; m++)
                {
                    float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                    float reach = Mathf.Sqrt((float)random.NextDouble()) * clusterRadius;
                    int memberKind = random.Next(prefabs.Length);
                    float memberYaw = (float)random.NextDouble() * 360f;
                    float memberScale = Mathf.Lerp(ScaleFor(area).x, ScaleFor(area).y, (float)random.NextDouble());
                    float memberWhite = 1f - Mathf.Pow(1f - (float)random.NextDouble(), WhiteBias);
                    float memberHue = (float)random.NextDouble() * 2f - 1f;
                    Vector2 memberLocal = local + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * reach;
                    Vector3 memberPoint = transform.TransformPoint(new Vector3(memberLocal.x, 0f, memberLocal.y));
                    if (!OnTerrain(terrain, memberPoint, out Vector3 memberWorld, out Vector3 memberNormal)
                        || Vector3.Angle(Vector3.up, memberNormal) > maxSlope)
                        continue;
                    Remember(spots, memberLocal);
                    placed.Add(Spawn(areaGroups[a].transform, area, clusterSameKind ? which : memberKind, memberWorld, memberNormal,
                        memberYaw, memberScale, memberWhite, memberHue));
                }
                if (placed.Count > MaxObjects)
                {
                    Debug.LogWarning($"{kind}: over {MaxObjects} made. Lower the density, the bunches or the areas.", this);
                    break;
                }
            }
        }
        // After the objects: registering a created object flushes a pending change to the list.
        Undo.RecordObject(this, $"Generate {kind}");
        made.AddRange(placed);
        groups = areaGroups;
        Undo.SetCurrentGroupName($"Generate {kind}");
        Undo.CollapseUndoOperations(group);
    }

    // Fills one area's child again, leaving every other area's as it is, as one undo step: what was in that child is removed, and
    // it is filled the way Generate fills it (patches, Keep, the terrain, the slope, bunches), from its own seed. Logs how many spots
    // each step kept, so an area that comes out empty says why. Spots are kept apart only from each other, not from other areas'.
    public int GenerateArea(int index)
    {
        string kind = GetType().Name;
        Terrain terrain = ground != null ? ground : Terrain.activeTerrain;
        // The holes may have changed since the last time.
        cliffEdges = null;
        if (index < 0 || index >= areas.Length || terrain == null || prefabs.Length == 0
            || !System.Array.TrueForAll(prefabs, p => p != null && PrefabUtility.IsPartOfPrefabAsset(p)))
        {
            Debug.LogWarning($"{kind}: can't fill area {index} (no such area, no terrain, or a prefab slot empty).", this);
            return 0;
        }
        int undoGroup = Undo.GetCurrentGroup();
        Undo.RecordObject(this, $"Generate {kind} Area {index}");
        while (groups.Count < areas.Length)
            groups.Add(null);
        GameObject holder = groups[index];
        if (holder == null)
        {
            string label = areas[index].stage != null ? areas[index].stage.name : $"Area {index}";
            holder = new GameObject(areas[index].fromSeason > 0 ? $"{label} (from season {areas[index].fromSeason})" : label);
            Undo.RegisterCreatedObjectUndo(holder, $"Generate {kind} Area {index}");
            holder.transform.SetParent(transform, false);
            groups[index] = holder;
        }
        for (int c = holder.transform.childCount - 1; c >= 0; c--)
        {
            GameObject old = holder.transform.GetChild(c).gameObject;
            made.Remove(old);
            Undo.DestroyObjectImmediate(old);
        }
        made.RemoveAll(m => m == null);

        var random = new System.Random(seed * 7919 + index * 104729 + 1);
        var seedRandom = new System.Random(seed);
        var patchOffset = new Vector2(seedRandom.Next(10000), seedRandom.Next(10000));
        var spots = new Dictionary<Vector2Int, List<Vector2>>();
        var placed = new List<GameObject>();
        Area area = Resolve(areas[index]);
        Vector2 size = area.max - area.min;
        int wanted = Mathf.RoundToInt(Mathf.Abs(size.x * size.y) * Mathf.Max(0f, density + area.extraDensity));
        int noRoom = 0, earlier = 0, dropped = 0, offTerrain = 0, steep = 0, stood = 0;
        for (int i = 0; i < wanted && placed.Count < MaxObjects; i++)
        {
            Vector2 local = Vector2.zero;
            bool found = false;
            for (int tries = 0; tries < TriesPerObject && !found; tries++)
            {
                local = new Vector2(Mathf.Lerp(area.min.x, area.max.x, (float)random.NextDouble()), Mathf.Lerp(area.min.y, area.max.y, (float)random.NextDouble()));
                found = !TooClose(spots, local);
            }
            float keep = (float)random.NextDouble();
            int which = random.Next(prefabs.Length);
            float yaw = (float)random.NextDouble() * 360f;
            float scale = Mathf.Lerp(ScaleFor(area).x, ScaleFor(area).y, (float)random.NextDouble());
            float white = 1f - Mathf.Pow(1f - (float)random.NextDouble(), WhiteBias);
            float hue = (float)random.NextDouble() * 2f - 1f;
            if (!found) { noRoom++; continue; }
            if (InEarlierArea(local, index)) { earlier++; continue; }
            float patch = Mathf.PerlinNoise(local.x / patchSize + patchOffset.x, local.y / patchSize + patchOffset.y);
            float chance = Mathf.Lerp(1f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.4f, 0.65f, patch)), clustering);
            Vector3 point = transform.TransformPoint(new Vector3(local.x, 0f, local.y));
            chance *= Keep(point, area);
            if (area.coverGround)
                chance = 1f;
            chance *= ShapeKeep(area, local, terrain, point);
            if (!area.coverGround)
                chance *= CliffKeep(terrain, point);
            if (keep > chance) { dropped++; continue; }
            if (!OnTerrain(terrain, point, out Vector3 world, out Vector3 normal)) { offTerrain++; continue; }
            if (Vector3.Angle(Vector3.up, normal) > maxSlope) { steep++; continue; }
            stood++;
            Remember(spots, local);
            placed.Add(Spawn(holder.transform, area, which, world, normal, yaw, scale, white, hue));
            int fewest = Mathf.Max(1, Mathf.Min(clusterCount.x, clusterCount.y)), most = Mathf.Max(1, Mathf.Max(clusterCount.x, clusterCount.y));
            int members = most > 1 ? random.Next(fewest, most + 1) : 1;
            for (int m = 1; m < members; m++)
            {
                float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                float reach = Mathf.Sqrt((float)random.NextDouble()) * clusterRadius;
                int memberKind = random.Next(prefabs.Length);
                float memberYaw = (float)random.NextDouble() * 360f;
                float memberScale = Mathf.Lerp(ScaleFor(area).x, ScaleFor(area).y, (float)random.NextDouble());
                float memberWhite = 1f - Mathf.Pow(1f - (float)random.NextDouble(), WhiteBias);
                float memberHue = (float)random.NextDouble() * 2f - 1f;
                Vector2 memberLocal = local + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * reach;
                Vector3 memberPoint = transform.TransformPoint(new Vector3(memberLocal.x, 0f, memberLocal.y));
                if (!OnTerrain(terrain, memberPoint, out Vector3 memberWorld, out Vector3 memberNormal)
                    || Vector3.Angle(Vector3.up, memberNormal) > maxSlope)
                    continue;
                Remember(spots, memberLocal);
                placed.Add(Spawn(holder.transform, area, clusterSameKind ? which : memberKind, memberWorld, memberNormal,
                    memberYaw, memberScale, memberWhite, memberHue));
            }
        }
        Undo.RecordObject(this, $"Generate {kind} Area {index}");
        made.AddRange(placed);
        Undo.CollapseUndoOperations(undoGroup);
        Debug.Log($"{kind}: area {index} filled with {placed.Count} ({wanted} spots asked: {stood} stood, {dropped} dropped by patches or paths, {earlier} in an earlier area, {offTerrain} off the terrain, {steep} too steep, {noRoom} no room).", this);
        return placed.Count;
    }

    // One placed on the ground there, leaning with it, tinted as its area says.
    GameObject Spawn(Transform parent, Area area, int which, Vector3 world, Vector3 normal, float yaw, float scale, float white, float hue)
    {
        Quaternion lean = Quaternion.FromToRotation(Vector3.up, Vector3.Slerp(Vector3.up, normal, followSlope));
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[which], parent);
        instance.transform.SetPositionAndRotation(world, lean * Quaternion.Euler(0f, yaw, 0f));
        instance.transform.localScale = Vector3.one * scale;
        if (area.whiten != Vector2.zero || area.hueRange > 0f || area.paintPetals)
        {
            // From the less white end to the whitest, whichever way round they were typed.
            float least = Mathf.Min(area.whiten.x, area.whiten.y), most = Mathf.Max(area.whiten.x, area.whiten.y);
            var tint = instance.AddComponent<WatercolorTint>();
            tint.whiten = Mathf.Lerp(least, most, white);
            tint.hueShift = hue * area.hueRange;
            tint.paintPetals = area.paintPetals;
            tint.petalHue = area.petalHue;
            WatercolorPaintedModel.ApplyUserValue(instance);
        }
        Undo.RegisterCreatedObjectUndo(instance, $"Generate {GetType().Name}");
        return instance;
    }

    // One empty child per area, named after it, to hold what is made there.
    List<GameObject> MakeGroups(string undoName)
    {
        var holders = new List<GameObject>();
        for (int a = 0; a < areas.Length; a++)
        {
            string label = areas[a].stage != null ? areas[a].stage.name : $"Area {a}";
            var holder = new GameObject(areas[a].fromSeason > 0 ? $"{label} (from season {areas[a].fromSeason})" : label);
            Undo.RegisterCreatedObjectUndo(holder, undoName);
            holder.transform.SetParent(transform, false);
            holders.Add(holder);
        }
        return holders;
    }

    // Puts what this generator already made into one child per area, by which area each stands in (the earlier one where they overlap),
    // without making anything again. For objects made before areas had their own children, or after From Season changed.
    public void SortIntoAreas()
    {
        int group = Undo.GetCurrentGroup();
        var oldGroups = new List<GameObject>(groups);
        List<GameObject> areaGroups = MakeGroups("Sort Into Areas");
        foreach (GameObject thing in made)
        {
            if (thing == null)
                continue;
            Vector3 local = transform.InverseTransformPoint(thing.transform.position);
            for (int a = 0; a < areas.Length; a++)
            {
                Area area = Resolve(areas[a]);
                Vector2 min = Vector2.Min(area.min, area.max), max = Vector2.Max(area.min, area.max);
                if (local.x < min.x || local.x > max.x || local.z < min.y || local.z > max.y)
                    continue;
                Undo.SetTransformParent(thing.transform, areaGroups[a].transform, "Sort Into Areas");
                break;
            }
        }
        foreach (GameObject old in oldGroups)
            if (old != null && old.transform.childCount == 0)
                Undo.DestroyObjectImmediate(old);
        Undo.RecordObject(this, "Sort Into Areas");
        groups = areaGroups;
        Undo.SetCurrentGroupName("Sort Into Areas");
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

    // A stage gap opened at joinZ (WorldGap): what this made at or past it moves along with the ground, and so do the areas.
    public void ShiftBeyond(float joinZ, float size)
    {
        WorldGap.ShiftAll(made, joinZ, size);
        ShiftAreasBeyond(joinZ, size);
    }

    // Typed areas: each edge at or past the join moves, so an area past it moves and one across it grows by the gap. Stage areas follow
    // their stage already.
    public void ShiftAreasBeyond(float joinZ, float size)
    {
        float local = transform.InverseTransformVector(Vector3.forward * size).z;
        Undo.RecordObject(this, "Open Stage Gap");
        for (int i = 0; i < areas.Length; i++)
        {
            if (areas[i].stage != null)
                continue;
            if (transform.TransformPoint(new Vector3(0f, 0f, areas[i].min.y)).z >= joinZ)
                areas[i].min.y += local;
            if (transform.TransformPoint(new Vector3(0f, 0f, areas[i].max.y)).z >= joinZ)
                areas[i].max.y += local;
        }
    }

    // Removes only the objects this generator made.
    public void Clear()
    {
        // The list changes before the objects go: destroying with Undo flushes the recorded change.
        GameObject[] old = made.ToArray();
        GameObject[] oldGroups = groups.ToArray();
        Undo.SetCurrentGroupName($"Clear {GetType().Name}");
        Undo.RecordObject(this, $"Clear {GetType().Name}");
        made.Clear();
        groups.Clear();
        foreach (GameObject instance in old)
            if (instance != null)
                Undo.DestroyObjectImmediate(instance);
        foreach (GameObject holder in oldGroups)
            if (holder != null)
                Undo.DestroyObjectImmediate(holder);
    }
#endif

    bool InEarlierArea(Vector2 local, int index)
    {
        if (areas[index].layOverEarlier)
            return false;
        for (int i = 0; i < index; i++)
        {
            Area earlier = Resolve(areas[i]);
            Vector2 min = Vector2.Min(earlier.min, earlier.max), max = Vector2.Max(earlier.min, earlier.max);
            if (local.x >= min.x && local.x <= max.x && local.y >= min.y && local.y <= max.y)
                return true;
        }
        return false;
    }

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
        // Not over a hole cut in the terrain (a cliff's edge): nothing to stand on.
        TerrainData data = terrain.terrainData;
        int hx = Mathf.Clamp(Mathf.FloorToInt(u * data.holesResolution), 0, data.holesResolution - 1);
        int hz = Mathf.Clamp(Mathf.FloorToInt(v * data.holesResolution), 0, data.holesResolution - 1);
        if (data.IsHole(hx, hz))
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
