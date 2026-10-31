using UnityEngine;

// One per scene: draws every creature's footprints with a single particle system, so more creatures cost no more draw calls.
// Prints land only on the snow (the active Terrain), lie along its slope, and fade out with the particle's lifetime.
// FootprintMaker decides when a foot lands.
[RequireComponent(typeof(ParticleSystem))]
public class FootprintSystem : MonoBehaviour
{
    [SerializeField] Material material;
    [Tooltip("Most prints on the ground at once. A new print is skipped while the ground is full.")]
    [SerializeField, Min(1)] int maxPrints = 256;
    [Tooltip("Seconds a print stays, including its fade.")]
    [SerializeField, Min(0.1f)] float lifetime = 25f;
    [Tooltip("Share of the lifetime when the print starts to fade.")]
    [SerializeField, Range(0f, 1f)] float fadeStart = 0.6f;
    [Tooltip("Random size change of each print (0.1 = up to 10%).")]
    [SerializeField, Range(0f, 0.5f)] float sizeJitter = 0.08f;
    [Tooltip("Random turn of each print, in degrees.")]
    [SerializeField, Range(0f, 45f)] float turnJitter = 6f;

    public static FootprintSystem Instance { get; private set; }

    new ParticleSystem particleSystem;

    void Awake()
    {
        Instance = this;
        particleSystem = GetComponent<ParticleSystem>();
        Configure();
        particleSystem.Play();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Configure()
    {
        var main = particleSystem.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = maxPrints;
        main.startSpeed = 0f;
        main.gravityModifier = 0f;
        main.startSize3D = true;
        main.startRotation3D = true;
        main.loop = true;
        main.playOnAwake = false;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

        var emission = particleSystem.emission;
        emission.enabled = false;
        var shape = particleSystem.shape;
        shape.enabled = false;

        var fade = particleSystem.colorOverLifetime;
        fade.enabled = true;
        var alpha = new Gradient();
        alpha.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, fadeStart), new GradientAlphaKey(0f, 1f) });
        fade.color = alpha;

        var printRenderer = GetComponent<ParticleSystemRenderer>();
        printRenderer.renderMode = ParticleSystemRenderMode.Mesh;
        printRenderer.mesh = FlatQuad();
        printRenderer.enableGPUInstancing = false;
        printRenderer.alignment = ParticleSystemRenderSpace.World;
        printRenderer.sharedMaterial = material;
        printRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        printRenderer.receiveShadows = false;
    }

    // A unit quad lying flat, its length along Z.
    static Mesh FlatQuad()
    {
        var mesh = new Mesh { name = "Footprint Quad" };
        mesh.vertices = new[] { new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, -0.5f) };
        mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
        mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        return mesh;
    }

    // Leaves a print under a foot, unless the foot is off the terrain or more than maxAboveSnow over it (on a log or a beam).
    public void Stamp(Vector3 position, Vector3 forward, Vector2 size, float maxAboveSnow)
    {
        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null)
            return;
        Vector3 local = position - terrain.transform.position;
        Vector3 terrainSize = terrain.terrainData.size;
        float u = local.x / terrainSize.x;
        float v = local.z / terrainSize.z;
        if (u < 0f || u > 1f || v < 0f || v > 1f)
            return;
        float ground = terrain.SampleHeight(position) + terrain.transform.position.y;
        if (position.y - ground > maxAboveSnow)
            return;

        Vector3 normal = terrain.terrainData.GetInterpolatedNormal(u, v);
        Vector3 along = Vector3.ProjectOnPlane(forward, normal);
        if (along.sqrMagnitude < 1e-5f)
            return;
        Quaternion rotation = Quaternion.AngleAxis(Random.Range(-turnJitter, turnJitter), normal) * Quaternion.LookRotation(along, normal);
        float scale = 1f + Random.Range(-sizeJitter, sizeJitter);

        var print = new ParticleSystem.EmitParams
        {
            position = new Vector3(position.x, ground + 0.01f, position.z),
            rotation3D = rotation.eulerAngles,
            startSize3D = new Vector3(size.x * scale, 1f, size.y * scale),
            startLifetime = lifetime,
            startColor = Color.white,
            applyShapeToPosition = false,
        };
        particleSystem.Emit(print, 1);
    }
}
