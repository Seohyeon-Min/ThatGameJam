using UnityEngine;

// Runs the stages in order on one long road (ADR 0010). After the baby falls asleep at a stage's sunlit spot, the season effect plays
// and the next stage starts right there: the baby sleeps on until it is woken, and the bird may fly on into the new stage (design doc §10, §11).
public class GameFlow : MonoBehaviour
{
    public static GameFlow Instance { get; private set; }

    [SerializeField] Stage[] stages;
    [SerializeField] BirdCamera birdCamera;

    int current;

    public Stage CurrentStage => stages[current];

    // Where the bird may fly: every stage's road up to the current one, and in the current stage only up to its first closed blocker.
    // The stages' areas follow each other along +Z.
    public Bounds FlyArea
    {
        get
        {
            Bounds area = stages[0].area;
            for (int i = 1; i <= current; i++)
                area.Encapsulate(stages[i].area);
            Vector3 max = area.max;
            max.z = Mathf.Min(max.z, CurrentStage.BirdForwardLimit);
            area.SetMinMax(area.min, max);
            return area;
        }
    }

    void Awake() => Instance = this;

    void Start()
    {
        Baby.Instance.ReachedRest += OnReachedRest;
        Stage first = stages[0];
        Baby.Instance.PlaceAsleep(first.babyStart, first.route);
        BirdController.Instance.Teleport(first.birdStart.position);
        BirdController.Instance.SetAltitude(first.birdAltitude, true);
        BeginStage(0);
        birdCamera.Cut();
    }

    // Dev tool: as if the baby had walked this stage and fallen asleep at its sunlit spot. The bird comes along to the next stage.
    public void SkipToNextStage()
    {
        BabyRoute route = CurrentStage.route;
        Baby.Instance.RestAt(route[route.Count - 1]);
        if (!EndStage())
            return;
        BirdController.Instance.Teleport(CurrentStage.birdStart.position);
        BirdController.Instance.SetAltitude(CurrentStage.birdAltitude, true);
        birdCamera.Cut();
    }

    void OnReachedRest() => EndStage();

    // Plays the season effect and starts the next stage where the baby sleeps. False after the last stage.
    bool EndStage()
    {
        CurrentStage.seasonEffect.Invoke();
        if (current + 1 >= stages.Length)
        {
            Debug.Log("Last stage finished. The ending is not implemented yet (plan step 4).");
            return false;
        }
        BeginStage(current + 1);
        Baby.Instance.Sleep(CurrentStage.route);
        BirdController.Instance.SetAltitude(CurrentStage.birdAltitude, false);
        return true;
    }

    void BeginStage(int index)
    {
        current = index;
        birdCamera.SetStage(CurrentStage);
    }
}
