using System;
using System.Collections;
using UnityEngine;

public enum BabyState { Sleeping, Waking, Walking, Waiting, Reacting, Resting }

// The baby: sleeps until the cry or its wake timer, walks its route, waits at blockers,
// and looks up at the bird whenever the bird was the one who helped (design doc §2.2, §4).
public class Baby : CryListener
{
    static readonly int WalkingParam = Animator.StringToHash("Walking");
    static readonly int AsleepParam = Animator.StringToHash("Asleep");
    static readonly int LookAroundParam = Animator.StringToHash("LookAround");
    static readonly int SleepState = Animator.StringToHash("Sleep");
    // Asleep under the snow, in the intro until its GetUp: Girl_Intro_Sleep.
    static readonly int IntroSleepState = Animator.StringToHash("IntroSleep");
    static readonly int QuietWaitParam = Animator.StringToHash("QuietWait");
    static readonly int IdleVariantParam = Animator.StringToHash("IdleVariant");
    static readonly int IdleState = Animator.StringToHash("Idle");
    static readonly int WalkState = Animator.StringToHash("Walk");
    static readonly int BalanceWalkState = Animator.StringToHash("BalanceWalk");
    static readonly int JumpState = Animator.StringToHash("Jump");
    static readonly int IntroWakeParam = Animator.StringToHash("IntroWake");

    [SerializeField] NaturalTimer wakeTimer;
    [SerializeField] float turnSpeed = 360f;
    [SerializeField] Animator animator;
    // The baby starts walking when this has played.
    [SerializeField] AnimationClip getUpClip;
    [Tooltip("Played instead of the GetUp clip the first time the baby wakes in the first stage (the intro), shaking off the snow")]
    [SerializeField] AnimationClip introGetUpClip;
    [Tooltip("The snow lying on her in the intro (Snow_Clumps). Shown only while she sleeps there before that first wake; off for good once she is up")]
    [SerializeField] Renderer snowClumps;
    [Tooltip("Played on the eyes during the GetUp clip.")]
    [SerializeField] ParticleSystem[] eyeSparkles;
    [Tooltip("The frame of the GetUp clip they play on: when the flowers appear in the eyes.")]
    [SerializeField, Min(0)] int eyeSparkleFrame;
    // The baby is asleep at the rest spot when this has played.
    [SerializeField] AnimationClip lieDownClip;
    // Stepping up onto a balance beam (a log) or down off the far end plays this once, covering that segment's
    // own movement, before Walk/BalanceWalk resumes.
    [SerializeField] AnimationClip jumpClip;

    public static Baby Instance { get; private set; }

    public BabyState State { get; private set; }
    public NaturalTimer WakeTimer => wakeTimer;
    // The blocker the baby is standing in front of, else null.
    public PathBlocker CurrentBlocker { get; private set; }

    // Reached the last point of its route and has lain down to sleep there.
    public event Action ReachedRest;

    BabyRoute route;
    bool cryPending;
    // Crossing a balance beam (a LeaningLog blocker, or a route point marked BalanceBeamStart):
    // BalanceWalk plays instead of Walk, and the baby ignores curious pauses/cries until BalanceBeamEnd.
    bool onBalanceBeam;
    bool gettingUp;
    // The intro GetUp has played: the snow is gone for the rest of the game.
    bool snowShaken;
    // 0 to 1: how far the cold tremor has eased in since she set off.
    float shiverFade;
    float curiousPauseCooldown;
    // Her actual current speed (m/s), eased toward the target speed in StepToward instead of snapping to it:
    // ramps up from a stop and brakes smoothly approaching the target. Reset to 0 in StandStill so every fresh
    // start (after waiting, a curious pause, a reaction) ramps up again instead of resuming at the old speed.
    float currentSpeed;
    // Stage 0 only: seconds until she may next pause to look up at a nearby bird (NoticeBird).
    float birdNoticeCooldown;
    // Standing still, looking up at the bird with just her head (NoticeBird): LateUpdate's gaze system targets
    // it while this is set, same as sheltering or a PointOfInterest.
    bool noticingBird;
    // Per-instance offset into the noise fields below, so a second baby (if one ever exists) doesn't wobble in lockstep.
    float noiseSeed;
    Quaternion modelBaseRotation;

