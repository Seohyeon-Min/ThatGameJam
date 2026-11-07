using System;
using System.Collections;
using UnityEngine;

// The rabbit burrow of stage 2 (design doc §8.2, ADR 0037). First, as the girl comes, the mother rabbit, out in front of the burrow,
// calls her young home, crying over and over at no beat (a yellow ring spreads each time). At each cry the young, scattered about
// her, prick their ears and hop a step after her: first to her side, then after her into the hole. The girl notices the first cry and
// trots a little that way. One of them slept through it,
// and wakes once the others are in: it sits on the way, afraid, wandering a little, not knowing where the hole is, and the girl stops
// for it. A cry near the hole is answered from inside after a short beat (a yellow ring at the mouth, the mother peeking out), and
// the little one pricks its ears, looks, runs home, and the way opens. Left alone, the mother calls it by herself as the natural time
// runs out. The bird never touches the rabbits: it only gets the family's voices to each other.
public class RabbitBurrow : PathBlocker
{
    enum Phase { Before, Calling, Lost, Home }

    [SerializeField] NaturalTimer timer;
    [Tooltip("The hole's mouth: where the rabbits go in, and where the mother peeks out.")]
    [SerializeField] Transform hole;

    [Header("First: the mother calls her young home")]
    [Tooltip("The girl coming by this point (in this stage) sets the mother calling.")]
    [SerializeField] Transform callTrigger;
    [SerializeField, Min(0.1f)] float callTriggerRadius = 1.2f;
    [SerializeField] BurrowRabbit mother;
    [Tooltip("Her call: the BirdCryPulse on her. Every particle system in it plays.")]
    [SerializeField] GameObject motherCall;
    [Tooltip("The young she calls in (not the one left behind).")]
    [SerializeField] BurrowRabbit[] young;
    [Tooltip("How far (m) a young rabbit hops after her at each of her cries.")]
    [SerializeField, Min(0.1f)] float youngStep = 1.6f;
    [Tooltip("Seconds between one young rabbit and the next, going in.")]
    [SerializeField, Min(0f)] float stagger = 0.3f;

    [Header("Then: the one left behind")]
    [Tooltip("Starts asleep on the way, wakes once the others are in.")]
    [SerializeField] BurrowRabbit lost;
    [Tooltip("How far (m) from its place it wanders, lost.")]
    [SerializeField, Min(0f)] float wanderRadius = 0.6f;
    [SerializeField] Vector2 wanderPause = new(1.2f, 2.8f);
    [Tooltip("The answer from inside (and the mother's own call, left alone): the BirdCryPulse at the mouth.")]
    [SerializeField] GameObject answer;
    [Tooltip("Small rings bubbling up at the mouth while a cry would be answered (the little one lost).")]
    [SerializeField] CryHint cryHint;

    public override NaturalTimer Timer => timer;

    Phase phase;
    bool girlWaiting;
    Coroutine wandering;
    bool[] youngStepping;
    Stage stage;

    void Awake()
    {
        stage = GetComponentInParent<Stage>();
        cryHint.Playing = false;
        timer.Resolved += OnResolved;
    }

    void OnDestroy() => timer.Resolved -= OnResolved;

    void OnEnable() => CryListener.Cried += OnCried;

    void OnDisable() => CryListener.Cried -= OnCried;

    public override void BeginWaiting()
    {
        girlWaiting = true;
        // Come here some other way than past the trigger (a dev skip): the call still comes first.
        if (phase == Phase.Before)
            StartCoroutine(CallYoung());
        else if (phase == Phase.Lost)
            timer.Begin();
    }

    void Update()
    {
        if (phase != Phase.Before || GameFlow.Instance == null || GameFlow.Instance.CurrentStage != stage || Baby.Instance == null)
            return;
        Vector3 offset = Baby.Instance.transform.position - callTrigger.position;
        offset.y = 0f;
        if (offset.sqrMagnitude <= callTriggerRadius * callTriggerRadius)
            StartCoroutine(CallYoung());
    }

    // Only the little one, lost, can be helped: while the mother is still calling the others the cry goes unanswered.
    void OnCried(Vector3 origin)
    {
        if (phase != Phase.Lost)
            return;
        Vector3 offset = origin - hole.position;
        offset.y = 0f;
        float radius = GameTuning.Current.burrowCryRadius;
        if (offset.sqrMagnitude <= radius * radius)
            timer.ResolveByBird();
    }

    IEnumerator CallYoung()
    {
        phase = Phase.Calling;
        // She cries over and over, as a player clicking at will would, until she is in, and at each cry the young hop a step
        // after her: first to her side, then after her into the hole.
        youngStepping = new bool[young.Length];
        StartCoroutine(MotherCries());
        // The girl hears it too, and trots a little her way (and from now on comes to the bird's cries the same way).
        if (Baby.Instance != null)
            Baby.Instance.NoticeCall(mother.transform);
        for (float t = 0f; t < 8f && !YoungGathered(); t += Time.deltaTime)
            yield return null;
        yield return new WaitForSeconds(0.2f);
        StartCoroutine(mother.GoIn(hole.position));
        while (mother.gameObject.activeSelf)
            yield return null;
        // She is in: the rest of the way, straight after her.
        yield return Together(young.Length, i => YoungGoIn(i));

        yield return new WaitForSeconds(0.8f);
        yield return lost.WakeUp();
        lost.SetAfraid(true);
        phase = Phase.Lost;
        cryHint.Playing = true;
        wandering = StartCoroutine(Wander());
        if (girlWaiting)
            timer.Begin();
    }

