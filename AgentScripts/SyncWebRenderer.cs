using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Makes the Mobile renderer (the one the Web build uses, QualitySettings WebGL default = Mobile) carry the same renderer features
// and layer masks as the PC renderer, so the Web build draws the stage 3 fog, the above-fog layer and the baby prism.
// Features missing from Mobile are copied from PC with their settings, in PC order. Mobile keeps its own Forward path and no SSAO.
// Running it again changes nothing.
// Run: unity command script run --file AgentScripts/SyncWebRenderer.cs --entry SyncWebRenderer.Run
public static class SyncWebRenderer
{
    const string SourcePath = "Assets/Settings/PC_Renderer.asset";
    const string TargetPath = "Assets/Settings/Mobile_Renderer.asset";
    static readonly string[] Skip = { "ScreenSpaceAmbientOcclusion" }; // The watercolor shaders do not read AO.

    public static string Run()
    {
        var source = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(SourcePath);
        var target = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(TargetPath);
        var added = new System.Collections.Generic.List<string>();
        foreach (ScriptableRendererFeature feature in source.rendererFeatures)
        {
            if (feature == null || System.Array.IndexOf(Skip, feature.name) >= 0)
                continue;
            if (target.rendererFeatures.Exists(f => f != null && f.name == feature.name))
                continue;
            var copy = Object.Instantiate(feature);
            copy.name = feature.name;
            AddFeature(target, copy);
            added.Add(copy.name);
        }

        var so = new SerializedObject(target);
        so.FindProperty("m_OpaqueLayerMask").intValue = source.opaqueLayerMask.value;
        so.FindProperty("m_TransparentLayerMask").intValue = source.transparentLayerMask.value;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
        AssetDatabase.SaveAssets();
        return $"added [{string.Join(", ", added)}], opaque={target.opaqueLayerMask.value}, transparent={target.transparentLayerMask.value}";
    }

    // What the renderer's Inspector does when you press "Add Renderer Feature".
    static void AddFeature(UniversalRendererData data, ScriptableRendererFeature feature)
    {
        AssetDatabase.AddObjectToAsset(feature, data);
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);
        var so = new SerializedObject(data);
        SerializedProperty features = so.FindProperty("m_RendererFeatures");
        SerializedProperty map = so.FindProperty("m_RendererFeatureMap");
        features.arraySize++;
        features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
        map.arraySize++;
        map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(data);
    }
}
