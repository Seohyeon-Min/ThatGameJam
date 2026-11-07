using System.Collections;
using UnityEngine;

// A rabbit of the stage 2 burrow (design doc §8.2). It sits about, now and then changing its idle, and hops where the burrow sends it.
// Over its clips it can prick its ears up or lay them back (turning the ear bones in LateUpdate), and shiver. One left behind can
// start asleep, wake, and sit about afraid. Started by the burrow, so a hop goes on while this rabbit is hidden in the hole.
public class BurrowRabbit : MonoBehaviour
{
    static readonly string[] Idles = { "SitIdle", "SitIdle_LookAround", "SitIdle_SniffGround", "SitIdle_Scratch", "Sniff" };
    // Afraid, it only sits looking about.
    static readonly string[] AfraidIdles = { "SitIdle_LookAround", "SitIdle" };

    [SerializeField] Animator animator;
    [SerializeField, Min(0.01f)] float hopSpeed = 0.5f;
    [Tooltip("Hopping fast: called home by its family.")]
    [SerializeField, Min(0.01f)] float runSpeed = 1.6f;
    [SerializeField] Vector2 idleSeconds = new(3f, 6f);
    [Tooltip("Going into the hole, it sinks this far before it is gone. Peeking out, it comes up from this deep.")]
    [SerializeField, Min(0f)] float sinkDepth = 0.3f;
    [Tooltip("The root bone of each ear (Ear1.L, Ear1.R): turned up when pricked, back when afraid.")]
    [SerializeField] Transform[] ears;
    [Tooltip("Sleeps where it is at the start, until the burrow wakes it.")]
    [SerializeField] bool startAsleep;
    [Tooltip("Seconds the wake-up clip takes.")]
    [SerializeField, Min(0f)] float wakeSeconds = 2.6f;
    [Tooltip("How far (m) and how fast it shakes while afraid.")]
    [SerializeField, Min(0f)] float shiverAmplitude = 0.006f;
    [SerializeField, Min(0f)] float shiverFrequency = 38f;

    public float RunSpeed => runSpeed;

    bool hopping;
    bool asleep;
    // Woken once: a rabbit that started asleep does not fall asleep again when it comes back on.
    bool woken;
    bool afraid;
    float nextIdle;
    // -1 laid back, 0 as the clip has them, 1 pricked up; and how far they have got there.
    float earTarget;
    float earLift;
    float shiver;
    Vector3 modelPosition;

    void Awake() => modelPosition = animator.transform.localPosition;

    void OnEnable()
    {
        if (startAsleep && !woken)
        {
            asleep = true;
            animator.Play("Sleep", 0, Random.value);
            return;
        }
        if (!asleep)
            PlayIdle();
    }

    void Update()
    {
        if (!hopping && !asleep && Time.time >= nextIdle)
            PlayIdle();
    }

    void LateUpdate()
    {
        earLift = Mathf.MoveTowards(earLift, earTarget, 5f * Time.deltaTime);
        if (Mathf.Abs(earLift) > 0.001f)
            TurnEars();
        shiver = Mathf.MoveTowards(shiver, afraid && !hopping ? 1f : 0f, 3f * Time.deltaTime);
        if (animator.transform == transform)
            return;
        Vector3 shake = Vector3.zero;
        if (shiver > 0f)
        {
            float t = Time.time * shiverFrequency;
            shake = new Vector3(Mathf.Sin(t), 0f, Mathf.Sin(t * 1.13f + 1.7f) * 0.6f) * (shiverAmplitude * shiver);
        }
        animator.transform.localPosition = modelPosition + shake;
    }

