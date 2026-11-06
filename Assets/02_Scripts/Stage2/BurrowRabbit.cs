using System.Collections;
using UnityEngine;

// A rabbit of the blocked burrow (design doc §8.2). It sits about, now and then changing its idle, and hops where the burrow sends it.
// Started by the burrow, so the hop goes on while this rabbit is hidden in the hole.
public class BurrowRabbit : MonoBehaviour
{
    static readonly string[] Idles = { "SitIdle", "SitIdle_LookAround", "SitIdle_SniffGround", "SitIdle_Scratch", "Sniff" };

    [SerializeField] Animator animator;
    [SerializeField, Min(0.01f)] float hopSpeed = 0.5f;
    [SerializeField] Vector2 idleSeconds = new(3f, 6f);
    [Tooltip("Going into the hole, it sinks this far before it is gone.")]
    [SerializeField, Min(0f)] float sinkDepth = 0.3f;

    bool hopping;
    float nextIdle;

    void OnEnable() => PlayIdle();

    void Update()
    {
        if (!hopping && Time.time >= nextIdle)
            PlayIdle();
    }

    void PlayIdle()
    {
        animator.CrossFadeInFixedTime(Idles[Random.Range(0, Idles.Length)], 0.25f);
        nextIdle = Time.time + Random.Range(idleSeconds.x, idleSeconds.y);
    }

    public IEnumerator HopTo(Vector3 point)
    {
        hopping = true;
        animator.CrossFadeInFixedTime("Hop", 0.2f);
        while (transform.position != point)
        {
            Vector3 direction = point - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), 10f * Time.deltaTime);
            transform.position = Vector3.MoveTowards(transform.position, point, hopSpeed * Time.deltaTime);
            yield return null;
        }
        hopping = false;
        PlayIdle();
    }

    public IEnumerator TurnTo(Quaternion rotation)
    {
        Quaternion from = transform.rotation;
        const float seconds = 0.4f;
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            transform.rotation = Quaternion.Slerp(from, rotation, t / seconds);
            yield return null;
        }
        transform.rotation = rotation;
    }

    // From hidden in the hole out to the point.
    public IEnumerator ComeOut(Vector3 hole, Vector3 point)
    {
        transform.position = hole;
        gameObject.SetActive(true);
        yield return HopTo(point);
    }

    // Hops to the hole and sinks into it.
    public IEnumerator GoIn(Vector3 hole)
    {
        yield return HopTo(hole);
        hopping = true;
        Vector3 top = transform.position;
        const float seconds = 0.3f;
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            transform.position = top + Vector3.down * (sinkDepth * t / seconds);
            yield return null;
        }
        gameObject.SetActive(false);
    }
}
