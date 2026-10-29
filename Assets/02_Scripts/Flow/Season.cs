using UnityEngine;
using UnityEngine.Events;

// The season's step: 0 at the start, one more each time the baby falls asleep at a sunlit spot and the next stage begins
// (design doc §10.2, §15.9). Five steps: 0 intro, 1 to 3 stages 1 to 3, 4 the ending (ADR 0018). VFX and environment changes listen to OnSeasonChanged; no real environment change is made yet.
// In Play Mode the Inspector can advance it or apply a typed level (SeasonEditor).
public class Season : MonoBehaviour
{
    [SerializeField, Min(0)] int level;
    [SerializeField] UnityEvent<int> onSeasonChanged;

    public int Level => level;
    // Raised with the new level whenever it changes, and once at the start with the starting level.
    public UnityEvent<int> OnSeasonChanged => onSeasonChanged;

    void Start() => onSeasonChanged.Invoke(level);

    public void AdvanceSeason() => SetSeason(level + 1);

    public void SetSeason(int newLevel)
    {
        level = Mathf.Max(0, newLevel);
        onSeasonChanged.Invoke(level);
    }
}