    // The head bone, turned toward a PointOfInterest (or the sheltering bird) while walking (LateUpdate, after the animator has posed it).
    // Null is handled gracefully: the rig just never gained this bone and the baby simply never glances.
    Transform headBone;
    PointOfInterest gazeTarget;
    float gazeTimeRemaining;
    float gazeBlend;
    Vector3 lookPoint;
    // Paused to look around (CuriousPause).
    bool lookingAround;

    protected override float CryRadius => GameTuning.Current.babyWakeCryRadius;

    void Awake()
    {
        Instance = this;
        wakeTimer.Resolved += byBird => StartCoroutine(WakeAndWalk(byBird));
        noiseSeed = UnityEngine.Random.Range(0f, 100f);
        modelBaseRotation = animator.transform.localRotation;
        curiousPauseCooldown = UnityEngine.Random.Range(GameTuning.Current.babyCuriousPauseIntervalMin, GameTuning.Current.babyCuriousPauseIntervalMax);
        birdNoticeCooldown = GameTuning.Current.babyBirdNoticeCooldown;
        headBone = animator.transform.Find("NPC-Girl-Rig/ROOT/Hips/Spine/Chest/Neck/Head");
    }

    // Cold tremor on the model only (never on transform, which drives movement/arrival checks elsewhere).
    // Off once asleep/resting: curled up out of the wind by then. Off while waking too (getting up, looking round): the animation
    // carries it alone. Once she sets off it eases in over babyShiverFadeIn, so it never starts with a jolt.
    void Update()
    {
        if (State is BabyState.Sleeping or BabyState.Resting or BabyState.Waking || gettingUp)
        {
            shiverFade = 0f;
            animator.transform.localRotation = modelBaseRotation;
            return;
        }
        GameTuning tuning = GameTuning.Current;
        shiverFade = Mathf.MoveTowards(shiverFade, 1f, Time.deltaTime / Mathf.Max(tuning.babyShiverFadeIn, 0.01f));
        // A slow-moving envelope on top of the shiver itself, so it isn't a constant metronomic hum: it
        // eases from barely shivering up to a harder tremble and back, unpredictably, like a real chill.
        float intensity = Mathf.Max(0f, 1f + Noise(130f, tuning.babyShiverIntensityFrequency) * 2f * tuning.babyShiverIntensityVariation)
            * Mathf.SmoothStep(0f, 1f, shiverFade);
        float x = Noise(57f, tuning.babyShiverFrequency) * 2f * tuning.babyShiverAngle * intensity;
        float z = Noise(83f, tuning.babyShiverFrequency) * 2f * tuning.babyShiverAngle * intensity;
        animator.transform.localRotation = modelBaseRotation * Quaternion.Euler(x, 0f, z);
    }

    // Turns the head toward gazeTarget, after the animator has already posed it this frame (standard
    // look-override timing). Blends in/out by angle instead of a fixed ramp time, so a target that
    // appears or disappears mid-gaze (balance beam start, a reaction interrupting it) still eases smoothly.
    void LateUpdate()
    {
        if (headBone == null)
            return;
        if (gazeTimeRemaining > 0f)
            gazeTimeRemaining -= Time.deltaTime;
        bool active = State == BabyState.Walking && !onBalanceBeam && !lookingAround;
        // The bird keeping the snow off her holds her eyes, over any landmark. Blending out keeps the last point looked at.
        if (BirdController.Instance.IsSheltering || noticingBird)
            lookPoint = BirdController.Instance.transform.position;
        else if (gazeTarget != null && gazeTimeRemaining > 0f)
            lookPoint = gazeTarget.LookPoint;
        else
            active = false;
        GameTuning tuning = GameTuning.Current;
        gazeBlend = Mathf.MoveTowards(gazeBlend, active ? 1f : 0f, Time.deltaTime / Mathf.Max(0.01f, tuning.babyGazeBlendTime));
        if (gazeBlend <= 0.001f)
            return;

        Vector3 animatedForward = headBone.forward;
        Vector3 toTarget = (lookPoint - headBone.position).normalized;
        Vector3 desiredDir = Vector3.RotateTowards(animatedForward, toTarget, Mathf.Deg2Rad * tuning.babyGazeMaxAngle, 0f);
        Quaternion desiredRot = Quaternion.LookRotation(desiredDir, headBone.up);
        headBone.rotation = Quaternion.Slerp(headBone.rotation, desiredRot, gazeBlend);
    }

