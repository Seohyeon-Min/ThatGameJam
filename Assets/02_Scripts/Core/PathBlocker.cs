using System;
using UnityEngine;

// Something on the baby's route that stops the baby until it opens.
// A StopPoint on the route refers to one. Opening comes after any opening animation, so the baby never walks into a half-open way.
public abstract class PathBlocker : MonoBehaviour
{
    // A line across the road; its Z is as far as the bird may fly while this is closed (ADR 0010). None: the bird is not held back.
    [SerializeField] Transform birdLimit;

    public event Action<bool> Opened;

    public Transform BirdLimit => birdLimit;

    public bool IsOpen { get; private set; }
    public bool OpenedByBird { get; private set; }

    // The timer that resolves this blocker, for dev tools. Null if there is none.
    public abstract NaturalTimer Timer { get; }

    // Whether the baby looks up at the bird after the bird opened this (design doc §4.2).
    public virtual bool LookUpWhenOpenedByBird => true;

    // The baby has stopped in front of this blocker.
    public abstract void BeginWaiting();

    protected void Open(bool byBird)
    {
        if (IsOpen)
            return;
        IsOpen = true;
        OpenedByBird = byBird;
        Opened?.Invoke(byBird);
    }
}
