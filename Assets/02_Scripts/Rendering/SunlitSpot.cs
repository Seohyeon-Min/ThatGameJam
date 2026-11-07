using System.Collections;
using System.Collections.Generic;
using FMODUnity;
using UnityEngine;

// The sunlit spot at the end of a route: one or more shafts of light, always shining. Each child with a LightBeam is one
// shaft; place and tilt them in the prefab. Level is how many are on, counted from the top of the hierarchy.
// Put it under the last point of the stage's route, at zero: that point is where the baby lies down, so move the point, not the spot.
// When the season advances (the baby fell asleep), the spot nearest the baby plays the child SpringRing outward from this
// spot. The ring stays disabled otherwise: it rebuilds its mesh with raycasts every frame while enabled, and draws nothing
// while disabled. What changes with each season listens to Season on its own, and can follow the ring by its _SpringRing global.
// Once the baby has got up in the next stage, GameFlow fades the shafts out (FadeOut); the ring is left alone.
[ExecuteAlways]
public class SunlitSpot : MonoBehaviour
{
    static readonly List<SunlitSpot> Spots = new();

    [Tooltip("How many shafts are on, counted from the first child.")]
    [SerializeField, Min(0)] int level = 1;

    SpringRing ring;
    Season season;
    int lastSeason;
    // The level before FadeOut put it to 0, for Show. Negative while it has not faded.
    int levelBeforeFade = -1;

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

    void OnDisable()
    {
        Spots.Remove(this);
        // Turned off mid-fade: done fading, rather than stuck half way.
        if (levelBeforeFade >= 0 && level > 0)
            FinishFade();
    }

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

    // The shafts fade out over the seconds and stay off.
    public void FadeOut(float seconds)
    {
        if (levelBeforeFade >= 0)
            return;
        levelBeforeFade = level;
        StartCoroutine(Fade(seconds));
    }

    // Dev tool (Back): the shafts on again, as before FadeOut.
    public void Show()
    {
        if (levelBeforeFade < 0)
            return;
        StopAllCoroutines();
        SetStrength(1f);
        SetLevel(levelBeforeFade);
        levelBeforeFade = -1;
    }

    IEnumerator Fade(float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            SetStrength(1f - t / seconds);
            yield return null;
        }
        FinishFade();
    }

    // Off for good, at full strength again so Show turns them on as they were.
    void FinishFade()
    {
        SetStrength(1f);
        SetLevel(0);
    }

    void SetStrength(float strength)
    {
        foreach (LightBeam beam in GetComponentsInChildren<LightBeam>(true))
            beam.Strength = strength;
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
        // Not for the ending's last ring (as she sleeps): the ending's music is playing then.
        EndingFlight ending = FindAnyObjectByType<EndingFlight>();
        if (ending == null || !ending.IsPlaying)
            RuntimeManager.PlayOneShot("event:/SFX/spiral");
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
