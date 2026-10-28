#if UNITY_EDITOR
// Editor-only: wrapped in UNITY_EDITOR instead of living in an Editor folder, so it compiles with the runtime scripts
// but is stripped from builds. (Scripts under Assets/02_Scripts/Editor were not picked up by this project's compile.)
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Bakes a WatercolorPaintedModel: mass normals and model-space pattern coordinates into mesh copies, a 3D pattern
// texture, and ThatGameJam/WatercolorFlat materials. Results go to Assets/01_Arts/Baked/<scene>_<model>_<id>/.
// See WatercolorPaintedModel for what each step is for. Bake the model in its rest pose (no animation playing),
// because skinned pieces are read in their current pose and the baked data is stored per vertex.
public static class WatercolorBaker
{
    const string BakeRoot = "Assets/01_Arts/Baked";
    const string FlatShader = "ThatGameJam/WatercolorFlat";

    const string PlainSceneViewMenu = "Tools/Watercolor/Plain Scene View";
    const string PlainSceneViewPref = "ThatGameJam.Watercolor.PlainSceneView";

    // Per-user editor preference, so turning it on does not change anyone else's editor.
    [InitializeOnLoadMethod]
    static void LoadPlainSceneView() => WatercolorManager.PlainSceneView = EditorPrefs.GetBool(PlainSceneViewPref, false);

    [MenuItem(PlainSceneViewMenu, false, 0)]
    static void TogglePlainSceneView()
    {
        WatercolorManager.PlainSceneView = !WatercolorManager.PlainSceneView;
        EditorPrefs.SetBool(PlainSceneViewPref, WatercolorManager.PlainSceneView);
        SceneView.RepaintAll();
    }

    [MenuItem(PlainSceneViewMenu, true)]
    static bool TogglePlainSceneViewValidate()
    {
        Menu.SetChecked(PlainSceneViewMenu, WatercolorManager.PlainSceneView);
        return true;
    }

    [MenuItem("Tools/Watercolor/Bake Selected Models")]
    static void BakeSelected()
    {
        foreach (var go in Selection.gameObjects)
        {
            var model = go.GetComponent<WatercolorPaintedModel>();
            if (model == null)
                model = Undo.AddComponent<WatercolorPaintedModel>(go);
            Debug.Log(Bake(model));
        }
    }

