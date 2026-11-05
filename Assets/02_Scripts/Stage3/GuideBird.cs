using System;
using UnityEngine;

// The small bird on the stump past the fog (Bird2): it shows the girl where the way ends. It sits there as birds do, in its idle,
// minding its own business. Like the maze's animals it answers the bird's cry with the same cry (a BirdCryPulse under it, drawn
// over the fog): it is the next to answer once the bird is past the maze's last animal, wherever the bird is from there on, and
// CliffEnding has the girl hurry toward it then. When it sees her coming it takes off and flies away out of sight, and she sits on
// the stump in its place.
public class GuideBird : CryListener
{
    // Its Animator (Bird2.controller): PerchIdle by default, Takeoff and Fly.
    [SerializeField] Animator animator;
    [Tooltip("The maze's last animal along the road: it answers every cry from past it (further along +Z). Empty: cries within Cry Radius.")]
    [SerializeField] Transform answersPast;
    [Tooltip("With no Answers Past: it answers cries this close to it, in metres.")]
    [SerializeField, Min(0f)] float cryRadius = 12f;
    [Tooltip("The cry it answers with: the BirdCryPulse under it, on the AboveFog layer so it shows over the dream fog. Every particle system in it plays.")]
    [SerializeField] GameObject cry;
    [Tooltip("Flying away, it heads this way (world space; up and out over the cliff, say), at this speed in metres per second.")]
    [SerializeField] Vector3 flyAwayDirection = new(0.4f, 0.6f, 1f);
    [SerializeField, Min(0f)] float flyAwaySpeed = 3f;
    [Tooltip("Seconds after taking off until it is gone (turned off).")]
    [SerializeField, Min(0f)] float flyAwayTime = 3f;

    static readonly int TakeoffState = Animator.StringToHash("Takeoff");

    Vector3 startPosition;
    Quaternion startRotation;
    float flewAt = -1f;

    // When it first answered a cry: CliffEnding hurries the girl then.
    public event Action Answered;

    public bool HasAnswered { get; private set; }
    public bool HasFlown => flewAt >= 0f;

    // Past the last animal the distance doesn't count, only which side of it the bird is.
    protected override float CryRadius => answersPast != null ? 10000f : cryRadius;

    void Awake()
    {
        startPosition = transform.position;
        startRotation = transform.rotation;
    }

    protected override void OnCry(Vector3 origin)
    {
        if (HasFlown || answersPast != null && origin.z <= answersPast.position.z)
            return;
        if (cry != null)
        {
            // The pulse's ring and sparkles are separate systems on its children.
            foreach (ParticleSystem system in cry.GetComponentsInChildren<ParticleSystem>(true))
                system.Play(false);
        }
        else
            Debug.LogWarning("GuideBird: no Cry set, so it answers with nothing. Set it to the BirdCryPulse under it.", this);
        if (HasAnswered)
            return;
        HasAnswered = true;
        Answered?.Invoke();
    }

    // Takes off and flies away; gone after flyAwayTime.
    public void FlyAway()
    {
        if (HasFlown)
            return;
        flewAt = Time.time;
        if (animator != null)
            animator.CrossFade(TakeoffState, 0.1f);
        Vector3 flat = Vector3.ProjectOnPlane(flyAwayDirection, Vector3.up);
        if (flat.sqrMagnitude > 1e-4f)
            transform.rotation = Quaternion.LookRotation(flat);
    }

    // Back on the stump as at the start (CliffEnding, when a dev tool takes the girl back there).
    public void ResetToStump()
    {
        flewAt = -1f;
        HasAnswered = false;
        transform.SetPositionAndRotation(startPosition, startRotation);
        gameObject.SetActive(true);
    }

    void Update()
    {
        if (!HasFlown)
            return;
        float flying = Time.time - flewAt;
        // Off the stump once the takeoff has lifted it (the clip's first part crouches in place).
        if (flying > 0.25f)
            transform.position += flyAwayDirection.normalized * flyAwaySpeed * Time.deltaTime;
        if (flying >= flyAwayTime)
            gameObject.SetActive(false);
    }
}
