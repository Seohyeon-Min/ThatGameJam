using System;
using UnityEngine;

// Something on the baby's route that stops the baby until it opens.
// A StopPoint on the route refers to one. Opening comes after any opening animation, so the baby never walks into a half-open way.
public abstract class PathBlocker : MonoBehaviour
{
    public event Action<bool> Opened;

    public bool IsOpen { get; private set; }
    public bool OpenedByBird { get; private set; }

    // The timer that resolves this blocker, for dev tools. Null if there is none.
    public abstract NaturalTimer Timer { get; }

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
