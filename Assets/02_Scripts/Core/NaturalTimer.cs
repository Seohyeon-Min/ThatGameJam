using System;
using UnityEngine;
using UnityEngine.Events;

// Time until an obstacle resolves by itself, with three waiting signals that grow stronger on the way (design doc §3).
// The bird can resolve it early. Resolved reports which of the two happened.
public class NaturalTimer : MonoBehaviour
{
    [SerializeField] NaturalTimerKind kind;
    [SerializeField] UnityEvent signal1;
    [SerializeField] UnityEvent signal2;
    [SerializeField] UnityEvent signal3;
    [SerializeField] UnityEvent<bool> onResolved;

    public event Action<bool> Resolved;

    public bool IsRunning { get; private set; }
    public bool IsResolved { get; private set; }
    public bool ResolvedByBird { get; private set; }
    public float Elapsed { get; private set; }
    public float Duration => GameTuning.Current.NaturalTime(kind);
    public float Progress => Mathf.Clamp01(Elapsed / Duration);
    // 0 before the first signal, then 1, 2, 3.
    public int SignalLevel { get; private set; }

    // Starts counting. Does nothing once started or resolved.
    public void Begin()
    {
        if (!IsResolved)
            IsRunning = true;
    }

    public void ResolveByBird() => Resolve(true);

    public void ResolveNaturally() => Resolve(false);

    // Back to the unstarted state, for timers that run once per stage (the baby's wake timer).
    public void ResetTimer()
    {
        IsRunning = false;
        IsResolved = false;
        ResolvedByBird = false;
        Elapsed = 0f;
        SignalLevel = 0;
    }

    void Update()
    {
        if (!IsRunning)
            return;

        Elapsed += Time.deltaTime;
        GameTuning tuning = GameTuning.Current;
        if (SignalLevel < 1 && Progress >= tuning.signal1At)
            RaiseSignal(1, signal1);
        if (SignalLevel < 2 && Progress >= tuning.signal2At)
            RaiseSignal(2, signal2);
        if (SignalLevel < 3 && Progress >= tuning.signal3At)
            RaiseSignal(3, signal3);
        if (Elapsed >= Duration)
            Resolve(false);
    }

    void RaiseSignal(int level, UnityEvent signal)
    {
        SignalLevel = level;
        signal.Invoke();
    }

    void Resolve(bool byBird)
    {
        if (IsResolved)
            return;
        IsRunning = false;
        IsResolved = true;
        ResolvedByBird = byBird;
        onResolved.Invoke(byBird);
        Resolved?.Invoke(byBird);
    }
}
