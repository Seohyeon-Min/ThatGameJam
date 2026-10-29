using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// An object the bird lands on when it is left-clicked (the leaning log top, a snow-laden branch, a roadside tree's branch).
// Its click collider must be on the Interactable layer, on this object or a child.
public class Perchable : MonoBehaviour
{
    static readonly List<Perchable> all = new();

    [SerializeField] Transform perchPoint;
    // The bird also lands here by itself when the player leaves it idle (roadside branches). Not on devices, which the player solves.
    [SerializeField] bool idleSpot;
    // While landing, the bird turns to face along the perch point's forward axis, whichever way is nearer its heading (roadside
    // branches: the axis runs across the branch, so its feet hold the branch). Off, it keeps the heading it flew in with (devices).
    [SerializeField] bool alignBird;
    [SerializeField] UnityEvent onPerched;

    public static IReadOnlyList<Perchable> All => all;

    // The bird has finished landing.
    public event Action Perched;
    // The object no longer holds the bird (for example the log starts to fall). The bird takes off.
    public event Action Released;

    public bool CanPerch { get; set; } = true;
    public Transform PerchPoint => perchPoint != null ? perchPoint : transform;
    public bool IsIdleSpot => idleSpot;
    public bool AlignsBird => alignBird;

    void OnEnable() => all.Add(this);
    void OnDisable() => all.Remove(this);

    public void NotifyPerched()
    {
        onPerched.Invoke();
        Perched?.Invoke();
    }

    public void Release() => Released?.Invoke();
}
