using UnityEngine;
using UnityEngine.InputSystem;

// Test ring for the spring spreading out (plan 4.15): a ring lying on the ground, growing from this object's position,
// with soft glowing orbs sitting along it and a soft glow rising from it.
// Needs a MeshRenderer whose material uses the ThatGameJam/SpringRing shader; the ring's size and look are on the material
// (drag Progress to see it, in Edit Mode too). Every frame this builds the ring at the material's radius and drops each
// point (and each orb's centre) straight down onto the first collider below it, like Blender's Shrinkwrap along the
// vertical axis. Orbs are camera-facing quads; the shader spreads their corners, so they sit on the ground by their size.
// Where a ray finds nothing the ring is hidden. Only colliders on Ground Layers count; triggers never do.
// Debug: in Play Mode the 0 key (main row or keypad) plays Progress 0 -> 1 over the material's Duration, in the Editor and
// development builds only. It overrides Progress on this renderer only while playing; the material is not changed.
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class SpringRing : MonoBehaviour
{
    const int MinSegments = 32;
    const int MaxSegments = 1024;

    [Tooltip("Colliders the ring lies on. The first one a downward ray hits wins.")]
    [SerializeField] LayerMask groundLayers = ~0;
    [Tooltip("Rays start this far above this object and reach this far below it, in world units.")]
    [SerializeField, Min(0.1f)] float rayReach = 50f;
    [Tooltip("Length of one piece of the ring along its edge, in world units. Smaller follows the ground more closely.")]
    [SerializeField, Min(0.05f)] float segmentLength = 0.3f;
    [Tooltip("Points across the ring's width that are dropped onto the ground.")]
    [SerializeField, Range(2, 8)] int rowsAcross = 4;

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
    MeshRenderer meshRenderer;
    MaterialPropertyBlock block;
    float elapsed;
    bool playing;

    void Reset()
    {
        int ground = LayerMask.GetMask("Ground");
        groundLayers = ground != 0 ? ground : ~0;
    }

    void OnDisable()
    {
        Stop();
        if (mesh != null)
            DestroyImmediate(mesh);
    }

    public void Play()
    {
        elapsed = 0f;
        playing = true;
    }

    void Stop()
    {
        playing = false;
        if (meshRenderer != null)
            meshRenderer.SetPropertyBlock(null);
    }

    void Update()
    {
        if (meshRenderer == null)
            meshRenderer = GetComponent<MeshRenderer>();
        Material material = meshRenderer.sharedMaterial;
        if (material == null || !material.HasProperty(ProgressId))
            return;

        float progress = material.GetFloat(ProgressId);
        if (Application.isPlaying)
        {
            Keyboard keyboard = Keyboard.current;
            if (Debug.isDebugBuild && keyboard != null && (keyboard.digit0Key.wasPressedThisFrame || keyboard.numpad0Key.wasPressedThisFrame))
                Play();

            if (playing)
            {
                elapsed += Time.deltaTime;
                progress = Mathf.Min(elapsed / Mathf.Max(material.GetFloat(DurationId), 0.01f), 1f);
                block ??= new MaterialPropertyBlock();
                block.SetFloat(ProgressId, progress);
                meshRenderer.SetPropertyBlock(block);
                if (progress >= 1f)
                    playing = false;
            }
        }

        if (mesh == null)
        {
            mesh = new Mesh { name = "SpringRing", hideFlags = HideFlags.DontSave };
            mesh.MarkDynamic();
        }
        GetComponent<MeshFilter>().sharedMesh = mesh;
        BuildRing(progress * material.GetFloat(MaxRadiusId), material.GetFloat(WidthId), material.GetFloat(LiftId),
            Mathf.RoundToInt(material.GetFloat(OrbCountId)), material.GetFloat(OrbSizeId), material.GetFloat(OrbSizeJitterId),
            material.GetFloat(RiseHeightId));
    }

    // A flat band around the centre, every point dropped onto the ground (uv.x goes once around, uv.y across, 0 inside),
    // then the orbs: one quad each, all four corners at the orb's centre; uv2 = (corner x, corner y, 1, size scale).
    void BuildRing(float radius, float width, float lift, int orbCount, float orbSize, float orbSizeJitter, float riseHeight)
    {
        int segments = Mathf.Clamp(Mathf.CeilToInt(Mathf.PI * 2f * (radius + width * 0.5f) / segmentLength), MinSegments, MaxSegments);
        int rows = rowsAcross;
        int bandVertices = (segments + 1) * rows;
        int riseStart = bandVertices;
        int orbStart = riseStart + (segments + 1) * 2;
        orbCount = Mathf.Max(0, orbCount);
        var vertices = new Vector3[orbStart + orbCount * 4];
        var uvs = new Vector2[vertices.Length];
        var uv2s = new Vector4[vertices.Length];
        var colors = new Color32[vertices.Length];
        var triangles = new int[segments * (rows - 1) * 6 + segments * 6 + orbCount * 6];

        Vector3 center = transform.position;
        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            float angle = t * Mathf.PI * 2f;
            var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            for (int r = 0; r < rows; r++)
            {
                float across = r / (float)(rows - 1);
                // The inside edge stops at the centre while the ring is still small.
                float pointRadius = Mathf.Max(0f, radius + (across - 0.5f) * width);
                Vector3 top = center + direction * pointRadius + Vector3.up * rayReach;
                bool hit = Physics.Raycast(top, Vector3.down, out RaycastHit ground, rayReach * 2f, groundLayers, QueryTriggerInteraction.Ignore);
                Vector3 point = hit ? ground.point + Vector3.up * lift : center + direction * pointRadius;
                int v = i * rows + r;
                vertices[v] = transform.InverseTransformPoint(point);
                uvs[v] = new Vector2(t, across);
                colors[v] = new Color32(255, 255, 255, hit ? (byte)255 : (byte)0);
            }
        }
        int k = 0;
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

        // Rise: two rows on the ring's middle line, both on the ground; the shader lifts the top row.
        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            float angle = t * Mathf.PI * 2f;
            var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            Vector3 top = center + direction * radius + Vector3.up * rayReach;
            bool hit = Physics.Raycast(top, Vector3.down, out RaycastHit ground, rayReach * 2f, groundLayers, QueryTriggerInteraction.Ignore);
            Vector3 local = transform.InverseTransformPoint(hit ? ground.point : top);
            for (int y = 0; y < 2; y++)
            {
                int v = riseStart + i * 2 + y;
                vertices[v] = local;
                uvs[v] = new Vector2(t, y);
                uv2s[v] = new Vector4(0f, 0f, 2f, 0f);
                colors[v] = new Color32(255, 255, 255, hit ? (byte)255 : (byte)0);
            }
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
            // Fixed places around the ring, so orbs spread apart as it grows instead of popping in and out.
            float angle = (o + 0.5f) / orbCount * Mathf.PI * 2f;
            var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            float scale = 1f + (Hash(o) * 2f - 1f) * orbSizeJitter;
            Vector3 top = transform.position + direction * radius + Vector3.up * rayReach;
            bool hit = Physics.Raycast(top, Vector3.down, out RaycastHit ground, rayReach * 2f, groundLayers, QueryTriggerInteraction.Ignore);
            // The orb's centre is a little lower than its radius above the ground, so it sinks in slightly.
            Vector3 centre = hit ? ground.point + Vector3.up * (orbSize * 0.5f * scale * 0.6f) : top;
            Vector3 local = transform.InverseTransformPoint(centre);
            int v = orbStart + o * 4;
            for (int c = 0; c < 4; c++)
            {
                vertices[v + c] = local;
                uv2s[v + c] = new Vector4(c == 0 || c == 3 ? -1f : 1f, c < 2 ? -1f : 1f, 1f, scale);
                colors[v + c] = new Color32(255, 255, 255, hit ? (byte)255 : (byte)0);
            }
            triangles[k++] = v;
            triangles[k++] = v + 2;
            triangles[k++] = v + 1;
            triangles[k++] = v;
            triangles[k++] = v + 3;
            triangles[k++] = v + 2;
        }

        mesh.Clear();
        mesh.indexFormat = vertices.Length > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.SetUVs(1, uv2s);
        mesh.colors32 = colors;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        // Orb corners and the rise's top row are moved by the shader, so leave room for them (the rise sways up to 2x).
        Bounds bounds = mesh.bounds;
        bounds.Expand(Mathf.Max(orbSize * (1f + orbSizeJitter), riseHeight * 2f) * 2f);
        mesh.bounds = bounds;
    }

    // Stable 0..1 value per orb, so each keeps its size.
    static float Hash(int i)
    {
        float x = Mathf.Sin(i * 127.1f + 311.7f) * 43758.5453f;
        return x - Mathf.Floor(x);
    }
}