    // Picks a PointOfInterest to glance at: inside notice radius, roughly ahead (not behind), weighted
    // by closeness and the POI's own weight, with a random factor so it isn't always the single closest one.
    PointOfInterest PickNearbyPointOfInterest()
    {
        GameTuning tuning = GameTuning.Current;
        PointOfInterest best = null;
        float bestScore = 0f;
        foreach (PointOfInterest poi in PointOfInterest.All)
        {
            Vector3 toPoi = poi.LookPoint - transform.position;
            float distance = toPoi.magnitude;
            if (distance < 0.01f || distance > tuning.babyGazeRadius)
                continue;
            if (Vector3.Dot(transform.forward, toPoi / distance) < -0.2f)
                continue;
            float score = poi.Weight * (1f - distance / tuning.babyGazeRadius) * UnityEngine.Random.value;
            if (score > bestScore)
            {
                bestScore = score;
                best = poi;
            }
        }
        return best;
    }

    // Smooth noise in [-0.5, 0.5); offset decorrelates it from the other channels sharing noiseSeed.
    float Noise(float offset, float frequency) => Mathf.PerlinNoise(noiseSeed + offset, Time.time * frequency) - 0.5f;

    // Moves the baby to the spot, already lying asleep there, ready to wake and walk the route.
    public void PlaceAsleep(Transform at, BabyRoute route)
    {
        RestAt(at);
        Sleep(route);
    }

    // Moves the baby to the spot lying asleep, as if it had walked there and fallen asleep. It does not wake from this by itself.
    public void RestAt(Transform spot)
    {
        StopAllCoroutines();
        gettingUp = false;
        lookingAround = false;
        transform.SetPositionAndRotation(spot.position, spot.rotation);
        animator.speed = 1f;
        // Already lying: no lying-down animation after the move. Under the snow in the intro, until it is shaken off.
        animator.Play(InIntro && !snowShaken ? IntroSleepState : SleepState, 0, 0f);
        CurrentBlocker = null;
        State = BabyState.Resting;
        animator.SetBool(WalkingParam, false);
        animator.SetBool(LookAroundParam, false);
        animator.SetBool(QuietWaitParam, false);
        animator.SetBool(AsleepParam, true);
        wakeTimer.ResetTimer();
        ShowSnow();
    }

    // Dev tool: stands the baby on the spot and has it lie down there, as at the end of its route: Reached Rest follows.
    public void LieDownAt(Transform spot)
    {
        StopAllCoroutines();
        gettingUp = false;
        lookingAround = false;
        transform.SetPositionAndRotation(spot.position, spot.rotation);
        CurrentBlocker = null;
        animator.SetBool(WalkingParam, false);
        animator.SetBool(LookAroundParam, false);
        animator.SetBool(AsleepParam, false);
        animator.Play(IdleState, 0, 0f);
        wakeTimer.ResetTimer();
        StartCoroutine(LieDown());
    }

    IEnumerator LieDown()
    {
        State = BabyState.Resting;
        // Timed by the clip's length below: at its normal rate.
        animator.speed = 1f;
        // One frame standing, so the animator goes through its lying-down transition.
        yield return null;
        animator.SetBool(WalkingParam, false);
        animator.SetBool(QuietWaitParam, false);
        animator.SetBool(AsleepParam, true);
        yield return new WaitForSeconds(lieDownClip.length);
        ReachedRest?.Invoke();
    }

