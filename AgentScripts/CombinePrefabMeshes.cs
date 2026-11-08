using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// Fewer renderers for the web build (ADR 0039): each prefab below is many child meshes with one material, all at the prefab's
// origin. This merges them into one mesh asset under Assets/01_Arts/Combined/, keeps the first child with that mesh and deletes
// the others. The shader reads only the object's origin (wind, ring bloom, sparkle), which stays the same; the pencil strokes'
// length follows the renderer's size, so on the small pieces they grow a little.
// Leaf cards are not merged: each card faces the camera around its own origin (WatercolorFlat LeafCardPositionWS).
// Rerunning does nothing to a prefab that already has one renderer. After rebuilding or re-baking a prefab, run this again.
// Run: unity command script run --file AgentScripts/CombinePrefabMeshes.cs --entry CombinePrefabMeshes.Run
public static class CombinePrefabMeshes
{
    static readonly string[] Prefabs =
    {
        "Assets/03_Prefabs/Flowers/Flower01.prefab",
        "Assets/03_Prefabs/Grass/Grass01.prefab",
    };
    const string Folder = "Assets/01_Arts/Combined";

    public static string Run()
    {
        var report = new StringBuilder();
        foreach (string path in Prefabs)
            report.AppendLine(Combine(path));
        AssetDatabase.SaveAssets();
        return report.ToString();
    }

    static string Combine(string path)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            if (renderers.Length < 2)
                return $"{path}: already one renderer";
            Material[] materials = renderers[0].sharedMaterials;
            if (materials.Length != 1 || renderers.Any(r => r.sharedMaterials.Length != 1 || r.sharedMaterial != materials[0]))
                return $"{path}: skipped, the pieces do not share one material";

            var pieces = new List<CombineInstance>();
            foreach (MeshRenderer renderer in renderers)
                pieces.Add(new CombineInstance
                {
                    mesh = renderer.GetComponent<MeshFilter>().sharedMesh,
                    transform = root.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix,
                });
            var mesh = new Mesh { name = root.name };
            mesh.CombineMeshes(pieces.ToArray(), true, true);

            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets/01_Arts", "Combined");
            string meshPath = $"{Folder}/{root.name}.asset";
            Mesh saved = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (saved != null)
            {
                EditorUtility.CopySerialized(mesh, saved);
                Object.DestroyImmediate(mesh);
            }
            else
            {
                AssetDatabase.CreateAsset(mesh, meshPath);
                saved = mesh;
            }

            // The kept piece takes the whole mesh, at the prefab's origin like the rest.
            MeshRenderer kept = renderers[0];
            kept.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            kept.transform.localScale = Vector3.one;
            kept.GetComponent<MeshFilter>().sharedMesh = saved;
            kept.name = root.name;
            foreach (MeshRenderer renderer in renderers.Skip(1))
                Object.DestroyImmediate(renderer.gameObject);

            PrefabUtility.SaveAsPrefabAsset(root, path);
            return $"{path}: {renderers.Length} pieces -> {meshPath} ({saved.vertexCount} vertices)";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
