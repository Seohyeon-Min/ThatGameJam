using UnityEngine;

// Migrating birds (easter_eggs.md): held zoomed all the way out, a flock passes high across the view, its shadows sweeping the ground.
// Once per game, from the stage From Stage on.
public class MigratingBirds : MonoBehaviour
{
    static readonly int MovingParam = Animator.StringToHash("Moving");
    static readonly int GlideFlapState = Animator.StringToHash("GlideFlap");

    [SerializeField] Discovery discovery;
    [Tooltip("Hidden until it flies. Its children are the birds.")]
    [SerializeField] Transform flock;
    [Tooltip("The first stage it may fly in, by GameFlow's order (0 the intro, 1 the first stage).")]
    [SerializeField, Min(0)] int fromStage = 1;
    [Tooltip("Seconds zoomed all the way out before it comes.")]
    [SerializeField, Min(0f)] float waitSeconds = 2f;
    [Tooltip("Height above the ground at the middle of the view.")]
    [SerializeField] float height = 7f;
    [Tooltip("How far either side of the middle of the view it starts and ends, in metres.")]
    [SerializeField, Min(1f)] float halfCrossing = 25f;
    [SerializeField, Min(0.1f)] float speed = 6f;
    [Tooltip("Seconds between wing beats while it flies. Between them it glides. Each bird strays from this on its own (Flap Variation), so the flock doesn't beat its wings in lockstep.")]
    [SerializeField, Min(0.5f)] float flapEvery = 2f;
    [Tooltip("How far each bird's own flap interval may randomly stray from Flap Every, as a fraction either way (0.4 = 60%-140% of it).")]
    [SerializeField, Range(0f, 0.9f)] float flapVariation = 0.4f;
    [Tooltip("How far each bird bobs and drifts off its formation spot, in metres, so the flock doesn't fly as one rigid shape.")]
    [SerializeField, Min(0f)] float wander = 0.3f;
    [Tooltip("How fast that bob/drift cycles.")]
    [SerializeField, Min(0.01f)] float wanderFrequency = 0.4f;

    BirdCamera birdCamera;
    float zoomedOut;
    bool flown;
    Vector3 from, to;
    float travelled = -1f;
    Animator[] birds;
    // Each bird's own formation spot (its authored localPosition under flock), the lateral/vertical bob offset
    // from it, and when it next beats its wings - all per-bird, so the flock reads as individuals, not a drill team.
    Vector3[] basePositions;
    float[] noiseSeeds;
    float[] nextFlapIn;

    void Start() => birdCamera = FindAnyObjectByType<BirdCamera>();

    void Update()
    {
        if (travelled >= 0f)
        {
            Fly();
            return;
        }
        if (flown || StageIndex() < fromStage)
            return;
        zoomedOut = birdCamera.IsZoomedAllTheWayOut ? zoomedOut + Time.deltaTime : 0f;
        if (zoomedOut >= waitSeconds)
            Begin();
    }

    static int StageIndex()
    {
        GameFlow flow = GameFlow.Instance;
        for (int i = 0; i < flow.Stages.Count; i++)
            if (flow.Stages[i] == flow.CurrentStage)
                return i;
        return 0;
    }

    // Across the view, along the camera's right, over the ground at its middle.
    void Begin()
    {
        flown = true;
        Camera view = Camera.main;
        Vector3 ground = BirdController.Instance.transform.position;
        Ray middle = view.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (new Plane(Vector3.up, GameFlow.Instance.CurrentStage.area.center).Raycast(middle, out float distance))
            ground = middle.GetPoint(distance);
        Vector3 across = Vector3.ProjectOnPlane(view.transform.right, Vector3.up).normalized;
        Vector3 centre = ground + Vector3.up * height;
        from = centre - across * halfCrossing;
        to = centre + across * halfCrossing;
        flock.SetPositionAndRotation(from, Quaternion.LookRotation(across));
        flock.gameObject.SetActive(true);
        birds = flock.GetComponentsInChildren<Animator>();
        basePositions = new Vector3[birds.Length];
        noiseSeeds = new float[birds.Length];
        nextFlapIn = new float[birds.Length];
        for (int i = 0; i < birds.Length; i++)
        {
            birds[i].SetBool(MovingParam, true);
            basePositions[i] = birds[i].transform.localPosition;
            noiseSeeds[i] = Random.Range(0f, 100f);
            // Staggered first flap, not all at once on the first beat either.
            nextFlapIn[i] = Random.Range(0f, flapEvery);
        }
        travelled = 0f;
    }

    void Fly()
    {
        float before = travelled;
        travelled += speed * Time.deltaTime;
        flock.position = Vector3.MoveTowards(from, to, travelled);
        for (int i = 0; i < birds.Length; i++)
        {
            // A gentle, individual bob and drift off the formation spot - smooth noise, each bird its own phase.
            float x = (Mathf.PerlinNoise(noiseSeeds[i], Time.time * wanderFrequency) - 0.5f) * 2f * wander;
            float y = (Mathf.PerlinNoise(noiseSeeds[i] + 50f, Time.time * wanderFrequency) - 0.5f) * 2f * wander;
            birds[i].transform.localPosition = basePositions[i] + new Vector3(x, y, 0f);

            nextFlapIn[i] -= Time.deltaTime;
            if (nextFlapIn[i] <= 0f)
            {
                nextFlapIn[i] = flapEvery * Random.Range(1f - flapVariation, 1f + flapVariation);
                birds[i].CrossFadeInFixedTime(GlideFlapState, 0.15f);
            }
        }
        if (before < halfCrossing && travelled >= halfCrossing)
            discovery.Reveal();
        if (travelled >= halfCrossing * 2f)
        {
            flock.gameObject.SetActive(false);
            travelled = -1f;
        }
    }
}