    // Stays asleep with its wake timer stopped, until Sleep starts it.
    public void HoldAsleep() => wakeTimer.ResetTimer();

    // Asleep where it is, ready to wake and walk the route. The next stage starts this way where the baby fell asleep (ADR 0010).
    public void Sleep(BabyRoute route)
    {
        StopAllCoroutines();
        gettingUp = false;
        lookingAround = false;
        this.route = route;
        CurrentBlocker = null;
        State = BabyState.Sleeping;
        // The GetUp that follows is timed by its clip's length: at its normal rate.
        animator.speed = 1f;
        animator.SetBool(WalkingParam, false);
        animator.SetBool(LookAroundParam, false);
        animator.SetBool(QuietWaitParam, false);
        animator.SetBool(AsleepParam, true);
        wakeTimer.ResetTimer();
        wakeTimer.Begin();
        ShowSnow();
    }

    static bool InIntro
    {
        get
        {
            GameFlow flow = GameFlow.Instance;
            return flow != null && flow.Stages.Count > 0 && flow.CurrentStage == flow.Stages[0];
        }
    }

    // The snow lies on her only while she sleeps in the intro, until the intro GetUp has shaken it off.
    void ShowSnow()
    {
        if (snowClumps != null)
            snowClumps.enabled = InIntro && !snowShaken;
    }

    protected override void OnCry(Vector3 origin)
    {
        if (State == BabyState.Sleeping)
            wakeTimer.ResolveByBird();
        else if (State == BabyState.Walking)
            cryPending = true;
    }

    // A glint on each eye when the flowers appear in the GetUp clip.
    IEnumerator SparkleEyes(AnimationClip clip)
    {
        yield return new WaitForSeconds(eyeSparkleFrame / clip.frameRate);
        foreach (ParticleSystem sparkle in eyeSparkles)
            sparkle.Play();
    }

