using System;
using UnityEngine;
using UnityEngine.Events;

// An object the bird lands on when it is left-clicked (the leaning log top, a snow-laden branch).
// Its click collider must be on the Interactable layer, on this object or a child.
public class Perchable : MonoBehaviour
{
    [SerializeField] Transform perchPoint;
    [SerializeField] UnityEvent onPerched;

    // The bird has finished landing.
    public event Action Perched;
    // The object no longer holds the bird (for example the log starts to fall). The bird takes off.
    public event Action Released;

    public bool CanPerch { get; set; } = true;
    public Transform PerchPoint => perchPoint != null ? perchPoint : transform;

    public void NotifyPerched()
    {
        onPerched.Invoke();
        Perched?.Invoke();
    }

    public void Release() => Released?.Invoke();
}
