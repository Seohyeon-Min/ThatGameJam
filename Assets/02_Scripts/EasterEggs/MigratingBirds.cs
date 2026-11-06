using UnityEngine;

// Migrating birds (easter_eggs.md): held zoomed all the way out, a flock passes high across the view, its shadows sweeping the ground.
// Once per game, from the stage From Stage on.
public class MigratingBirds : MonoBehaviour
{
    static readonly int MovingParam = Animator.StringToHash("Moving");

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

    BirdCamera birdCamera;
    float zoomedOut;
    bool flown;
    Vector3 from, to;
    float travelled = -1f;

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
        foreach (Animator bird in flock.GetComponentsInChildren<Animator>())
            bird.SetBool(MovingParam, true);
        travelled = 0f;
    }

    void Fly()
    {
        float before = travelled;
        travelled += speed * Time.deltaTime;
        flock.position = Vector3.MoveTowards(from, to, travelled);
        if (before < halfCrossing && travelled >= halfCrossing)
            discovery.Reveal();
        if (travelled >= halfCrossing * 2f)
        {
            flock.gameObject.SetActive(false);
            travelled = -1f;
        }
    }
}
