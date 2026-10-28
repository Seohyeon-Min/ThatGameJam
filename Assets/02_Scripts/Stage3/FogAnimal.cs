using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// A small animal hidden on walkable ground in the fog (design doc §9.3). It answers the cry with a sound and a short glow
// above the fog, and steps aside once when the baby comes close.
public class FogAnimal : CryListener
{
    // Design TBD (§16).
    [SerializeField] float cryRadius = 5f;
    [SerializeField] UnityEvent onAnswer;
    // A child above the fog, shown while answering.
    [SerializeField] GameObject glow;
    [SerializeField] float glowDuration = 1f;
    [SerializeField] float stepAsideRange = 1.5f;
    [SerializeField] float stepAsideDistance = 1f;
    [SerializeField] float stepAsideDuration = 0.5f;

    float glowEnd;
    bool steppedAside;

    protected override float CryRadius => cryRadius;

    protected override void OnCry(Vector3 origin) => Answer();

    public void Answer()
    {
        onAnswer.Invoke();
        glowEnd = Time.time + glowDuration;
        glow.SetActive(true);
    }

    void Update()
    {
        if (glow.activeSelf && Time.time >= glowEnd)
            glow.SetActive(false);

        if (steppedAside)
            return;
        Transform baby = Baby.Instance.transform;
        Vector3 toAnimal = transform.position - baby.position;
        toAnimal.y = 0f;
        if (toAnimal.sqrMagnitude > stepAsideRange * stepAsideRange)
            return;

        // Sideways to the baby's way, on the side the animal already is.
        Vector3 side = baby.right;
        side.y = 0f;
        side.Normalize();
        if (Vector3.Dot(side, toAnimal) < 0f)
            side = -side;
        steppedAside = true;
        StartCoroutine(StepAside(transform.position + side * stepAsideDistance));
    }

    IEnumerator StepAside(Vector3 target)
    {
        Vector3 from = transform.position;
        for (float time = 0f; time < stepAsideDuration; time += Time.deltaTime)
        {
            transform.position = Vector3.Lerp(from, target, Mathf.SmoothStep(0f, 1f, time / stepAsideDuration));
            yield return null;
        }
        transform.position = target;
    }
}
