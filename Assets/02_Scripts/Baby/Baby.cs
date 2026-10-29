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
    static readonly int WalkState = Animator.StringToHash("Walk");
    static readonly int BalanceWalkState = Animator.StringToHash("BalanceWalk");

    [SerializeField] NaturalTimer wakeTimer;
    [SerializeField] float turnSpeed = 360f;
    [SerializeField] Animator animator;
    // The baby starts walking when this has played.
    [SerializeField] AnimationClip getUpClip;
    // The baby is asleep at the rest spot when this has played.
    [SerializeField] AnimationClip lieDownClip;

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
    float curiousPauseCooldown;
    // Per-instance offset into the noise fields below, so a second baby (if one ever exists) doesn't wobble in lockstep.
    float noiseSeed;
    Quaternion modelBaseRotation;

    protected override float CryRadius => GameTuning.Current.babyWakeCryRadius;

    void Awake()
    {
        Instance = this;
        wakeTimer.Resolved += byBird => StartCoroutine(WakeAndWalk(byBird));
        noiseSeed = UnityEngine.Random.Range(0f, 100f);
        modelBaseRotation = animator.transform.localRotation;
        curiousPauseCooldown = UnityEngine.Random.Range(GameTuning.Current.babyCuriousPauseIntervalMin, GameTuning.Current.babyCuriousPauseIntervalMax);
    }

    // Cold tremor on the model only (never on transform, which drives movement/arrival checks elsewhere).
    // Off once asleep/resting: curled up out of the wind by then.
    void Update()
    {
        if (State == BabyState.Sleeping || State == BabyState.Resting)
        {
            animator.transform.localRotation = modelBaseRotation;
            return;
        }
        GameTuning tuning = GameTuning.Current;
        float x = Noise(57f, tuning.babyShiverFrequency) * 2f * tuning.babyShiverAngle;
        float z = Noise(83f, tuning.babyShiverFrequency) * 2f * tuning.babyShiverAngle;
        animator.transform.localRotation = modelBaseRotation * Quaternion.Euler(x, 0f, z);
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
        transform.SetPositionAndRotation(spot.position, spot.rotation);
        // Already lying: no lying-down animation after the move.
        animator.Play(SleepState, 0, 0f);
        CurrentBlocker = null;
        State = BabyState.Resting;
        animator.SetBool(WalkingParam, false);
        animator.SetBool(LookAroundParam, false);
        animator.SetBool(AsleepParam, true);
        wakeTimer.ResetTimer();
    }

    // Asleep where it is, ready to wake and walk the route. The next stage starts this way where the baby fell asleep (ADR 0010).
    public void Sleep(BabyRoute route)
    {
        StopAllCoroutines();
        this.route = route;
        CurrentBlocker = null;
        State = BabyState.Sleeping;
        animator.SetBool(WalkingParam, false);
        animator.SetBool(LookAroundParam, false);
        animator.SetBool(AsleepParam, true);
        wakeTimer.ResetTimer();
        wakeTimer.Begin();
    }

    protected override void OnCry(Vector3 origin)
    {
        if (State == BabyState.Sleeping)
            wakeTimer.ResolveByBird();
        else if (State == BabyState.Walking)
            cryPending = true;
    }

    IEnumerator WakeAndWalk(bool byBird)
    {
        State = BabyState.Waking;
        GameTuning tuning = GameTuning.Current;
        animator.SetBool(AsleepParam, false);
        yield return new WaitForSeconds(getUpClip.length);
        if (byBird)
            yield return LookUp(tuning.babyLookUpDuration);
        else
        {
            animator.SetBool(LookAroundParam, true);
            yield return new WaitForSeconds(tuning.babyLookAroundDuration);
            animator.SetBool(LookAroundParam, false);
        }

        for (int i = 0; i < route.Count; i++)
        {
            State = BabyState.Walking;
            yield return WalkTo(route[i].position);

            if (onBalanceBeam && route[i].TryGetComponent(out BalanceBeamEnd _))
            {
                onBalanceBeam = false;
                animator.CrossFade(WalkState, 0.25f);
            }
            else if (!onBalanceBeam && route[i].TryGetComponent(out BalanceBeamStart _))
            {
                onBalanceBeam = true;
                animator.CrossFade(BalanceWalkState, 0.25f);
            }

            if (!route[i].TryGetComponent(out StopPoint stop))
                continue;

            // The bird may have opened it before the baby arrived (ADR 0003 Q4); the baby then only reacts.
            PathBlocker blocker = stop.Blocker;
            if (!blocker.IsOpen)
            {
                // Waits standing whatever the stop's pose: the model has no crouch or sit yet.
                State = BabyState.Waiting;
                StandStill();
                CurrentBlocker = blocker;
                blocker.BeginWaiting();
                yield return new WaitUntil(() => blocker.IsOpen);
                CurrentBlocker = null;
            }
            if (blocker.OpenedByBird && blocker.LookUpWhenOpenedByBird)
            {
                State = BabyState.Reacting;
                yield return LookUp(tuning.babyLookUpDuration);
            }
            // A fallen log or similar: arms-out balance until the far end (BalanceBeamEnd) is reached.
            if (blocker is LeaningLog)
            {
                onBalanceBeam = true;
                animator.CrossFade(BalanceWalkState, 0.25f);
            }
        }

        State = BabyState.Resting;
        animator.SetBool(WalkingParam, false);
        animator.SetBool(AsleepParam, true);
        yield return new WaitForSeconds(lieDownClip.length);
        ReachedRest?.Invoke();
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
                else if ((curiousPauseCooldown -= Time.deltaTime) <= 0f)
                {
                    yield return CuriousPause();
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

    // Pauses the walk to glance around, like a child noticing something nearby rather than marching straight through.
    IEnumerator CuriousPause()
    {
        GameTuning tuning = GameTuning.Current;
        curiousPauseCooldown = UnityEngine.Random.Range(tuning.babyCuriousPauseIntervalMin, tuning.babyCuriousPauseIntervalMax);
        StandStill();
        animator.SetBool(LookAroundParam, true);
        yield return new WaitForSeconds(tuning.babyLookAroundDuration);
        animator.SetBool(LookAroundParam, false);
    }

    // One frame of walking toward the target; true on arrival. Also lets the blocker the baby waits at lead it (the fog trail).
    // Heading wanders a little and pace breathes a little around babyWalkSpeed, purely cosmetic: transform.position still
    // closes in on the exact target, so arrival (and anything retracing this path, e.g. the fog trail) is unaffected.
    public bool StepToward(Vector3 target)
    {
        animator.SetBool(WalkingParam, true);
        GameTuning tuning = GameTuning.Current;
        Vector3 toTarget = target - transform.position;
        float wanderYaw = Noise(31f, tuning.babyWanderFrequency) * 2f * tuning.babyWanderAngle;
        TurnToward(Quaternion.Euler(0f, wanderYaw, 0f) * toTarget);
        float speedFactor = 1f + Noise(0f, tuning.babySpeedVariationFrequency) * 2f * tuning.babySpeedVariationAmount;
        transform.position = Vector3.MoveTowards(transform.position, target, tuning.babyWalkSpeed * speedFactor * Time.deltaTime);
        return transform.position == target;
    }

    // Standing still while a blocker leads the baby.
    public void StandStill() => animator.SetBool(WalkingParam, false);

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
