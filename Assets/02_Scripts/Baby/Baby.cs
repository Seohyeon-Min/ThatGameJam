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

    public void PlaceAsleep(Transform start, BabyRoute route)
    {
        StopAllCoroutines();
        transform.SetPositionAndRotation(start.position, start.rotation);
        this.route = route;
        CurrentBlocker = null;
        State = BabyState.Sleeping;
        animator.SetBool(WalkingParam, false);
        animator.SetBool(LookAroundParam, false);
        animator.SetBool(AsleepParam, true);
        // Already lying: no lying-down animation after the move.
        animator.Play(SleepState, 0, 0f);
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
            yield return null;
    }

    // One frame of walking toward the target; true on arrival. Also lets the blocker the baby waits at lead it (the fog trail).
    public bool StepToward(Vector3 target)
    {
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
