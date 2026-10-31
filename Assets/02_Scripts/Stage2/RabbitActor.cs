using System.Collections;
using UnityEngine;

// A sleeping rabbit that wakes only when the family leaves naturally.
public class RabbitActor : MonoBehaviour
{
    static readonly string[] SleepStates = { "Sleep", "Sleep_Flat", "Sleep_Side" };
    static readonly string[] WakeStates = { "WakeUp", "WakeUp_Flat", "WakeUp_Side" };

    [SerializeField] Animator animator;
    [SerializeField] AnimationClip wakeClip;
    [SerializeField, Range(0, 2)] int sleepVariant;
    [SerializeField] float hopSpeed = 0.6f;
    [SerializeField] Transform[] exitPoints;

    public bool HasLeft { get; private set; }

    bool leaving;
    Coroutine stirring;
    Quaternion sleepingRotation;

    void Awake()
    {
        animator.Play(SleepStates[sleepVariant], 0, 0f);
    }

    // A small turn while still playing the sleeping animation; this never wakes the rabbit.
    public void Stir()
    {
        if (leaving || stirring != null)
            return;
        sleepingRotation = transform.localRotation;
        stirring = StartCoroutine(StirSleeping());
    }

    IEnumerator StirSleeping()
    {
        const float duration = 0.6f;
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float angle = Mathf.Sin(elapsed / duration * Mathf.PI * 2f) * 3f;
            transform.localRotation = sleepingRotation * Quaternion.Euler(0f, angle, 0f);
            yield return null;
        }
        transform.localRotation = sleepingRotation;
        stirring = null;
    }

    public IEnumerator Leave()
    {
        if (leaving)
            yield break;
        leaving = true;
        if (stirring != null)
        {
            StopCoroutine(stirring);
            stirring = null;
            transform.localRotation = sleepingRotation;
        }
        animator.Play(WakeStates[sleepVariant], 0, 0f);
        yield return new WaitForSeconds(wakeClip.length);

        animator.CrossFadeInFixedTime("Hop", 0.25f);
        foreach (Transform point in exitPoints)
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
        animator.CrossFadeInFixedTime("SitIdle", 0.25f);
        HasLeft = true;
    }
}
