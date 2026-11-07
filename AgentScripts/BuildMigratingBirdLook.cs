using System.Text;
using UnityEditor;
using UnityEngine;

// The migrating birds' own paint (the MigratingBirds easter egg): plain watercolor (WatercolorFlat) in the browns and greys of a
// wild bird, one colour per part of the bird model, so they look like a flock of real birds and not like the player's glowing
// prism bird. Makes the materials and puts them on every bird in the egg's prefab. Rerunning sets them again with these colours.

// unity command script run --file AgentScripts/BuildMigratingBirdLook.cs --entry BuildMigratingBirdLook.Run
public static class BuildMigratingBirdLook
{
    const string PrefabPath = "Assets/03_Prefabs/EasterEggs/MigratingBirds.prefab";
    const string Folder = "Assets/01_Arts/Materials/MigratingBird";
    const string ShaderName = "ThatGameJam/WatercolorFlat";

    // The bird model's parts (renderer names) and their colour.
    static readonly (string part, string material, Color colour)[] Parts =
    {
        ("Plane", "MigratingBird_Body", new Color(0.55f, 0.48f, 0.40f)),      // body and wings: warm grey-brown
        ("WingHand.L", "MigratingBird_WingTip", new Color(0.30f, 0.27f, 0.25f)), // wing tips: dark
        ("WingHand.R", "MigratingBird_WingTip", new Color(0.30f, 0.27f, 0.25f)),
        ("Icosphere", "MigratingBird_Head", new Color(0.24f, 0.23f, 0.24f)),  // head: near black
        ("Circle", "MigratingBird_Eye", new Color(0.12f, 0.11f, 0.11f)),
    };

    public static string Run()
    {
        var report = new StringBuilder();
        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets/01_Arts/Materials", "MigratingBird");
        Shader shader = Shader.Find(ShaderName);
        if (shader == null)
            return "No " + ShaderName + " shader.";

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            int painted = 0;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                foreach (var (part, name, colour) in Parts)
                    if (renderer.name == part)
                    {
                        renderer.sharedMaterials = new[] { MaterialFor(shader, name, colour) };
                        painted++;
                    }
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            report.AppendLine($"Painted {painted} renderers in {PrefabPath}.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        AssetDatabase.SaveAssets();
        return report.ToString();
    }

    static Material MaterialFor(Shader shader, string name, Color colour)
    {
        string path = $"{Folder}/{name}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = shader;
        material.SetColor("_BaseColor", colour);
        EditorUtility.SetDirty(material);
        return material;
    }
}
