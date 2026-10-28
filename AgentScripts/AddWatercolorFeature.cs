using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Adds the watercolor renderer features to the PC and Mobile renderers (Mobile is the WebGL default quality level).
// - WatercolorBlurFeature: active. The lightweight bilateral blur used with the WatercolorLit material.
// - WatercolorKuwaharaFeature: present but inactive. Kept for comparison; tick it on the renderer to try it.
// Both do nothing until a Volume enables their override, so Main.unity is unaffected.
// Idempotent: a renderer that already has a feature keeps it as is.
// Run: unity command script run --file AgentScripts/AddWatercolorFeature.cs --entry AddWatercolorFeature.Run
public static class AddWatercolorFeature
{
    public static string Run()
    {
        var log = new System.Text.StringBuilder();
        foreach (var (path, quality) in new[]
        {
            ("Assets/Settings/PC_Renderer.asset", WatercolorQualityPreset.High),
            ("Assets/Settings/Mobile_Renderer.asset", WatercolorQualityPreset.Medium),
        })
        {
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
            if (data == null)
            {
                log.AppendLine($"{path}: not found");
                continue;
            }

            var kuwahara = Find<WatercolorKuwaharaFeature>(data);
            if (kuwahara == null)
            {
                kuwahara = Add<WatercolorKuwaharaFeature>(data, "WatercolorKuwahara", "Hidden/ThatGameJam/WatercolorKuwahara");
                kuwahara.settings.quality = quality;
            }
            kuwahara.SetActive(false);

            if (Find<WatercolorBlurFeature>(data) == null)
                Add<WatercolorBlurFeature>(data, "WatercolorBlur", "Hidden/ThatGameJam/WatercolorBlur");

            data.SetDirty();
            EditorUtility.SetDirty(kuwahara);
            EditorUtility.SetDirty(data);
            log.AppendLine($"{path}: blur active, kuwahara inactive ({kuwahara.settings.quality})");
        }
        AssetDatabase.SaveAssets();
        return log.ToString();
    }

    static T Find<T>(UniversalRendererData data) where T : ScriptableRendererFeature
    {
        foreach (var feature in data.rendererFeatures)
            if (feature is T match)
                return match;
        return null;
    }

    static T Add<T>(UniversalRendererData data, string name, string shaderName) where T : ScriptableRendererFeature
    {
        var feature = ScriptableObject.CreateInstance<T>();
        feature.name = name;
        AssetDatabase.AddObjectToAsset(feature, data);

        var featureSo = new SerializedObject(feature);
        featureSo.FindProperty("shader").objectReferenceValue = Shader.Find(shaderName);
        featureSo.ApplyModifiedPropertiesWithoutUndo();

        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);

        var so = new SerializedObject(data);
        var features = so.FindProperty("m_RendererFeatures");
        var map = so.FindProperty("m_RendererFeatureMap");
        features.arraySize++;
        features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
        map.arraySize++;
        map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
        so.ApplyModifiedPropertiesWithoutUndo();
        return feature;
    }
}
