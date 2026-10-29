using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// An object the bird lands on when it is left-clicked (the leaning log top, a snow-laden branch, a roadside tree's branch).
// Its click collider must be on the Interactable layer, on this object or a child.
public class Perchable : MonoBehaviour
{
    static readonly int CameraFadeExemptId = Shader.PropertyToID("_CameraFadeExempt");
    static readonly List<Perchable> all = new();

    [SerializeField] Transform perchPoint;
    // The bird also lands here by itself when the player leaves it idle (roadside branches). Not on devices, which the player solves.
    [SerializeField] bool idleSpot;
    [SerializeField] UnityEvent onPerched;

    public static IReadOnlyList<Perchable> All => all;

    // The bird has finished landing.
    public event Action Perched;
    // The object no longer holds the bird (for example the log starts to fall). The bird takes off.
    public event Action Released;

    public bool CanPerch { get; set; } = true;
    public Transform PerchPoint => perchPoint != null ? perchPoint : transform;
    public bool IsIdleSpot => idleSpot;

    // The whole object this perch belongs to (for a tree's branch, the tree itself), not just this collider's own
    // renderer, so camera-fade can be held off the whole thing instead of only the branch the bird's feet touch.
    Renderer[] wholeObjectRenderers;
    MaterialPropertyBlock cameraFadeBlock;

    void OnEnable() => all.Add(this);
    void OnDisable() => all.Remove(this);

    public void NotifyPerched()
    {
        SetCameraFadeExempt(true);
        onPerched.Invoke();
        Perched?.Invoke();
    }

    public void Release()
    {
        SetCameraFadeExempt(false);
        Released?.Invoke();
    }

    // While the bird stands here, this whole object never camera-fades (WatercolorFlat's _CameraFadeExempt),
    // however close that fade's trigger (the bird itself) is: otherwise the tree the bird is standing in would
    // fade along with whatever else is blocking the view, and the bird would read as floating on nothing.
    public void SetCameraFadeExempt(bool exempt)
    {
        if (wholeObjectRenderers == null)
        {
            Transform root = transform.parent != null ? transform.parent : transform;
            wholeObjectRenderers = root.GetComponentsInChildren<Renderer>();
            cameraFadeBlock = new MaterialPropertyBlock();
        }
        cameraFadeBlock.SetFloat(CameraFadeExemptId, exempt ? 1f : 0f);
        foreach (Renderer r in wholeObjectRenderers)
            r.SetPropertyBlock(cameraFadeBlock);
    }
}
