using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// The cliff under the end of the terrain, where it drops away over the clouds (the terrain alone just stops there, a thin sheet).
// - The point the ending plays out on (round Point X) stands out: on either side of it the edge is cut back in a curve (terrain
//   holes, the terrain's material clipping them), and stays cut back all the way to both ends of the terrain.
// - The edge rounded over all along: the terrain's last metres curving down (its heights lowered, never raised), and on from
//   there a snow shoulder (the terrain's own material, smooth) bending on down to upright; under that a rough, faceted rock wall,
//   jutting out unevenly and stepping out at ledges, never in under the shoulder.
//   One mesh, saved as an asset, on one object beside the terrain.
// Rerunning makes the mesh again; the terrain is only ever cut and lowered further, so the same outline leaves it as it is.
// unity command script run --file AgentScripts/BuildCliffFace.cs --entry BuildCliffFace.Run
public static class BuildCliffFace
{
    const string MeshPath = "Assets/01_Arts/Models/Cliff/CliffFace.asset";
    const string RockPath = "Assets/01_Arts/Materials/Watercolor/WC_Rock.mat";
    // The point: its middle (world X), the half width it keeps the edge where it is, from there the metres over which the edge
    // curves back, how far back, and from where (half width) it comes out again, over so many metres, to the edge as it was
    // (past the terrain's ends: never, so only the point stands out).
    const float PointX = 0f;
    const float PointHalfWidth = 7f;
    const float CurveIn = 12f;
    const float CutBack = 6f;
    const float ComeBackFrom = 1000f;
    const float ComeBack = 12f;
    // The rounding: its radius, and how far down the terrain itself curves by its edge (no more than the terrain goes below the
    // ground there).
    const float Radius = 3f;
    const float TerrainDrop = 0.9f;
    const int ShoulderRows = 8;
    // How far in from the edge the shoulder starts, under the terrain (past the holes' steps).
    const float Tuck = 1f;
    // Metres down the wall goes under the shoulder, the sizes of its facets across and down, and how far it juts in and out.
    const float Depth = 70f;
    const float StepAcross = 1.4f;
    const float StepDown = 1.8f;
    const float Jut = 3f;
    const float LedgeEvery = 7f;

    static float straightEdge;

