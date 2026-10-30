using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Gives each season step its own watercolor / gouache look. When Season advances, the scene's WatercolorManager switches to that
// level's WatercolorLook and the new look spreads out from the spring ring (SeasonWipeFeature). Without that renderer feature,
// it blends over Blend Seconds on the whole screen instead (numbers and colours blend, switches flip halfway). Element 0 is
// level 0 (the intro), element 1 is level 1, and so on; a level with no look keeps the current one. Put it next to the manager.
// A look's Volume Profile crossfades over Blend Seconds on the whole screen, since post-processing comes after the ring's mask.
public class WatercolorSeasonLooks : MonoBehaviour
{
    // The ring starts within this many frames of the season's change, so before then "no ring" doesn't mean it ended.
    const int RingStartFrames = 2;

    [Tooltip("One look per season level, starting at level 0. Leave a slot empty to keep the look from before.")]
    [SerializeField] WatercolorLook[] looks = { };
    [Tooltip("Crossfade time of the looks' volume profiles. Also the look's own blend time if the season wipe isn't set up.")]
    [SerializeField, Min(0f)] float blendSeconds = 2f;

    WatercolorManager manager;
    Volume volume;
    Volume incoming;
    float volumeBlend = 1f;
    Season season;
    int currentLevel;
    WatercolorManager.Settings from;
    WatercolorLook to;
    float blend = 1f;
    int wipeCheckFrom;

    void Start()
    {
        manager = FindAnyObjectByType<WatercolorManager>();
        season = FindAnyObjectByType<Season>();
        if (manager == null || season == null)
            return;
        volume = FindAnyObjectByType<Volume>();

        // The game starts in its level's look at once.
        currentLevel = season.Level;
        WatercolorLook first = LookFor(currentLevel);
        if (first != null)
        {
            manager.Apply(first.settings, 1f);
            if (volume != null && first.volumeProfile != null)
                volume.sharedProfile = first.volumeProfile;
        }
        season.OnSeasonChanged.AddListener(OnSeasonChanged);
    }

    void OnDestroy()
    {
        if (season != null)
            season.OnSeasonChanged.RemoveListener(OnSeasonChanged);
        SeasonWipe.Release();
    }

    // Season also raises its event at the start and on Apply Level, so an unchanged level does nothing.
    void OnSeasonChanged(int level)
    {
        if (level == currentLevel)
            return;
        currentLevel = level;
        WatercolorLook look = LookFor(level);
        if (look == null)
            return;
        CrossfadeVolume(look.volumeProfile);

        Camera view = Camera.main;
        if (view != null && SeasonWipe.Capture(view, Characters()))
        {
            to = null;
            manager.Apply(look.settings, 1f);
            wipeCheckFrom = Time.frameCount + RingStartFrames;
            return;
        }

        Debug.LogWarning("WatercolorSeasonLooks: no photo was taken, so the look blends on the whole screen. Is SeasonWipeFeature on this renderer?", this);
        from = JsonUtility.FromJson<WatercolorManager.Settings>(JsonUtility.ToJson(manager.settings));
        to = look;
        blend = 0f;
    }

    // A second global Volume on top fades in; when it's full, the scene's Volume takes its profile and it fades back out of use.
    void CrossfadeVolume(VolumeProfile profile)
    {
        if (volume == null || profile == null || profile == volume.sharedProfile)
            return;
        if (incoming == null)
        {
            incoming = new GameObject("SeasonVolumeFade").AddComponent<Volume>();
            incoming.transform.SetParent(transform, false);
            incoming.isGlobal = true;
            incoming.priority = volume.priority + 1f;
        }
        // A fade still running ends here, so the new one starts from what's on screen as closely as it can.
        if (volumeBlend < 1f && incoming.sharedProfile != null)
            volume.sharedProfile = incoming.sharedProfile;
        incoming.sharedProfile = profile;
        incoming.weight = 0f;
        volumeBlend = 0f;
    }

    void UpdateVolumeFade()
    {
        if (volumeBlend >= 1f || incoming == null)
            return;
        volumeBlend = blendSeconds > 0f ? Mathf.Min(1f, volumeBlend + Time.deltaTime / blendSeconds) : 1f;
        incoming.weight = Mathf.SmoothStep(0f, 1f, volumeBlend);
        if (volumeBlend >= 1f)
        {
            volume.sharedProfile = incoming.sharedProfile;
            incoming.weight = 0f;
        }
    }

    // What moves over the background and so must not stay behind in the photo.
    static Renderer[] Characters()
    {
        var renderers = new List<Renderer>();
        if (Baby.Instance != null)
            renderers.AddRange(Baby.Instance.GetComponentsInChildren<Renderer>());
        if (BirdController.Instance != null)
            renderers.AddRange(BirdController.Instance.GetComponentsInChildren<Renderer>());
        return renderers.ToArray();
    }

    void Update()
    {
        UpdateVolumeFade();

        // The ring has ended (or never came): free the photo, so a ring played later doesn't show it again.
        if (wipeCheckFrom > 0 && Time.frameCount >= wipeCheckFrom && !SeasonWipe.RingPlaying)
        {
            SeasonWipe.Release();
            wipeCheckFrom = 0;
        }

        if (to == null)
            return;
        blend = blendSeconds > 0f ? Mathf.Min(1f, blend + Time.deltaTime / blendSeconds) : 1f;
        manager.Blend(from, to.settings, Mathf.SmoothStep(0f, 1f, blend));
        if (blend >= 1f)
            to = null;
    }

    WatercolorLook LookFor(int level) => level >= 0 && level < looks.Length ? looks[level] : null;
}
