using System.Text;
using UnityEditor;
using UnityEngine;

// A hint on the easter eggs the bird sets off by being clicked onto them (landing on their Perchable): the same sparkle as the
// leaning log's (InteractiveSparkle, shown by InteractiveIndicator while the perch can still be used), but smaller and fainter,
// so it whispers rather than points. Makes EggSparkle, a variant of InteractiveSparkle, and puts one at the perch point of every
// perch in those eggs' prefabs. Eggs set off by the bird's cry (the squirrel, the cliff echo) have their own hint and are left
// out, as are the migrating birds (zooming out). The snow flower's glowing snow had the log's sparkle already, stopped by the egg
// itself when it is dug (SnowFlower's Glow): it is swapped for this one in the same place.
// Rerunning sets the variant's values again and replaces the sparkles it put there.

// unity command script run --file AgentScripts/BuildEggSparkles.cs --entry BuildEggSparkles.Run
public static class BuildEggSparkles
{
    const string SourcePath = "Assets/03_Prefabs/InteractiveSparkle.prefab";
    const string VariantPath = "Assets/03_Prefabs/EggSparkle.prefab";
    const string MaterialPath = "Assets/01_Arts/Materials/vfx/EggSparkle.mat";

    static readonly string[] Eggs =
    {
        "Assets/03_Prefabs/EasterEggs/CornelianCherry.prefab",
        "Assets/03_Prefabs/EasterEggs/IcicleXylophone.prefab",
        "Assets/03_Prefabs/EasterEggs/SnowWhirl.prefab",
        "Assets/03_Prefabs/EasterEggs/StumpLadybug.prefab",
        // The ladybug egg as it is now: in the leaning log's stump.
        "Assets/03_Prefabs/EasterEggs/LeaningLogStump.prefab",
    };

    public static string Run()
    {
        var report = new StringBuilder();
        GameObject variant = MakeVariant();
        report.AppendLine($"Made {VariantPath}.");

        foreach (string path in Eggs)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int added = 0;
                // Perches that share a perch point (the icicles all have the bird stand at one spot) get one sparkle there.
                var points = new System.Collections.Generic.HashSet<Transform>();
                foreach (Perchable perch in root.GetComponentsInChildren<Perchable>(true))
                {
                    if (!points.Add(perch.PerchPoint))
                    {
                        if (perch.TryGetComponent(out InteractiveIndicator extra))
                            Object.DestroyImmediate(extra);
                        continue;
                    }
                    var indicator = perch.GetComponent<InteractiveIndicator>();
                    if (indicator == null)
                        indicator = perch.gameObject.AddComponent<InteractiveIndicator>();
                    var so = new SerializedObject(indicator);
                    SerializedProperty sparkle = so.FindProperty("sparkle");
                    // What was there before: the one adding the component spawns (the log's size), or this script's from a run before.
                    if (sparkle.objectReferenceValue is ParticleSystem old)
                        Object.DestroyImmediate(old.gameObject);
                    for (int i = perch.PerchPoint.childCount - 1; i >= 0; i--)
                        if (perch.PerchPoint.GetChild(i).name == variant.name)
                            Object.DestroyImmediate(perch.PerchPoint.GetChild(i).gameObject);

                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(variant, perch.PerchPoint);
                    instance.transform.localPosition = Vector3.zero;
                    instance.transform.localRotation = Quaternion.identity;
                    sparkle.objectReferenceValue = instance.GetComponent<ParticleSystem>();
                    so.ApplyModifiedPropertiesWithoutUndo();
                    added++;
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
                report.AppendLine($"{System.IO.Path.GetFileNameWithoutExtension(path)}: {added} sparkle(s).");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
        report.AppendLine(SwapSnowFlowerGlow(variant));
        AssetDatabase.SaveAssets();
        return report.ToString();
    }

    static string SwapSnowFlowerGlow(GameObject variant)
    {
        const string path = "Assets/03_Prefabs/EasterEggs/SnowFlower.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var egg = root.GetComponent<SnowFlower>();
            var so = new SerializedObject(egg);
            SerializedProperty glow = so.FindProperty("glow");
            if (glow.objectReferenceValue is not ParticleSystem old)
                return "SnowFlower: no glow to swap.";
            Transform place = old.transform;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(variant, place.parent);
            instance.transform.SetLocalPositionAndRotation(place.localPosition, place.localRotation);
            instance.transform.SetSiblingIndex(place.GetSiblingIndex());
            string was = old.name;
            Object.DestroyImmediate(old.gameObject);
            glow.objectReferenceValue = instance.GetComponent<ParticleSystem>();
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
            return $"SnowFlower: glow {was} swapped for {instance.name}.";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // The log's sparkle a little smaller and fainter, spread a little wider, and yellow like it, but laid over the picture
    // (PaintedSparkle) instead of added onto it: the log's adds light, which over white snow turns white and is lost.
    static GameObject MakeVariant()
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
        try
        {
            var particles = instance.GetComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.startSize = new ParticleSystem.MinMaxCurve(0.055f, 0.095f);
            main.startColor = new Color(1f, 1f, 1f, 0.7f);
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 3f;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.radius = 0.5f;
            instance.GetComponent<ParticleSystemRenderer>().sharedMaterial = SparkleMaterial();
            return PrefabUtility.SaveAsPrefabAsset(instance, VariantPath);
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    static Material SparkleMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("ThatGameJam/PaintedSparkle"));
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        material.shader = Shader.Find("ThatGameJam/PaintedSparkle");
        material.SetColor("_Color", new Color(2.2f, 1.8f, 0.15f, 1f));
        material.SetFloat("_EdgeSoftness", 0.4f);
        material.SetFloat("_Cover", 0.85f);
        EditorUtility.SetDirty(material);
        return material;
    }
}
