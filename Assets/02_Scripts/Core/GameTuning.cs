using UnityEngine;

public enum NaturalTimerKind { Wake, LeaningLog, RabbitBurrow, FogSection, Cloud }

// All timing and speed values in one asset, so playtime can be tuned without code changes.
// The asset lives at Assets/04_Data/Resources/GameTuning.asset.
[CreateAssetMenu(menuName = "ThatGameJam/Game Tuning")]
public class GameTuning : ScriptableObject
{
    [Header("Natural resolution (seconds)")]
    public float wakeTime = 10f;
    public float leaningLogTime = 30f;
    public float rabbitBurrowTime = 30f;
    public float fogSectionTime = 30f;
    public float cloudTime = 40f;

    [Header("Waiting signals (fraction of the natural resolution time)")]
    [Range(0f, 1f)] public float signal1At = 0.25f;
    [Range(0f, 1f)] public float signal2At = 0.5f;
    [Range(0f, 1f)] public float signal3At = 0.75f;

    [Header("Baby")]
    public float babyWalkSpeed = 1.2f;
    // How fast her actual speed (StepToward) ramps toward target speed (m/s per second), both setting off from a
    // stop and braking to a stop at the target (stopping distance = currentSpeed^2 / (2*babyDecel)), instead of
    // snapping straight to it.
    public float babyAccel = 2.5f;
    public float babyDecel = 3.5f;
    // While turning sharply (not roughly facing where she's headed), her speed is held down toward this fraction
    // of normal, so a sharp direction change reads as turn-then-walk instead of sliding sideways into the turn.
    [Range(0f, 1f)] public float babyTurnSlowdownFloor = 0.35f;
    public float babyLookUpDuration = 1.5f;
    public float babyLookAroundDuration = 1.5f;
    public float babyWakeCryRadius = 8f;
    // A cry doesn't interrupt her stride the instant it arrives: she keeps walking for a random beat in
    // this range first, so the reaction reads as noticing rather than an instant, frame-perfect flinch.
    public float babyReactionDelayMin = 0.15f;
    public float babyReactionDelayMax = 0.4f;
    // How often (seconds, random between the two) she switches to a new idle variant (Idle/Hum/Stretch) while
    // standing and waiting, so she doesn't just breathe in place the whole time.
    public float babyIdleVariantIntervalMin = 3f;
    public float babyIdleVariantIntervalMax = 6f;
    // The intro (stage 0): how close the bird must come for her to pause and look up at it with just her head
    // (NoticeBird), how long she holds the look, and the cooldown before she may notice it again.
    public float babyBirdNoticeRadius = 5f;
    public float babyBirdNoticeDuration = 1f;
    public float babyBirdNoticeCooldown = 6f;
    // Organic walking: gentle speed/heading noise so the route doesn't look ruler-straight.
    [Range(0f, 1f)] public float babySpeedVariationAmount = 0.15f;
    public float babySpeedVariationFrequency = 0.6f;
    public float babyWanderAngle = 12f;
    public float babyWanderFrequency = 0.3f;
    // How often (seconds, random between the two) the baby checks mid-walk for a nearby PointOfInterest worth a glance.
    // No stop if nothing's there - she just keeps walking (real kids don't halt to scan empty air).
    public float babyCuriousPauseIntervalMin = 4f;
    public float babyCuriousPauseIntervalMax = 9f;
    // Glancing at a nearby PointOfInterest (a tree, a landmark): keeps walking, only turns the head.
    public float babyGazeRadius = 6f;
    public float babyGazeDuration = 1.5f;
    // Max degrees the head may turn off the animated pose. Clamps so a POI behind the baby never
    // snaps the head to an impossible angle; beyond this it just doesn't notice that one.
    public float babyGazeMaxAngle = 60f;
    // Seconds for the head's turn-toward-target blend to ramp fully in (and, separately, fully out).
    public float babyGazeBlendTime = 0.35f;
    // The ending, as she watches the bird go: seconds one blink takes, and the seconds between blinks (picked between these).
    public float babyBlinkDuration = 0.18f;
    public float babyBlinkIntervalMin = 2f;
    public float babyBlinkIntervalMax = 4.5f;
    // The ending's last shot, as the sky opens (Baby.LookUpAtSky), all at full reach: metres she rises onto her toes, degrees her
    // feet tip down to stand on them, and degrees she leans back. Her arms stay as the Idle animation has them.
    public float babySkyTiptoeHeight = 0.05f;
    public float babySkyTiptoeAngle = 30f;
    public float babySkyLeanBack = 10f;
    // Stepping onto/off a balance beam (JumpTo): extra height added at the arc's midpoint, on top of the
    // straight start-to-target rise, so the hop reads as a hop and not a diagonal slide up a ramp.
    public float babyJumpArcHeight = 0.25f;
    // Seconds she holds still, turned toward the landing spot, before leaping: a brief wind-up so the jump
    // doesn't spring out of nowhere.
    public float babyJumpAnticipation = 0.2f;
    // Crossing a log (BalanceWalk): slower and careful than on the ground (m/s).
    public float babyBalanceWalkSpeed = 0.45f;
    // Seconds she stands after hopping down off the far end of a log, before she walks on.
    public float babyLandPause = 0.8f;
    // Walk and BalanceWalk play faster or slower with how fast she is actually moving, so her steps keep up with the ground.
    // Each value is the speed (m/s) at which that clip plays at its normal rate. The art notes (README_Animations_KO.md) give the
    // speeds at which the feet do not slide: Walk about 0.1 m/s, BalanceWalk about 0.05 m/s at model scale 1, twice that on Main's
    // scale-2 baby. These defaults keep both clips at about their normal rate, as before; lower them toward those to cut the sliding.
    public float babyWalkClipSpeed = 0.9f;
    public float babyBalanceClipSpeed = 0.45f;
    // Limits on that playback rate, so a big mismatch never turns the legs into a blur or a crawl.
    public float babyClipRateMin = 0.5f;
    public float babyClipRateMax = 2.5f;
    // Cold shiver: small additive tremor on the model while awake (design doc: winter, following the bird).
    // 2.5deg/9Hz on both axes at once read as the whole body buzzing, not a cold shiver; toned down.
    public float babyShiverAngle = 0.8f;
    public float babyShiverFrequency = 3.5f;
    // How much a slow envelope swings the shiver's intensity up and down (0 = constant, 1 = fades fully to
    // nothing at its lowest), so the tremor rises and falls unevenly instead of humming at one constant level.
    [Range(0f, 1f)] public float babyShiverIntensityVariation = 0.7f;
    public float babyShiverIntensityFrequency = 0.35f;
    // Seconds the shiver takes to ease in once she sets off after waking (none while she gets up and looks round).
    public float babyShiverFadeIn = 3f;
    // At the sunlit spot she lies down only with the bird this close (XZ) and her on the screen. Until then she plays nearby
    // (PlaySpot), each play lasting between the two (seconds).
    public float babyRestBirdDistance = 10f;
    public float babyPlaySecondsMin = 5f;
    public float babyPlaySecondsMax = 9f;

