using UnityEngine;

// Real, world-space falling snow: particles simulate in world space and physically collide
// with the Terrain and level colliders (not a camera-locked screen effect). The emission box
// recenters above followTarget's X/Z every frame so coverage follows the player/camera without
// the particles themselves being tied to the camera. Right-click the component header and
// choose "Apply Snowfall Settings", or toggle autoRegenerate to preview changes live.
[ExecuteAlways]
[RequireComponent(typeof(ParticleSystem))]
public class Snowfall : MonoBehaviour
{
    [Header("Coverage")]
    [SerializeField] Vector2 areaSize = new Vector2(12f, 30f);
    [Tooltip("Recenters the emission box above this target's X/Z every frame (e.g. the camera point or baby). Leave empty for a static area.")]
    [SerializeField] Transform followTarget;

    [Header("Snow")]
    [SerializeField] float spawnRate = 220f;
    [SerializeField] float fallSpeed = 1.5f;
    [SerializeField] float fallSpeedVariation = 0.4f;
    [Tooltip("Horizontal push (X, Z, units/second) - how much and which way the snow is blown as it falls.")]
    [SerializeField] Vector2 wind = Vector2.zero;
    [Tooltip("Optional extra side-to-side sway on top of the wind. 0 disables it.")]
    [SerializeField] float turbulence = 0f;
    [SerializeField] float flakeSize = 0.1f;
    [SerializeField] float sizeVariation = 0.03f;
    [SerializeField] float rotationSpeed = 20f; // degrees/sec tumble
    [SerializeField] Color snowColor = Color.white;
    [Tooltip("Safety net so flakes that miss every collider still despawn instead of falling forever.")]
    [SerializeField] float maxLifetime = 10f;
    [Tooltip("Extra headroom above the steady-state particle count (spawnRate x maxLifetime). Too little here is what causes snow to burst, stall, then burst again - raising it never hurts, it only costs a bit of memory for unused slots.")]
    [SerializeField] float particleBudgetHeadroom = 1.3f;

    [Header("Physical Interaction")]
    [SerializeField] bool collideWithWorld = true;
    [SerializeField] LayerMask collisionLayers = ~0;
    [Range(0f, 1f)] [SerializeField] float bounce = 0f;
    [Tooltip("1 = flake is destroyed the instant it lands, like real accumulation disappearing into the ground.")]
    [Range(0f, 1f)] [SerializeField] float collisionLifetimeLoss = 1f;

    [Header("Look")]
    [Tooltip("Particle material. Leave empty to auto-generate a soft round white dot sprite, so flakes read as round snow rather than square particle blobs.")]
    [SerializeField] Material snowMaterial;
    [Tooltip("Resolution of the auto-generated round flake texture, only used when snowMaterial is empty.")]
    [SerializeField] int generatedTextureSize = 32;

    [Header("Editing")]
    [SerializeField] bool autoRegenerate = true;

    Material generatedMaterial;
    Texture2D generatedTexture;

    void OnValidate()
    {
        particleBudgetHeadroom = Mathf.Max(1f, particleBudgetHeadroom);
        if (autoRegenerate)
            Configure();
    }

    // OnValidate is editor-only, so a built player never configures the particle system
    // (and its auto-generated material) without this - the renderer is left with whatever
    // material was last serialized, which can be null.
    void Awake() => Configure();

    void LateUpdate()
    {
        if (followTarget != null)
            transform.position = new Vector3(followTarget.position.x, transform.position.y, followTarget.position.z);
    }

    [ContextMenu("Apply Snowfall Settings")]
    public void Configure()
    {
        ParticleSystem ps = GetComponent<ParticleSystem>();

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        // Must exceed spawnRate * maxLifetime (the steady-state particle count) or the system hits its
        // cap, stalls emission until a backed-up cohort dies off together, then bursts again in sync.
        main.maxParticles = Mathf.CeilToInt(spawnRate * maxLifetime * particleBudgetHeadroom);
        main.startSpeed = 0f; // motion comes from velocityOverLifetime below, not initial speed
        main.startSize = new ParticleSystem.MinMaxCurve(flakeSize - sizeVariation, flakeSize + sizeVariation);
        main.startColor = snowColor;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
        main.gravityModifier = 0f;
        main.startLifetime = maxLifetime;

        var emission = ps.emission;
        emission.rateOverTime = spawnRate;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(areaSize.x, 0.1f, areaSize.y);

        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.y = new ParticleSystem.MinMaxCurve(-(fallSpeed + fallSpeedVariation), -fallSpeed);
        velocity.x = new ParticleSystem.MinMaxCurve(wind.x - 0.1f, wind.x + 0.1f);
        velocity.z = new ParticleSystem.MinMaxCurve(wind.y - 0.1f, wind.y + 0.1f);

        var noise = ps.noise;
        noise.enabled = turbulence > 0f;
        // Separate axes so noise only sways flakes sideways - full-strength Y noise can momentarily
        // outrun the downward fall velocity and make flakes look like they're drifting back upward.
        noise.separateAxes = true;
        noise.strengthX = turbulence;
        noise.strengthY = turbulence * 0.15f;
        noise.strengthZ = turbulence;
        noise.frequency = 0.3f;
        noise.scrollSpeed = 0.2f;
        noise.damping = true;

        var rotation = ps.rotationOverLifetime;
        rotation.enabled = rotationSpeed != 0f;
        rotation.z = new ParticleSystem.MinMaxCurve(-rotationSpeed * Mathf.Deg2Rad, rotationSpeed * Mathf.Deg2Rad);

        var collision = ps.collision;
        collision.enabled = collideWithWorld;
        collision.type = ParticleSystemCollisionType.World;
        collision.mode = ParticleSystemCollisionMode.Collision3D;
        collision.collidesWith = collisionLayers;
        collision.bounce = bounce;
        collision.lifetimeLoss = collisionLifetimeLoss;
        collision.dampen = 0.5f;
        collision.enableDynamicColliders = true;
        collision.maxCollisionShapes = 256;

        ParticleSystemRenderer renderer = GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = snowMaterial != null ? snowMaterial : GetGeneratedMaterial();
    }

    // Built-in particle/sprite materials default to a square blob; a soft round alpha dot reads as actual snow.
    Material GetGeneratedMaterial()
    {
        if (generatedTexture == null || generatedTexture.width != generatedTextureSize)
        {
            generatedTexture = new Texture2D(generatedTextureSize, generatedTextureSize, TextureFormat.RGBA32, false);
            generatedTexture.hideFlags = HideFlags.DontSave;
            float radius = generatedTextureSize / 2f;
            Vector2 center = new Vector2(radius, radius);
            for (int y = 0; y < generatedTextureSize; y++)
            {
                for (int x = 0; x < generatedTextureSize; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center) / radius;
                    float alpha = Mathf.Pow(Mathf.Clamp01(1f - dist), 2f);
                    generatedTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            generatedTexture.Apply();
        }

        if (generatedMaterial == null)
        {
            generatedMaterial = new Material(Shader.Find("Sprites/Default"));
            generatedMaterial.hideFlags = HideFlags.DontSave;
            generatedMaterial.mainTexture = generatedTexture;
        }

        return generatedMaterial;
    }
}
