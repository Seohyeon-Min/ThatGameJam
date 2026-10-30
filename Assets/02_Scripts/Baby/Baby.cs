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
    static readonly int QuietWaitParam = Animator.StringToHash("QuietWait");

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

    protected override float CryRadius => GameTuning.Current.babyWakeCryRadius;

    void Awake()
    {
        Instance = this;
        wakeTimer.Resolved += byBird => StartCoroutine(WakeAndWalk(byBird));
    }

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
        animator.SetBool(QuietWaitParam, false);
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
        animator.SetBool(QuietWaitParam, false);
        animator.SetBool(AsleepParam, true);
        wakeTimer.ResetTimer();
        wakeTimer.Begin();
    }

    protected override void OnCry(Vector3 origin)
    {
        if (State == BabyState.Sleeping)
            wakeTimer.ResolveByBird();
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
                while (!blocker.IsOpen)
                {
                    if (stop.ObserveTarget != null)
                        TurnToward(stop.ObserveTarget.position - transform.position);
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
        }

        State = BabyState.Resting;
        animator.SetBool(WalkingParam, false);
        animator.SetBool(QuietWaitParam, false);
        animator.SetBool(AsleepParam, true);
        yield return new WaitForSeconds(lieDownClip.length);
        ReachedRest?.Invoke();
    }

    IEnumerator WalkTo(Vector3 target)
    {
        while (!StepToward(target))
            yield return null;
    }

    // One frame of walking toward the target; true on arrival. Also lets the blocker the baby waits at lead it (the fog trail).
    public bool StepToward(Vector3 target)
    {
        animator.SetBool(QuietWaitParam, false);
        animator.SetBool(WalkingParam, true);
        TurnToward(target - transform.position);
        transform.position = Vector3.MoveTowards(transform.position, target, GameTuning.Current.babyWalkSpeed * Time.deltaTime);
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