    // The edge's Z at this X.
    static float Edge(float x)
    {
        float a = Mathf.Abs(x - PointX);
        float back = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(PointHalfWidth, PointHalfWidth + CurveIn, a))
            * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ComeBackFrom, ComeBackFrom + ComeBack, a)));
        return straightEdge - CutBack * back;
    }

    // Out from the edge, flat (unit, X/Z).
    static Vector3 Outward(float x)
    {
        float slope = (Edge(x + 0.05f) - Edge(x - 0.05f)) / 0.1f;
        return new Vector3(-slope, 0f, 1f).normalized;
    }

    // Metres out past the edge (negative: in from it), along the way out there.
    static float Out(float x, float z) => (z - Edge(x)) * Outward(x).z;

    public static string Run()
    {
        Terrain terrain = GameObject.Find("02_WORLD/World/Terrain").GetComponent<Terrain>();
        TerrainData data = terrain.terrainData;
        Vector3 origin = terrain.transform.position;
        Vector3 size = data.size;
        straightEdge = origin.z + size.z;
        float left = origin.x, right = origin.x + size.x;
        float inset = Mathf.Sqrt(Radius * Radius - (Radius - TerrainDrop) * (Radius - TerrainDrop));
        Undo.RegisterCompleteObjectUndo(data, "Cliff Edge");

        // The holes: off past the edge (a little in from it, so no step of them sticks out over the shoulder). Where the edge is
        // the terrain's own straight end, none are needed.
        int holesRes = data.holesResolution;
        bool[,] solid = data.GetHoles(0, 0, holesRes, holesRes);
        int cut = 0;
        for (int row = 0; row < holesRes; row++)
            for (int column = 0; column < holesRes; column++)
            {
                float x = origin.x + (column + 0.5f) / holesRes * size.x;
                float z = origin.z + (row + 0.5f) / holesRes * size.z;
                if (solid[row, column] && Edge(x) < straightEdge - 0.05f && Out(x, z) > -0.35f)
                {
                    solid[row, column] = false;
                    cut++;
                }
            }
        if (cut > 0)
            data.SetHoles(0, 0, solid);

        // The heights: from the bend in from the edge, lowered along the circle, from the height just before the bend.
        int res = data.heightmapResolution;
        float[,] heights = data.GetHeights(0, 0, res, res);
        int lowered = 0;
        for (int column = 0; column < res; column++)
        {
            float x = origin.x + (float)column / (res - 1) * size.x;
            float facing = Outward(x).z;
            float referenceZ = Edge(x) - (inset + 0.6f) / facing;
            int referenceRow = Mathf.Clamp(Mathf.FloorToInt((referenceZ - origin.z) / size.z * (res - 1)), 0, res - 1);
            float reference = heights[referenceRow, column];
            for (int row = referenceRow + 1; row < res; row++)
            {
                float z = origin.z + (float)row / (res - 1) * size.z;
                float along = Mathf.Min(Out(x, z) + inset, Radius);
                if (along <= 0f)
                    continue;
                float fall = Radius - Mathf.Sqrt(Mathf.Max(0f, Radius * Radius - along * along));
                float target = Mathf.Max(0f, reference - fall / size.y);
                if (target < heights[row, column] - 2e-5f)
                {
                    heights[row, column] = target;
                    lowered++;
                }
            }
        }
        if (lowered > 0)
            data.SetHeights(0, 0, heights);
        EditorUtility.SetDirty(data);

        var depths = new List<float> { 0f };
        for (float d = 0f; d < Depth; )
        {
            d += StepDown * (1f + d / 25f);
            depths.Add(Mathf.Min(d, Depth));
        }

        // Columns along the edge, about evenly along its length.
        var columns = new List<float> { left };
        for (float x = left; x < right; )
        {
            x += StepAcross * Outward(x).z;
            columns.Add(Mathf.Min(x, right));
        }
        int across = columns.Count - 1;

        var shoulder = new Vector3[across + 1, ShoulderRows + 1];
        var wall = new Vector3[across + 1, depths.Count];
        var outwards = new Vector3[across + 1];
        float startAngle = Mathf.Asin(Mathf.Clamp01((inset - Tuck) / Radius));
        for (int i = 0; i <= across; i++)
        {
            float x0 = columns[i];
            Vector3 way = Outward(x0);
            outwards[i] = way;
            Vector3 bend = new Vector3(x0, 0f, Edge(x0)) - way * inset;
            float top = origin.y + terrain.SampleHeight(bend - way * 0.6f);
            for (int j = 0; j <= ShoulderRows; j++)
            {
                float angle = Mathf.Lerp(startAngle, Mathf.PI * 0.5f, (float)j / ShoulderRows);
                Vector3 at = bend + way * (Radius * Mathf.Sin(angle));
                at.y = top - Radius + Radius * Mathf.Cos(angle);
                // A hair under the terrain where it lies under it (never below the lowest the terrain could go, there).
                if (j <= 2)
                    at.y = Mathf.Max(at.y, origin.y) - 0.04f;
                shoulder[i, j] = at;
            }
            Vector3 foot = shoulder[i, ShoulderRows];
            Vector3 along = Vector3.Cross(Vector3.up, way);
            for (int j = 0; j < depths.Count; j++)
            {
                float d = depths[j];
                // Easing in from the shoulder's smooth foot, so there is no step or overhang under it.
                float share = Mathf.SmoothStep(0f, 1f, d / 10f);
                Vector3 at = foot;
                if (i > 0 && i < across && j > 0)
                    at += along * ((Mathf.PerlinNoise(i * 0.71f + 3.1f, j * 0.53f) - 0.5f) * StepAcross * 0.7f * share);
                at.y = foot.y - d;
                if (j > 0 && j < depths.Count - 1)
                    at.y += (Mathf.PerlinNoise(i * 0.37f, j * 0.91f + 7.7f) - 0.5f) * StepDown * 0.6f;
                float jut = (Mathf.PerlinNoise(at.x * 0.05f + 11f, at.y * 0.04f) - 0.5f) * 2f * Jut
                    + (Mathf.PerlinNoise(at.x * 0.23f, at.y * 0.21f + 21f) - 0.5f) * Jut * 0.7f;
                float ledges = d / (LedgeEvery * (1f + 0.4f * Mathf.PerlinNoise(at.x * 0.04f, 5f)));
                float stepped = Mathf.Floor(ledges) + Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((ledges - Mathf.Floor(ledges)) * 4f));
                // Out a little more at each ledge going down, as a cliff widens to its foot, and never in under the shoulder: a wall
                // going in under it leaves the shoulder hanging there, seen from the side or below, as a sheet in the air.
                wall[i, j] = at + way * (Mathf.Max(0f, jut * 0.6f + stepped * 0.5f) * share);
            }
        }

        Vector3 pivot = new(left, origin.y, straightEdge);
        var vertices = new List<Vector3>();
        var snow = new List<int>();
        var rock = new List<int>();
        void Add(List<int> into, int a, int b, int c, Vector3 outward)
        {
            if (Vector3.Dot(Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]), outward) < 0f)
                (b, c) = (c, b);
            into.Add(a);
            into.Add(b);
            into.Add(c);
        }
        // The shoulder smooth: its corners shared.
        int rows = ShoulderRows + 1;
        for (int i = 0; i <= across; i++)
            for (int j = 0; j < rows; j++)
                vertices.Add(shoulder[i, j] - pivot);
        for (int i = 0; i < across; i++)
            for (int j = 0; j < ShoulderRows; j++)
            {
                int a = i * rows + j, b = (i + 1) * rows + j;
                Vector3 outward = outwards[i] + Vector3.up;
                Add(snow, a, b, b + 1, outward);
                Add(snow, a, b + 1, a + 1, outward);
            }
        // The wall faceted: each triangle its own corners.
        void Facet(Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
        {
            int at = vertices.Count;
            vertices.Add(a - pivot);
            vertices.Add(b - pivot);
            vertices.Add(c - pivot);
            Add(rock, at, at + 1, at + 2, outward);
        }
        for (int i = 0; i < across; i++)
            for (int j = 0; j < depths.Count - 1; j++)
            {
                Vector3 a = wall[i, j], b = wall[i + 1, j], c = wall[i, j + 1], e = wall[i + 1, j + 1];
                Vector3 outward = outwards[i];
                if ((i + j) % 2 == 0)
                {
                    Facet(a, b, e, outward);
                    Facet(a, e, c, outward);
                }
                else
                {
                    Facet(a, b, c, outward);
                    Facet(b, e, c, outward);
                }
            }

        var mesh = new Mesh { name = "CliffFace", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32, subMeshCount = 2 };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(snow, 0);
        mesh.SetTriangles(rock, 1);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        System.IO.Directory.CreateDirectory("Assets/01_Arts/Models/Cliff");
        Mesh saved = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
        if (saved != null)
        {
            EditorUtility.CopySerialized(mesh, saved);
            saved.name = "CliffFace";
            EditorUtility.SetDirty(saved);
        }
        else
        {
            AssetDatabase.CreateAsset(mesh, MeshPath);
            saved = mesh;
        }

        // The terrain's material clips its holes.
        Material floor = terrain.materialTemplate;
        if (floor.HasProperty("_TerrainHoles") && floor.GetFloat("_TerrainHoles") < 0.5f)
        {
            Undo.RecordObject(floor, "Cliff Edge");
            floor.SetFloat("_TerrainHoles", 1f);
            EditorUtility.SetDirty(floor);
        }
        AssetDatabase.SaveAssets();

        Transform world = terrain.transform.parent;
        Transform face = world.Find("CliffFace");
        if (face == null)
        {
            var made = new GameObject("CliffFace", typeof(MeshFilter), typeof(MeshRenderer));
            Undo.RegisterCreatedObjectUndo(made, "Build Cliff Face");
            face = made.transform;
            face.SetParent(world, false);
        }
        face.gameObject.isStatic = true;
        face.SetPositionAndRotation(pivot, Quaternion.identity);
        face.localScale = Vector3.one;
        face.GetComponent<MeshFilter>().sharedMesh = saved;
        face.GetComponent<MeshRenderer>().sharedMaterials = new[] { floor, AssetDatabase.LoadAssetAtPath<Material>(RockPath) };
        EditorSceneManager.MarkSceneDirty(face.gameObject.scene);
        return $"CliffFace: holes cut {cut}, heights lowered {lowered}, {rock.Count / 3} rock facets; edge at the point z {Edge(PointX):F2}, cut back to {Edge(PointX + 22f):F2}";
    }
}
