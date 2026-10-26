using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Builds Assets/00_Scenes/Sam_Test.unity: a straight road, an AI child, two obstacles, a goal and the player bird.
// Run: unity command script run --file AgentScripts/BuildSamTestScene.cs --entry BuildSamTestScene.Build
public static class BuildSamTestScene
{
    const string ScenePath = "Assets/00_Scenes/Sam_Test.unity";
    const string MaterialFolder = "Assets/01_Arts/Materials/Test";

    public static string Build()
    {
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        Primitive(PrimitiveType.Cube, "Snow", new Vector3(0f, -0.15f, 13f), new Vector3(30f, 0.1f, 40f), Mat("Snow", new Color(0.92f, 0.95f, 1f)));
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

        Camera camera = Camera.main;
        SetRef(bird.AddComponent<BirdController>(), "cameraTransform", camera.transform);
        SetRef(camera.gameObject.AddComponent<QuarterViewCamera>(), "target", bird.transform);

        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
        AssetDatabase.SaveAssets();
        return ScenePath;
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

    static Material Mat(string name, Color color)
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
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
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