    IEnumerator WakeAndWalk(bool byBird)
    {
        State = BabyState.Waking;
        // Starts while the baby gets up, so it can show that; the baby walks only once both are over.
        Cutscene cutscene = GameFlow.Instance.CurrentStage.wakeCutscene;
        if (cutscene != null)
            cutscene.Play();
        GameTuning tuning = GameTuning.Current;
        bool intro = InIntro && !snowShaken && introGetUpClip != null;
        AnimationClip clip = intro ? introGetUpClip : getUpClip;
        animator.SetBool(IntroWakeParam, intro);
        animator.SetBool(AsleepParam, false);
        gettingUp = true;
        StartCoroutine(SparkleEyes(clip));
        yield return new WaitForSeconds(clip.length);
        gettingUp = false;
        if (intro)
        {
            snowShaken = true;
            ShowSnow();
        }
        if (byBird)
            yield return LookUp(tuning.babyLookUpDuration);
        else
        {
            animator.SetBool(LookAroundParam, true);
            yield return new WaitForSeconds(tuning.babyLookAroundDuration);
            animator.SetBool(LookAroundParam, false);
        }

        if (cutscene != null)
            yield return new WaitUntil(() => !cutscene.IsPlaying);
        // Set whenever a BalanceBeamStart/End marker or a freshly-opened LeaningLog is reached: the step up onto
        // (or down off) the beam is the *next* route segment, not the point just arrived at, so it's the next
        // segment that plays as a jump instead of an ordinary walk (design doc request: the climb itself should
        // be the jump, not a jump played in place after already having walked up to height).
        bool jumpNextSegment = false;

        for (int i = 0; i < route.Count; i++)
        {
            State = BabyState.Walking;
            if (jumpNextSegment)
            {
                jumpNextSegment = false;
                yield return JumpTo(route[i].position);
                // Down off the far end of a log (not onto one): she stands a moment on the ground before walking on.
                if (!onBalanceBeam && tuning.babyLandPause > 0f)
                {
                    animator.CrossFade(IdleState, 0.2f);
                    yield return new WaitForSeconds(tuning.babyLandPause);
                }
                animator.CrossFade(onBalanceBeam ? BalanceWalkState : WalkState, 0.25f);
            }
            else
            {
                yield return WalkTo(route[i].position);
            }

            if (onBalanceBeam && route[i].TryGetComponent(out BalanceBeamEnd _))
            {
                onBalanceBeam = false;
                jumpNextSegment = true;
            }
            else if (!onBalanceBeam && route[i].TryGetComponent(out BalanceBeamStart _))
            {
                onBalanceBeam = true;
                jumpNextSegment = true;
            }

            if (!route[i].TryGetComponent(out StopPoint stop))
                continue;

            // The bird may have opened it before the baby arrived (ADR 0003 Q4); the baby then only reacts.
            PathBlocker blocker = stop.Blocker;
            // No crouch clip yet: use only calm breathing for this pose, including its reaction.
            animator.SetBool(QuietWaitParam, stop.Pose == WaitPose.Crouch);
            if (!blocker.IsOpen)
            {
                // Waits standing whatever the stop's pose: the model has no crouch or sit yet.
                State = BabyState.Waiting;
                StandStill();
                CurrentBlocker = blocker;
                blocker.BeginWaiting();
                // Switches idle variant (Idle/Hum/Stretch) every so often so a long wait doesn't just breathe in
                // place; skipped while observing something specific, which takes full attention instead.
                float idleVariantCooldown = UnityEngine.Random.Range(tuning.babyIdleVariantIntervalMin, tuning.babyIdleVariantIntervalMax);
                while (!blocker.IsOpen)
                {
                    if (stop.ObserveTarget != null)
                        TurnToward(stop.ObserveTarget.position - transform.position);
                    else if ((idleVariantCooldown -= Time.deltaTime) <= 0f)
                    {
                        idleVariantCooldown = UnityEngine.Random.Range(tuning.babyIdleVariantIntervalMin, tuning.babyIdleVariantIntervalMax);
                        animator.SetInteger(IdleVariantParam, UnityEngine.Random.Range(0, 3));
                    }
                    yield return null;
                }
                CurrentBlocker = null;
            }
            if (stop.ObserveTarget != null)
            {
                State = BabyState.Reacting;
                StandStill();
                for (float time = 0f; time < 0.8f; time += Time.deltaTime)
                {
                    TurnToward(stop.ObserveTarget.position - transform.position);
                    yield return null;
                }
            }
            if (blocker.OpenedByBird && blocker.LookUpWhenOpenedByBird)
            {
                State = BabyState.Reacting;
                yield return LookUp(tuning.babyLookUpDuration);
            }
            if (blocker.OpenedByBird && stop.ByBirdRoute != null)
            {
                State = BabyState.Walking;
                for (int detour = 0; detour < stop.ByBirdRoute.Count; detour++)
                    yield return WalkTo(stop.ByBirdRoute[detour].position);
            }
            // A fallen log or similar: arms-out balance until the far end (BalanceBeamEnd) is reached.
            // The climb onto it is the next segment (see jumpNextSegment above).
            if (blocker is LeaningLog)
            {
                onBalanceBeam = true;
                jumpNextSegment = true;
            }
        }

        yield return LieDown();
    }

