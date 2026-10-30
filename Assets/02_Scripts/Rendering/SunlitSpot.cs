using System.Collections.Generic;
using UnityEngine;

// The sunlit spot at the end of a route: one or more shafts of light, always shining. Each child with a LightBeam is one
// shaft; place and tilt them in the prefab. Level is how many are on, counted from the top of the hierarchy.
// When the season advances (the baby fell asleep), the spot nearest the baby plays the child SpringRing outward from this
// spot. The ring stays disabled otherwise: it rebuilds its mesh with raycasts every frame while enabled, and draws nothing
// while disabled. What changes with each season listens to Season on its own, and can follow the ring by its _SpringRing global.
[ExecuteAlways]
public class SunlitSpot : MonoBehaviour
{
    static readonly List<SunlitSpot> Spots = new();

    [Tooltip("How many shafts are on, counted from the first child.")]
    [SerializeField, Min(0)] int level = 1;

    SpringRing ring;
    Season season;
    int lastSeason;

    public void SetLevel(int newLevel)
    {
        level = Mathf.Max(0, newLevel);
        Apply();
    }

    void OnEnable()
    {
        Spots.Add(this);
        Apply();
    }

    void OnDisable() => Spots.Remove(this);

    void OnValidate() => Apply();

    void OnTransformChildrenChanged() => Apply();

    void Start()
    {
        if (!Application.isPlaying)
            return;
        ring = GetComponentInChildren<SpringRing>(true);
        season = FindAnyObjectByType<Season>();
        if (season == null)
            return;
        lastSeason = season.Level;
        season.OnSeasonChanged.AddListener(OnSeasonChanged);
    }

    void OnDestroy()
    {
        if (season != null)
            season.OnSeasonChanged.RemoveListener(OnSeasonChanged);
    }

    void Update()
    {
        if (Application.isPlaying && ring != null && ring.enabled && !ring.IsPlaying)
            ring.enabled = false;
    }

    // Season also raises its event at the start and on Apply Level, so only a rise counts.
    void OnSeasonChanged(int level)
    {
        bool rose = level > lastSeason;
        lastSeason = level;
        if (rose && NearestTo(Baby.Instance.transform.position) == this)
            PlayRing();
    }

    public static void PlayRingNearestBaby() => NearestTo(Baby.Instance.transform.position)?.PlayRing();

    void PlayRing()
    {
        if (ring == null)
        {
            Debug.LogWarning("SunlitSpot: no SpringRing child to play.", this);
            return;
        }
        ring.enabled = true;
        ring.Play();
    }

    static SunlitSpot NearestTo(Vector3 position)
    {
        SunlitSpot nearest = null;
        float best = float.MaxValue;
        foreach (SunlitSpot spot in Spots)
        {
            float distance = (spot.transform.position - position).sqrMagnitude;
            if (distance < best)
            {
                best = distance;
                nearest = spot;
            }
        }
        return nearest;
    }

    void Apply()
    {
        LightBeam[] beams = GetComponentsInChildren<LightBeam>(true);
        for (int i = 0; i < beams.Length; i++)
            beams[i].GetComponent<MeshRenderer>().enabled = i < level;
    }
}
