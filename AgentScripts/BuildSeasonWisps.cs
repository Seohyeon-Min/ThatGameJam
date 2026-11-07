using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// The wind swirl at every turn of the season (SeasonWisps), in Main: an object beside Season with the flat quad it is drawn on
// (disabled until a ring plays), and its five steps, from a faint, slowly building swirl (1) to the ending's glowing one (5).
// Rerunning makes it again with these values.

// unity command script run --file AgentScripts/BuildSeasonWisps.cs --entry BuildSeasonWisps.Run
public static class BuildSeasonWisps
{
    const string MaterialPath = "Assets/01_Arts/Materials/vfx/SnowWisps.mat";
    // Half the quad's size, in metres: past the strokes of the widest ring (40 m).
    const float Reach = 60f;

    public static string Run()
    {
        var report = new StringBuilder();
        var season = Object.FindAnyObjectByType<Season>(FindObjectsInactive.Include);
        if (season == null)
            return "No Season in the open scene.";
        Transform parent = season.transform.parent;
        Transform old = parent != null ? parent.Find("SeasonWisps") : null;
        if (old == null)
            foreach (GameObject root in season.gameObject.scene.GetRootGameObjects())
                if (root.name == "SeasonWisps")
                    old = root.transform;
        if (old != null)
            Object.DestroyImmediate(old.gameObject);

        var go = new GameObject("SeasonWisps");
        go.transform.SetParent(parent, false);
        go.transform.SetSiblingIndex(season.transform.GetSiblingIndex() + 1);

        var quadGo = new GameObject("Quad", typeof(MeshFilter), typeof(MeshRenderer));
        quadGo.transform.SetParent(go.transform, false);
        quadGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        quadGo.transform.localScale = new Vector3(Reach * 2f, Reach * 2f, 1f);
        quadGo.GetComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        var renderer = quadGo.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        quadGo.SetActive(false);

        var wisps = go.AddComponent<SeasonWisps>();
        var so = new SerializedObject(wisps);
        so.FindProperty("season").objectReferenceValue = season;
        so.FindProperty("quad").objectReferenceValue = renderer;
        var steps = so.FindProperty("steps");
        steps.arraySize = 5;
        // Seconds: the rings of steps 1 to 4 spread over 10 s, the ending's over 40 s (its Duration Scale 4).
        //            colour (alpha = strength)              strokes width spiral swirl  dry   build up  gone by
        SetStep(steps, 0, new Color(1f, 1f, 1f, 0.45f),         6, 0.6f, 0.25f, 0.02f, 0.9f,  4f,  9f);
        SetStep(steps, 1, new Color(1f, 1f, 1f, 0.65f),         9, 0.8f, 0.3f, 0.03f, 0.85f,  3f,  9f);
        SetStep(steps, 2, new Color(1f, 1f, 1f, 0.85f),        12, 1.0f, 0.35f, 0.045f, 0.8f, 2f,  9f);
        SetStep(steps, 3, new Color(1f, 1f, 1f, 0.95f),        16, 1.15f, 0.4f, 0.06f, 0.8f,  1.2f, 9f);
        // The ending's: many strokes, broad and wide, plain white with no glow, striking at once and gone fast, like the first
        // swirl was.
        SetStep(steps, 4, new Color(1f, 1f, 1f, 1f),     24, 1.4f, 0.5f, 0.035f, 0.9f,  0.15f, 3f);
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(season.gameObject.scene);
        report.AppendLine($"Made SeasonWisps under {(parent != null ? parent.name : "the scene root")}.");
        return report.ToString();
    }

    static void SetStep(SerializedProperty steps, int index, Color color, int strokes, float width, float spiral, float swirl,
        float dryBrush, float buildUp, float goneBy)
    {
        SerializedProperty step = steps.GetArrayElementAtIndex(index);
        step.FindPropertyRelative("color").colorValue = color;
        step.FindPropertyRelative("strokes").intValue = strokes;
        step.FindPropertyRelative("width").floatValue = width;
        step.FindPropertyRelative("spiral").floatValue = spiral;
        step.FindPropertyRelative("swirl").floatValue = swirl;
        step.FindPropertyRelative("dryBrush").floatValue = dryBrush;
        step.FindPropertyRelative("buildUp").floatValue = buildUp;
        step.FindPropertyRelative("goneBy").floatValue = goneBy;
    }
}
