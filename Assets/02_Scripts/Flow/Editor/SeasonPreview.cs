using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Tools > Season: shows the scene as it looks at a season level, outside Play Mode: the watercolor look and its volume profile
// (WatercolorSeasonLooks), the trees' leaves (TreeGenerator), the grass and flowers of later stages (ScatterGenerator) and the
// melted snowbanks (Stage2SnowMelt). Snowfall and the spring ring only play in Play Mode.
// End Preview puts everything back as it was. So does saving the scene or entering Play Mode, so a preview is never saved. What it
// changed is kept in SessionState, so a script reload in between still puts it back.
[InitializeOnLoad]
static class SeasonPreview
{
    const string ActiveKey = "SeasonPreview.Active";
    const string LevelKey = "SeasonPreview.Level";
    const string SettingsKey = "SeasonPreview.Settings";
    const string ProfileKey = "SeasonPreview.Profile";
    const string ShownKey = "SeasonPreview.Shown";

    static SeasonPreview()
    {
        EditorSceneManager.sceneSaving += (_, _) => End();
        EditorApplication.playModeStateChanged += change =>
        {
            if (change == PlayModeStateChange.ExitingEditMode)
                End();
        };
    }

    static bool Active => SessionState.GetBool(ActiveKey, false);

    [MenuItem("Tools/Season/Level 0 (Intro)", false, 0)] static void Level0() => Show(0);
    [MenuItem("Tools/Season/Level 1 (Stage 1)", false, 1)] static void Level1() => Show(1);
    [MenuItem("Tools/Season/Level 2 (Stage 2)", false, 2)] static void Level2() => Show(2);
    [MenuItem("Tools/Season/Level 3 (Stage 3)", false, 3)] static void Level3() => Show(3);
    [MenuItem("Tools/Season/Level 4 (Ending)", false, 4)] static void Level4() => Show(4);

    [MenuItem("Tools/Season/End Preview", false, 20)]
    static void EndPreview() => End();

    [MenuItem("Tools/Season/Level 0 (Intro)", true)] static bool CanShow0() => Check(0);
    [MenuItem("Tools/Season/Level 1 (Stage 1)", true)] static bool CanShow1() => Check(1);
    [MenuItem("Tools/Season/Level 2 (Stage 2)", true)] static bool CanShow2() => Check(2);
    [MenuItem("Tools/Season/Level 3 (Stage 3)", true)] static bool CanShow3() => Check(3);
    [MenuItem("Tools/Season/Level 4 (Ending)", true)] static bool CanShow4() => Check(4);
    [MenuItem("Tools/Season/End Preview", true)] static bool CanEnd() => Active && !Application.isPlaying;

    // Ticks the level shown. In Play Mode the Season's own buttons do this.
    static bool Check(int level)
    {
        Menu.SetChecked(MenuPath(level), Active && SessionState.GetInt(LevelKey, -1) == level);
        return !Application.isPlaying;
    }

    static string MenuPath(int level) => level switch
    {
        0 => "Tools/Season/Level 0 (Intro)",
        1 => "Tools/Season/Level 1 (Stage 1)",
        2 => "Tools/Season/Level 2 (Stage 2)",
        3 => "Tools/Season/Level 3 (Stage 3)",
        _ => "Tools/Season/Level 4 (Ending)",
    };

    static void Show(int level)
    {
        if (!Active)
            Remember();
        SessionState.SetBool(ActiveKey, true);
        SessionState.SetInt(LevelKey, level);

        var looks = Object.FindAnyObjectByType<WatercolorSeasonLooks>();
        var manager = Object.FindAnyObjectByType<WatercolorManager>();
        WatercolorLook look = looks != null ? looks.LookAtLevel(level) : null;
        if (look != null && manager != null)
        {
            manager.Apply(look.settings, 1f);
            Volume volume = SceneVolume();
            if (volume != null && look.volumeProfile != null)
                volume.sharedProfile = look.volumeProfile;
        }
        foreach (var trees in Object.FindObjectsByType<TreeGenerator>(FindObjectsInactive.Include))
            trees.PreviewSeason(level);
        foreach (var scatter in Object.FindObjectsByType<ScatterGenerator>(FindObjectsInactive.Include))
            scatter.PreviewSeason(level);
        foreach (var melt in Object.FindObjectsByType<Stage2SnowMelt>(FindObjectsInactive.Include))
            melt.PreviewSeason(level);
        Refresh();
    }

    // What a preview can change, as it is now.
    static void Remember()
    {
        var manager = Object.FindAnyObjectByType<WatercolorManager>();
        SessionState.SetString(SettingsKey, manager != null ? JsonUtility.ToJson(manager.settings) : "");
        Volume volume = SceneVolume();
        SessionState.SetString(ProfileKey, volume != null && volume.sharedProfile != null ? AssetDatabase.GetAssetPath(volume.sharedProfile) : "");
        var shown = new StringBuilder();
        foreach (GameObject target in Switched())
            shown.Append(target.GetInstanceID()).Append(target.activeSelf ? ":1;" : ":0;");
        SessionState.SetString(ShownKey, shown.ToString());
    }

    static void End()
    {
        if (!Active)
            return;
        SessionState.SetBool(ActiveKey, false);
        var manager = Object.FindAnyObjectByType<WatercolorManager>();
        string settings = SessionState.GetString(SettingsKey, "");
        if (manager != null && settings.Length > 0)
        {
            // Through Apply, which keeps the manager's own Form Light and sends the values to the shaders.
            var before = JsonUtility.FromJson<WatercolorManager.Settings>(settings);
            manager.Apply(before, 1f);
        }
        Volume volume = SceneVolume();
        string profile = SessionState.GetString(ProfileKey, "");
        if (volume != null && profile.Length > 0)
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profile);
        foreach (string entry in SessionState.GetString(ShownKey, "").Split(';'))
        {
            int colon = entry.IndexOf(':');
            if (colon < 0 || !int.TryParse(entry.Substring(0, colon), out int id))
                continue;
            if (EditorUtility.InstanceIDToObject(id) is GameObject target)
                target.SetActive(entry.Substring(colon + 1) == "1");
        }
        // The oaks swap materials rather than turn on and off: back to the scene's own level, as Generate left them.
        var season = Object.FindAnyObjectByType<Season>();
        foreach (var trees in Object.FindObjectsByType<TreeGenerator>(FindObjectsInactive.Include))
            trees.RestoreBark(season != null ? season.Level : 0);
        Refresh();
    }

    // Everything the generators and the snowmelt turn on and off for a season.
    static IEnumerable<GameObject> Switched()
    {
        foreach (var trees in Object.FindObjectsByType<TreeGenerator>(FindObjectsInactive.Include))
            foreach (Transform child in trees.transform)
                yield return child.gameObject;
        foreach (var scatter in Object.FindObjectsByType<ScatterGenerator>(FindObjectsInactive.Include))
            foreach (Transform child in scatter.transform)
                yield return child.gameObject;
        foreach (var melt in Object.FindObjectsByType<Stage2SnowMelt>(FindObjectsInactive.Include))
            foreach (Transform bank in melt.Snowbanks)
                if (bank != null)
                    yield return bank.gameObject;
    }

    // The scene's own Volume, not one a season crossfade added.
    static Volume SceneVolume()
    {
        var looks = Object.FindAnyObjectByType<WatercolorSeasonLooks>();
        if (looks != null && looks.SceneVolume != null)
            return looks.SceneVolume;
        return Object.FindAnyObjectByType<Volume>();
    }

    static void Refresh()
    {
        EditorApplication.QueuePlayerLoopUpdate();
        SceneView.RepaintAll();
    }
}
