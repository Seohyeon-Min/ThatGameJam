using UnityEngine;

// Replaces the water's flat built-in Plane mesh with a gentle S-curve ribbon, built in the Editor (ExecuteAlways) so it can be
// tuned by eye without entering Play Mode. The ribbon's half width plus its swing (Amplitude) are kept within 5 local units
// each way - the flat Plane's own half-extent - so the water never spills past where the terrain's straight trench was cut; it
// only wanders inside the footprint that used to be a straight rectangle. Each triangle is emitted in both winding orders so
// the ribbon renders correctly regardless of which way Unity's convention happens to cull, since this can't be previewed here.
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter))]
public class WindingWaterMesh : MonoBehaviour
{
    [SerializeField, Min(2)] int segments = 40;
    [Tooltip("Half the ribbon's width, in local units. Plus Amplitude, keep this at or under 5 (the flat Plane's own half-extent) so the water stays inside the terrain's trench.")]
    [SerializeField, Range(0.5f, 4.5f)] float halfWidth = 3.2f;
    [Tooltip("How far sideways (local Z) the centre line swings from straight.")]
    [SerializeField, Range(0f, 4f)] float amplitude = 1.3f;
    [Tooltip("Local-X length of one full S-curve.")]
    [SerializeField, Min(0.5f)] float wavelength = 4f;
    [Tooltip("Shifts the curve along its length, so side-by-side streams don't wiggle in lockstep.")]
    [SerializeField] float phase = 0f;

    Mesh mesh;

    void OnEnable() => Build();
    void OnValidate() => Build();

    void Build()
    {
        if (mesh == null)
            mesh = new Mesh { name = "WindingWater" };
        else
            mesh.Clear();

        int cols = segments + 1;
        var vertices = new Vector3[cols * 2];
        var uvs = new Vector2[cols * 2];
        var normals = new Vector3[cols * 2];
        for (int i = 0; i < cols; i++)
        {
            float t = (float)i / segments;
            float x = Mathf.Lerp(-5f, 5f, t);
            float centre = amplitude * Mathf.Sin(2f * Mathf.PI * x / wavelength + phase);
            vertices[i] = new Vector3(x, 0f, centre - halfWidth);
            vertices[cols + i] = new Vector3(x, 0f, centre + halfWidth);
            uvs[i] = new Vector2(t, 0f);
            uvs[cols + i] = new Vector2(t, 1f);
            normals[i] = Vector3.up;
            normals[cols + i] = Vector3.up;
        }

        var triangles = new int[segments * 12];
        for (int i = 0; i < segments; i++)
        {
            int v00 = i, v01 = i + 1, v10 = cols + i, v11 = cols + i + 1;
            int o = i * 12;
            // One winding, then its reverse, so the ribbon shows up whichever way the GPU culls.
            triangles[o + 0] = v00; triangles[o + 1] = v10; triangles[o + 2] = v01;
            triangles[o + 3] = v01; triangles[o + 4] = v10; triangles[o + 5] = v11;
            triangles[o + 6] = v00; triangles[o + 7] = v01; triangles[o + 8] = v10;
            triangles[o + 9] = v01; triangles[o + 10] = v11; triangles[o + 11] = v10;
        }

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.normals = normals;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();

        GetComponent<MeshFilter>().sharedMesh = mesh;
    }
}
