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
    // Seconds the bird hovers without input before it lands on the nearest roadside branch.
    public float birdIdlePerchDelay = 2f;
    // How fast the bird's horizontal speed ramps toward birdSpeed (and toward 0 when slowing for a landing).
    public float birdAcceleration = 10f;
    // How hard it brakes approaching a perch: speed near the branch is sqrt(2 * this * remaining distance).
    public float birdLandingDeceleration = 12f;
    // Safety cap on how long the body stays put after leaving a perch if the Takeoff clip's FootRelease event
    // never arrives (it normally clears this well before the timeout; see BirdAnimationEvents.onFootRelease).
    public float birdTakeoffHoldTimeout = 1f;
    // Degrees/second the bird's heading can turn. Caps how sharply it can snap towards a new target.
    public float birdTurnSpeed = 240f;
    [Header("Bird bank (visual only)")]
    public float birdMaxBankAngle = 25f;
    // Bank degrees per degree/second of turn rate.
    public float birdBankSensitivity = 0.15f;
    // Degrees/second the visual bank can change, in or out of a turn.
    public float birdBankSpeed = 150f;

    [Header("Camera zoom")]
    // Fraction of the authored follow distance each wheel tick adds or removes (direction only: a browser's
    // wheel delta size isn't portable, per AGENTS.md).
    public float cameraZoomStep = 0.1f;
    public float cameraZoomMin = 0.5f;
    public float cameraZoomMax = 1f;
    // How fast the camera eases to the new zoom level, in zoom-fraction per second.
    public float cameraZoomSpeed = 3f;

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
