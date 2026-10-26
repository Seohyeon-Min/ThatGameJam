using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Builds Assets/03_Prefabs/Tree.prefab and Assets/00_Scenes/Sam_Test.unity:
// a straight road lined with trees, an AI child, two obstacles, a goal and the player bird.
// Run: unity command script run --file AgentScripts/BuildSamTestScene.cs --entry BuildSamTestScene.Build
public static class BuildSamTestScene
{
    const string ScenePath = "Assets/00_Scenes/Sam_Test.unity";
    const string MaterialFolder = "Assets/01_Arts/Materials/Test";
    const string TreePrefabPath = "Assets/03_Prefabs/Tree.prefab";

    public static string Build()
    {
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        GameObject treePrefab = BuildTreePrefab();

        Primitive(PrimitiveType.Cube, "Snow", new Vector3(0f, -0.15f, 13f), new Vector3(120f, 0.1f, 120f), Mat("Snow", new Color(0.92f, 0.95f, 1f)));
        Primitive(PrimitiveType.Cube, "Road", new Vector3(0f, -0.05f, 13f), new Vector3(3f, 0.1f, 30f), Mat("Road", new Color(0.55f, 0.6f, 0.7f)));

        Material obstacleMat = Mat("Obstacle", new Color(0.45f, 0.3f, 0.2f));
        foreach (float z in new[] { 6f, 14f })
            Primitive(PrimitiveType.Cube, $"Obstacle_{z}", new Vector3(0f, 0.75f, z), new Vector3(3f, 1.5f, 0.5f), obstacleMat)
                .AddComponent<Obstacle>();

        GameObject goal = Primitive(PrimitiveType.Cylinder, "Goal", new Vector3(0f, 0.05f, 24f), new Vector3(2f, 0.05f, 2f), Mat("Goal", new Color(0.5f, 0.85f, 0.45f)));
        Object.DestroyImmediate(goal.GetComponent<Collider>());

        GameObject child = Primitive(PrimitiveType.Capsule, "Child", new Vector3(0f, 0.6f, 0f), Vector3.one * 0.6f, Mat("Child", new Color(1f, 0.6f, 0.4f)));
        SetRef(child.AddComponent<ChildWalker>(), "goal", goal.transform);

        GameObject bird = Primitive(PrimitiveType.Sphere, "Bird", new Vector3(2f, 2.5f, 0f), Vector3.one * 0.5f, Mat("Bird", Color.white));
        Rigidbody body = bird.AddComponent<Rigidbody>();
        body.useGravity = false;
        body.constraints = RigidbodyConstraints.FreezeRotation;
        body.interpolation = RigidbodyInterpolation.Interpolate;

        // The prefab's branch points along local +Z, so each tree is turned to face the road.
        foreach (float z in new[] { 3f, 10f, 17f })
        {
            PlaceTree(treePrefab, new Vector3(-3.2f, 0f, z), 90f);
            PlaceTree(treePrefab, new Vector3(3.2f, 0f, z), -90f);
        }

        Camera camera = Camera.main;
        SetRef(bird.AddComponent<BirdController>(), "cameraTransform", camera.transform);
        SetRef(camera.gameObject.AddComponent<QuarterViewCamera>(), "target", bird.transform);

        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
        AssetDatabase.SaveAssets();
        return ScenePath;
    }

    // Trunk, crown and one branch along +Z with a perch point at its tip. Only the trunk has a collider.
    static GameObject BuildTreePrefab()
    {
        var root = new GameObject("Tree");

        GameObject trunk = Primitive(PrimitiveType.Cylinder, "Trunk", new Vector3(0f, 2f, 0f), new Vector3(0.4f, 2f, 0.4f), Mat("Trunk", new Color(0.35f, 0.25f, 0.18f)));
        trunk.transform.SetParent(root.transform, false);

        Material leaves = Mat("Leaves", new Color(0.3f, 0.45f, 0.35f));
        GameObject crown = Primitive(PrimitiveType.Sphere, "Crown", new Vector3(0f, 4.6f, 0f), Vector3.one * 2.4f, leaves);
        crown.transform.SetParent(root.transform, false);
        Object.DestroyImmediate(crown.GetComponent<Collider>());

        GameObject branch = Primitive(PrimitiveType.Cylinder, "Branch", new Vector3(0f, 2.8f, 0.9f), new Vector3(0.12f, 0.9f, 0.12f), trunk.GetComponent<Renderer>().sharedMaterial);
        branch.transform.SetParent(root.transform, false);
        branch.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        Object.DestroyImmediate(branch.GetComponent<Collider>());

        var perch = new GameObject("Perch");
        perch.transform.SetParent(root.transform, false);
        perch.transform.localPosition = new Vector3(0f, 3.11f, 1.6f);

        GameObject indicator = Primitive(PrimitiveType.Sphere, "Indicator", new Vector3(0f, 0.45f, 0f), Vector3.one * 0.2f, Mat("PerchIndicator", new Color(1f, 0.85f, 0.3f), "Universal Render Pipeline/Unlit"));
        indicator.transform.SetParent(perch.transform, false);
        Object.DestroyImmediate(indicator.GetComponent<Collider>());
        SetRef(perch.AddComponent<PerchPoint>(), "indicator", indicator);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, TreePrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    static void PlaceTree(GameObject prefab, Vector3 position, float yaw)
    {
        var tree = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        tree.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
    }

    static GameObject Primitive(PrimitiveType type, string name, Vector3 position, Vector3 scale, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.position = position;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = material;
        return go;
    }

    static Material Mat(string name, Color color, string shader = "Universal Render Pipeline/Lit")
    {
        string path = $"{MaterialFolder}/{name}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            if (!AssetDatabase.IsValidFolder(MaterialFolder))
            {
                if (!AssetDatabase.IsValidFolder("Assets/01_Arts/Materials"))
                    AssetDatabase.CreateFolder("Assets/01_Arts", "Materials");
                AssetDatabase.CreateFolder("Assets/01_Arts/Materials", "Test");
            }
            mat = new Material(Shader.Find(shader));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetColor("_BaseColor", color);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static void SetRef(Object component, string field, Object value)
    {
        var so = new SerializedObject(component);
        so.FindProperty(field).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
