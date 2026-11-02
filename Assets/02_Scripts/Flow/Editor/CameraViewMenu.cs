using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;

// Tools > Camera: switches what the game camera shows, in and out of Play Mode. Camera 1 is the camera as it was, Camera 2 the middle
// of Stage 2. Outside Play Mode the point the camera follows moves, and Camera 1 puts it back; it is not worth saving, as the game
// places it again when it starts.
static class CameraViewMenu
{
    const string HomeKey = "CameraViewMenu.Home";
    const string HasHomeKey = "CameraViewMenu.HasHome";

    [MenuItem("Tools/Camera/Camera 1 %#1")]
    static void ShowCurrent()
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

    [MenuItem("Tools/Camera/Camera 2 %#2")]
    static void ShowStage2() => ShowStage(2);

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
            return;
        if (!SessionState.GetBool(HasHomeKey, false))
        {
            SessionState.SetVector3(HomeKey, camera.transform.position);
            SessionState.SetBool(HasHomeKey, true);
        }
        camera.JumpInEditor(flow.Stages[index].area.center);
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
