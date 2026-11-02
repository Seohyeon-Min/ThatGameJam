using System.Collections;
using UnityEngine;

// The small animal at one fork of the fog maze, a rabbit for now (ADR 0027). When the bird cries nearby it hops down the
// right way and disappears at the end, and the girl takes that way.
public class FogRabbit : CryListener
{
    static readonly int SitIdleState = Animator.StringToHash("SitIdle");
    static readonly int HopState = Animator.StringToHash("Hop");

    [SerializeField] Animator animator;
    // The bird has to cry about at the fork: flat distance, so its flying height does not matter.
    [SerializeField] float answerRadius = 3.5f;
    [SerializeField] float hopSpeed = 2f;
    // Where it hops, in order. It disappears at the last one.
    [SerializeField] Transform[] runPoints;

    // The cry has reached it, so the girl knows the right way at this fork, also if it is already gone.
    public bool HasRun { get; private set; }

    // Every cry reaches OnCry, which checks the flat distance itself.
    protected override float CryRadius => Mathf.Infinity;

    // The model's controller starts asleep, as the rabbit family uses it.
    void Awake() => animator.Play(SitIdleState, 0, 0f);

    protected override void OnCry(Vector3 origin)
    {
        Vector3 offset = origin - transform.position;
        offset.y = 0f;
        if (HasRun || offset.sqrMagnitude > answerRadius * answerRadius)
            return;
        HasRun = true;
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        animator.CrossFadeInFixedTime(HopState, 0.15f);
        foreach (Transform point in runPoints)
        {
            while (transform.position != point.position)
            {
                Vector3 direction = point.position - transform.position;
                direction.y = 0f;
                if (direction.sqrMagnitude > 0.0001f)
                    transform.rotation = Quaternion.LookRotation(direction);
                transform.position = Vector3.MoveTowards(transform.position, point.position, hopSpeed * Time.deltaTime);
                yield return null;
            }
        }
        gameObject.SetActive(false);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.6f, 0.2f);
        Vector3 previous = transform.position;
        foreach (Transform point in runPoints)
        {
            if (point == null)
                continue;
            Gizmos.DrawLine(previous, point.position);
            previous = point.position;
        }
    }
}
