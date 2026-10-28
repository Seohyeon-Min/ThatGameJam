using System.Collections;
using UnityEngine;

// Runs the stages in order: places the baby and bird, and after the baby sleeps in the rest area,
// plays the season effect and moves to the next stage behind a fade (design doc §10, §11).
public class GameFlow : MonoBehaviour
{
    public static GameFlow Instance { get; private set; }

    [SerializeField] Stage[] stages;
    [SerializeField] ScreenFader fader;
    [SerializeField] BirdCamera birdCamera;

    int current;
    bool transitioning;

    public Stage CurrentStage => stages[current];

    void Awake() => Instance = this;

    void Start()
    {
        Baby.Instance.ReachedRest += OnReachedRest;
        BeginStage(0);
    }

    public void SkipToNextStage()
    {
        if (!transitioning)
            StartCoroutine(Transition(false));
    }

    void OnReachedRest()
    {
        if (!transitioning)
            StartCoroutine(Transition(true));
    }

    IEnumerator Transition(bool playSeasonEffect)
    {
        transitioning = true;
        if (playSeasonEffect)
        {
            CurrentStage.seasonEffect.Invoke();
            yield return new WaitForSeconds(CurrentStage.seasonEffectDuration);
        }

        if (current + 1 < stages.Length)
        {
            yield return fader.FadeOut();
            BeginStage(current + 1);
            yield return fader.FadeIn();
        }
        else
        {
            Debug.Log("Last stage finished. The ending is not implemented yet (plan step 4).");
        }
        transitioning = false;
    }

    void BeginStage(int index)
    {
        current = index;
        Stage stage = stages[index];
        Baby.Instance.PlaceAsleep(stage.babyStart, stage.route);
        BirdController.Instance.Teleport(stage.birdStart.position);
        BirdController.Instance.SetAltitude(stage.birdAltitude, true);
        // Also cuts the camera to the new stage instead of sweeping across the world.
        birdCamera.SetStage(stage);
    }
}
