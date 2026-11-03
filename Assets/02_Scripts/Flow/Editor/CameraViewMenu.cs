using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;

// Tools > Camera: shows a stage through the game camera, in and out of Play Mode: the middle of its route (GameFlow.StageViewPoint).
// Back returns to the camera as it was (in Play Mode, following the bird). Outside Play Mode the point the camera follows moves, and
// Back puts it back; it is not worth saving, as the game places it again when it starts.
static class CameraViewMenu
{
    const string HomeKey = "CameraViewMenu.Home";
    const string HasHomeKey = "CameraViewMenu.HasHome";

    [MenuItem("Tools/Camera/Back %#0", false, 0)]
    static void Back()
    {
        if (Application.isPlaying)
        {
            GameFlow.Instance.CameraFollowBird();
            return;
        }
        BirdCamera camera = Object.FindAnyObjectByType<BirdCamera>();
        if (camera == null || !SessionState.GetBool(HasHomeKey, false))
            return;
        camera.JumpInEditor(SessionState.GetVector3(HomeKey, camera.transform.position));
        SessionState.SetBool(HasHomeKey, false);
        Refresh();
    }

    [MenuItem("Tools/Camera/Intro", false, 20)]
    static void ShowIntro() => ShowStage(0);

    [MenuItem("Tools/Camera/Stage 1 %#1", false, 21)]
    static void ShowStage1() => ShowStage(1);

    [MenuItem("Tools/Camera/Stage 2 %#2", false, 22)]
    static void ShowStage2() => ShowStage(2);

    [MenuItem("Tools/Camera/Stage 3 %#3", false, 23)]
    static void ShowStage3() => ShowStage(3);

    static void ShowStage(int index)
    {
        if (Application.isPlaying)
        {
            GameFlow.Instance.CameraShowStage(index);
            return;
        }
        BirdCamera camera = Object.FindAnyObjectByType<BirdCamera>();
        GameFlow flow = Object.FindAnyObjectByType<GameFlow>();
        if (camera == null || flow == null || index >= flow.Stages.Count)
        {
            Debug.LogWarning($"Tools/Camera: the scene has no stage {index}.");
            return;
        }
        if (!SessionState.GetBool(HasHomeKey, false))
        {
            SessionState.SetVector3(HomeKey, camera.transform.position);
            SessionState.SetBool(HasHomeKey, true);
        }
        camera.JumpInEditor(flow.StageViewPoint(index));
        Refresh();
    }

    static void Refresh()
    {
        // The brain doesn't run by itself outside Play Mode, and the Game view would wait for it.
        Camera main = Camera.main;
        if (main != null && main.TryGetComponent(out CinemachineBrain brain))
        {
            CinemachineBrain.UpdateMethods updateMethod = brain.UpdateMethod;
            brain.UpdateMethod = CinemachineBrain.UpdateMethods.ManualUpdate;
            brain.ManualUpdate();
            brain.UpdateMethod = updateMethod;
        }
        EditorApplication.QueuePlayerLoopUpdate();
        SceneView.RepaintAll();
    }
}
