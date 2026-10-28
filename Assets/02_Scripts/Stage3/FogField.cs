using UnityEngine;

// The fog over one fog section, as a low-resolution density grid over an XZ rectangle centred on this transform (plan 4.12).
// The grid is the only source of truth: the fog layer renderers read it as a texture, and game logic asks DensityAt.
public class FogField : MonoBehaviour
{
    [SerializeField] Vector2 size = new(16f, 10f);
    [SerializeField] int resolution = 128;
    // Stacked transparent quads using the ThatGameJam/Fog shader; each is sized and placed by the builder.
    [SerializeField] Renderer[] layers;

    static readonly int MaskId = Shader.PropertyToID("_FogMask");
    static readonly int AreaId = Shader.PropertyToID("_FogArea");
    static readonly int DensityId = Shader.PropertyToID("_Density");

    byte[] cells;
    Texture2D mask;
    MaterialPropertyBlock block;
    bool dirty;
    float density = 1f;

    // Overall thickness multiplier, for the first waiting signal (the fog edge thins).
    public float Density
    {
        get => density;
        set
        {
            density = value;
            dirty = true;
        }
    }

    Vector2 Min => new Vector2(transform.position.x, transform.position.z) - size * 0.5f;

    void Awake()
    {
        cells = new byte[resolution * resolution];
        System.Array.Fill(cells, byte.MaxValue);
        mask = new Texture2D(resolution, resolution, TextureFormat.R8, false, true)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };
        block = new MaterialPropertyBlock();
        dirty = true;
    }

    void OnDestroy() => Destroy(mask);

    public bool Contains(Vector3 point, float margin = 0f)
    {
        Vector2 min = Min - Vector2.one * margin;
        Vector2 max = Min + size + Vector2.one * margin;
        return point.x >= min.x && point.x <= max.x && point.z >= min.y && point.z <= max.y;
    }

    // 0 = clear, 1 = full fog. Outside the area there is no fog.
    public float DensityAt(Vector3 point)
    {
        if (!Contains(point))
            return 0f;
        Vector2Int cell = CellAt(point);
        return cells[cell.y * resolution + cell.x] / 255f;
    }

    // Lowers the fog inside a soft-edged circle to at most (1 - strength).
    public void Clear(Vector3 center, float radius, float strength = 1f)
    {
        float cellSize = size.x / resolution;
        Vector2Int from = CellAt(center - new Vector3(radius, 0f, radius));
        Vector2Int to = CellAt(center + new Vector3(radius, 0f, radius));
        Vector2 min = Min;
        for (int y = from.y; y <= to.y; y++)
        {
            for (int x = from.x; x <= to.x; x++)
            {
                Vector2 cellCenter = min + new Vector2((x + 0.5f) * cellSize, (y + 0.5f) * size.y / resolution);
                float distance = Vector2.Distance(cellCenter, new Vector2(center.x, center.z));
                if (distance > radius)
                    continue;
                // Full strength in the inner 60 %, fading to nothing at the rim.
                float falloff = Mathf.Clamp01((radius - distance) / (radius * 0.4f));
                byte limit = (byte)Mathf.RoundToInt(255f * (1f - strength * falloff));
                int index = y * resolution + x;
                if (cells[index] > limit)
                {
                    cells[index] = limit;
                    dirty = true;
                }
            }
        }
    }

    Vector2Int CellAt(Vector3 point)
    {
        Vector2 local = (new Vector2(point.x, point.z) - Min) / size;
        return new Vector2Int(
            Mathf.Clamp(Mathf.FloorToInt(local.x * resolution), 0, resolution - 1),
            Mathf.Clamp(Mathf.FloorToInt(local.y * resolution), 0, resolution - 1));
    }

    void LateUpdate()
    {
        if (!dirty)
            return;
        dirty = false;
        mask.SetPixelData(cells, 0);
        mask.Apply(false);
        Vector2 min = Min;
        foreach (Renderer layer in layers)
        {
            layer.GetPropertyBlock(block);
            block.SetTexture(MaskId, mask);
            block.SetVector(AreaId, new Vector4(min.x, min.y, size.x, size.y));
            block.SetFloat(DensityId, density);
            layer.SetPropertyBlock(block);
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.8f, 0.8f, 1f);
        Gizmos.DrawWireCube(transform.position + Vector3.up * 0.7f, new Vector3(size.x, 1.4f, size.y));
    }
}
