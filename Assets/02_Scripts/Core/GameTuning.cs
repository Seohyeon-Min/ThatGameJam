using UnityEngine;

public enum NaturalTimerKind { Wake, LeaningLog, RabbitFamily, FogSection, Cloud }

// All timing and speed values in one asset, so playtime can be tuned without code changes.
// The asset lives at Assets/04_Data/Resources/GameTuning.asset.
[CreateAssetMenu(menuName = "ThatGameJam/Game Tuning")]
public class GameTuning : ScriptableObject
{
    [Header("Natural resolution (seconds)")]
    public float wakeTime = 10f;
    public float leaningLogTime = 30f;
    public float rabbitFamilyTime = 30f;
    public float fogSectionTime = 30f;
    public float cloudTime = 40f;

    [Header("Waiting signals (fraction of the natural resolution time)")]
    [Range(0f, 1f)] public float signal1At = 0.25f;
    [Range(0f, 1f)] public float signal2At = 0.5f;
    [Range(0f, 1f)] public float signal3At = 0.75f;

    [Header("Baby")]
    public float babyWalkSpeed = 1.2f;
    public float babyLookUpDuration = 1.5f;
    public float babyLookAroundDuration = 1.5f;
    public float babyWakeCryRadius = 8f;

    [Header("Bird")]
    public float birdSpeed = 6f;
    public float birdAltitudeSpeed = 3f;
    // Arriving at a perch until perched: the Bird_Landing clip's GripComplete event, when the toes have gripped.
    public float birdPerchDuration = 1.2f;
    public float birdCryCooldown = 0.8f;

    [Header("Flow")]
    public float fadeDuration = 0.6f;

    static GameTuning current;

    public static GameTuning Current => current != null ? current : current = Resources.Load<GameTuning>("GameTuning");

    public float NaturalTime(NaturalTimerKind kind) => kind switch
    {
        NaturalTimerKind.Wake => wakeTime,
        NaturalTimerKind.LeaningLog => leaningLogTime,
        NaturalTimerKind.RabbitFamily => rabbitFamilyTime,
        NaturalTimerKind.FogSection => fogSectionTime,
        _ => cloudTime,
    };
}
