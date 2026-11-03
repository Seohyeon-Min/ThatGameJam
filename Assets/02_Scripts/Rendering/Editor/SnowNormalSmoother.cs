using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Makes snow made of many clumps read as one mass under SoftSnow: a copy of the mesh whose normals are blurred across everything within
// Merge Distance, so clumps that touch or nearly touch share one rounded surface and their own edges between them stop showing. Only the
// outline of the whole heap fades. The copy is saved under Assets/01_Arts/Baked/Snow and put on the renderer; the bones and weights stay.
// Tools > Watercolor > Smooth Snow Normals, on the selected renderers, or on the Baby's Snow_Clumps when none is selected.
// Run it again after the girl is exported again. The watercolor bake leaves SoftSnow renderers alone, so it keeps this mesh.
public static class SnowNormalSmoother
{
    const string Folder = "Assets/01_Arts/Baked/Snow";
    // Share of the whole snow mesh's size within which normals are blended: clumps closer than this merge.
    public const float MergeShare = 0.08f;

    [MenuItem("Tools/Watercolor/Smooth Snow Normals")]
    public static void SmoothSelectedOrBaby()
    {
        var renderers = new List<Renderer>();
        foreach (GameObject selected in Selection.gameObjects)
            if (selected.TryGetComponent(out Renderer renderer))
                renderers.Add(renderer);
        if (renderers.Count == 0)
            foreach (Renderer renderer in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (renderer.name == "Snow_Clumps")
                    renderers.Add(renderer);
        if (renderers.Count == 0)
            Debug.LogWarning("SnowNormalSmoother: select a snow renderer, or open a scene with the Baby's Snow_Clumps.");
        foreach (Renderer renderer in renderers)
            SmoothOn(renderer);
    }

    public static void SmoothOn(Renderer renderer)
    {
        Mesh source = renderer is SkinnedMeshRenderer skinned ? skinned.sharedMesh
            : renderer.TryGetComponent(out MeshFilter filter) ? filter.sharedMesh : null;
        if (source == null)
            return;
        // Always from the model's own mesh, so running it twice doesn't blur twice.
        string sourcePath = AssetDatabase.GetAssetPath(source);
        if (sourcePath.StartsWith(Folder))
        {
            Mesh original = FindOriginal(renderer);
            if (original != null)
                source = original;
        }

        Mesh smooth = Smoothed(source, MergeShare);
        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets/01_Arts/Baked", "Snow");
        string path = $"{Folder}/{source.name}_smooth.asset";
        Mesh saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (saved == null)
            AssetDatabase.CreateAsset(smooth, path);
        else
        {
            EditorUtility.CopySerialized(smooth, saved);
            Object.DestroyImmediate(smooth);
            smooth = saved;
            EditorUtility.SetDirty(saved);
        }
        AssetDatabase.SaveAssets();

        Undo.RecordObject(renderer, "Smooth Snow Normals");
        if (renderer is SkinnedMeshRenderer skinnedRenderer)
            skinnedRenderer.sharedMesh = smooth;
        else if (renderer.TryGetComponent(out MeshFilter meshFilter))
        {
            Undo.RecordObject(meshFilter, "Smooth Snow Normals");
            meshFilter.sharedMesh = smooth;
            PrefabUtility.RecordPrefabInstancePropertyModifications(meshFilter);
        }
        PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(renderer.gameObject.scene);
        Debug.Log($"SnowNormalSmoother: {renderer.name} has smoothed normals ({path}). Save the scene.", renderer);
    }

    // The mesh the model itself gives this renderer, found by the renderer's path in its prefab source.
    static Mesh FindOriginal(Renderer renderer)
    {
        Object source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(renderer);
        return source is SkinnedMeshRenderer skinned ? skinned.sharedMesh
            : source is MeshRenderer mesh && mesh.TryGetComponent(out MeshFilter filter) ? filter.sharedMesh : null;
    }

    // Each normal becomes the weighted sum of the normals within the radius, nearer counting more, found through a grid of cells.
    static Mesh Smoothed(Mesh source, float share)
    {
        Mesh mesh = Object.Instantiate(source);
        mesh.name = source.name + "_smooth";
        Vector3[] positions = source.vertices;
        Vector3[] normals = source.normals;
        float radius = Mathf.Max(source.bounds.size.magnitude * share, 1e-5f);
        float radiusSquared = radius * radius;

        var cells = new Dictionary<Vector3Int, List<int>>();
        Vector3Int Cell(Vector3 p) => new(Mathf.FloorToInt(p.x / radius), Mathf.FloorToInt(p.y / radius), Mathf.FloorToInt(p.z / radius));
        for (int i = 0; i < positions.Length; i++)
        {
            Vector3Int cell = Cell(positions[i]);
            if (!cells.TryGetValue(cell, out List<int> list))
                cells[cell] = list = new List<int>();
            list.Add(i);
        }

        var result = new Vector3[normals.Length];
        for (int i = 0; i < positions.Length; i++)
        {
            Vector3Int cell = Cell(positions[i]);
            Vector3 sum = Vector3.zero;
            for (int x = -1; x <= 1; x++)
                for (int y = -1; y <= 1; y++)
                    for (int z = -1; z <= 1; z++)
                        if (cells.TryGetValue(cell + new Vector3Int(x, y, z), out List<int> near))
                            foreach (int j in near)
                            {
                                float distanceSquared = (positions[j] - positions[i]).sqrMagnitude;
                                if (distanceSquared < radiusSquared)
                                {
                                    float weight = 1f - distanceSquared / radiusSquared;
                                    sum += normals[j] * (weight * weight);
                                }
                            }
            result[i] = sum.sqrMagnitude > 1e-12f ? sum.normalized : normals[i];
        }
        mesh.normals = result;
        return mesh;
    }
}
