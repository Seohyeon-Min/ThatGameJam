using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;

// Main toolbar, beside Play: a dropdown picks a stage of the open scene's GameFlow, and the button plays from there, skipping the intro and
// the stages before it: the baby wakes at the stage's start right away (GameFlow.StartAtStage). Unity's own Play still starts from the intro.
// Each stage can also be played from its end (GameFlow's stage end start): the baby already up at the route's last stop, so the fog
// stage starts just out of the fog, before the cliff. The pick is kept per editor session.
[InitializeOnLoad]
static class StageStartToolbar
{
    const string PickPath = "ThatGameJam/Stage Start Pick";
    const string PlayPath = "ThatGameJam/Stage Start Play";
    const string PickKey = "StageStartToolbar.Pick";

    // If Play Mode never started (a compile error), the next ordinary Play must not start from the stage.
    static StageStartToolbar() => EditorApplication.playModeStateChanged += change =>
    {
        if (change == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.EraseInt(GameFlow.StartStageKey);
            SessionState.EraseBool(GameFlow.StartAtEndKey);
            SessionState.EraseBool(GameFlow.StartEndingKey);
        }
    };

    const string PickEndKey = "StageStartToolbar.PickEnd";

    static int Pick
    {
        get => SessionState.GetInt(PickKey, 1);
        set => SessionState.SetInt(PickKey, value);
    }

    // The pick is the stage's end rather than its start.
    static bool PickEnd
    {
        get => SessionState.GetBool(PickEndKey, false);
        set => SessionState.SetBool(PickEndKey, value);
    }

    [MainToolbarElement(PickPath, defaultDockPosition = MainToolbarDockPosition.Middle)]
    static MainToolbarElement CreatePick() =>
        new MainToolbarDropdown(new MainToolbarContent(PickName(), null, "The stage the play button beside this starts from."), ShowStages);

    [MainToolbarElement(PlayPath, defaultDockPosition = MainToolbarDockPosition.Middle)]
    static MainToolbarElement CreatePlay() =>
        new MainToolbarButton(new MainToolbarContent("Stage", EditorGUIUtility.IconContent("PlayButton").image as Texture2D, "Plays from the stage picked beside this: no intro, the baby wakes at its start."), PlayFromPick);

    static void ShowStages(Rect at)
    {
        var menu = new GenericMenu();
        GameFlow flow = Object.FindAnyObjectByType<GameFlow>();
        if (flow == null || flow.Stages == null || flow.Stages.Count < 2)
            menu.AddDisabledItem(new GUIContent("No GameFlow with stages in the open scene"));
        else
            for (int i = 1; i < flow.Stages.Count; i++)
            {
                int index = i;
                // Picking a stage plays from it at once; the button beside plays from it again.
                menu.AddItem(new GUIContent(StageName(index)), Pick == index && !PickEnd, () => PickAndPlay(index, false));
            }
            menu.AddSeparator("");
            for (int i = 1; i < flow.Stages.Count; i++)
            {
                int index = i;
                menu.AddItem(new GUIContent(StageName(index) + " End"), Pick == index && PickEnd, () => PickAndPlay(index, true));
            }
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Ending (from the bird flying out)"), false, PlayEnding);
        menu.DropDown(at);
    }

    static void PickAndPlay(int index, bool atEnd)
    {
        Pick = index;
        PickEnd = atEnd;
        MainToolbar.Refresh(PickPath);
        PlayFrom(index, atEnd);
    }

    // The same as the toolbar, for when it isn't shown there.
    [MenuItem("Tools/Play From Stage/Stage 1")] static void PlayStage1() => PlayFrom(1);
    [MenuItem("Tools/Play From Stage/Stage 2")] static void PlayStage2() => PlayFrom(2);
    [MenuItem("Tools/Play From Stage/Stage 3")] static void PlayStage3() => PlayFrom(3);
    [MenuItem("Tools/Play From Stage/Stage 1 End")] static void PlayStage1End() => PlayFrom(1, true);
    [MenuItem("Tools/Play From Stage/Stage 2 End")] static void PlayStage2End() => PlayFrom(2, true);
    [MenuItem("Tools/Play From Stage/Stage 3 End (out of the fog)")] static void PlayStage3End() => PlayFrom(3, true);

    // The ending at once: the last stage, the girl already sitting by the stump, the bird already halfway out over the drop.
    [MenuItem("Tools/Play From Stage/Ending (from the bird flying out)")]
    static void PlayEnding()
    {
        GameFlow flow = Object.FindAnyObjectByType<GameFlow>();
        int last = flow != null && flow.Stages != null ? flow.Stages.Count - 1 : -1;
        if (last < 1)
        {
            Debug.LogWarning("Stage start: no GameFlow with stages in the open scene.");
            return;
        }
        if (PlayFrom(last, true))
            SessionState.SetBool(GameFlow.StartEndingKey, true);
    }

    static void PlayFromPick() => PlayFrom(Pick, PickEnd);

    static bool PlayFrom(int index, bool atEnd = false)
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("Stage start: stop Play Mode first.");
            return false;
        }
        GameFlow flow = Object.FindAnyObjectByType<GameFlow>();
        if (flow == null || index >= flow.Stages.Count)
        {
            Debug.LogWarning($"Stage start: the open scene has no stage {index}.");
            return false;
        }
        if (EditorUtility.scriptCompilationFailed)
        {
            Debug.LogWarning("Stage start: fix the compile errors first.");
            return false;
        }
        Pick = index;
        PickEnd = atEnd;
        SessionState.SetInt(GameFlow.StartStageKey, index);
        SessionState.SetBool(GameFlow.StartAtEndKey, atEnd);
        Debug.Log($"Stage start: playing from {StageName(index)}{(atEnd ? " End" : "")}.");
        EditorApplication.isPlaying = true;
        return true;
    }

    static string PickName() => StageName(Pick) + (PickEnd ? " End" : "");

    // The stage's object name, or its number when the scene has no such stage.
    static string StageName(int index)
    {
        GameFlow flow = Object.FindAnyObjectByType<GameFlow>();
        Stage stage = flow != null && flow.Stages != null && index < flow.Stages.Count ? flow.Stages[index] : null;
        return stage != null ? $"{index}: {stage.name}" : $"Stage {index}";
    }
}
