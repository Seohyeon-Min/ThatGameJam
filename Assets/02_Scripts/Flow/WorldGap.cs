#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Editor only: opens a gap across the whole world at one Z (or closes one, with a negative size), for Stage.AdjustSpacing.
// The terrain grows by the gap there: its ground past the join moves along +Z, and the ground at the join is drawn out across the gap,
// so the road and the banks run on through it. What stands on the terrain past the join moves with its ground: the terrain's siblings
// (the streams and their water) and what the tree, flower and grass generators made. Stages move themselves, and generators under a
// stage move with it. The gap itself is left without trees and flowers: Generate again to fill it. Undo puts everything back.
public static class WorldGap
{
    public static void Open(float joinZ, float size)
    {
        if (Mathf.Approximately(size, 0f))
            return;
        foreach (Terrain terrain in Terrain.activeTerrains)
        {
            Stretch(terrain, joinZ, size);
            MoveSiblings(terrain, joinZ, size);
        }
        foreach (TreeGenerator generator in Object.FindObjectsByType<TreeGenerator>(FindObjectsSortMode.None))
            if (generator.GetComponentInParent<Stage>() == null)
                generator.ShiftBeyond(joinZ, size);
        foreach (ScatterGenerator generator in Object.FindObjectsByType<ScatterGenerator>(FindObjectsSortMode.None))
            if (generator.GetComponentInParent<Stage>() == null)
                generator.ShiftBeyond(joinZ, size);
        Debug.Log($"WorldGap: {(size > 0f ? "opened" : "closed")} {Mathf.Abs(size):0.##} m at z {joinZ:0.##}. Generate the trees, flowers and grass again to fill it.");
    }

    // Where the ground at local z of the new terrain comes from on the old one.
    static float Source(float z, float join, float size) =>
        size >= 0f ? (z < join ? z : z < join + size ? join : z - size) : (z < join + size ? z : z - size);

    static void Stretch(Terrain terrain, float joinZ, float size)
    {
        TerrainData data = terrain.terrainData;
        float join = joinZ - terrain.transform.position.z;
        float oldLength = data.size.z;
        // Past the far end: nothing of this terrain moves. Before its near end: all of it does.
        if (join >= oldLength)
            return;
        if (join <= 0f)
        {
            Undo.RecordObject(terrain.transform, "Open Stage Gap");
            terrain.transform.position += Vector3.forward * size;
            return;
        }
        float newLength = oldLength + size;
        if (newLength < 1f)
        {
            Debug.LogWarning($"WorldGap: {terrain.name} would be shorter than a metre. Left as it is.", terrain);
            return;
        }

        Undo.RegisterCompleteObjectUndo(data, "Open Stage Gap");

        int resolution = data.heightmapResolution;
        float[,] oldHeights = data.GetHeights(0, 0, resolution, resolution);
        var heights = new float[resolution, resolution];
        for (int row = 0; row < resolution; row++)
        {
            Row(row, resolution, newLength, oldLength, join, size, out int from, out int next, out float blend);
            for (int x = 0; x < resolution; x++)
                heights[row, x] = Mathf.Lerp(oldHeights[from, x], oldHeights[next, x], blend);
        }

        int mapWidth = data.alphamapWidth, mapLength = data.alphamapHeight, layers = data.alphamapLayers;
        float[,,] oldMaps = layers > 0 ? data.GetAlphamaps(0, 0, mapWidth, mapLength) : null;
        float[,,] maps = layers > 0 ? new float[mapLength, mapWidth, layers] : null;
        for (int row = 0; row < mapLength && layers > 0; row++)
        {
            Row(row, mapLength, newLength, oldLength, join, size, out int from, out int next, out float blend);
            for (int x = 0; x < mapWidth; x++)
                for (int layer = 0; layer < layers; layer++)
                    maps[row, x, layer] = Mathf.Lerp(oldMaps[from, x, layer], oldMaps[next, x, layer], blend);
        }

        TreeInstance[] trees = data.treeInstances;
        for (int i = 0; i < trees.Length; i++)
        {
            float z = trees[i].position.z * oldLength;
            trees[i].position.z = Mathf.Clamp01((z < join ? z : Mathf.Max(join, z + size)) / newLength);
        }
        if (data.detailPrototypes.Length > 0)
            Debug.LogWarning($"WorldGap: {terrain.name} has detail layers (painted grass); they weren't moved.", terrain);

        Vector3 dimensions = data.size;
        dimensions.z = newLength;
        data.size = dimensions;
        data.SetHeights(0, 0, heights);
        if (layers > 0)
            data.SetAlphamaps(0, 0, maps);
        data.treeInstances = trees;
        EditorUtility.SetDirty(data);
    }

    // The two old rows a new row is drawn from, and how far between them.
    static void Row(int row, int count, float newLength, float oldLength, float join, float size, out int from, out int next, out float blend)
    {
        float z = Source(row / (float)(count - 1) * newLength, join, size);
        float at = Mathf.Clamp01(z / oldLength) * (count - 1);
        from = Mathf.FloorToInt(at);
        next = Mathf.Min(from + 1, count - 1);
        blend = at - from;
    }

    // The terrain's siblings past the join, but not generators: they move only what they made.
    static void MoveSiblings(Terrain terrain, float joinZ, float size)
    {
        Transform parent = terrain.transform.parent;
        if (parent == null)
            return;
        var moved = new List<string>();
        foreach (Transform sibling in parent)
        {
            if (sibling == terrain.transform || sibling.GetComponent<TreeGenerator>() != null || sibling.GetComponent<ScatterGenerator>() != null)
                continue;
            if (sibling.position.z < joinZ)
                continue;
            Undo.RecordObject(sibling, "Open Stage Gap");
            sibling.position += Vector3.forward * size;
            moved.Add(sibling.name);
        }
        if (moved.Count > 0)
            Debug.Log($"WorldGap: moved {string.Join(", ", moved)} by {size:0.##} m.");
    }

    // Moves each transform at or past the join along Z, as one undo step with the rest.
    public static void ShiftAll(IEnumerable<GameObject> objects, float joinZ, float size)
    {
        foreach (GameObject thing in objects)
        {
            if (thing == null || thing.transform.position.z < joinZ)
                continue;
            Undo.RecordObject(thing.transform, "Open Stage Gap");
            thing.transform.position += Vector3.forward * size;
        }
    }
}
#endif