    // Re-bakes every WatercolorPaintedModel in the open scenes, e.g. after changing the bake-time sizes in WatercolorManager.
    [MenuItem("Tools/Watercolor/Bake All Models In Scene")]
    public static void BakeAll()
    {
        var models = Object.FindObjectsByType<WatercolorPaintedModel>();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            for (int i = 0; i < models.Length; i++)
            {
                EditorUtility.DisplayProgressBar("Watercolor Bake", models[i].name, (float)i / models.Length);
                Bake(models[i]);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
        Debug.Log($"Watercolor: baked {models.Length} models in {watch.Elapsed.TotalSeconds:F1} s");
    }

    struct Part
    {
        public Renderer renderer;
        public Mesh source;
        public Vector3[] positionsRoot;
        public Vector3[] normalsWorld;
        public Matrix4x4 meshToWorld;
        public Bounds boundsRoot;
    }

    public static string Bake(WatercolorPaintedModel model)
    {
        Transform root = model.transform;
        // The model's previous bake, found through its pattern. Its folder can differ from the new one (the object id
        // changes when the scene is rebuilt), so it is deleted after this bake instead of being left behind.
        string oldPattern = model.pattern != null ? AssetDatabase.GetAssetPath(model.pattern) : "";
        string oldFolder = oldPattern.Length > 0 ? oldPattern.Substring(0, oldPattern.LastIndexOf('/')) : null;
        string folder = EnsureFolder(model);
        var groups = new Dictionary<Material, List<Part>>();
        var allBounds = new Bounds();
        bool first = true;

        foreach (var renderer in root.GetComponentsInChildren<Renderer>())
        {
            if (!renderer.enabled || renderer.sharedMaterial == null || !TryReadPart(renderer, root, out Part part))
                continue;
            if (!groups.TryGetValue(renderer.sharedMaterial, out var list))
                groups[renderer.sharedMaterial] = list = new List<Part>();
            list.Add(part);
            foreach (var p in part.positionsRoot)
            {
                if (first) { allBounds = new Bounds(p, Vector3.zero); first = false; }
                else allBounds.Encapsulate(p);
            }
        }
        if (first)
            return $"{root.name}: no renderers to bake";

        // Pad so trilinear sampling at the surface never reads past the edge.
        float longest = Mathf.Max(allBounds.size.x, Mathf.Max(allBounds.size.y, allBounds.size.z));
        allBounds.Expand(longest * 0.04f);
        longest = Mathf.Max(allBounds.size.x, Mathf.Max(allBounds.size.y, allBounds.size.z));
        int seed = model.seed != 0 ? model.seed : folder.GetHashCode();

        // Blotch sizes are a share of the screen height, turned into world size at this model's distance from the
        // camera, so near and far, small and large models all get the same blotch size on screen.
        var style = WatercolorManager.Current;
        float rootScale = root.lossyScale.x;
        Vector3 centerWorld = root.TransformPoint(allBounds.center);
        float pigmentSize = WorldSizeForScreen(centerWorld, style.blotchSizePercent, longest * model.pigmentScale * rootScale) / rootScale;
        float backrunSize = WorldSizeForScreen(centerWorld, style.backrunSizePercent, longest * model.backrunScale * rootScale) / rootScale;
        Texture3D pattern = BakePattern(model, allBounds, longest, pigmentSize, backrunSize, seed, folder + "/Pattern.asset");
        float ScreenSize(float percent) => WorldSizeForScreen(centerWorld, percent, longest * rootScale * percent / 30f) / rootScale;
        Texture3D chaos = BakeChaos(pattern.width, pattern.height, pattern.depth, allBounds, seed,
            ScreenSize(style.chaosChunkSizePercent), style.chaosSplitChance,
            ScreenSize(Mathf.Min(style.chaosDropSizeMinPercent, style.chaosDropSizeMaxPercent)),
            ScreenSize(Mathf.Max(style.chaosDropSizeMinPercent, style.chaosDropSizeMaxPercent)),
            ScreenSize(style.pencilPatchSizePercent), style.pencilPatchChance,
            folder + "/Chaos.asset");
        model.pattern = pattern;
        model.modelBounds = allBounds;

        int pieces = 0, masses = 0;
        float gap = model.groupDistance * longest;
        foreach (var (sourceMaterial, sameColour) in groups)
        foreach (var parts in SplitByDistance(sameColour, gap))
        {
            masses++;
            Material flat = FlatMaterial(sourceMaterial, pattern, folder);
            flat.SetTexture("_ChaosTex", chaos);
            // A floor is one wide flat piece; an enclosing ellipsoid would bend its normals, so keep its own.
            float massBlend = flat.GetFloat("_Floor") > 0.5f ? 0f : model.massNormal;
            Bounds groupBounds = parts[0].positionsRoot.Length > 0 ? new Bounds(parts[0].positionsRoot[0], Vector3.zero) : new Bounds();
            foreach (var part in parts)
                foreach (var p in part.positionsRoot)
                    groupBounds.Encapsulate(p);
            Vector3 radius = Vector3.Max(groupBounds.extents, Vector3.one * longest * 0.02f);

            foreach (var part in parts)
            {
                Mesh mesh = Object.Instantiate(part.source);
                mesh.name = part.source.name.Replace("_watercolor", "") + "_watercolor";
                var normals = new Vector3[part.positionsRoot.Length];
                var coords = new List<Vector3>(part.positionsRoot.Length);
                Matrix4x4 worldToMeshNormal = part.meshToWorld.transpose;
                for (int i = 0; i < normals.Length; i++)
                {
                    Vector3 p = part.positionsRoot[i];
                    // Gradient of the group's enclosing ellipsoid: every piece of the group shades as one rounded mass.
                    Vector3 d = p - groupBounds.center;
                    Vector3 massRoot = new Vector3(d.x / (radius.x * radius.x), d.y / (radius.y * radius.y), d.z / (radius.z * radius.z));
                    Vector3 massWorld = (root.rotation * massRoot).normalized;
                    Vector3 n = Vector3.Slerp(part.normalsWorld[i], massWorld, massBlend).normalized;
                    normals[i] = worldToMeshNormal.MultiplyVector(n).normalized;
                    coords.Add(new Vector3(
                        (p.x - allBounds.min.x) / allBounds.size.x,
                        (p.y - allBounds.min.y) / allBounds.size.y,
                        (p.z - allBounds.min.z) / allBounds.size.z));
                }
                mesh.normals = normals;
                mesh.SetUVs(3, coords);

                Mesh saved = SaveAsset(mesh, folder + "/" + Sanitize(RelativePath(root, part.renderer.transform)) + "_mesh.asset");
                Undo.RecordObject(part.renderer, "Watercolor Bake");
                if (part.renderer is SkinnedMeshRenderer skinned)
                    skinned.sharedMesh = saved;
                else
                {
                    var filter = part.renderer.GetComponent<MeshFilter>();
                    Undo.RecordObject(filter, "Watercolor Bake");
                    filter.sharedMesh = saved;
                }
                var mats = part.renderer.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                    mats[i] = flat;
                part.renderer.sharedMaterials = mats;
                PrefabUtility.RecordPrefabInstancePropertyModifications(part.renderer);
                pieces++;
            }
        }

        EditorUtility.SetDirty(model);
        PrefabUtility.RecordPrefabInstancePropertyModifications(model);
        EditorSceneManager.MarkSceneDirty(model.gameObject.scene);
        AssetDatabase.SaveAssets();
        DeleteOldBake(oldFolder, folder);
        return $"{root.name}: {pieces} pieces in {masses} masses ({groups.Count} colours) -> {folder}";
    }

    static bool TryReadPart(Renderer renderer, Transform root, out Part part)
    {
        part = default;
        Transform t = renderer.transform;
        Mesh source;
        Vector3[] vertices, normals;
        Matrix4x4 meshToWorld;
        if (renderer is SkinnedMeshRenderer skinned && skinned.sharedMesh != null)
        {
            source = skinned.sharedMesh;
            var posed = new Mesh();
            skinned.BakeMesh(posed, false);
            vertices = posed.vertices;
            normals = posed.normals;
            Object.DestroyImmediate(posed);
            // BakeMesh without scale gives vertices in the renderer's space minus its scale.
            meshToWorld = Matrix4x4.TRS(t.position, t.rotation, Vector3.one);
        }
        else if (renderer is MeshRenderer && renderer.TryGetComponent(out MeshFilter filter) && filter.sharedMesh != null)
        {
            source = filter.sharedMesh;
            vertices = source.vertices;
            normals = source.normals;
            meshToWorld = t.localToWorldMatrix;
        }
        else
            return false;
        if (vertices.Length == 0 || source.vertexCount != vertices.Length)
            return false;

        Matrix4x4 normalToWorld = meshToWorld.inverse.transpose;
        var positionsRoot = new Vector3[vertices.Length];
        var normalsWorld = new Vector3[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            positionsRoot[i] = root.InverseTransformPoint(meshToWorld.MultiplyPoint3x4(vertices[i]));
            normalsWorld[i] = normals.Length == vertices.Length ? normalToWorld.MultiplyVector(normals[i]).normalized : Vector3.up;
        }
        var bounds = new Bounds(positionsRoot[0], Vector3.zero);
        foreach (var p in positionsRoot)
            bounds.Encapsulate(p);
        part = new Part { renderer = renderer, source = source, positionsRoot = positionsRoot, normalsWorld = normalsWorld, meshToWorld = meshToWorld, boundsRoot = bounds };
        return true;
    }

    // Same colour is not enough to merge: pieces join one mass only when their bounds come within `gap` of each other
    // (directly or through a chain of pieces). So hair strands merge, but a head and the legs stay separate masses.
    static List<List<Part>> SplitByDistance(List<Part> parts, float gap)
    {
        var parent = new int[parts.Count];
        for (int i = 0; i < parent.Length; i++)
            parent[i] = i;
        int Find(int i) => parent[i] == i ? i : parent[i] = Find(parent[i]);

        for (int i = 0; i < parts.Count; i++)
        {
            var a = parts[i].boundsRoot;
            a.Expand(gap * 2f);
            for (int j = i + 1; j < parts.Count; j++)
                if (a.Intersects(parts[j].boundsRoot))
                    parent[Find(i)] = Find(j);
        }

        var clusters = new Dictionary<int, List<Part>>();
        for (int i = 0; i < parts.Count; i++)
        {
            int r = Find(i);
            if (!clusters.TryGetValue(r, out var list))
                clusters[r] = list = new List<Part>();
            list.Add(parts[i]);
        }
        return new List<List<Part>>(clusters.Values);
    }

    // r: pigment blotches. g: backrun inside. b: backrun rim. a: low-frequency noise N for uneven edge pigment.
    // All in the model's own space, seeded per model.
    // World size of `screenPercent` of the screen height at `worldPoint`, seen from the main camera.
    // Falls back to `fallback` when there is no camera.
    static float WorldSizeForScreen(Vector3 worldPoint, float screenPercent, float fallback)
    {
        var camera = Camera.main;
        if (camera == null)
            return fallback;
        float viewHeight = camera.orthographic
            ? camera.orthographicSize * 2f
            : 2f * Vector3.Distance(camera.transform.position, worldPoint) * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        return viewHeight * screenPercent / 100f;
    }

    // Sizes are in model (root) units. Resolution follows the detail size, so a voxel is about the same size on
    // screen for every model; patternResolution caps it.
    static Texture3D BakePattern(WatercolorPaintedModel model, Bounds bounds, float longest, float pigmentSize, float backrunSize, int seed, string path)
    {
        // The ragged backrun edge is the finest detail (0.12 of a bloom); give it about two voxels.
        float voxel = Mathf.Max(Mathf.Min(pigmentSize, backrunSize) * 0.06f, longest / model.patternResolution);
        int nx = Mathf.Clamp(Mathf.CeilToInt(bounds.size.x / voxel), 4, model.patternResolution);
        int ny = Mathf.Clamp(Mathf.CeilToInt(bounds.size.y / voxel), 4, model.patternResolution);
        int nz = Mathf.Clamp(Mathf.CeilToInt(bounds.size.z / voxel), 4, model.patternResolution);
        int count = nx * ny * nz;
        var pigment = new float[count];
        var field = new float[count];
        var edgeNoise = new float[count];
        var offsetA = new Vector3(seed % 97, (seed / 97) % 89, (seed / 8633) % 83);
        var offsetB = offsetA + new Vector3(31.7f, 11.3f, 57.1f);
        var offsetC = offsetA + new Vector3(73.1f, 29.9f, 5.3f);
        var offsetD = offsetA + new Vector3(13.7f, 91.1f, 37.3f);

        for (int z = 0, i = 0; z < nz; z++)
            for (int y = 0; y < ny; y++)
                for (int x = 0; x < nx; x++, i++)
                {
                    Vector3 q = new Vector3((x + 0.5f) / nx * bounds.size.x, (y + 0.5f) / ny * bounds.size.y, (z + 0.5f) / nz * bounds.size.z);
                    pigment[i] = Fbm(q / pigmentSize + offsetA);
                    // Low-frequency bloom shape with a high-frequency ragged edge.
                    field[i] = Noise(q / backrunSize + offsetB) + (Noise(q / (backrunSize * 0.12f) + offsetC) - 0.5f) * 0.15f;
                    edgeNoise[i] = Noise(q / (pigmentSize * 0.6f) + offsetD);
                }

        // Threshold at the requested percentile, so coverage means what it says regardless of the noise statistics.
        var sorted = (float[])field.Clone();
        System.Array.Sort(sorted);
        float threshold = sorted[Mathf.Clamp((int)((1f - model.backrunCoverage) * (count - 1)), 0, count - 1)];
        if (model.backrunCoverage <= 0f)
            threshold = 2f;

        var colors = new Color32[count];
        for (int i = 0; i < count; i++)
        {
            float f = field[i];
            float inside = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(threshold - 0.01f, threshold + 0.01f, f));
            // The rim is a narrow band just inside the bloom edge, where pushed pigment pools.
            float rim = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(threshold - 0.015f, threshold + 0.005f, f))
                * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(threshold + 0.005f, threshold + 0.035f, f)));
            colors[i] = new Color32((byte)(pigment[i] * 255f), (byte)(inside * 255f), (byte)(rim * 255f), (byte)(edgeNoise[i] * 255f));
        }

        var texture = new Texture3D(nx, ny, nz, TextureFormat.RGBA32, false)
        {
            name = "WatercolorPattern",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };
        texture.SetPixels32(colors);
        texture.Apply(false);
        return SaveAsset(texture, path);
    }

    // One Voronoi "drop" field: space is split into chunks of `chunk` size; each chunk gets, with `chance`, one drop
    // centred on its feature point, with a random radius in [dropMin, dropMax]. A point only takes the drop of the
    // chunk it belongs to, so drops stay inside their chunk. Returns the drop's coverage (1 at the centre, 0 at the
    // edge, slightly ragged) and a random value per chunk in `cellRandom`.
    static float Drop(Vector3 q, float chunk, float chance, float dropMin, float dropMax, int salt, Vector3 edgeOffset, out float cellRandom)
    {
        Vector3 c = q / chunk;
        int cx = Mathf.FloorToInt(c.x), cy = Mathf.FloorToInt(c.y), cz = Mathf.FloorToInt(c.z);
        float best = float.MaxValue;
        int bx = 0, by = 0, bz = 0;
        for (int dz = -1; dz <= 1; dz++)
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int px = cx + dx, py = cy + dy, pz = cz + dz;
                    var point = new Vector3(px + Hash(px, py, pz + salt), py + Hash(px + salt, py, pz), pz + Hash(px, py + salt, pz)) * chunk;
                    float d = (point - q).sqrMagnitude;
                    if (d < best) { best = d; bx = px; by = py; bz = pz; }
                }
        cellRandom = Hash(bx, by, bz + 17 * salt);
        if (Hash(bx + 11 * salt, by, bz) >= chance)
            return 0f;
        float radius = Mathf.Lerp(dropMin, dropMax, Hash(bx, by + 13 * salt, bz));
        float t = Mathf.Sqrt(best) / Mathf.Max(radius, 1e-4f) + (Noise(q / Mathf.Max(radius * 0.35f, 1e-4f) + edgeOffset) - 0.5f) * 0.3f;
        return 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
    }

    // r: colour chaos drop (0.5 = no change, 0/1 = -/+ full hue shift).
    // g: pencil patch coverage. b: pencil stroke angle offset for the patch (0.5 = none).
    static Texture3D BakeChaos(int nx, int ny, int nz, Bounds bounds, int seed,
        float chunk, float chance, float dropMin, float dropMax,
        float pencilChunk, float pencilChance, string path)
    {
        chunk = Mathf.Max(chunk, 1e-4f);
        pencilChunk = Mathf.Max(pencilChunk, 1e-4f);
        int s = seed * 7919;
        var pixels = new Color32[nx * ny * nz];
        var edgeOffset = new Vector3(seed % 53, 17.3f, 91.7f);
        var pencilEdge = edgeOffset + new Vector3(41.1f, 7.7f, 3.3f);
        for (int z = 0, i = 0; z < nz; z++)
            for (int y = 0; y < ny; y++)
                for (int x = 0; x < nx; x++, i++)
                {
                    Vector3 q = new Vector3((x + 0.5f) / nx * bounds.size.x, (y + 0.5f) / ny * bounds.size.y, (z + 0.5f) / nz * bounds.size.z);

                    float coverage = Drop(q, chunk, chance, dropMin, dropMax, s, edgeOffset, out float h);
                    float signedAmount = (h < 0.5f ? -1f : 1f) * Mathf.Lerp(0.4f, 1f, Mathf.Abs(h * 2f - 1f));
                    float chaos = signedAmount * coverage;

                    // Pencil patches: one patch per chosen chunk, filling most of it.
                    float patch = Drop(q, pencilChunk, pencilChance, pencilChunk * 0.6f, pencilChunk * 1.0f, s + 101, pencilEdge, out float angle);

                    pixels[i] = new Color32(
                        (byte)Mathf.RoundToInt((0.5f + 0.5f * chaos) * 255f),
                        (byte)Mathf.RoundToInt(patch * 255f),
                        (byte)Mathf.RoundToInt(angle * 255f),
                        255);
                }

        var texture = new Texture3D(nx, ny, nz, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };
        texture.SetPixels32(pixels);
        texture.Apply(false);
        return SaveAsset(texture, path);
    }

    static Material FlatMaterial(Material source, Texture3D pattern, string folder)
    {
        string name = source.name.Replace("_watercolor", "");
        string path = folder + "/" + Sanitize(name) + "_watercolor.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        bool created = material == null;
        if (created)
            material = new Material(Shader.Find(FlatShader));
        if (material != source)
        {
            material.shader = Shader.Find(FlatShader);
            if (source.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", source.GetColor("_BaseColor"));
            else if (source.HasProperty("_Color"))
                material.SetColor("_BaseColor", source.GetColor("_Color"));
            // The model's own texture drives every colour calculation. Older materials (Standard, from some FBX
            // exports) keep it in _MainTex.
            string texProperty = source.HasProperty("_BaseMap") && source.GetTexture("_BaseMap") != null ? "_BaseMap"
                : source.HasProperty("_MainTex") ? "_MainTex" : null;
            if (texProperty != null)
            {
                material.SetTexture("_BaseMap", source.GetTexture(texProperty));
                material.SetTextureScale("_BaseMap", source.GetTextureScale(texProperty));
                material.SetTextureOffset("_BaseMap", source.GetTextureOffset(texProperty));
            }
            if (source.HasProperty("_Floor"))
                material.SetFloat("_Floor", source.GetFloat("_Floor"));
        }
        material.SetTexture("_PatternTex", pattern);
        material.SetFloat("_HasPattern", 1f);
        material.enableInstancing = true;
        if (created)
            AssetDatabase.CreateAsset(material, path);
        EditorUtility.SetDirty(material);
        return material;
    }

    // Overwrites in place when the asset exists, so references from other scenes stay valid.
    static T SaveAsset<T>(T asset, string path) where T : Object
    {
        // The main object's name must match the file name, or Unity warns on every import.
        asset.name = Path.GetFileNameWithoutExtension(path);
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
        EditorUtility.CopySerialized(asset, existing);
        Object.DestroyImmediate(asset);
        EditorUtility.SetDirty(existing);
        return existing;
    }

    // Deletes a model's previous bake folder once nothing in the open scenes uses it any more. A duplicated model
    // still points at the original's folder, so a folder another model uses is kept.
    static void DeleteOldBake(string oldFolder, string newFolder)
    {
        if (string.IsNullOrEmpty(oldFolder) || oldFolder == newFolder || !oldFolder.StartsWith(BakeRoot + "/") || !AssetDatabase.IsValidFolder(oldFolder))
            return;
        foreach (var other in Object.FindObjectsByType<WatercolorPaintedModel>())
            if (other.pattern != null && AssetDatabase.GetAssetPath(other.pattern).StartsWith(oldFolder + "/"))
                return;
        AssetDatabase.DeleteAsset(oldFolder);
    }

    static string EnsureFolder(WatercolorPaintedModel model)
    {
        if (!AssetDatabase.IsValidFolder(BakeRoot))
            AssetDatabase.CreateFolder("Assets/01_Arts", "Baked");
        // Prefab instances share a name and their prefab's object id, so the instance id keeps each model's folder unique.
        var gid = GlobalObjectId.GetGlobalObjectIdSlow(model.gameObject);
        ulong id = gid.targetPrefabId != 0 ? gid.targetPrefabId : gid.targetObjectId;
        string name = Sanitize($"{model.gameObject.scene.name}_{model.name}_{id}");
        string folder = $"{BakeRoot}/{name}";
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder(BakeRoot, name);
        return folder;
    }

    static string RelativePath(Transform root, Transform t)
    {
        string path = t.name;
        while (t.parent != null && t != root && t.parent != root)
        {
            t = t.parent;
            path = t.name + "_" + path;
        }
        return path;
    }

    static string Sanitize(string s)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            s = s.Replace(c, '_');
        return s.Replace(' ', '_').Replace('.', '_');
    }

    static float Hash(int x, int y, int z)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + z * 1274126177);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / 16777215f;
        }
    }

    static float Noise(Vector3 p)
    {
        int x = Mathf.FloorToInt(p.x), y = Mathf.FloorToInt(p.y), z = Mathf.FloorToInt(p.z);
        float fx = p.x - x, fy = p.y - y, fz = p.z - z;
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);
        fz = fz * fz * (3f - 2f * fz);
        float a = Mathf.Lerp(Hash(x, y, z), Hash(x + 1, y, z), fx);
        float b = Mathf.Lerp(Hash(x, y + 1, z), Hash(x + 1, y + 1, z), fx);
        float c = Mathf.Lerp(Hash(x, y, z + 1), Hash(x + 1, y, z + 1), fx);
        float d = Mathf.Lerp(Hash(x, y + 1, z + 1), Hash(x + 1, y + 1, z + 1), fx);
        return Mathf.Lerp(Mathf.Lerp(a, b, fy), Mathf.Lerp(c, d, fy), fz);
    }

    static float Fbm(Vector3 p) => Noise(p) * 0.55f + Noise(p * 2.03f + Vector3.one * 17.1f) * 0.3f + Noise(p * 4.01f + Vector3.one * 41.7f) * 0.15f;
}

[CustomEditor(typeof(WatercolorPaintedModel))]
public class WatercolorPaintedModelEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();
        if (GUILayout.Button("Bake"))
            foreach (var t in targets)
                Debug.Log(WatercolorBaker.Bake((WatercolorPaintedModel)t));
    }
}

[CustomEditor(typeof(WatercolorManager))]
public class WatercolorManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Blotch / backrun sizes and the colour chaos chunk, chance and drop sizes are used when baking. Re-bake after changing them.", MessageType.Info);
        if (GUILayout.Button("Bake All Models In Scene"))
            WatercolorBaker.BakeAll();
    }
}
#endif
