using UnityEngine;
using UnityEngine.InputSystem;

// Test ring for the spring spreading out (plan 4.15): a ring lying on the ground, growing from this object's position,
// with soft glowing orbs sitting along it and a soft glow rising from it.
// Needs a MeshRenderer whose material uses the ThatGameJam/SpringRing shader; the ring's size and look are on the material
// (drag Progress to see it, in Edit Mode too). Whenever its radius or the material's values change, this rebuilds the ring
// at the material's radius and drops each point (and each orb's centre) straight down onto the first collider below it,
// like Blender's Shrinkwrap along the vertical axis. Orbs are camera-facing quads; the shader spreads their corners, so
// they sit on the ground by their size. Where a ray finds nothing the ring is hidden. Only colliders on Ground Layers
// count; triggers never do. In Play Mode the ground below is taken to stay put while a ring plays: each spot is cast once.
// Debug: in Play Mode the 0 key (main row or keypad) plays Progress 0 -> 1 over the material's Duration, in the Editor and
// development builds only. It overrides Progress on this renderer only while playing; the material is not changed.
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class SpringRing : MonoBehaviour
{
    const int MinSegments = 32;
    const int MaxSegments = 1024;
    // Most the ground may rise or fall across one grid cell for a point to take its height blended from the grid.
    const float MaxBlendStep = 0.1f;

    [Tooltip("Colliders the ring lies on. The first one a downward ray hits wins.")]
    [SerializeField] LayerMask groundLayers = ~0;
    [Tooltip("Rays start this far above this object and reach this far below it, in world units.")]
    [SerializeField, Min(0.1f)] float rayReach = 50f;
    [Tooltip("Length of one piece of the ring along its edge at its largest, and the spacing of the ground heights it follows, in world units. Smaller follows the ground more closely.")]
    [SerializeField, Min(0.05f)] float segmentLength = 0.3f;
    [Tooltip("Points across the ring's width that are dropped onto the ground.")]
    [SerializeField, Range(2, 8)] int rowsAcross = 4;
    [Tooltip("Progress over the play time (both 0 to 1). The default bursts out, then settles into a slow, calm spread.")]
    [SerializeField] AnimationCurve playCurve = new(new Keyframe(0f, 0f, 0f, 7f), new Keyframe(0.18f, 0.68f, 1.1f, 1.1f), new Keyframe(1f, 1f, 0.1f, 0f));
    [Tooltip("This ring plays over the material's Duration times this: above 1 it opens more slowly than the other rings sharing the material (the ending's, for its weight).")]
    [SerializeField, Min(0.1f)] float durationScale = 1f;

    // While a ring plays, its centre (xyz) and radius (w), for what follows it, like the snow clearing. All zero otherwise.
    static readonly int SpringRingId = Shader.PropertyToID("_SpringRing");
    static readonly int ProgressId = Shader.PropertyToID("_Progress");
    static readonly int DurationId = Shader.PropertyToID("_Duration");
    static readonly int MaxRadiusId = Shader.PropertyToID("_MaxRadius");
    static readonly int WidthId = Shader.PropertyToID("_Width");
    static readonly int LiftId = Shader.PropertyToID("_Lift");
    static readonly int OrbCountId = Shader.PropertyToID("_OrbCount");
    static readonly int OrbSizeId = Shader.PropertyToID("_OrbSize");
    static readonly int OrbSizeJitterId = Shader.PropertyToID("_OrbSizeJitter");
    static readonly int RiseHeightId = Shader.PropertyToID("_RiseHeight");

    Mesh mesh;
    MeshFilter meshFilter;
    MeshRenderer meshRenderer;
    MaterialPropertyBlock block;
    float elapsed;
    bool playing;

    // Fixed for the ring's largest size, so the buffers and triangles are made once and only refilled as it grows.
    int segments;
    int rows;
    int orbCount;
    Vector3[] vertices;
    Vector2[] uvs;
    Vector4[] uv2s;
    Color32[] colors;
    int[] triangles;
    float builtJitter = float.NaN;
    bool trianglesSet;
    bool layoutDirty = true;
    // What the mesh was last built from; an unchanged frame keeps it.
    Vector4 builtShape;
    Vector2 builtLook;
    Matrix4x4 builtMatrix;

    // Ground heights on a square grid around the centre, one cell per segment length, raycast the first time a ring point
    // falls next to them. A point takes its height blended between the four grid points around it where the ground there
    // is whole and nearly flat; elsewhere it casts its own ray, as if there were no grid.
    float cellSize;
    int gridHalf;
    int gridSide;
    Vector3 gridCentre;
    float[] groundHeights;
    // 0: not cast yet, 1: hit, 2: missed.
    byte[] groundState;

    void Reset()
    {
        int ground = LayerMask.GetMask("Ground");
        groundLayers = ground != 0 ? ground : ~0;
    }

    void OnValidate() => layoutDirty = true;

    void OnDisable()
    {
        Stop();
        if (mesh != null)
            DestroyImmediate(mesh);
    }

    public bool IsPlaying => playing;

    public void Play()
    {
        elapsed = 0f;
        playing = true;
        ClearGround();
    }

    void Stop()
    {
        if (playing)
            Shader.SetGlobalVector(SpringRingId, Vector4.zero);
        playing = false;
        if (meshRenderer != null)
            meshRenderer.SetPropertyBlock(null);
    }

    void Update()
    {
        if (meshRenderer == null)
        {
            meshRenderer = GetComponent<MeshRenderer>();
            meshFilter = GetComponent<MeshFilter>();
        }
        Material material = meshRenderer.sharedMaterial;
        if (material == null || !material.HasProperty(ProgressId))
            return;

        float progress = material.GetFloat(ProgressId);
        float maxRadius = material.GetFloat(MaxRadiusId);
        if (Application.isPlaying)
        {
            Keyboard keyboard = Keyboard.current;
            if (Debug.isDebugBuild && keyboard != null && (keyboard.digit0Key.wasPressedThisFrame || keyboard.numpad0Key.wasPressedThisFrame))
                Play();

            if (playing)
            {
                elapsed += Time.deltaTime;
                float played = Mathf.Min(elapsed / Mathf.Max(material.GetFloat(DurationId) * durationScale, 0.01f), 1f);
                progress = playCurve.Evaluate(played);
                block ??= new MaterialPropertyBlock();
                block.SetFloat(ProgressId, progress);
                meshRenderer.SetPropertyBlock(block);
                Vector3 centre = transform.position;
                Shader.SetGlobalVector(SpringRingId, new Vector4(centre.x, centre.y, centre.z, progress * maxRadius));
                if (played >= 1f)
                {
                    playing = false;
                    Shader.SetGlobalVector(SpringRingId, Vector4.zero);
                }
            }
        }

        float width = material.GetFloat(WidthId);
        int orbs = Mathf.Max(0, Mathf.RoundToInt(material.GetFloat(OrbCountId)));
        int newSegments = Mathf.Clamp(Mathf.CeilToInt(Mathf.PI * 2f * (maxRadius + width * 0.5f) / segmentLength), MinSegments, MaxSegments);
        // One cell of margin, so the four grid points around any ring point are inside the grid.
        int newGridHalf = Mathf.CeilToInt((maxRadius + width * 0.5f) / segmentLength) + 1;
        if (layoutDirty || newSegments != segments || rowsAcross != rows || orbs != orbCount || newGridHalf != gridHalf)
            Layout(newSegments, orbs, newGridHalf);

        if (mesh == null)
        {
            mesh = new Mesh { name = "SpringRing", hideFlags = HideFlags.DontSave };
            mesh.MarkDynamic();
            meshFilter.sharedMesh = mesh;
            trianglesSet = false;
        }

        var shape = new Vector4(progress * maxRadius, width, material.GetFloat(LiftId), material.GetFloat(OrbSizeId));
        var look = new Vector2(material.GetFloat(OrbSizeJitterId), material.GetFloat(RiseHeightId));
        Matrix4x4 matrix = transform.localToWorldMatrix;
        // Out of Play Mode the colliders below may have been moved, so cast again on every rebuild.
        if (!Application.isPlaying || transform.position != gridCentre)
            ClearGround();
        else if (trianglesSet && shape == builtShape && look == builtLook && matrix == builtMatrix)
            return;
        builtShape = shape;
        builtLook = look;
        builtMatrix = matrix;
        BuildRing(shape.x, shape.y, shape.z, shape.w, look.x, look.y);
    }

    void ClearGround()
    {
        gridCentre = transform.position;
        if (groundState != null)
            System.Array.Clear(groundState, 0, groundState.Length);
    }

    // A flat band around the centre (uv.x goes once around, uv.y across, 0 inside), a rise on its middle line, then the
    // orbs: one quad each, all four corners at the orb's centre; uv2 = (corner x, corner y, 1, size scale).
    // Everything here but the orbs' size scale stays the same as the ring grows; BuildRing only moves the points.
    void Layout(int newSegments, int orbs, int newGridHalf)
    {
        layoutDirty = false;
        segments = newSegments;
        rows = rowsAcross;
        orbCount = orbs;
        int bandVertices = (segments + 1) * rows;
        int riseStart = bandVertices;
        int orbStart = riseStart + (segments + 1) * 2;
        vertices = new Vector3[orbStart + orbCount * 4];
        uvs = new Vector2[vertices.Length];
        uv2s = new Vector4[vertices.Length];
        colors = new Color32[vertices.Length];
        triangles = new int[segments * (rows - 1) * 6 + segments * 6 + orbCount * 6];
        builtJitter = float.NaN;
        trianglesSet = false;

        cellSize = segmentLength;
        gridHalf = newGridHalf;
        gridSide = gridHalf * 2 + 1;
        groundHeights = new float[gridSide * gridSide];
        groundState = new byte[gridSide * gridSide];
        gridCentre = transform.position;

        int k = 0;
        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            for (int r = 0; r < rows; r++)
                uvs[i * rows + r] = new Vector2(t, r / (float)(rows - 1));
            for (int y = 0; y < 2; y++)
            {
                uvs[riseStart + i * 2 + y] = new Vector2(t, y);
                uv2s[riseStart + i * 2 + y] = new Vector4(0f, 0f, 2f, 0f);
            }
        }
        for (int i = 0; i < segments; i++)
            for (int r = 0; r < rows - 1; r++)
            {
                int v = i * rows + r;
                triangles[k++] = v;
                triangles[k++] = v + 1;
                triangles[k++] = v + rows;
                triangles[k++] = v + 1;
                triangles[k++] = v + rows + 1;
                triangles[k++] = v + rows;
            }
        for (int i = 0; i < segments; i++)
        {
            int v = riseStart + i * 2;
            triangles[k++] = v;
            triangles[k++] = v + 1;
            triangles[k++] = v + 2;
            triangles[k++] = v + 1;
            triangles[k++] = v + 3;
            triangles[k++] = v + 2;
        }
        for (int o = 0; o < orbCount; o++)
        {
            int v = orbStart + o * 4;
            triangles[k++] = v;
            triangles[k++] = v + 2;
            triangles[k++] = v + 1;
            triangles[k++] = v;
            triangles[k++] = v + 3;
            triangles[k++] = v + 2;
        }
    }

    // Every point dropped onto the ground.
    void BuildRing(float radius, float width, float lift, float orbSize, float orbSizeJitter, float riseHeight)
    {
        int riseStart = (segments + 1) * rows;
        int orbStart = riseStart + (segments + 1) * 2;
        Vector3 center = transform.position;
        for (int i = 0; i <= segments; i++)
        {
            float angle = i / (float)segments * Mathf.PI * 2f;
            var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            for (int r = 0; r < rows; r++)
            {
                float across = r / (float)(rows - 1);
                // The inside edge stops at the centre while the ring is still small.
                Vector3 point = center + direction * Mathf.Max(0f, radius + (across - 0.5f) * width);
                bool hit = GroundHeight(point, out float ground);
                if (hit)
                    point.y = ground + lift;
                int v = i * rows + r;
                vertices[v] = transform.InverseTransformPoint(point);
                colors[v] = new Color32(255, 255, 255, hit ? (byte)255 : (byte)0);
            }

            // Rise: two rows on the ring's middle line, both on the ground; the shader lifts the top row.
            Vector3 middle = center + direction * radius;
            bool riseHit = GroundHeight(middle, out float riseGround);
            middle.y = riseHit ? riseGround : center.y + rayReach;
            Vector3 local = transform.InverseTransformPoint(middle);
            for (int y = 0; y < 2; y++)
            {
                int v = riseStart + i * 2 + y;
                vertices[v] = local;
                colors[v] = new Color32(255, 255, 255, riseHit ? (byte)255 : (byte)0);
            }
        }

        bool sizesChanged = orbSizeJitter != builtJitter;
        builtJitter = orbSizeJitter;
        for (int o = 0; o < orbCount; o++)
        {
            // Fixed places around the ring, so orbs spread apart as it grows instead of popping in and out.
            float angle = (o + 0.5f) / orbCount * Mathf.PI * 2f;
            var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            float scale = 1f + (Hash(o) * 2f - 1f) * orbSizeJitter;
            Vector3 centre = center + direction * radius;
            bool hit = GroundHeight(centre, out float ground);
            // The orb's centre is a little lower than its radius above the ground, so it sinks in slightly.
            centre.y = hit ? ground + orbSize * 0.5f * scale * 0.6f : center.y + rayReach;
            Vector3 local = transform.InverseTransformPoint(centre);
            int v = orbStart + o * 4;
            for (int c = 0; c < 4; c++)
            {
                vertices[v + c] = local;
                if (sizesChanged)
                    uv2s[v + c] = new Vector4(c == 0 || c == 3 ? -1f : 1f, c < 2 ? -1f : 1f, 1f, scale);
                colors[v + c] = new Color32(255, 255, 255, hit ? (byte)255 : (byte)0);
            }
        }

        if (!trianglesSet)
            mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetColors(colors);
        if (!trianglesSet)
        {
            mesh.indexFormat = vertices.Length > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0, false);
            trianglesSet = true;
            sizesChanged = true;
        }
        if (sizesChanged)
            mesh.SetUVs(1, uv2s);
        // Orb corners and the rise's top row are moved by the shader, so leave room for them (the rise sways up to 2x).
        Bounds bounds = mesh.bounds;
        bounds.Expand(Mathf.Max(orbSize * (1f + orbSizeJitter), riseHeight * 2f) * 2f);
        mesh.bounds = bounds;
    }

    // The ground's height below a point; false where it has none.
    bool GroundHeight(Vector3 point, out float height)
    {
        float fx = (point.x - gridCentre.x) / cellSize + gridHalf;
        float fz = (point.z - gridCentre.z) / cellSize + gridHalf;
        int x = Mathf.FloorToInt(fx);
        int z = Mathf.FloorToInt(fz);
        // Past the grid (a play curve that overshoots 1), near the ground's edge or where it steps (logs, ledges), blending
        // would hide or float the point, so it is cast straight down from the point itself.
        if (x < 0 || z < 0 || x + 1 >= gridSide || z + 1 >= gridSide
            || !GridHeight(x, z, out float h00) || !GridHeight(x + 1, z, out float h10)
            || !GridHeight(x, z + 1, out float h01) || !GridHeight(x + 1, z + 1, out float h11)
            || Mathf.Max(Mathf.Max(h00, h10), Mathf.Max(h01, h11)) - Mathf.Min(Mathf.Min(h00, h10), Mathf.Min(h01, h11)) > MaxBlendStep)
            return Cast(point.x, point.z, out height);
        float tx = fx - x;
        height = Mathf.Lerp(Mathf.Lerp(h00, h10, tx), Mathf.Lerp(h01, h11, tx), fz - z);
        return true;
    }

    bool GridHeight(int x, int z, out float height)
    {
        int index = z * gridSide + x;
        if (groundState[index] == 0)
        {
            bool hit = Cast(gridCentre.x + (x - gridHalf) * cellSize, gridCentre.z + (z - gridHalf) * cellSize, out groundHeights[index]);
            groundState[index] = hit ? (byte)1 : (byte)2;
        }
        height = groundHeights[index];
        return groundState[index] == 1;
    }

    // Rays start rayReach above this object and reach as far below it.
    bool Cast(float x, float z, out float height)
    {
        bool hit = Physics.Raycast(new Vector3(x, transform.position.y + rayReach, z), Vector3.down, out RaycastHit ground,
            rayReach * 2f, groundLayers, QueryTriggerInteraction.Ignore);
        height = ground.point.y;
        return hit;
    }

    // Stable 0..1 value per orb, so each keeps its size.
    static float Hash(int i)
    {
        float x = Mathf.Sin(i * 127.1f + 311.7f) * 43758.5453f;
        return x - Mathf.Floor(x);
    }
}
