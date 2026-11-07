using System.Text;
using UnityEditor;
using UnityEngine;

// The migrating birds' own paint (the MigratingBirds easter egg): the body and wings in plumage (FeatherBird: rows of feathers,
// dark wing tips, a paler underside, matte), the head and beak plain dark watercolor (WatercolorFlat), so they look like a flock
// of real birds and not like the player's glowing prism bird. Makes the materials and puts them on every bird in the egg's
// prefab. Rerunning sets them again with these values.

// unity command script run --file AgentScripts/BuildMigratingBirdLook.cs --entry BuildMigratingBirdLook.Run
public static class BuildMigratingBirdLook
{
    const string PrefabPath = "Assets/03_Prefabs/EasterEggs/MigratingBirds.prefab";
    const string Folder = "Assets/01_Arts/Materials/MigratingBird";
    const string FeatherShader = "ThatGameJam/FeatherBird";
    const string PlainShader = "ThatGameJam/WatercolorFlat";

    // The bird model's parts (renderer names), their material and its shader, and its colour (FeatherBird: the back's).
    static readonly (string part, string material, string shader, Color colour)[] Parts =
    {
        ("Plane", "MigratingBird_Feather", FeatherShader, new Color(0.55f, 0.48f, 0.40f)),      // body and wings
        ("WingHand.L", "MigratingBird_Feather", FeatherShader, new Color(0.55f, 0.48f, 0.40f)), // wing tips
        ("WingHand.R", "MigratingBird_Feather", FeatherShader, new Color(0.55f, 0.48f, 0.40f)),
        ("Circle", "MigratingBird_Head", PlainShader, new Color(0.16f, 0.15f, 0.15f)),
        ("Icosphere", "MigratingBird_Beak", PlainShader, new Color(0.20f, 0.18f, 0.16f)),
    };

    public static string Run()
    {
        var report = new StringBuilder();
        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets/01_Arts/Materials", "MigratingBird");

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            int painted = 0;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                foreach (var (part, name, shaderName, colour) in Parts)
                    if (renderer.name == part)
                    {
                        Shader shader = Shader.Find(shaderName);
                        if (shader == null)
                            return "No " + shaderName + " shader.";
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
