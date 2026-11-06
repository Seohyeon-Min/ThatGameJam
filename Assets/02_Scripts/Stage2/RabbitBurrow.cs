using System;
using System.Collections;
using UnityEngine;

// The blocked rabbit burrow of stage 2 (design doc §8.2, ADR 0036). Rabbits on the road wait by the burrow, whose entrance snow keeps
// them from the rabbits inside. The snow goes as it is dug: from inside once a cry has called the rabbits there (they answer with the
// bird's own cry), and from outside while the bird stands at it pecking. Each alone takes GameTuning.burrowDigTime, both together half,
// and what is dug stays dug. Left alone, the rabbits inside start digging by themselves so they break through as the natural time runs
// out. Once through, they come out, take the road's rabbits in, and the road opens.
public class RabbitBurrow : PathBlocker
{
    [SerializeField] NaturalTimer timer;
    [Tooltip("Where the bird stands to clear the snow. Its click collider is the snow.")]
    [SerializeField] Perchable snowPerch;
    [Tooltip("The entrance's snow: it shrinks as it is dug and is gone once through.")]
    [SerializeField] Transform snow;
    [Tooltip("The rabbits inside answer a cry with this: the BirdCryPulse at the entrance. Every particle system in it plays.")]
    [SerializeField] GameObject answer;
    [Tooltip("The hole, where the rabbits come out and go in.")]
    [SerializeField] Transform hole;
    [Tooltip("The rabbits on the road.")]
    [SerializeField] BurrowRabbit[] roadRabbits;
    [Tooltip("The rabbits inside, hidden until the burrow is through.")]
    [SerializeField] BurrowRabbit[] burrowRabbits;
    [Tooltip("Where each rabbit from inside sits while the road's rabbits go in, in the order of Burrow Rabbits.")]
    [SerializeField] Transform[] waitPoints;
    [Tooltip("Seconds between the road's rabbits setting off for the hole.")]
    [SerializeField, Min(0f)] float stagger = 0.35f;
    [Tooltip("Snow thrown up while it is dug: a burst on each peck, and a little all the time the rabbits dig.")]
    [SerializeField] ParticleSystem digSnow;
    [Tooltip("While the burrow is shut, now and then a road rabbit hops over here, sniffs at the snow and hops back.")]
    [SerializeField] Transform pacePoint;
    [SerializeField] Vector2 paceSeconds = new(3f, 6f);

    public override NaturalTimer Timer => timer;

    ParticleSystem[] answerSystems;
    Vector3 snowScale;
    Quaternion snowRotation;
    float dug;
    bool called;
    bool birdHelped;
    bool opening;
    float nextPeck;
    float nextDigSnow;
    Coroutine pacing;

    void Awake()
    {
        answerSystems = answer.GetComponentsInChildren<ParticleSystem>(true);
        snowScale = snow.localScale;
        snowRotation = snow.localRotation;
        timer.Resolved += OnResolved;
    }

    void Start() => pacing = StartCoroutine(Pace());

    void OnDestroy() => timer.Resolved -= OnResolved;

    void OnEnable() => CryListener.Cried += OnCried;

    void OnDisable() => CryListener.Cried -= OnCried;

    public override void BeginWaiting() => timer.Begin();

    void OnCried(Vector3 origin)
    {
        if (opening)
            return;
        Vector3 offset = origin - hole.position;
        offset.y = 0f;
        float radius = GameTuning.Current.burrowCryRadius;
        if (offset.sqrMagnitude > radius * radius)
            return;
        called = true;
        birdHelped = true;
        foreach (ParticleSystem system in answerSystems)
            system.Play(false);
    }

    void Update()
    {
        if (opening)
            return;
        GameTuning tuning = GameTuning.Current;
        BirdController bird = BirdController.Instance;
        bool pecking = bird != null && bird.IsPerched && bird.Perch == snowPerch;
        if (pecking && Time.time >= nextPeck)
        {
            nextPeck = Time.time + bird.Peck(1) + 0.15f;
            digSnow.Emit(6);
        }
        bool digging = called || timer.IsRunning && timer.Elapsed >= timer.Duration - tuning.burrowDigTime;
        if (digging && Time.time >= nextDigSnow)
        {
            nextDigSnow = Time.time + 0.25f;
            digSnow.Emit(3);
        }
        birdHelped |= pecking;

        // Dug from inside, the snow trembles.
        snow.localRotation = digging
            ? snowRotation * Quaternion.Euler(Mathf.Sin(Time.time * 23f) * 2f, 0f, Mathf.Sin(Time.time * 19f) * 2f)
            : snowRotation;
        float rate = (digging ? 1f : 0f) + (pecking ? 1f : 0f);
        if (rate == 0f)
            return;
        dug = Mathf.Min(1f, dug + rate * Time.deltaTime / tuning.burrowDigTime);
        ShowSnow();
        if (dug < 1f)
            return;
        if (birdHelped)
            timer.ResolveByBird();
        else
            timer.ResolveNaturally();
    }

    void ShowSnow()
    {
        float shrink = 1f - 0.4f * dug;
        snow.localScale = new Vector3(snowScale.x * shrink, snowScale.y * (1f - dug), snowScale.z * shrink);
    }

    // The natural time can run out with the bird's late help still digging: that help counts.
    void OnResolved(bool byBird) => StartCoroutine(OpenWay(byBird || birdHelped));

    IEnumerator OpenWay(bool byBird)
    {
        opening = true;
        StopCoroutine(pacing);
        snowPerch.CanPerch = false;
        snowPerch.Release();
        snow.localRotation = snowRotation;
        // A dev shortcut resolves the timer directly: the rest of the snow still goes.
        float from = dug;
        const float rest = 0.5f;
        for (float t = 0f; t < rest && from < 1f; t += Time.deltaTime)
        {
            dug = Mathf.Lerp(from, 1f, t / rest);
            ShowSnow();
            yield return null;
        }
        snow.gameObject.SetActive(false);
        digSnow.Emit(20);

        yield return Together(burrowRabbits.Length, i => burrowRabbits[i].ComeOut(hole.position, waitPoints[i].position));
        yield return new WaitForSeconds(0.4f);
        yield return Together(roadRabbits.Length, i => Delayed(i * stagger, roadRabbits[i].GoIn(hole.position)));
        Open(byBird);
        yield return Together(burrowRabbits.Length, i => burrowRabbits[i].GoIn(hole.position));
    }

    // The road's rabbits want to go in: one at a time hops to the snow and back to its place, leaving tracks (FootprintMaker).
    // Only once its stage has come, so no tracks show up from nowhere while the burrow is still hidden.
    IEnumerator Pace()
    {
        Stage stage = GetComponentInParent<Stage>();
        while (true)
        {
            yield return new WaitForSeconds(UnityEngine.Random.Range(paceSeconds.x, paceSeconds.y));
            if (GameFlow.Instance == null || GameFlow.Instance.CurrentStage != stage)
                continue;
            BurrowRabbit rabbit = roadRabbits[UnityEngine.Random.Range(0, roadRabbits.Length)];
            Vector3 seat = rabbit.transform.position;
            Quaternion facing = rabbit.transform.rotation;
            yield return rabbit.HopTo(pacePoint.position + pacePoint.forward * UnityEngine.Random.Range(-0.3f, 0.3f));
            yield return new WaitForSeconds(1.5f);
            yield return rabbit.HopTo(seat);
            yield return rabbit.TurnTo(facing);
        }
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