    [Header("Stage change")]
    // Once the baby has got up in the next stage, the sunlit spot she slept in fades out over this many seconds.
    public float sunlitSpotFadeSeconds = 1.5f;

    [Header("Rabbit burrow (ADR 0037)")]
    // The mother's first cry, calling her young: the girl's head comes up and she holds still this long (s), then trots this far (m)
    // toward her, at most this fast (m/s, slowing as she gets there), and watches this long (s).
    public float babyCallNoticePause = 0.6f;
    public float babyCallTrotDistance = 1.5f;
    public float babyCallTrotSpeed = 2f;
    public float babyCallWatchTime = 0.8f;
    // Then she comes back onto her way this far (m) ahead of where she is along it, instead of cutting straight to the next point.
    public float babyCallRejoinAhead = 1.5f;
    // After a cry, this many seconds before the mother answers from the hole.
    public float burrowAnswerDelay = 0.6f;
    // A cry this close to the hole (XZ) is answered from inside.
    public float burrowCryRadius = 10f;

    [Header("Fog maze (ADR 0028)")]
    // At a fork whose animal has not answered, the girl stands this long before she picks a way at random.
    public float fogForkWaitTime = 3f;
    // At the wall of a dead end she stands this long before she turns back.
    public float fogWallPauseTime = 1f;
    // When the bird cries she runs to it at this speed.
    public float fogRunSpeed = 2.5f;
    // Having run to the bird, she waits this long for another cry before she walks on by herself.
    public float fogRunIdleTime = 3f;