    bool YoungGathered()
    {
        foreach (BurrowRabbit rabbit in young)
        {
            Vector3 offset = rabbit.transform.position - mother.transform.position;
            offset.y = 0f;
            if (offset.magnitude > 0.7f)
                return false;
        }
        return true;
    }

    // Each young rabbit, hearing her, pricks its ears and hops a step (up to youngStep m) toward her side, unless still hopping.
    void YoungHear()
    {
        for (int i = 0; i < young.Length; i++)
            if (!youngStepping[i])
                StartCoroutine(YoungStep(i));
    }

    IEnumerator YoungStep(int i)
    {
        youngStepping[i] = true;
        BurrowRabbit rabbit = young[i];
        yield return new WaitForSeconds(UnityEngine.Random.Range(0.12f, 0.3f));
        rabbit.PrickEars();
        Vector3 her = mother.transform.position;
        Vector3 from = rabbit.transform.position - her;
        from.y = 0f;
        Vector3 beside = her + (from.sqrMagnitude > 0.0001f ? from.normalized : -mother.transform.forward) * 0.45f;
        if (Vector3.Distance(rabbit.transform.position, beside) > 0.15f)
        {
            yield return rabbit.TurnToward(beside, 0.2f);
            yield return rabbit.HopTo(Vector3.MoveTowards(rabbit.transform.position, beside, youngStep), true);
        }
        youngStepping[i] = false;
    }

    IEnumerator YoungGoIn(int i)
    {
        while (youngStepping[i])
            yield return null;
        yield return new WaitForSeconds(i * stagger);
        yield return young[i].GoIn(hole.position);
    }

    // Now a few quick cries, now a pause, at no beat, while she is still calling.
    IEnumerator MotherCries()
    {
        while (phase == Phase.Calling && mother.gameObject.activeSelf)
        {
            Cry(motherCall);
            YoungHear();
            yield return new WaitForSeconds(UnityEngine.Random.value < 0.2f
                ? UnityEngine.Random.Range(0.25f, 0.4f)
                : UnityEngine.Random.Range(0.9f, 1.6f));
        }
    }

    // One more cry from the pulse, over any still spreading: each system emits what its first burst would.
    static void Cry(GameObject pulse)
    {
        foreach (ParticleSystem system in pulse.GetComponentsInChildren<ParticleSystem>())
        {
            ParticleSystem.EmissionModule emission = system.emission;
            int count = emission.burstCount > 0 ? Mathf.Max(1, Mathf.RoundToInt(emission.GetBurst(0).count.constant)) : 1;
            system.Emit(count);
        }
    }

    // A little hop this way or that about its place, and a look round, over and over.
    IEnumerator Wander()
    {
        Vector3 seat = lost.transform.position;
        while (true)
        {
            yield return new WaitForSeconds(UnityEngine.Random.Range(wanderPause.x, wanderPause.y));
            Vector2 step = UnityEngine.Random.insideUnitCircle * wanderRadius;
            yield return lost.HopTo(seat + new Vector3(step.x, 0f, step.y));
        }
    }

    void OnResolved(bool byBird)
    {
        // Resolved before the call was over (a dev shortcut): the others are in already.
        bool skipCall = phase != Phase.Lost;
        phase = Phase.Home;
        cryHint.Playing = false;
        if (skipCall)
        {
            StopAllCoroutines();
            mother.gameObject.SetActive(false);
            foreach (BurrowRabbit rabbit in young)
                rabbit.gameObject.SetActive(false);
        }
        StartCoroutine(GoHome(byBird, skipCall));
    }

    IEnumerator GoHome(bool byBird, bool skipCall)
    {
        if (skipCall)
            yield return lost.WakeUp();
        if (wandering != null)
            StopCoroutine(wandering);
        lost.Halt();

        if (byBird)
            yield return new WaitForSeconds(GameTuning.Current.burrowAnswerDelay);
        Play(answer);
        StartCoroutine(mother.PeekOut(hole.position, lost.transform.position));
        yield return new WaitForSeconds(0.3f);
        lost.SetAfraid(false);
        lost.PrickEars();
        yield return lost.TurnToward(hole.position, 0.5f);
        yield return new WaitForSeconds(0.35f);
        // The mother goes back in just before it gets there, and it follows her down.
        float run = Vector3.Distance(lost.transform.position, hole.position) / lost.RunSpeed;
        StartCoroutine(Delayed(Mathf.Max(0f, run - 0.4f), mother.Sink()));
        yield return lost.GoIn(hole.position, true);
        Open(byBird);
    }

    static void Play(GameObject pulse)
    {
        foreach (ParticleSystem system in pulse.GetComponentsInChildren<ParticleSystem>(true))
            system.Play(false);
    }

    IEnumerator Together(int count, Func<int, IEnumerator> routine)
    {
        int left = count;
        for (int i = 0; i < count; i++)
            StartCoroutine(Then(routine(i), () => left--));
        while (left > 0)
            yield return null;
    }

    static IEnumerator Then(IEnumerator routine, Action done)
    {
        yield return routine;
        done();
    }

    static IEnumerator Delayed(float seconds, IEnumerator routine)
    {
        yield return new WaitForSeconds(seconds);
        yield return routine;
    }
}
