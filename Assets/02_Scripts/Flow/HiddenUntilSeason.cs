using System.Collections.Generic;
using UnityEngine;

// On a stage's gimmick (the logs, the rabbit family): keeps it out of sight, and out of reach, until its stage comes, so it can't be
// seen from the stage before. It shows up with the season that starts its stage (the season level is the stage's place in GameFlow:
// 0 is the first, after the sky intro), after WatercolorSeasonLooks has photographed the old look, so it appears inside the spring
// ring as the ring spreads, like the trees' leaves. Turns off its rendering and colliders, not the object, so its scripts keep running.
// Rendering goes off through forceRenderingOff, which an Animator writing its defaults (the leaning log's) doesn't turn back on.
public class HiddenUntilSeason : MonoBehaviour
{
    [Tooltip("Shows from this season level on: its stage's place in GameFlow (Stage 1 = 1).")]
    [SerializeField, Min(0)] int shownFromLevel = 1;

    // Only colliders that were on to begin with come back on.
    readonly List<Collider> colliders = new();
    Season season;
    int pendingLevel = -1;

    void Start()
    {
        foreach (Collider c in GetComponentsInChildren<Collider>())
            if (c.enabled)
                colliders.Add(c);
        season = FindAnyObjectByType<Season>();
        Show(season == null || season.Level >= shownFromLevel);
        if (season != null)
            season.OnSeasonChanged.AddListener(OnSeasonChanged);
    }

    void OnDestroy()
    {
        if (season != null)
            season.OnSeasonChanged.RemoveListener(OnSeasonChanged);
    }

    // Not at once: WatercolorSeasonLooks photographs the old look in this same event.
    void OnSeasonChanged(int level) => pendingLevel = level;

    void LateUpdate()
    {
        if (pendingLevel < 0)
            return;
        Show(pendingLevel >= shownFromLevel);
        pendingLevel = -1;
    }

    void Show(bool shown)
    {
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            r.forceRenderingOff = !shown;
        foreach (Collider c in colliders)
            if (c != null)
                c.enabled = shown;
    }
}
