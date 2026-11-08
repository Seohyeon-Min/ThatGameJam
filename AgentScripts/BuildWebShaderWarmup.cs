using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
public static class BuildWebShaderWarmup
{
    public static string Run()
    {
        var trace = AssetDatabase.LoadAssetAtPath<GraphicsStateCollection>("Assets/04_Data/WebGameplayTrace.graphicsstate");
        var variants = new List<GraphicsStateCollection.ShaderVariant>(); trace.GetVariants(variants);
        var collection = new ShaderVariantCollection();
        var report = new List<string>();
        foreach (var v in variants)
        {
            var keywords = v.keywords.Select(k=>k.name).ToArray();
            int accepted = 0;
            foreach (PassType pass in Enum.GetValues(typeof(PassType)))
            {
                ShaderVariantCollection.ShaderVariant converted;
                try { converted = new ShaderVariantCollection.ShaderVariant(v.shader, pass, keywords); }
                catch (ArgumentException) { continue; }
                collection.Add(converted); accepted++;
                report.Add($"{v.shader.name}\t{pass}\t{string.Join(",",keywords)}");
            }
            if (accepted == 0) throw new Exception("No valid SVC pass for " + v.shader.name);
        }
        File.WriteAllLines("Builds/web_shader_conversion.tsv", report);
        const string output = "Assets/04_Data/WebShaderWarmup.shadervariants";
        var existing = AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(output);
        if (existing) { EditorUtility.CopySerialized(collection, existing); UnityEngine.Object.DestroyImmediate(collection); }
        else { AssetDatabase.CreateAsset(collection, output); existing = collection; }
        EditorUtility.SetDirty(existing);
        var graphics = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
        var preloaded = graphics.FindProperty("m_PreloadedShaders");
        bool found = false;
        for (int i = 0; i < preloaded.arraySize; i++) found |= preloaded.GetArrayElementAtIndex(i).objectReferenceValue == existing;
        if (!found) { preloaded.InsertArrayElementAtIndex(preloaded.arraySize); preloaded.GetArrayElementAtIndex(preloaded.arraySize - 1).objectReferenceValue = existing; }
        graphics.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
        return $"captured={variants.Count} accepted={report.Count} unique={existing.variantCount} shaders={existing.shaderCount} preloaded={preloaded.arraySize}";
    }
}
