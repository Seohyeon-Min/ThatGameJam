using UnityEditor;
using UnityEngine;

// Makes Assets/03_Prefabs/Trees/Pine_Leafy from the pine with needles. Like the other tree prefabs it is the plain model: the
// TreeGenerator paints it in watercolor in the scene, as it does the rest. It has no perch point, so the bird doesn't land on it.
// Run: unity command script run --file AgentScripts/BuildPinePrefab.cs --entry BuildPinePrefab.Run
public static class BuildPinePrefab
{
    const string Path = "Assets/03_Prefabs/Trees/Pine_Leafy.prefab";

    public static string Run()
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/01_Arts/Mesh/pine_tree/pine_tree_l_01.fbx");
        var pine = (GameObject)PrefabUtility.InstantiatePrefab(model);
        PrefabUtility.UnpackPrefabInstance(pine, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        pine.name = "Pine_Leafy";
        PrefabUtility.SaveAsPrefabAsset(pine, Path);
        Object.DestroyImmediate(pine);
        AssetDatabase.SaveAssets();

        var saved = AssetDatabase.LoadAssetAtPath<GameObject>(Path);
        var bounds = new Bounds(saved.transform.position, Vector3.zero);
        foreach (var renderer in saved.GetComponentsInChildren<Renderer>())
            bounds.Encapsulate(renderer.bounds);
        return $"{Path}: {saved.GetComponentsInChildren<Renderer>().Length} renderers, size {bounds.size}";
    }
}
