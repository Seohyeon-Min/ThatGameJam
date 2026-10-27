using System.Collections;
using UnityEngine;

// The leaning dead tree by the water (design doc §7.3). It falls toward the water and becomes the baby's bridge,
// when the bird perches on top or when its timer runs out (the wind wins).
public class LeaningLog : PathBlocker
{
    [SerializeField] NaturalTimer timer;
    [SerializeField] Perchable top;
    // At the base; this is what rotates.
    [SerializeField] Transform pivot;
    // Local euler angles of the pivot once fallen.
    [SerializeField] Vector3 fallenEuler;
    [SerializeField] float fallDuration = 1.2f;
    // Sway at the end of the timer, in degrees along the fall direction.
    [SerializeField] float maxSwayAngle = 4f;
    [SerializeField] float swaySpeed = 1.5f;

    Quaternion standing;
    Quaternion fallen;

    public override NaturalTimer Timer => timer;

    void Awake()
    {
        standing = pivot.localRotation;
        fallen = Quaternion.Euler(fallenEuler);
        top.Perched += timer.ResolveByBird;
        timer.Resolved += byBird => StartCoroutine(Fall(byBird));
    }

    public override void BeginWaiting() => timer.Begin();

    // Sways along the fall direction, more as the wind grows.
    void Update()
    {
        if (!timer.IsRunning)
            return;
        float angle = Mathf.Sin(Time.time * swaySpeed * 2f * Mathf.PI) * maxSwayAngle * timer.Progress;
        pivot.localRotation = Quaternion.SlerpUnclamped(standing, fallen, angle / Quaternion.Angle(standing, fallen));
    }

    IEnumerator Fall(bool byBird)
    {
        top.Release();
        top.CanPerch = false;
        Quaternion from = pivot.localRotation;
        for (float time = 0f; time < fallDuration; time += Time.deltaTime)
        {
            float t = time / fallDuration;
            pivot.localRotation = Quaternion.Slerp(from, fallen, t * t);
            yield return null;
        }
        pivot.localRotation = fallen;
        Open(byBird);
    }
}
