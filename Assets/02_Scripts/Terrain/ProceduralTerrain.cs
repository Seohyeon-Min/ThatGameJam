using UnityEngine;

// Generates a gently rolling, snow-field-like heightmap on this object's Terrain using
// layered (fBm) Perlin noise. Right-click the component header and choose "Generate Terrain",
// or toggle autoRegenerate to preview changes live while tweaking fields in the Inspector.
[ExecuteAlways]
[RequireComponent(typeof(Terrain))]
public class ProceduralTerrain : MonoBehaviour
{
    [Header("Terrain Size")]
    [SerializeField] int resolution = 513; // heightmap resolution, forced to power-of-two + 1
    [SerializeField] Vector2 worldSize = new Vector2(120f, 120f);
    [SerializeField] float heightScale = 4f;
    [Tooltip("Terrain's own pivot sits at its corner, not its center. Set this to where you want the terrain centered (e.g. the character's spawn point) and the corner gets pushed back automatically.")]
    [SerializeField] Vector2 centerXZ;
    [Tooltip("World Y of the terrain's lowest point (its transform.position.y). Keep at the character's ground level.")]
    [SerializeField] float baseHeight = 0f;

    [Header("Look")]
    [Tooltip("Assign a plain white/snow material (e.g. Assets/01_Arts/Materials/Test/Snow.mat) to override the default Terrain diffuse look.")]
    [SerializeField] Material snowMaterial;

    [Header("Noise - Big Rolling Shape")]
    [Tooltip("World units (meters) per noise cycle - NOT heightmap pixels, so resizing worldSize keeps bumps the same physical size. Larger = broader, gentler hills. Smaller = tighter, choppier bumps.")]
    [SerializeField] float noiseScale = 20f;
    [SerializeField] int octaves = 3;
    [Range(0f, 1f)] [SerializeField] float persistence = 0.4f; // amplitude falloff per octave
    [SerializeField] float lacunarity = 2f; // frequency growth per octave
    [SerializeField] Vector2 offset;
    [SerializeField] int seed;

    [Header("Shape")]
    [Tooltip("Above 1, flattens most of the field and leaves only gentle rounded rises, like snow drifts.")]
    [SerializeField] float flattenPower = 1.6f;
    [SerializeField] AnimationCurve heightCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Header("Editing")]
    [SerializeField] bool autoRegenerate = true;

    void OnValidate()
    {
        resolution = Mathf.ClosestPowerOfTwo(resolution - 1) + 1;
        octaves = Mathf.Max(1, octaves);
        noiseScale = Mathf.Max(0.0001f, noiseScale);

        if (autoRegenerate)
            Generate();
    }

    [ContextMenu("Generate Terrain")]
    public void Generate()
    {
        Terrain terrainComp = GetComponent<Terrain>();
        TerrainData data = terrainComp.terrainData;
        if (data == null)
        {
            Debug.LogWarning("ProceduralTerrain: assign a TerrainData asset to this Terrain first.", this);
            return;
        }

        if (snowMaterial != null)
            terrainComp.materialTemplate = snowMaterial;

        data.heightmapResolution = resolution;
        data.size = new Vector3(worldSize.x, heightScale, worldSize.y);
        transform.position = new Vector3(centerXZ.x - worldSize.x / 2f, baseHeight, centerXZ.y - worldSize.y / 2f);

        int size = data.heightmapResolution;
        float[,] heights = new float[size, size];

        System.Random rng = new System.Random(seed);
        Vector2[] octaveOffsets = new Vector2[octaves];
        for (int i = 0; i < octaves; i++)
            octaveOffsets[i] = new Vector2(rng.Next(-100000, 100000) + offset.x, rng.Next(-100000, 100000) + offset.y);

        float maxPossibleHeight = 0f;
        float amp = 1f;
        for (int i = 0; i < octaves; i++)
        {
            maxPossibleHeight += amp;
            amp *= persistence;
        }

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // World-space position (meters) of this vertex, not its raw pixel index - keeps
                // noiseScale meaning "meters per cycle" regardless of worldSize or resolution.
                float worldX = x / (float)(size - 1) * worldSize.x;
                float worldZ = y / (float)(size - 1) * worldSize.y;

                float amplitude = 1f;
                float frequency = 1f;
                float noiseHeight = 0f;

                for (int i = 0; i < octaves; i++)
                {
                    float sampleX = (worldX + octaveOffsets[i].x) / noiseScale * frequency;
                    float sampleY = (worldZ + octaveOffsets[i].y) / noiseScale * frequency;
                    float perlin = Mathf.PerlinNoise(sampleX, sampleY) * 2f - 1f;

                    noiseHeight += perlin * amplitude;
                    amplitude *= persistence;
                    frequency *= lacunarity;
                }

                float normalized = Mathf.InverseLerp(-maxPossibleHeight, maxPossibleHeight, noiseHeight);
                normalized = Mathf.Pow(normalized, flattenPower);
                heights[y, x] = heightCurve.Evaluate(normalized);
            }
        }

        data.SetHeights(0, 0, heights);
    }
}
