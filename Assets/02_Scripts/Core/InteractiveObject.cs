using System.Collections;
using FMODUnity;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

// Prototyping prop: reacts to a left click on its own collider, or to the bird's cry within cryRadius.
// Scatter copies of this to try interactions out before building the real per-stage device.
[RequireComponent(typeof(Collider))]
public class InteractiveObject : CryListener
{
    [SerializeField] float cryRadius = 3f;
    [Tooltip("Optional. Plays once on every reaction.")]
    [SerializeField] EventReference reactSound;
    [Tooltip("Hook up whatever the reaction should be here (an Animator trigger, a ParticleSystem, ...). A built-in scale bounce always plays too, so something is visible even unwired.")]
    [SerializeField] UnityEvent onReact;

    protected override float CryRadius => cryRadius;

    Collider ownCollider;
    Vector3 baseScale;

    void Awake()
    {
        ownCollider = GetComponent<Collider>();
        baseScale = transform.localScale;
    }

    void Update()
    {
        Mouse mouse = Mouse.current;
        if (Encyclopedia.IsOpen || mouse == null || !mouse.leftButton.wasPressedThisFrame)
            return;
        Ray ray = Camera.main.ScreenPointToRay(mouse.position.ReadValue());
        if (Physics.Raycast(ray, out RaycastHit hit) && hit.collider == ownCollider)
            React();
    }

    protected override void OnCry(Vector3 origin) => React();

    void React()
    {
        if (!reactSound.IsNull)
            RuntimeManager.PlayOneShot(reactSound, transform.position);
        onReact.Invoke();
        StopAllCoroutines();
        StartCoroutine(Bounce());
    }

    IEnumerator Bounce()
    {
        const float duration = 0.25f;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            transform.localScale = baseScale * (1f + 0.25f * Mathf.Sin(Mathf.PI * t / duration));
            yield return null;
        }
        transform.localScale = baseScale;
    }
}
