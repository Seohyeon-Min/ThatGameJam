using System;
using System.Collections;
using UnityEngine;

// Both the demonstration and the rabbit detour use this snow-laden branch (design doc §8).
public class SnowBranch : MonoBehaviour
{
    [SerializeField] Perchable perch;
    [SerializeField] Transform pivot;
    [SerializeField] Transform snow;
    [SerializeField] Vector3 raisedEuler;
    [SerializeField, Min(0f)] float springDuration = 0.65f;
    [SerializeField] ParticleSystem powder;

    bool shedding;

    public bool IsOpen { get; private set; }
    public event Action Cleared;
    public Perchable Perch => perch;

    void Awake()
    {
        if (powder != null)
            powder.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    void OnEnable() => perch.Perched += ShedSnow;
    void OnDisable() => perch.Perched -= ShedSnow;

    public void ShedSnow()
    {
        if (shedding || IsOpen)
            return;
        shedding = true;
        DisablePerch();
        snow.gameObject.SetActive(false);
        PlaySnow();
        StartCoroutine(Raise());
    }

    // The family's first waiting signal, not an effect that loops before the baby arrives.
    public void PulseSnow()
    {
        if (!shedding && !IsOpen && powder != null)
            powder.Emit(12);
    }

    public void DisablePerch()
    {
        perch.CanPerch = false;
        perch.Release();
    }

    void PlaySnow()
    {
        if (powder != null)
            powder.Emit(12);
    }

    IEnumerator Raise()
    {
        Quaternion bent = pivot.localRotation;
        Quaternion raised = Quaternion.Euler(raisedEuler);
        for (float elapsed = 0f; elapsed < springDuration; elapsed += Time.deltaTime)
        {
            float t = elapsed / springDuration;
            // Ease out with a small overshoot, then settle at the raised pose.
            float x = t - 1f;
            float spring = 1f + 2.2f * x * x * x + 1.2f * x * x;
            pivot.localRotation = Quaternion.SlerpUnclamped(bent, raised, spring);
            yield return null;
        }
        pivot.localRotation = raised;
        IsOpen = true;
        Cleared?.Invoke();
    }
}
