using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Fills ShaderPrewarm (01_SYSTEM/ShaderPrewarm in Main) with one entry per material the scene can draw: every renderer's
// materials, on the renderer's own mesh, plus materials only scripts reference (put on at run time) whose shader a renderer
// also uses, on a quad. Run again after adding materials.
// Run: unity command script run --file AgentScripts/BuildShaderPrewarm.cs --entry BuildShaderPrewarm.Run
public static class BuildShaderPrewarm
{
    public static string Run()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != "Assets/00_Scenes/Main.unity")
            return "open Main first";
        GameObject system = scene.GetRootGameObjects().First(g => g.name == "01_SYSTEM");
        Transform holder = system.transform.Find("ShaderPrewarm");
        if (holder == null)
        {
            holder = new GameObject("ShaderPrewarm").transform;
            holder.SetParent(system.transform, false);
        }
        var prewarm = holder.GetComponent<ShaderPrewarm>() ?? holder.gameObject.AddComponent<ShaderPrewarm>();

        GameObject primitive = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Mesh quad = primitive.GetComponent<MeshFilter>().sharedMesh;
        Object.DestroyImmediate(primitive);
        var entries = new List<ShaderPrewarm.Entry>();
        var seen = new HashSet<Material>();
        foreach (Renderer renderer in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include))
        {
            Mesh mesh = renderer switch
            {
                MeshRenderer => renderer.GetComponent<MeshFilter>()?.sharedMesh,
                SkinnedMeshRenderer skinned => skinned.sharedMesh,
                _ => null,
            };
            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] == null || !seen.Add(materials[i]))
                    continue;
                // Meshes made at load (not saved with the scene) can't be referenced; the shader is the same on a quad.
                bool own = mesh != null && i < mesh.subMeshCount && (mesh.hideFlags & HideFlags.DontSave) == 0;
                entries.Add(new ShaderPrewarm.Entry
                {
                    mesh = own ? mesh : quad,
                    subMesh = own ? i : 0,
                    material = materials[i],
                    layer = renderer.gameObject.layer,
                    renderingLayerMask = renderer.renderingLayerMask,
                    shadowCasting = renderer.shadowCastingMode,
                    receiveShadows = renderer.receiveShadows,
                });
            }
        }
        var shaders = new HashSet<Shader>(seen.Select(m => m.shader));
        int fromScripts = 0;
        foreach (Material material in EditorUtility.CollectDependencies(scene.GetRootGameObjects()).OfType<Material>())
        {
            if (!shaders.Contains(material.shader) || !seen.Add(material))
                continue;
            fromScripts++;
            entries.Add(new ShaderPrewarm.Entry
            {
                mesh = quad,
                material = material,
                renderingLayerMask = 1,
                shadowCasting = UnityEngine.Rendering.ShadowCastingMode.On,
                receiveShadows = true,
            });
        }

        var serialized = new SerializedObject(prewarm);
        serialized.FindProperty("target").objectReferenceValue = Camera.main;
        SerializedProperty list = serialized.FindProperty("entries");
        list.arraySize = entries.Count;
        for (int i = 0; i < entries.Count; i++)
        {
            SerializedProperty e = list.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("mesh").objectReferenceValue = entries[i].mesh;
            e.FindPropertyRelative("subMesh").intValue = entries[i].subMesh;
            e.FindPropertyRelative("material").objectReferenceValue = entries[i].material;
            e.FindPropertyRelative("layer").intValue = entries[i].layer;
            e.FindPropertyRelative("renderingLayerMask").uintValue = entries[i].renderingLayerMask;
            e.FindPropertyRelative("shadowCasting").enumValueIndex = (int)entries[i].shadowCasting;
            e.FindPropertyRelative("receiveShadows").boolValue = entries[i].receiveShadows;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return $"entries={entries.Count} fromScripts={fromScripts} shaders={shaders.Count} camera={Camera.main?.name}";
    }
}
