#if UNITY_EDITOR
// Editor-only: wrapped in UNITY_EDITOR for the same reason as WatercolorBaker.
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Prepares the bird for ThatGameJam/BirdPrism. Copies its meshes (bone weights stay, so the animation still works) and
// bakes into UV1: xyz the rest-pose position with half the wingspan as 1, w the heat (1 on the body and along the wings'
// leading edge, 0 at the trailing edge). Adds the glowing fringe material and the flying petals and dust.
// Results go to Assets/01_Arts/Baked/BirdPrism/. Materials there keep their values when baking again.
public static class BirdPrismBaker
{
    const string Folder = "Assets/01_Arts/Baked/BirdPrism";
    const int HeatBins = 24;

    [MenuItem("Tools/Watercolor/Bake Prism Bird")]
    public static void BakeMenu() => Debug.Log(Bake());

    public static string Bake()
    {
        var bird = Object.FindAnyObjectByType<BirdController>();
        if (bird == null)
            return "Prism bird: no BirdController in the open scenes";
        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets/01_Arts/Baked", "BirdPrism");

        Material body = LoadOrCreateMaterial("BirdPrism", "ThatGameJam/BirdPrism");
        Material fringe = LoadOrCreateMaterial("BirdPrismGlow", "ThatGameJam/BirdPrismGlow");
        Material petal = LoadOrCreateMaterial("BirdPrismPetal", "ThatGameJam/BirdPrismPiece");
        Material dust = LoadOrCreateMaterial("BirdPrismDust", "ThatGameJam/BirdPrismPiece", material => material.SetFloat("_Petal", 0f));

        // The bird no longer takes the watercolor bake; Bake All would put the watercolor meshes back.
        if (bird.TryGetComponent(out WatercolorPaintedModel painted))
            Undo.DestroyObjectImmediate(painted);

        var renderers = bird.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        var sources = new Dictionary<SkinnedMeshRenderer, Mesh>();
        var bounds = new Bounds();
        bool first = true;
        SkinnedMeshRenderer largest = null;
        foreach (var renderer in renderers)
        {
            Mesh source = SourceOf(renderer.sharedMesh);
            if (source == null)
                continue;
            sources[renderer] = source;
            if (first) { bounds = source.bounds; first = false; }
            else bounds.Encapsulate(source.bounds);
            if (largest == null || source.vertexCount > sources[largest].vertexCount)
                largest = renderer;
        }
        // The wings define the heat: the largest mesh and the wing hands, which all spread sideways.
        var wingVertices = new List<Vector3>();
        foreach (var pair in sources)
            if (pair.Key == largest || pair.Key.name.StartsWith("WingHand"))
                wingVertices.AddRange(pair.Value.vertices);
        float halfSpan = Mathf.Max(bounds.extents.x, 1e-5f);
        Vector3 centre = bounds.center;
        var leading = new float[HeatBins];
        var trailing = new float[HeatBins];
        FindWingEdges(wingVertices, centre, halfSpan, leading, trailing);

        string log = "";
        foreach (var pair in sources)
        {
            var renderer = pair.Key;
            bool isWing = renderer == largest || renderer.name.StartsWith("WingHand");
            Mesh baked = BakeMesh(pair.Value, centre, halfSpan, isWing, leading, trailing);
            string path = $"{Folder}/{pair.Value.name}_prism.asset";
            baked.name = System.IO.Path.GetFileNameWithoutExtension(path);
            // A new asset every time: copying over one with another vertex count leaves its index buffer broken.
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(baked, path);
            // The petals and dust spawn on the mesh, which needs it readable in a build.
            var serialized = new SerializedObject(baked);
            serialized.FindProperty("m_IsReadable").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Undo.RecordObject(renderer, "Bake Prism Bird");
            renderer.sharedMesh = baked;
            renderer.sharedMaterials = new[] { body, fringe };
            log += $"{renderer.name}: {baked.vertexCount} vertices; ";
        }

        if (largest != null)
        {
            SetUpPieces(bird.transform, "Prism Petals", largest, petal, true);
            SetUpPieces(bird.transform, "Prism Dust", largest, dust, false);
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(bird.gameObject.scene);
        return "Prism bird: " + log;
    }

    static Material LoadOrCreateMaterial(string name, string shader, System.Action<Material> firstTime = null)
    {
        string path = $"{Folder}/{name}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
            return material;
        material = new Material(Shader.Find(shader)) { name = name };
        firstTime?.Invoke(material);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    // Baking again starts from the original mesh, not from an earlier result.
    static Mesh SourceOf(Mesh current)
    {
        if (current == null || !AssetDatabase.GetAssetPath(current).StartsWith(Folder))
            return current;
        string name = current.name.Replace("_prism", "");
        foreach (string guid in AssetDatabase.FindAssets($"{name} t:Mesh"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.StartsWith(Folder))
                continue;
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null && mesh.name == name)
                return mesh;
        }
        return current;
    }

    // For each slice across the wingspan: the front and back of the wing along the bird's length (mesh y).
    static void FindWingEdges(List<Vector3> vertices, Vector3 centre, float halfSpan, float[] leading, float[] trailing)
    {
        for (int i = 0; i < HeatBins; i++)
        {
            leading[i] = float.MinValue;
            trailing[i] = float.MaxValue;
        }
        foreach (Vector3 v in vertices)
        {
            int bin = BinOf(v, centre, halfSpan);
            leading[bin] = Mathf.Max(leading[bin], v.y);
            trailing[bin] = Mathf.Min(trailing[bin], v.y);
        }
        // Empty slices borrow from the neighbour nearer the body.
        for (int i = 1; i < HeatBins; i++)
        {
            if (leading[i] == float.MinValue)
            {
                leading[i] = leading[i - 1];
                trailing[i] = trailing[i - 1];
            }
        }
    }

    static int BinOf(Vector3 v, Vector3 centre, float halfSpan) =>
        Mathf.Clamp(Mathf.FloorToInt(Mathf.Abs(v.x - centre.x) / halfSpan * HeatBins), 0, HeatBins - 1);

    static Mesh BakeMesh(Mesh source, Vector3 centre, float halfSpan, bool isWing, float[] leading, float[] trailing)
    {
        Vector3[] positions = source.vertices;
        var rest = new List<Vector4>(positions.Length);
        foreach (Vector3 v in positions)
        {
            Vector3 p = (v - centre) / halfSpan;
            float heat = 1f;
            if (isWing)
            {
                int bin = BinOf(v, centre, halfSpan);
                float front = (leading[bin] - v.y) / Mathf.Max(leading[bin] - trailing[bin], 1e-5f);
                float along = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.5f, front));
                float nearBody = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.05f, 0.2f, Mathf.Abs(p.x)));
                heat = Mathf.Max(along * Mathf.Lerp(1f, 0.7f, Mathf.Abs(p.x)), nearBody);
            }
            rest.Add(new Vector4(p.x, p.y, p.z, heat));
        }

        Mesh mesh = Object.Instantiate(source);
        mesh.SetUVs(1, rest);
        return mesh;
    }

    static void SetUpPieces(Transform bird, string name, SkinnedMeshRenderer from, Material material, bool petals)
    {
        Transform child = bird.Find(name);
        if (child == null)
        {
            child = new GameObject(name).transform;
            Undo.RegisterCreatedObjectUndo(child.gameObject, "Bake Prism Bird");
            child.SetParent(bird, false);
        }
        child.gameObject.layer = bird.gameObject.layer;
        var system = child.GetComponent<ParticleSystem>();
        if (system == null)
            system = child.gameObject.AddComponent<ParticleSystem>();

        var main = system.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = petals ? new ParticleSystem.MinMaxCurve(3f, 6f) : new ParticleSystem.MinMaxCurve(1f, 2.5f);
        main.startSpeed = petals ? new ParticleSystem.MinMaxCurve(0.1f, 0.4f) : new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
        main.startSize = petals ? new ParticleSystem.MinMaxCurve(0.05f, 0.15f) : new ParticleSystem.MinMaxCurve(0.008f, 0.03f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = petals ? 0.01f : 0f;
        main.maxParticles = petals ? 40 : 150;
        main.startColor = new ParticleSystem.MinMaxGradient(petals ? PetalColours() : DustColours()) { mode = ParticleSystemGradientMode.RandomColor };

        var emission = system.emission;
        emission.rateOverTime = petals ? 5f : 40f;
        emission.rateOverDistance = petals ? 1.5f : 8f;

        var shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.SkinnedMeshRenderer;
        shape.skinnedMeshRenderer = from;

        var noise = system.noise;
        noise.enabled = true;
        noise.strength = petals ? 0.25f : 0.15f;
        noise.frequency = 0.5f;

        var fade = system.colorOverLifetime;
        fade.enabled = true;
        var alpha = new Gradient();
        alpha.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        fade.color = alpha;

        var renderer = child.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        EditorUtility.SetDirty(system);
    }

    // Warm white, cream, pink and orange, with a little green at one end (random colour reads the gradient evenly).
    static Gradient PetalColours()
    {
        var gradient = new Gradient { mode = GradientMode.Fixed };
        gradient.SetKeys(new[]
        {
            new GradientColorKey(new Color(1f, 0.95f, 0.85f), 0.4f),
            new GradientColorKey(new Color(1f, 0.82f, 0.45f), 0.65f),
            new GradientColorKey(new Color(1f, 0.65f, 0.72f), 0.8f),
            new GradientColorKey(new Color(1f, 0.5f, 0.2f), 0.95f),
            new GradientColorKey(new Color(0.3f, 0.85f, 0.4f), 1f),
        }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return gradient;
    }

    static Gradient DustColours()
    {
        var gradient = new Gradient { mode = GradientMode.Fixed };
        gradient.SetKeys(new[]
        {
            new GradientColorKey(new Color(1f, 0.8f, 0.4f), 0.6f),
            new GradientColorKey(new Color(1f, 0.55f, 0.2f), 1f),
        }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return gradient;
    }
}
#endif
