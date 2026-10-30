using UnityEngine;
using UnityEngine.Rendering;

// A saved look for the watercolor / gouache shaders: every WatercolorManager value. Make one per season
// (Create > ThatGameJam > Watercolor Look, or "Copy From Scene Manager" in its menu) and list them in WatercolorSeasonLooks.
// Scene references (Form Light) are not stored; the manager keeps its own.
[CreateAssetMenu(menuName = "ThatGameJam/Watercolor Look", fileName = "WatercolorLook")]
public class WatercolorLook : ScriptableObject
{
    [Tooltip("The scene's Volume takes this profile with the look. Empty keeps the profile from before.")]
    public VolumeProfile volumeProfile;
    public WatercolorManager.Settings settings = new();

    [ContextMenu("Copy From Scene Manager")]
    void CopyFromSceneManager()
    {
        var manager = FindAnyObjectByType<WatercolorManager>();
        if (manager == null)
            return;
        settings = JsonUtility.FromJson<WatercolorManager.Settings>(JsonUtility.ToJson(manager.settings));
        settings.formLight = null;
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    [ContextMenu("Apply To Scene Manager")]
    void ApplyToSceneManager()
    {
        var manager = FindAnyObjectByType<WatercolorManager>();
        if (manager == null)
            return;
#if UNITY_EDITOR
        UnityEditor.Undo.RecordObject(manager, "Apply Watercolor Look");
#endif
        manager.Apply(settings, 1f);
    }
}
