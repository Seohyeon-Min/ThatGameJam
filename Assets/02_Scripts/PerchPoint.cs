using System.Collections.Generic;
using UnityEngine;

// A spot on a branch where the bird can sit. The indicator pulses while the bird is close enough to perch here.
public class PerchPoint : MonoBehaviour
{
    public static readonly List<PerchPoint> All = new();

    [SerializeField] GameObject indicator;
    [SerializeField] float pulseSpeed = 4f;
    [SerializeField] float pulseAmount = 0.2f;

    Vector3 indicatorScale;

    void Awake()
    {
        indicatorScale = indicator.transform.localScale;
        indicator.SetActive(false);
    }

    void OnEnable() => All.Add(this);

    void OnDisable() => All.Remove(this);

    public void SetHighlighted(bool highlighted) => indicator.SetActive(highlighted);

    void Update()
    {
        if (indicator.activeSelf)
            indicator.transform.localScale = indicatorScale * (1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount);
    }
}
