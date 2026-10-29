using UnityEngine;

// Snow falls only in the early seasons: it falls in the intro and stops when the first stage begins (season 1). Stopping ends
// only new flakes; the ones in the air fall on, so the snowfall dies down instead of vanishing.
[RequireComponent(typeof(ParticleSystem))]
public class SeasonSnow : MonoBehaviour
{
    [SerializeField] Season season;
    // The first season without snow.
    [SerializeField, Min(0)] int stopsAt = 1;

    ParticleSystem snow;

    void Awake() => snow = GetComponent<ParticleSystem>();

    void OnEnable()
    {
        season.OnSeasonChanged.AddListener(Apply);
        Apply(season.Level);
    }

    void OnDisable() => season.OnSeasonChanged.RemoveListener(Apply);

    void Apply(int level)
    {
        if (level >= stopsAt)
            snow.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        else if (!snow.isEmitting)
            snow.Play(true);
    }
}
