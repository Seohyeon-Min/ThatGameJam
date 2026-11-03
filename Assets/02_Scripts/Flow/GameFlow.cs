using System.Collections.Generic;
using UnityEngine;

// Runs the stages in order on one long road (ADR 0010). After the baby falls asleep at a stage's sunlit spot, the season advances
// and the next stage starts right there: the baby sleeps on until it is woken, and the bird may fly on into the new stage (design doc §10, §11).
public class GameFlow : MonoBehaviour
{
    public static GameFlow Instance { get; private set; }

    [SerializeField] Stage[] stages;
    [SerializeField] BirdCamera birdCamera;
    [SerializeField] Season season;
    [Tooltip("Plays when the game starts. Empty or turned off: the game just starts.")]
    [SerializeField] IntroSequence intro;

    int current;
    // The last stage has ended; nothing more happens (the ending is not built yet).
    bool finished;

    public Stage CurrentStage => stages[current];
    // The authored stage order (StageEditor's spacing tool cascades along this, not scene hierarchy order).
    public IReadOnlyList<Stage> Stages => stages;

    // Where the bird may fly: every stage's area up to the current one, and ahead no further than the current stage's area ends, a little
    // past its sunlit spot so the bird can wait around the light (the director's call, over ADR 0012's stop at the spot itself).
    // The stages' areas follow each other along +Z.
    public Bounds FlyArea
    {
        get
        {
            Bounds area = stages[0].area;
            for (int i = 1; i <= current; i++)
                area.Encapsulate(stages[i].area);
            Vector3 max = area.max;
            max.z = CurrentStage.area.max.z;
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
        if (intro != null && intro.isActiveAndEnabled)
        {
            // The baby sleeps on, however long the player stays up there.
            Baby.Instance.HoldAsleep();
            intro.Play();
        }
    }

    // The intro has ended, and the baby can wake from here on. The bird is at the first stage's start, or above the tree it will
    // drop onto, with the camera framed as if it were at the start.
    public void FinishIntro(Perchable landOn, float landFromHeight)
    {
        Stage first = stages[0];
        if (landOn != null)
        {
            BirdController.Instance.Teleport(landOn.PerchPoint.position + Vector3.up * landFromHeight);
            BirdController.Instance.SetAltitude(first.birdAltitude, false);
            birdCamera.HoldWhileBirdPerches(first.birdStart.position);
        }
        else
        {
            BirdController.Instance.Teleport(first.birdStart.position);
            BirdController.Instance.SetAltitude(first.birdAltitude, true);
        }
        Baby.Instance.Sleep(first.route);
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

    // Dev tool: the game camera follows the bird again.
    public void CameraFollowBird() => birdCamera.LockOn(null);

    // Dev tool: the game camera stays on this stage (its place in Stages), at StageViewPoint.
    public void CameraShowStage(int index)
    {
        if (index < stages.Length)
            birdCamera.LockOn(StageViewPoint(index));
    }

    // The middle of a stage's route, from where the baby starts to its sunlit spot: where that stage plays. Not the middle of its
    // flight area, which reaches into the stages before and after it.
    public Vector3 StageViewPoint(int index)
    {
        BabyRoute route = stages[index].route;
        return (route[0].position + route[route.Count - 1].position) * 0.5f;
    }

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
