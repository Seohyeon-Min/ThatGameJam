using UnityEditor;
using UnityEditor.Timeline;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.UIElements;

// Debug tool on a Cutscene's Inspector: Lock Preview Here holds this Timeline's preview on a frame, so the Game view keeps showing that frame's
// animation and camera whatever is selected, and looks and materials can be tuned against it. Press again, turn off the Timeline window's
// padlock, or press Play to let go. Outside Play Mode only, with the Timeline window open. Nothing is saved in the scene.
[CustomEditor(typeof(Cutscene))]
public class CutsceneEditor : Editor
{
    public override VisualElement CreateInspectorGUI()
    {
        var root = new VisualElement();
        InspectorElement.FillDefaultInspector(root, serializedObject, this);

        PlayableDirector director = ((Cutscene)target).GetComponent<PlayableDirector>();
        var frame = new IntegerField("Locked Frame (30 per second)") { value = CutscenePreviewLock.Frame };
        frame.RegisterValueChangedCallback(change => CutscenePreviewLock.Frame = Mathf.Max(0, change.newValue));
        var button = new Button { style = { marginTop = 6 } };
        button.clicked += () =>
        {
            CutscenePreviewLock.Toggle(director);
            frame.SetValueWithoutNotify(CutscenePreviewLock.Frame);
        };
        root.Add(button);
        root.Add(frame);

        root.schedule.Execute(() =>
        {
            bool locked = CutscenePreviewLock.IsLockedOn(director);
            button.text = locked ? "Unlock Preview" : "Lock Preview Here";
            frame.style.display = locked ? DisplayStyle.Flex : DisplayStyle.None;
        }).Every(200);
        return root;
    }
}

[InitializeOnLoad]
static class CutscenePreviewLock
{
    const string ActiveKey = "CutscenePreviewLock.Active";
    const string FrameKey = "CutscenePreviewLock.Frame";

    // A script reload turns the preview off.
    static bool restorePreview = true;

    static bool IsActive => SessionState.GetBool(ActiveKey, false);

    public static int Frame
    {
        get => SessionState.GetInt(FrameKey, 0);
        set => SessionState.SetInt(FrameKey, value);
    }

    static CutscenePreviewLock()
    {
        EditorApplication.update += Hold;
        EditorApplication.playModeStateChanged += change =>
        {
            if (change == PlayModeStateChange.ExitingEditMode && IsActive)
                Release();
        };
    }

    public static bool IsLockedOn(PlayableDirector director) => IsActive && TimelineEditor.masterDirector == director;

    public static void Toggle(PlayableDirector director)
    {
        if (IsLockedOn(director))
        {
            Release();
            return;
        }
        TimelineEditorWindow window = TimelineEditor.GetOrCreateWindow();
        window.locked = false;
        window.SetTimeline(director);
        window.locked = true;
        Frame = Mathf.RoundToInt((float)window.playbackControls.GetCurrentTime(TimelinePlaybackControls.Context.Global) * Cutscene.AnimationFramesPerSecond);
        SessionState.SetBool(ActiveKey, true);
        restorePreview = true;
    }

    static void Release()
    {
        SessionState.SetBool(ActiveKey, false);
        TimelineEditorWindow window = TimelineEditor.GetWindow();
        if (window != null)
            window.locked = false;
    }

    static void Hold()
    {
        if (!IsActive || EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        // Right after a script reload these aren't back yet.
        TimelineEditorWindow window = TimelineEditor.GetWindow();
        if (window == null || TimelineEditor.masterDirector == null)
            return;
        if (!window.locked)
        {
            Release();
            return;
        }

        TimelinePlaybackControls controls = window.playbackControls;
        if (restorePreview)
        {
            restorePreview = false;
            // Playing is the one public way to turn the preview on.
            controls.Play();
            controls.Pause();
        }
        double time = Frame / (double)Cutscene.AnimationFramesPerSecond;
        if (System.Math.Abs(controls.GetCurrentTime(TimelinePlaybackControls.Context.Global) - time) > 0.001d)
        {
            controls.SetCurrentTime(time, TimelinePlaybackControls.Context.Global);
            TimelineEditor.Refresh(RefreshReason.SceneNeedsUpdate);
        }
    }
}
