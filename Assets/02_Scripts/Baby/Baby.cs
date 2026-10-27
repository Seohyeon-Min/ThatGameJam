using System;
using System.Collections;
using UnityEngine;

public enum BabyState { Sleeping, Waking, Walking, Waiting, Reacting, Resting }

// The baby: sleeps until the cry or its wake timer, walks its route, waits at blockers,
// and looks up at the bird whenever the bird was the one who helped (design doc §2.2, §4).
public class Baby : CryListener
{
    [SerializeField] NaturalTimer wakeTimer;
    [SerializeField] float turnSpeed = 360f;

    public static Baby Instance { get; private set; }

    public BabyState State { get; private set; }
    public NaturalTimer WakeTimer => wakeTimer;
    // The blocker the baby is standing in front of, else null.
    public PathBlocker CurrentBlocker { get; private set; }

    // Reached the last point of its route and fell asleep there.
    public event Action ReachedRest;

    BabyRoute route;

    protected override float CryRadius => GameTuning.Current.babyWakeCryRadius;

    void Awake()
    {
        Instance = this;
        wakeTimer.Resolved += byBird => StartCoroutine(WakeAndWalk(byBird));
        visualPosition = visual.localPosition;
        visualScale = visual.localScale;
    }

    public void PlaceAsleep(Transform start, BabyRoute route)
    {
        StopAllCoroutines();
        transform.SetPositionAndRotation(start.position, start.rotation);
        this.route = route;
        CurrentBlocker = null;
        State = BabyState.Sleeping;
        pose = Pose.Lying;
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
        if (byBird)
            yield return LookUp(tuning.babyLookUpDuration);
        else
        {
            pose = Pose.LookAround;
            yield return new WaitForSeconds(tuning.babyLookAroundDuration);
        }

        for (int i = 0; i < route.Count; i++)
        {
            State = BabyState.Walking;
            pose = Pose.Stand;
            yield return WalkTo(route[i].position);

            if (!route[i].TryGetComponent(out StopPoint stop))
                continue;

            // The bird may have opened it before the baby arrived (ADR 0003 Q4); the baby then only reacts.
            PathBlocker blocker = stop.Blocker;
            if (!blocker.IsOpen)
            {
                State = BabyState.Waiting;
                pose = WaitPoseToPose(stop.Pose);
                CurrentBlocker = blocker;
                blocker.BeginWaiting();
                yield return new WaitUntil(() => blocker.IsOpen);
                CurrentBlocker = null;
            }
            if (blocker.OpenedByBird)
            {
                State = BabyState.Reacting;
                yield return LookUp(tuning.babyLookUpDuration);
            }
        }

        State = BabyState.Resting;
        pose = Pose.Lying;
        ReachedRest?.Invoke();
    }

    IEnumerator WalkTo(Vector3 target)
    {
        while (transform.position != target)
        {
            Vector3 position = Vector3.MoveTowards(transform.position, target, GameTuning.Current.babyWalkSpeed * Time.deltaTime);
            TurnToward(target - transform.position);
            transform.position = position;
            yield return null;
        }
    }

    // Turns the body toward the bird; the head tilt is part of the pose.
    IEnumerator LookUp(float duration)
    {
        pose = Pose.LookUp;
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

    #region Placeholder poses: moves the grey capsule "Visual". Delete when the Animator arrives.

    enum Pose { Stand, Crouch, Sit, Lying, LookUp, LookAround }

    [SerializeField] Transform visual;

    Pose pose = Pose.Lying;
    Vector3 visualPosition;
    Vector3 visualScale;

    static Pose WaitPoseToPose(WaitPose waitPose) => waitPose switch
    {
        WaitPose.Crouch => Pose.Crouch,
        WaitPose.Sit => Pose.Sit,
        _ => Pose.Stand,
    };

    void LateUpdate()
    {
        Quaternion rotation = Quaternion.identity;
        Vector3 scale = visualScale;
        Vector3 position = visualPosition;
        switch (pose)
        {
            case Pose.Crouch:
                scale = Vector3.Scale(visualScale, new Vector3(1.15f, 0.55f, 1.15f));
                position *= 0.55f;
                break;
            case Pose.Sit:
                scale = Vector3.Scale(visualScale, new Vector3(1f, 0.7f, 1f));
                position *= 0.7f;
                rotation = Quaternion.Euler(-15f, 0f, 0f);
                break;
            case Pose.Lying:
                // Rustling grows with the wake signals (rustling, rubbing eyes, stretching).
                float wiggle = State == BabyState.Sleeping ? Mathf.Sin(Time.time * 6f) * 4f * wakeTimer.SignalLevel : 0f;
                rotation = Quaternion.Euler(0f, wiggle, 90f);
                position = new Vector3(0f, visualScale.x * 0.5f, 0f); // Unity capsule radius
                break;
            case Pose.LookUp:
                rotation = Quaternion.Euler(-25f, 0f, 0f);
                break;
            case Pose.LookAround:
                rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 4f) * 12f);
                break;
        }
        float blend = 1f - Mathf.Exp(-12f * Time.deltaTime);
        visual.SetLocalPositionAndRotation(
            Vector3.Lerp(visual.localPosition, position, blend),
            Quaternion.Slerp(visual.localRotation, rotation, blend));
        visual.localScale = Vector3.Lerp(visual.localScale, scale, blend);
    }

    #endregion
}
