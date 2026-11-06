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
    public BirdCamera BirdCamera => birdCamera;

    // Per stage, its cliff at the end (the fog stage's CliffEnding), or null.
    CliffEnding[] cliffs;

    // Where the bird may fly: every stage's area up to the current one, and ahead no further than the current stage's area ends, a little
    // past its sunlit spot so the bird can wait around the light (the director's call, over ADR 0012's stop at the spot itself).
    // The stages' areas follow each other along +Z. A stage with a cliff at its end ends at the edge until the girl sits there.
    public Bounds FlyArea => Area(cliff => cliff.BirdFarEnd);

    // Where the camera's aim may go: the fly area, but before the girl sits at a cliff, no further than a little before its edge.
    public Bounds CameraArea => Area(cliff => cliff.CameraFarEnd);

    Bounds Area(System.Func<CliffEnding, float> cliffFarEnd)
    {
        Bounds area = stages[0].area;
        for (int i = 1; i <= current; i++)
            area.Encapsulate(stages[i].area);
        Vector3 max = area.max;
        max.z = CurrentStage.area.max.z;
        CliffEnding cliff = cliffs != null ? cliffs[current] : null;
        if (cliff != null)
            max.z = Mathf.Min(max.z, cliffFarEnd(cliff));
        area.SetMinMax(area.min, max);
        return area;
    }

    void Awake()
    {
        Instance = this;
        cliffs = new CliffEnding[stages.Length];
        for (int i = 0; i < stages.Length; i++)
            cliffs[i] = stages[i].GetComponentInChildren<CliffEnding>(true);
    }

#if UNITY_EDITOR
    // Set by the toolbar's stage start (StageStartToolbar) just before Play Mode, and read once here.
    public const string StartStageKey = "GameFlow.StartStage";
    // Set with it: start at the stage's end instead, at its last stop (the fog stage: just out of the fog, before the cliff).
    public const string StartAtEndKey = "GameFlow.StartAtEnd";
    // Set with them: play the stage's ending at once (CliffEnding.PlayEndingNow), from its first shot.
    public const string StartEndingKey = "GameFlow.StartEnding";
#endif

    void Start()
    {
        Baby.Instance.ReachedRest += OnReachedRest;
#if UNITY_EDITOR
        int startStage = UnityEditor.SessionState.GetInt(StartStageKey, -1);
        UnityEditor.SessionState.EraseInt(StartStageKey);
        bool atEnd = UnityEditor.SessionState.GetBool(StartAtEndKey, false);
        UnityEditor.SessionState.EraseBool(StartAtEndKey);
        bool ending = UnityEditor.SessionState.GetBool(StartEndingKey, false);
        UnityEditor.SessionState.EraseBool(StartEndingKey);
        if (startStage > 0 && startStage < stages.Length)
        {
            if (ending && cliffs[startStage] != null)
            {
                Debug.Log($"Stage start: skipping to the ending of stage {startStage} ({stages[startStage].name}).", stages[startStage]);
                StartCoroutine(StartAtEnding(startStage));
                return;
            }
            Debug.Log($"Stage start: skipping the intro, starting at {(atEnd ? "the end of " : "")}stage {startStage} ({stages[startStage].name}).", stages[startStage]);
            StartCoroutine(StartAtStage(startStage, atEnd));
            return;
        }
#endif
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

#if UNITY_EDITOR
    // Dev tool (the toolbar's stage start): skips the intro and the stages before this one. The season is the stage's own, the baby lies
    // asleep at the stage's start with the bird at its start, and she wakes right away, so the stage's wake cutscene plays from the
    // beginning. Devices of the stages skipped stay closed.
    // At its end instead: no waking, she stands at the route's last stop and goes on from there, the bird a little behind her.
    System.Collections.IEnumerator StartAtStage(int index, bool atEnd)
    {
        Stage stage = stages[index];
        BeginStage(index);
        season.SetSeason(season.Level + index);
        // The stage's look at once: no ring or crossfade for the change just made.
        WatercolorSeasonLooks looks = FindAnyObjectByType<WatercolorSeasonLooks>();
        if (looks != null)
            looks.JumpTo(season.Level);
        Baby.Instance.PlaceAsleep(stage.babyStart, stage.route);
        BirdController.Instance.Teleport(stage.birdStart.position);
        BirdController.Instance.SetAltitude(stage.birdAltitude, true);
        birdCamera.Cut();
        if (atEnd)
        {
            int last = stage.route.Count - 1;
            while (last > 0 && !stage.route[last].TryGetComponent(out StopPoint _))
                last--;
            Transform from = stage.route[last];
            Baby.Instance.WalkOnFrom(stage.route, last);
            BirdController.Instance.Teleport(from.position + new Vector3(1.5f, stage.birdAltitude, -2f));
            birdCamera.Cut();
            yield break;
        }
        // One frame for the animator and the camera to settle there before she gets up.
        yield return null;
        Baby.Instance.WakeTimer.ResolveNaturally();
    }
#endif

#if UNITY_EDITOR
    // Dev tool (the toolbar's Ending): the stage's end, then after a frame for it to settle, its ending from the first shot.
    System.Collections.IEnumerator StartAtEnding(int index)
    {
        yield return StartAtStage(index, true);
        yield return null;
        cliffs[index].PlayEndingNow();
    }
#endif

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
        // The baby asleep and the season changing open the sunlit spot's semi-lock, as a device opens its own.
        foreach (CameraZone zone in CurrentStage.GetComponentsInChildren<CameraZone>())
            if (!zone.HasDevice)
                zone.Release();
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