    [Header("Bird")]
    // Its flying speed goes with how far off where it is going is (the pointer, held; or where it was clicked): birdMinSpeed
    // when that is birdSlowDistance away or nearer, birdSpeed (its most) from birdFastDistance on, eased between.
    public float birdSpeed = 6f;
    public float birdMinSpeed = 2f;
    public float birdSlowDistance = 1f;
    public float birdFastDistance = 6f;
    // From this speed on it glides; slower it only beats its wings. Below the speed less the margin it stops gliding.
    public float birdGlideSpeed = 3.5f;
    public float birdGlideMargin = 0.5f;
    public float birdAltitudeSpeed = 3f;
    // Arriving at a perch until perched: the Bird_Landing clip's GripComplete event, when the toes have gripped.
    public float birdPerchDuration = 1.2f;
    // At least the bird call's own clip length (event:/SFX/bird, ~1.88s), so spamming the cry can't stack overlapping calls.
    public float birdCryCooldown = 1.9f;
    // Seconds the bird hovers without input before it lands on the nearest roadside branch.
    public float birdIdlePerchDelay = 2f;
    // How close (world units) a branch must be for idling to auto-perch on it. The camera follows the bird,
    // so landing on a branch far across the screen reads as the camera snapping there unasked; being sent to
    // the nearest branch when refused (Refuse) is unrestricted, by design.
    public float birdIdlePerchRadius = 2.5f;
    // A press on a branch let go within this many seconds lands on it; held longer, the bird follows the pointer instead.
    public float birdPerchClickSeconds = 0.2f;
    // How fast the bird's horizontal speed ramps toward birdSpeed (and toward 0 when slowing for a landing).
    public float birdAcceleration = 10f;
    // How hard it brakes approaching a perch: speed near the branch is sqrt(2 * this * remaining distance).
    public float birdLandingDeceleration = 12f;
    // Safety cap on how long the body stays put after leaving a perch if the Takeoff clip's FootRelease event
    // never arrives (it normally clears this well before the timeout; see BirdAnimationEvents.onFootRelease).
    public float birdTakeoffHoldTimeout = 1f;
    // Degrees/second the bird's heading can turn. Caps how sharply it can snap towards a new target.
    public float birdTurnSpeed = 240f;
    // Sheltering the baby from the snow: how far ahead of her the bird flies, and how fast (m/s) it closes a gap
    // on top of matching her pace once it has reached her.
    public float birdShelterAhead = 0.6f;
    // Height above her feet it flies at while sheltering, lower than the stage's altitude; it climbs back on release.
    public float birdShelterHeight = 2.8f;
    public float birdShelterCatchUp = 0.5f;
    // The wheel moves the bird with the zoom: at cameraZoomMax it flies this many times as high above the stage's ground as at
    // zoom 1 (the stage's altitude), and at cameraZoomMin it flies this low (this far above any log, bank or water under it) and
    // comes down where it can stand: terrain no steeper than birdLandMaxSlope degrees, so not the banks down to the water.
    public float birdZoomedOutHeightScale = 1.5f;
    public float birdLowFlightHeight = 0.6f;
    public float birdLandMaxSlope = 25f;
    // Seconds a change of height takes along motionCurve: taking off, coming down to the ground, a new stage's altitude.
    public float birdClimbDuration = 1f;
    // How fast Bird_Walk plays (1 = as made). The walking speed follows from it so the feet never slide: slower steps, slower walk.
    public float birdWalkClipRate = 2f;
    // On top of that, how much faster it actually goes than its steps carry it (1 = feet never slide). The clips keep their pace.
    public float birdWalkSpeedScale = 4f;
    // Degrees a second it turns to face the way it walks: quicker than in the air, so it faces where it steps.
    public float birdWalkTurnSpeed = 720f;
    // Setting off on the ground, it hops (Bird_Hop, faster) for a while between these many seconds, then walks.
    public Vector2 birdHopSeconds = new(1f, 3f);
    // Rising or sinking in the air faster than this (metres a second, the wheel's zoom moving its height), it hovers in place
    // (Bird_Hover) and looks the way it goes: its head tips this many degrees per metre a second, up to the most, eased at this rate.
    public float birdClimbHoverSpeed = 0.25f;
    // Seconds it keeps hovering after its height stops changing, so a wheel turned in ticks (each a small rise that eases to a stop)
    // keeps one hover going instead of cutting between hover and glide at every tick.
    public float birdClimbHoverHold = 0.8f;
    // How fast Bird_Hover plays while it rises or sinks (1 = as made); hovering still, it plays at 1.
    public float birdClimbHoverRate = 1.8f;
    public float birdClimbHeadPerSpeed = 25f;
    public float birdClimbHeadMost = 45f;
    public float birdClimbHeadEase = 6f;
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
    public float cameraZoomMax = 1.25f;
    // Seconds one wheel tick's zoom takes, along motionCurve.
    public float cameraZoomDuration = 0.6f;

    [Header("Camera framing the bird with the baby")]
    // Up to the near distance (XZ, at zoom 1) from the baby the camera frames both; from there to the far distance it slides
    // gradually over to the bird alone. Set by distance only, so the camera stops when the bird stops.
    public float cameraTogetherNearDistance = 4f;
    public float cameraTogetherFarDistance = 14f;
    // The bird stays within this fraction of half the screen's shorter side from the screen's centre while both are framed, so it
    // stays inside the screen margin BirdController keeps it in, also in a narrow browser window.
    public float cameraTogetherMaxBirdScreen = 0.8f;
    // Where between the bird (0) and the baby (1) the framed point lies.
    public float cameraTogetherBabyShare = 0.5f;
    // While framing both, the camera holds still until the framed point has moved this far (at zoom 1). Past it, it closes the
    // part beyond in about this many seconds, so it eases into following instead of setting off at once. Small moves of the bird
    // don't move the camera.
    public float cameraTogetherDeadZone = 1.5f;
    public float cameraTogetherFollowTime = 0.2f;

    [Header("Easing")]
    // 0 to 1 over 0 to 1. The zoom, the bird's changes of height and its way up or down to a branch all follow it.
    public AnimationCurve motionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    static GameTuning current;

    public static GameTuning Current => current != null ? current : current = Resources.Load<GameTuning>("GameTuning");

    public float NaturalTime(NaturalTimerKind kind) => kind switch
    {
        NaturalTimerKind.Wake => wakeTime,
        NaturalTimerKind.LeaningLog => leaningLogTime,
        NaturalTimerKind.RabbitBurrow => rabbitBurrowTime,
        NaturalTimerKind.FogSection => fogSectionTime,
        _ => cloudTime,
    };
}
