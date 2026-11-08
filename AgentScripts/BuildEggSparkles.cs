using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// A hint on the easter eggs the bird sets off by being clicked onto them (landing on their Perchable): the very sparkle the
// leaning log has (InteractiveSparkle), only a little smaller (EggSparkle, its variant with nothing else changed), shown by
// InteractiveIndicator while the perch can still be used.
// One at the perch point of every perch in those eggs' prefabs (perches sharing a point get one). The snow flower's glowing
// snow gets it too, in the place of its old glow. Eggs set off by the bird's cry (the squirrel, the cliff echo) have their own
// hint and are left out, as are the migrating birds (zooming out).
// Rerunning replaces the sparkles it put there (and the smaller EggSparkle an earlier version of this put there).

// unity command script run --file AgentScripts/BuildEggSparkles.cs --entry BuildEggSparkles.Run
public static class BuildEggSparkles
{
    const string SourcePath = "Assets/03_Prefabs/InteractiveSparkle.prefab";
    const string VariantPath = "Assets/03_Prefabs/EggSparkle.prefab";
    // Of the log's size.
    const float SizeShare = 0.8f;
    static readonly string[] OldNames = { "InteractiveSparkle", "EggSparkle" };

    static readonly string[] Eggs =
    {
        "Assets/03_Prefabs/EasterEggs/CornelianCherry.prefab",
        "Assets/03_Prefabs/EasterEggs/IcicleXylophone.prefab",
        "Assets/03_Prefabs/EasterEggs/SnowWhirl.prefab",
        "Assets/03_Prefabs/EasterEggs/StumpLadybug.prefab",
        // The ladybug egg as it is now: in the leaning log's stump.
        "Assets/03_Prefabs/EasterEggs/LeaningLogStump.prefab",
        "Assets/03_Prefabs/EasterEggs/SnowFlower.prefab",
    };

    public static string Run()
    {
        var report = new System.Text.StringBuilder();
        GameObject sparkle = MakeVariant();
        foreach (string path in Eggs)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int added = 0;
                // The snow flower's old glow, wherever it was: the indicator below now shows the sparkle and stops it once dug.
                if (root.TryGetComponent(out SnowFlower flower))
                {
                    var flowerSo = new SerializedObject(flower);
                    if (flowerSo.FindProperty("glow").objectReferenceValue is ParticleSystem oldGlow)
                        Object.DestroyImmediate(oldGlow.gameObject);
                }

                var points = new HashSet<Transform>();
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
                    SerializedProperty field = so.FindProperty("sparkle");
                    // What was there before: the one adding the component spawns, or this script's from a run before.
                    if (field.objectReferenceValue is ParticleSystem old)
                        Object.DestroyImmediate(old.gameObject);
                    for (int i = perch.PerchPoint.childCount - 1; i >= 0; i--)
                        if (System.Array.IndexOf(OldNames, perch.PerchPoint.GetChild(i).name) >= 0)
                            Object.DestroyImmediate(perch.PerchPoint.GetChild(i).gameObject);

                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(sparkle, perch.PerchPoint);
                    instance.transform.localPosition = Vector3.zero;
                    instance.transform.localRotation = Quaternion.identity;
                    var particles = instance.GetComponent<ParticleSystem>();
                    field.objectReferenceValue = particles;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    added++;

                    if (flower != null)
                    {
                        var flowerSo = new SerializedObject(flower);
                        flowerSo.FindProperty("glow").objectReferenceValue = particles;
                        flowerSo.ApplyModifiedPropertiesWithoutUndo();
                    }
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
                report.AppendLine($"{System.IO.Path.GetFileNameWithoutExtension(path)}: {added} sparkle(s).");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
        AssetDatabase.SaveAssets();
        return report.ToString();
    }

    // Made again from the log's each run, so only the size differs from it.
    static GameObject MakeVariant()
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
        try
        {
            ParticleSystem.MainModule main = instance.GetComponent<ParticleSystem>().main;
            ParticleSystem.MinMaxCurve size = main.startSize;
            main.startSize = new ParticleSystem.MinMaxCurve(size.constantMin * SizeShare, size.constantMax * SizeShare);
            instance.name = "EggSparkle";
            return PrefabUtility.SaveAsPrefabAsset(instance, VariantPath);
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }
}
