using System.Collections.Generic;
using UnityEngine;

// Light marking the baby's destination, like sun through a gap in the clouds: a faint slanted shaft from far above (off
// screen) and, strongest of all, a lumpy pool of light painted only on the ground. Place this object where the shaft lands.
// Needs a MeshRenderer whose material uses the ThatGameJam/LightBeam shader; the look is on the material.
// One mesh holds both. Shaft: a ribbon along To Sky that the shader turns to face the camera. Pool: a grid around the foot,
// each point dropped straight down onto the Ground layer, so it follows the terrain and never lands on trees or characters.
// Changing a field or moving the object rebuilds the mesh, in Edit Mode too. Where no ground exists at all (e.g. Prefab Mode)
// the pool lies flat, to preview it.
// The pool nearest the camera is also told to the shaders (_WC_SunPool*), so WatercolorFlat can light the baby inside it.
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class LightBeam : MonoBehaviour
{
    [Header("Shaft")]
    [Tooltip("World direction from the foot up to the light. The default comes down from the upper right of the fixed camera's view.")]
    [SerializeField] Vector3 toSky = new Vector3(0.35f, 1f, 0.35f);
    [Tooltip("Length of the shaft from the ground up, in world units. Make it reach well past the top of the screen.")]
    [SerializeField, Min(0.1f)] float height = 60f;
    [Tooltip("Width of the shaft where it meets the ground, in world units.")]
    [SerializeField, Min(0.01f)] float footWidth = 2.5f;
    [Tooltip("Width of the shaft at its top, in world units. Wider than the foot spreads it out like light through a gap in the clouds.")]
    [SerializeField, Min(0.01f)] float topWidth = 4f;
    [Tooltip("Rows along the shaft. More rows make the taper and the edge wobble smoother.")]
    [SerializeField, Range(2, 128)] int rows = 32;

    [Header("Ground Pool")]
    [Tooltip("Reach of the pool of light from the foot, in world units. Its lumpy shape is on the material.")]
    [SerializeField, Min(0.1f)] float poolRadius = 2.2f;
    [Tooltip("Grid cells across the pool. More follows bumpy ground more closely.")]
    [SerializeField, Range(2, 64)] int poolCells = 24;
    [Tooltip("Height above the ground, in world units, to keep the pool from flickering into it.")]
    [SerializeField, Min(0f)] float poolLift = 0.03f;
    [Tooltip("Rays start this far above each grid point and reach this far below it, in world units.")]
    [SerializeField, Min(0.1f)] float rayReach = 20f;

    static readonly List<LightBeam> Active = new();
    static readonly int SunPoolId = Shader.PropertyToID("_WC_SunPool");
    static readonly int SunPoolSlantId = Shader.PropertyToID("_WC_SunPoolSlant");
    static readonly int SunPoolShapeId = Shader.PropertyToID("_WC_SunPoolShape");
    static readonly int SunPoolColorId = Shader.PropertyToID("_WC_SunPoolColor");

    Mesh mesh;

    void OnEnable()
    {
        Active.Add(this);
        Build();
    }

    void OnDisable()
    {
        Active.Remove(this);
        if (Active.Count == 0)
            Shader.SetGlobalVector(SunPoolId, Vector4.zero);
    }

    void OnValidate() => Build();

    void Update()
    {
        if (Application.isPlaying || !transform.hasChanged)
            return;
        transform.hasChanged = false;
        Build();
    }

    void LateUpdate()
    {
        LightBeam nearest = Nearest();
        if (nearest == this)
            PublishPool();
        else if (nearest == null)
            Shader.SetGlobalVector(SunPoolId, Vector4.zero);
    }

    // The nearest beam that's switched on; SunlitSpot switches beams by their renderers. Null when none is on.
    static LightBeam Nearest()
    {
        Camera view = Camera.main;
        Vector3 from = view != null ? view.transform.position : Vector3.zero;
        LightBeam nearest = null;
        float best = float.MaxValue;
        foreach (LightBeam beam in Active)
        {
            if (!beam.GetComponent<MeshRenderer>().enabled)
                continue;
            float distance = (beam.transform.position - from).sqrMagnitude;
            if (distance < best)
            {
                best = distance;
                nearest = beam;
            }
        }
        return nearest;
    }

    void PublishPool()
    {
        Material material = GetComponent<MeshRenderer>().sharedMaterial;
        if (material == null || !material.HasProperty("_PoolBlobSize"))
            return;
        Vector3 foot = transform.position;
        float rise = Mathf.Max(toSky.y, 0.01f);
        Shader.SetGlobalVector(SunPoolId, new Vector4(foot.x, foot.y, foot.z, poolRadius));
        Shader.SetGlobalVector(SunPoolSlantId, new Vector4(toSky.x / rise, toSky.z / rise, material.GetFloat("_PoolDrift"), 0f));
        Shader.SetGlobalVector(SunPoolShapeId, new Vector4(material.GetFloat("_PoolBlobSize"), material.GetFloat("_PoolRagged"), material.GetFloat("_PoolThreshold"), material.GetFloat("_PoolEdge")));
        Shader.SetGlobalColor(SunPoolColorId, material.GetColor("_PoolColor"));
    }

    void OnDestroy()
    {
        if (mesh == null)
            return;
        if (Application.isPlaying)
            Destroy(mesh);
        else
            DestroyImmediate(mesh);
    }

    void Build()
    {
        if (mesh == null)
        {
            mesh = new Mesh { name = "LightBeam" };
            mesh.hideFlags = HideFlags.DontSave;
        }

        int side = poolCells + 1;
        int ribbonVertices = rows * 2;
        var vertices = new Vector3[ribbonVertices + side * side];
        var normals = new Vector3[vertices.Length];
        var uvs = new Vector2[vertices.Length];
        var extras = new List<Vector4>(new Vector4[vertices.Length]);
        var triangles = new int[((rows - 1) + poolCells * poolCells) * 6];

        // Shaft: two vertices at each row's centre; the shader spreads them sideways by uv2.x, across the view.
        Vector3 axis = transform.InverseTransformDirection(toSky.sqrMagnitude > 1e-6f ? toSky.normalized : Vector3.up);
        var bounds = new Bounds(Vector3.zero, Vector3.zero);
        for (int r = 0; r < rows; r++)
        {
            float t = r / (float)(rows - 1);
            float halfWidth = Mathf.Lerp(footWidth, topWidth, t) * 0.5f;
            float along = t * height;
            Vector3 centre = axis * along;
            for (int s = 0; s < 2; s++)
            {
                int v = r * 2 + s;
                vertices[v] = centre;
                normals[v] = axis;
                uvs[v] = new Vector2(s, t);
                extras[v] = new Vector4(s == 0 ? -halfWidth : halfWidth, along, 0f, 0f);
            }
            // The shader turns the ribbon around its axis, so the bounds must cover every turn.
            bounds.Encapsulate(new Bounds(centre, Vector3.one * halfWidth * 2f));
        }
        for (int r = 0; r < rows - 1; r++)
        {
            int i = r * 2;
            int k = r * 6;
            triangles[k] = i;
            triangles[k + 1] = i + 2;
            triangles[k + 2] = i + 1;
            triangles[k + 3] = i + 1;
            triangles[k + 4] = i + 2;
            triangles[k + 5] = i + 3;
        }

        // Pool: world-aligned around the foot, then dropped onto the ground.
        int groundMask = LayerMask.GetMask("Ground");
        Vector3 foot = transform.position;
        int hits = 0;
        for (int z = 0; z < side; z++)
        {
            for (int x = 0; x < side; x++)
            {
                var across = new Vector2(x / (float)poolCells * 2f - 1f, z / (float)poolCells * 2f - 1f);
                Vector3 point = foot + new Vector3(across.x, 0f, across.y) * poolRadius;
                bool found = Physics.Raycast(point + Vector3.up * rayReach, Vector3.down, out RaycastHit hit, rayReach * 2f, groundMask, QueryTriggerInteraction.Ignore);
                if (found)
                {
                    point = hit.point + Vector3.up * poolLift;
                    hits++;
                }
                int v = ribbonVertices + z * side + x;
                vertices[v] = transform.InverseTransformPoint(point);
                normals[v] = Vector3.up;
                extras[v] = new Vector4(across.x, across.y, 1f, found ? 1f : 0f);
                bounds.Encapsulate(vertices[v]);
            }
        }
        if (hits == 0)
        {
            for (int v = ribbonVertices; v < vertices.Length; v++)
                extras[v] = new Vector4(extras[v].x, extras[v].y, 1f, 1f);
        }

        int next = (rows - 1) * 6;
        for (int z = 0; z < poolCells; z++)
        {
            for (int x = 0; x < poolCells; x++)
            {
                int i = ribbonVertices + z * side + x;
                triangles[next++] = i;
                triangles[next++] = i + side;
                triangles[next++] = i + 1;
                triangles[next++] = i + 1;
                triangles[next++] = i + side;
                triangles[next++] = i + side + 1;
            }
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.SetUVs(1, extras);
        mesh.triangles = triangles;
        mesh.bounds = bounds;
        GetComponent<MeshFilter>().sharedMesh = mesh;
    }
}
