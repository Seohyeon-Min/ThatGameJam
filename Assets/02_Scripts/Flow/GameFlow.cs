using UnityEngine;

// Runs the stages in order on one long road (ADR 0010). After the baby falls asleep at a stage's sunlit spot, the season advances
// and the next stage starts right there: the baby sleeps on until it is woken, and the bird may fly on into the new stage (design doc §10, §11).
public class GameFlow : MonoBehaviour
{
    public static GameFlow Instance { get; private set; }

    [SerializeField] Stage[] stages;
    [SerializeField] BirdCamera birdCamera;
    [SerializeField] Season season;

    int current;
    // The last stage has ended; nothing more happens (the ending is not built yet).
    bool finished;

    public Stage CurrentStage => stages[current];

    // Where the bird may fly: every stage's area up to the current one, and in the current stage only up to its sunlit spot (ADR 0012).
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

    // Dev tool: the baby lies down at this stage's sunlit spot, so the end of the stage plays out as usual. The bird and camera go there.
    public void LieDownAtSunlitSpot()
    {
        BabyRoute route = CurrentStage.route;
        Transform spot = route[route.Count - 1];
        Baby.Instance.LieDownAt(spot);
        birdCamera.MoveLock(spot.position);
        BirdController.Instance.Teleport(spot.position);
        BirdController.Instance.SetAltitude(CurrentStage.birdAltitude, true);
        birdCamera.Cut();
    }

    // Dev tool: back one stage and one season, to watch the change again. The baby sleeps at that stage's start. Opened devices
    // stay open: this is for the season change, not for playing the stage over.
    public void BackOneStage()
    {
        if (finished)
            finished = false;
        else if (current > 0)
            BeginStage(current - 1);
        else
            return;
        season.SetSeason(season.Level - 1);
        Baby.Instance.PlaceAsleep(CurrentStage.babyStart, CurrentStage.route);
        BirdController.Instance.Teleport(CurrentStage.birdStart.position);
        BirdController.Instance.SetAltitude(CurrentStage.birdAltitude, true);
        birdCamera.MoveLock(SunlitSpotPosition);
        birdCamera.Cut();
    }

    // Dev tool: holds the camera on this stage's sunlit spot, or lets it follow the bird again. The lock stays there when the
    // stage changes, so the change can be watched.
    public void ToggleCameraLock() => birdCamera.ToggleLock(SunlitSpotPosition);

    public bool CameraLocked => birdCamera.IsLocked;

    Vector3 SunlitSpotPosition => CurrentStage.route[CurrentStage.route.Count - 1].position;

    void OnReachedRest() => EndStage();

    // Advances the season and starts the next stage where the baby sleeps. False after the last stage.
    bool EndStage()
    {
        if (finished)
            return false;
        season.AdvanceSeason();
        if (current + 1 >= stages.Length)
        {
            finished = true;
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
