using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Builds Seohyeon_Pastel_Test.unity: a copy of the watercolor test scene (as it is in the editor, unsaved changes
// included) with every WatercolorFlat material swapped for a ThatGameJam/SoftPastel copy and a PastelStyle object added.
// The watercolor scene stays open and unchanged. Pastel materials go to Assets/01_Arts/Materials/Pastel.
// Run: unity command script run --file AgentScripts/BuildPastelTestScene.cs --entry BuildPastelTestScene.Build
public static class BuildPastelTestScene
{
    const string SourceScenePath = "Assets/00_Scenes/Seohyeon_Watercolor_Test.unity";
    const string ScenePath = "Assets/00_Scenes/Seohyeon_Pastel_Test.unity";
    const string MaterialFolder = "Assets/01_Arts/Materials/Pastel";

    public static string Build()
    {
        var source = EditorSceneManager.GetSceneByPath(SourceScenePath);
        if (!source.isLoaded)
            return "Open " + SourceScenePath + " first";
        EditorSceneManager.SaveScene(source, ScenePath, true);

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        Shader pastel = Shader.Find("ThatGameJam/SoftPastel");
        if (!AssetDatabase.IsValidFolder(MaterialFolder))
            AssetDatabase.CreateFolder("Assets/01_Arts/Materials", "Pastel");

        var copies = new Dictionary<Material, Material>();
        int swapped = 0;
        foreach (var root in scene.GetRootGameObjects())
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            var materials = renderer.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < materials.Length; i++)
            {
                Material sourceMaterial = materials[i];
                if (sourceMaterial == null || sourceMaterial.shader.name != "ThatGameJam/WatercolorFlat")
                    continue;
                if (!copies.TryGetValue(sourceMaterial, out Material copy))
                {
                    copy = new Material(pastel);
                    copy.CopyPropertiesFromMaterial(sourceMaterial);
                    copy.shader = pastel;
                    string path = AssetDatabase.GenerateUniqueAssetPath($"{MaterialFolder}/{sourceMaterial.name}.mat");
                    AssetDatabase.CreateAsset(copy, path);
                    copies[sourceMaterial] = copy;
                }
                materials[i] = copy;
                changed = true;
            }
            if (changed)
            {
                renderer.sharedMaterials = materials;
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                swapped++;
            }
        }

        var style = new GameObject("Pastel Style", typeof(PastelStyle));
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(style, scene);

        EditorSceneManager.SaveScene(scene);
        EditorSceneManager.CloseScene(scene, true);
        AssetDatabase.SaveAssets();
        return $"{ScenePath}: {swapped} renderers, {copies.Count} pastel materials";
    }
}