    IEnumerator WalkTo(Vector3 target)
    {
        while (!StepToward(target))
        {
            // Full attention on the crossing while balancing: no glancing around or reacting to cries.
            if (!onBalanceBeam)
            {
                if (cryPending)
                {
                    cryPending = false;
                    yield return ReactToCry();
                }
                // The intro only: the bird flying close overhead is itself worth a glance, cry or not.
                else if (InIntro && (birdNoticeCooldown -= Time.deltaTime) <= 0f && BirdNearby())
                {
                    birdNoticeCooldown = GameTuning.Current.babyBirdNoticeCooldown;
                    yield return NoticeBird();
                }
                else if ((curiousPauseCooldown -= Time.deltaTime) <= 0f)
                {
                    curiousPauseCooldown = UnityEngine.Random.Range(GameTuning.Current.babyCuriousPauseIntervalMin, GameTuning.Current.babyCuriousPauseIntervalMax);
                    PointOfInterest poi = PickNearbyPointOfInterest();
                    // A specific thing nearby: glance at it without breaking stride (LateUpdate turns only the head).
                    // Nothing nearby: fall back to the old full stop-and-look-around.
                    if (poi != null)
                    {
                        gazeTarget = poi;
                        gazeTimeRemaining = GameTuning.Current.babyGazeDuration;
                    }
                    else
                    {
                        yield return CuriousPause();
                    }
                }
            }
            yield return null;
        }
    }

    // Pauses the walk to look at the bird when it cries nearby mid-route.
    IEnumerator ReactToCry()
    {
        State = BabyState.Reacting;
        yield return LookUp(GameTuning.Current.babyLookUpDuration);
        State = BabyState.Walking;
    }

    bool BirdNearby() => BirdController.Instance != null
        && Vector3.Distance(transform.position, BirdController.Instance.transform.position) <= GameTuning.Current.babyBirdNoticeRadius;

    // Stage 0: the bird flies close by overhead. She pauses and looks up at it with just her head (LateUpdate,
    // via noticingBird), body left facing the way she was already walking, then goes straight back to that.
    IEnumerator NoticeBird()
    {
        StandStill();
        noticingBird = true;
        yield return new WaitForSeconds(GameTuning.Current.babyBirdNoticeDuration);
        noticingBird = false;
    }

    // Pauses the walk to glance around, like a child noticing something nearby rather than marching straight through.
    IEnumerator CuriousPause()
    {
        GameTuning tuning = GameTuning.Current;
        StandStill();
        // Looks around even under the sheltering bird: LateUpdate leaves the head to the animation meanwhile.
        lookingAround = true;
        animator.SetBool(LookAroundParam, true);
        yield return new WaitForSeconds(tuning.babyLookAroundDuration);
        animator.SetBool(LookAroundParam, false);
        lookingAround = false;
    }

    // The jump's position at progress t (0 to 1): eased horizontal lerp, plus a sine arc over the straight
    // height lerp so it reads as a hop. Shared with BabyRoute's gizmo so the Scene view preview can never
    // drift out of sync with what actually plays at runtime.
    public static Vector3 EvaluateJump(Vector3 start, Vector3 target, float t, float arcHeight)
    {
        float eased = Mathf.SmoothStep(0f, 1f, t);
        Vector3 flat = Vector3.Lerp(new Vector3(start.x, 0f, start.z), new Vector3(target.x, 0f, target.z), eased);
        float height = Mathf.Lerp(start.y, target.y, eased) + Mathf.Sin(t * Mathf.PI) * arcHeight;
        return new Vector3(flat.x, height, flat.z);
    }

    // Stepping up onto a balance beam or down off the far end: one in-place hop before Walk/BalanceWalk resumes.
    // Covers the segment itself (height included) over the clip's own timing, instead of ordinary walk-speed
    // interpolation finishing the climb before the jump animation plays. StandStill(): the walk-cycle animator
    // bool should read false while this plays.
    IEnumerator JumpTo(Vector3 target)
    {
        StandStill();
        GameTuning tuning = GameTuning.Current;
        // A brief wind-up, turned toward the landing spot, so the jump doesn't spring out of nowhere.
        for (float time = 0f; time < tuning.babyJumpAnticipation; time += Time.deltaTime)
        {
            TurnToward(target - transform.position);
            yield return null;
        }
        animator.CrossFade(JumpState, 0.1f);
        Vector3 start = transform.position;
        float arcHeight = tuning.babyJumpArcHeight;
        for (float time = 0f; time < jumpClip.length; time += Time.deltaTime)
        {
            // Turning continues every frame (as WalkTo does), not just once at takeoff, so a route that
            // bends right at the beam still has the baby facing the far end by the time she lands.
            TurnToward(target - transform.position);
            transform.position = EvaluateJump(start, target, time / jumpClip.length, arcHeight);
            yield return null;
        }
        transform.position = target;
    }

