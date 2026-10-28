using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Makes the bird show through whatever hides it (trees, leaves) as a flat silhouette, on both URP renderers.
// Adds the "Bird" layer and two Render Objects features: the first redraws the bird and marks its visible pixels in the stencil,
// the second draws the silhouette only where the bird is behind something and not marked. Running it again changes nothing.
// Run: unity command script run --file AgentScripts/SetupBirdSilhouette.cs --entry SetupBirdSilhouette.Run
public static class SetupBirdSilhouette
{
    const string LayerName = "Bird";
    const string MaterialPath = "Assets/01_Arts/Materials/BirdSilhouette.mat";
    static readonly string[] RendererPaths = { "Assets/Settings/PC_Renderer.asset", "Assets/Settings/Mobile_Renderer.asset" };
    static readonly Color SilhouetteColor = new(0.3f, 0.85f, 1f, 0.9f);

    public static string Run()
    {
        int layer = AddLayer();
        Material material = SilhouetteMaterial(); // Also recolours an existing one.
        foreach (string path in RendererPaths)
        {
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
            if (data.rendererFeatures.Exists(f => f != null && f.name == "BirdVisible"))
                continue;

            var visible = ScriptableObject.CreateInstance<RenderObjects>();
            visible.name = "BirdVisible";
            visible.settings.Event = RenderPassEvent.AfterRenderingOpaques;
            visible.settings.filterSettings.LayerMask = 1 << layer;
            visible.settings.overrideMode = RenderObjects.RenderObjectsSettings.OverrideMaterialMode.None;
            visible.settings.stencilSettings = new StencilStateData
            {
                overrideStencilState = true, stencilReference = 1, stencilCompareFunction = CompareFunction.Always,
                passOperation = StencilOp.Replace, failOperation = StencilOp.Keep, zFailOperation = StencilOp.Keep,
            };

            var hidden = ScriptableObject.CreateInstance<RenderObjects>();
            hidden.name = "BirdSilhouette";
            hidden.settings.Event = RenderPassEvent.AfterRenderingOpaques;
            hidden.settings.filterSettings.LayerMask = 1 << layer;
            hidden.settings.overrideMode = RenderObjects.RenderObjectsSettings.OverrideMaterialMode.Material;
            hidden.settings.overrideMaterial = material;
            hidden.settings.overrideDepthState = true;
            hidden.settings.depthCompareFunction = CompareFunction.Greater;
            hidden.settings.enableWrite = false;
            hidden.settings.stencilSettings = new StencilStateData
            {
                overrideStencilState = true, stencilReference = 1, stencilCompareFunction = CompareFunction.NotEqual,
                passOperation = StencilOp.Keep, failOperation = StencilOp.Keep, zFailOperation = StencilOp.Keep,
            };

            AddFeature(data, visible);
            AddFeature(data, hidden);
        }
        AssetDatabase.SaveAssets();
        return $"layer {layer}";
    }

    static int AddLayer()
    {
        int layer = LayerMask.NameToLayer(LayerName);
        if (layer >= 0)
            return layer;
        var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");
        for (int i = 8; i < layers.arraySize; i++)
            if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue))
            {
                layers.GetArrayElementAtIndex(i).stringValue = LayerName;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
                return i;
            }
        throw new System.InvalidOperationException("No free layer for Bird");
    }

    static Material SilhouetteMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        material.SetColor("_BaseColor", SilhouetteColor);
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(material);
        return material;
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
