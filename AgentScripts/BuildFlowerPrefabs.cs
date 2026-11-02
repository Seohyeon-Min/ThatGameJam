using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Makes Assets/03_Prefabs/Flowers/Flower01..03 from the flower meshes, painted in watercolor like the baby: the bake reads each
// mesh's own colour and texture. Each flower is put in the open scene for the bake, saved as a prefab, and removed again.
// Run: unity command script run --file AgentScripts/BuildFlowerPrefabs.cs --entry BuildFlowerPrefabs.Run
public static class BuildFlowerPrefabs
{
    const string Folder = "Assets/03_Prefabs/Flowers";

    public static string Run()
    {
        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets/03_Prefabs", "Flowers");
        string report = "";
        foreach (string mesh in new[] { "flower01", "flower02", "flower03" })
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/01_Arts/Mesh/{mesh}/{mesh}.obj");
            var flower = (GameObject)PrefabUtility.InstantiatePrefab(model);
            PrefabUtility.UnpackPrefabInstance(flower, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            flower.name = "Flower" + mesh.Substring(6);
            // Small and many: their shadows would cost more than they show.
            foreach (var renderer in flower.GetComponentsInChildren<Renderer>())
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            var painted = flower.AddComponent<WatercolorPaintedModel>();
            string bake = WatercolorBaker.Bake(painted);
            string path = $"{Folder}/{flower.name}.prefab";
            PrefabUtility.SaveAsPrefabAsset(flower, path);
            Object.DestroyImmediate(flower);

            var saved = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            report += $"{path}: {bake}\n";
            foreach (var renderer in saved.GetComponentsInChildren<Renderer>())
            {
                Material material = renderer.sharedMaterial;
                report += $"  {renderer.name}: {material.name} [{material.shader.name}] colour={material.GetColor("_BaseColor")} texture={(material.GetTexture("_BaseMap") != null ? material.GetTexture("_BaseMap").name : "none")}\n";
                break;
            }
        }
        AssetDatabase.SaveAssets();
        return report;
    }
}
