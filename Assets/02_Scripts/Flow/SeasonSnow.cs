using UnityEngine;

// Snow falls only in the early seasons: it falls in the intro and stops when the first stage begins (season 1). Stopping ends
// only new flakes; the ones in the air fall on, so the snowfall dies down instead of vanishing.
// If the spring ring plays then, a gust rides its front: a shell of outward force that hurls flakes away as it spreads, strong
// while the ring bursts out and dying as it calms. The swirl that shows it is SeasonWisps, at every turn of the season.
// The SnowParticle shader fades what's left a little behind the front, and when the ring ends the remaining flakes are cleared
// so they don't come back into view.
[RequireComponent(typeof(ParticleSystem))]
public class SeasonSnow : MonoBehaviour
{
    static readonly int SpringRingId = Shader.PropertyToID("_SpringRing");

    [SerializeField] Season season;
    // The first season without snow.
    [SerializeField, Min(0)] int stopsAt = 1;
    [Tooltip("How hard the gust pushes, times the ring's speed squared. Higher hurls the flakes further.")]
    [SerializeField, Min(0f)] float gustStrength = 0.25f;
    [Tooltip("Thickness of the gust at the ring's front, in world units.")]
    [SerializeField, Min(0.1f)] float gustThickness = 4f;

    ParticleSystem snow;
    ParticleSystemForceField gust;
    bool clearing;
    bool ringSeen;
    float lastRadius;

    void Awake() => snow = GetComponent<ParticleSystem>();

    void OnEnable()
    {
        season.OnSeasonChanged.AddListener(Apply);
        Apply(season.Level);
    }

    void OnDisable()
    {
        season.OnSeasonChanged.RemoveListener(Apply);
        EndGust();
    }

    // After every Update, so a ring that ended this frame takes its flakes with it before they are drawn.
    void LateUpdate()
    {
        if (!clearing)
            return;
        Vector4 ring = Shader.GetGlobalVector(SpringRingId);
        if (ring.w > 0f)
        {
            if (!ringSeen)
                StartGust();
            ringSeen = true;
            MoveGust(ring);
        }
        else if (ringSeen)
        {
            EndGust();
            snow.Clear(true);
            clearing = false;
        }
    }

    void StartGust()
    {
        var forces = snow.externalForces;
        forces.enabled = true;
        forces.influenceFilter = ParticleSystemGameObjectFilter.LayerMask;
        forces.influenceMask = ~0;
        gust = new GameObject("SnowGust").AddComponent<ParticleSystemForceField>();
        gust.shape = ParticleSystemForceFieldShape.Sphere;
        lastRadius = 0f;
    }

    void MoveGust(Vector4 ring)
    {
        gust.transform.position = ring;
        gust.startRange = Mathf.Max(0f, ring.w - gustThickness);
        gust.endRange = ring.w + 0.01f;
        float speed = Time.deltaTime > 0f ? Mathf.Max(0f, ring.w - lastRadius) / Time.deltaTime : 0f;
        lastRadius = ring.w;
        // Negative gravity pushes away from the centre.
        gust.gravity = -gustStrength * speed * speed;
    }

    void EndGust()
    {
        if (gust != null)
            Destroy(gust.gameObject);
        gust = null;
    }

    void Apply(int level)
    {
        if (level >= stopsAt)
        {
            if (snow.isEmitting)
            {
                clearing = true;
                ringSeen = false;
            }
            snow.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
        else if (!snow.isEmitting)
        {
            clearing = false;
            snow.Play(true);
        }
    }
}