    // One frame of walking toward the target; true on arrival. Also lets the blocker the baby waits at lead it (the fog maze).
    // Heading wanders a little and pace breathes a little around babyWalkSpeed (babyBalanceWalkSpeed on a log), and the walk
    // clip plays at a matching rate: transform.position still closes in on the exact target, so arrival is unaffected.
    public bool StepToward(Vector3 target)
    {
        animator.SetBool(QuietWaitParam, false);
        animator.SetBool(WalkingParam, true);
        GameTuning tuning = GameTuning.Current;
        Vector3 toTarget = target - transform.position;
        float distanceRemaining = toTarget.magnitude;
        float wanderYaw = Noise(31f, tuning.babyWanderFrequency) * 2f * tuning.babyWanderAngle;
        TurnToward(Quaternion.Euler(0f, wanderYaw, 0f) * toTarget);
        float speedFactor = 1f + Noise(0f, tuning.babySpeedVariationFrequency) * 2f * tuning.babySpeedVariationAmount;
        // Slower and careful along a log.
        float speed = (onBalanceBeam ? tuning.babyBalanceWalkSpeed : tuning.babyWalkSpeed) * speedFactor;
        // Held back while not yet roughly facing the target, so a sharp turn reads as turning-then-walking
        // instead of sliding sideways into it (the floor is never 0: small heading corrections barely slow her).
        if (distanceRemaining > 0.001f)
        {
            float facingAlignment = Vector3.Dot(transform.forward, toTarget / distanceRemaining);
            float turnSlowdown = Mathf.Clamp01(Mathf.InverseLerp(-0.2f, 0.6f, facingAlignment));
            speed *= Mathf.Lerp(tuning.babyTurnSlowdownFloor, 1f, turnSlowdown);
        }
        // Eases from a stop and brakes toward the target instead of snapping straight to target speed: the
        // braking speed is capped by the classic stopping-distance curve for babyDecel over what's left to walk.
        // Floored so the last sliver of distance doesn't shrink the speed (and so the step) to nothing forever.
        float brakingSpeed = Mathf.Max(0.1f, Mathf.Sqrt(2f * Mathf.Max(tuning.babyDecel, 0.01f) * distanceRemaining));
        currentSpeed = Mathf.MoveTowards(currentSpeed, Mathf.Min(speed, brakingSpeed), tuning.babyAccel * Time.deltaTime);
        // The walk clip keeps pace with the movement (StandStill and the other states put it back to 1).
        float clipSpeed = onBalanceBeam ? tuning.babyBalanceClipSpeed : tuning.babyWalkClipSpeed;
        animator.speed = Mathf.Clamp(currentSpeed / Mathf.Max(clipSpeed, 0.01f), tuning.babyClipRateMin, tuning.babyClipRateMax);
        transform.position = Vector3.MoveTowards(transform.position, target, currentSpeed * Time.deltaTime);
        return transform.position == target;
    }

    // Standing still while a blocker leads the baby.
    public void StandStill()
    {
        animator.SetBool(WalkingParam, false);
        animator.speed = 1f;
        // So the next walk ramps up from a stop again, instead of resuming at whatever speed she had.
        currentSpeed = 0f;
    }

    // Stands and turns toward the bird. The model has no look-up animation yet.
    IEnumerator LookUp(float duration)
    {
        StandStill();
        for (float time = 0f; time < duration; time += Time.deltaTime)
        {
            TurnToward(BirdController.Instance.transform.position - transform.position);
            yield return null;
        }
    }

    void TurnToward(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), turnSpeed * Time.deltaTime);
    }
}