    // Each ear (from its root to the next bone) toward straight up, or back along the body, over what the clip has it at.
    void TurnEars()
    {
        Vector3 back = (-transform.forward + transform.up * 0.25f).normalized;
        Vector3 goal = earLift > 0f ? Vector3.up : back;
        float amount = Mathf.Abs(earLift) * (earLift > 0f ? 1f : 0.7f);
        foreach (Transform ear in ears)
        {
            if (ear == null || ear.childCount == 0)
                continue;
            Vector3 along = ear.GetChild(0).position - ear.position;
            if (along.sqrMagnitude < 1e-8f)
                continue;
            along.Normalize();
            ear.rotation = Quaternion.FromToRotation(along, Vector3.Slerp(along, goal, amount)) * ear.rotation;
        }
    }

    void PlayIdle()
    {
        string[] idles = afraid ? AfraidIdles : Idles;
        animator.CrossFadeInFixedTime(idles[Random.Range(0, idles.Length)], 0.25f);
        nextIdle = Time.time + Random.Range(idleSeconds.x, idleSeconds.y);
    }

    // Ears up: it has heard something.
    public void PrickEars() => earTarget = 1f;

    // Afraid: ears back, shivering, only looking about. Not afraid: ears as the clip has them.
    public void SetAfraid(bool value)
    {
        afraid = value;
        earTarget = value ? -1f : 0f;
        if (!hopping && !asleep)
            PlayIdle();
    }

    public IEnumerator WakeUp()
    {
        if (!asleep)
            yield break;
        animator.CrossFadeInFixedTime("WakeUp", 0.3f);
        yield return new WaitForSeconds(wakeSeconds);
        asleep = false;
        woken = true;
        PlayIdle();
    }

    // Stops where it is, a hop cut short (its coroutine stopped) included.
    public void Halt()
    {
        if (!hopping)
            return;
        hopping = false;
        animator.speed = 1f;
        PlayIdle();
    }

    public IEnumerator HopTo(Vector3 point, bool run = false)
    {
        hopping = true;
        animator.CrossFadeInFixedTime("Hop", 0.2f);
        float speed = run ? runSpeed : hopSpeed;
        animator.speed = run ? Mathf.Clamp(runSpeed / hopSpeed, 1f, 2f) : 1f;
        while (transform.position != point)
        {
            Vector3 direction = point - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), 10f * Time.deltaTime);
            transform.position = Vector3.MoveTowards(transform.position, point, speed * Time.deltaTime);
            yield return null;
        }
        hopping = false;
        animator.speed = 1f;
        PlayIdle();
    }

    public IEnumerator TurnTo(Quaternion rotation, float seconds = 0.4f)
    {
        Quaternion from = transform.rotation;
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            transform.rotation = Quaternion.Slerp(from, rotation, t / seconds);
            yield return null;
        }
        transform.rotation = rotation;
    }

    public IEnumerator TurnToward(Vector3 point, float seconds = 0.4f)
    {
        Vector3 direction = point - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
            yield break;
        yield return TurnTo(Quaternion.LookRotation(direction), seconds);
    }

    // Hops to the hole and sinks into it.
    public IEnumerator GoIn(Vector3 hole, bool run = false)
    {
        yield return HopTo(hole, run);
        yield return Sink();
    }

    // From hidden in the hole, up to sit in its mouth looking toward the point.
    public IEnumerator PeekOut(Vector3 hole, Vector3 lookAt)
    {
        Vector3 direction = lookAt - hole;
        direction.y = 0f;
        transform.SetPositionAndRotation(hole + Vector3.down * sinkDepth,
            direction.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(direction) : transform.rotation);
        gameObject.SetActive(true);
        animator.CrossFadeInFixedTime("Sniff", 0f);
        nextIdle = Time.time + 30f;
        const float seconds = 0.45f;
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            transform.position = hole + Vector3.down * (sinkDepth * (1f - Mathf.SmoothStep(0f, 1f, t / seconds)));
            yield return null;
        }
        transform.position = hole;
    }

    // Down into the hole from where it sits, and gone.
    public IEnumerator Sink()
    {
        hopping = true;
        Vector3 top = transform.position;
        const float seconds = 0.3f;
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            transform.position = top + Vector3.down * (sinkDepth * t / seconds);
            yield return null;
        }
        hopping = false;
        gameObject.SetActive(false);
    }
}
