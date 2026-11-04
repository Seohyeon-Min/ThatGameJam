using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Puts a semi-lock camera zone (CameraZone without a device) on each stage's sunlit spot in Main, like the gimmicks' zones: as the
// bird comes up to the spot the camera eases in and keeps the spot in view, and follows the bird again once it flies off. The stage
// without a sunlit spot (stage 3 ends at the cliff, which has its own zone) is left alone. Rerunning puts them in place again.
// unity command script run --file AgentScripts/AddSunlitCameraZones.cs --entry AddSunlitCameraZones.Run
public static class AddSunlitCameraZones
{
    const string ZoneName = "CameraZone_SunlitSpot";
    // How far before the spot, along the road (+Z), the trigger band lies.
    const float TriggerBefore = 4f;
    static readonly Vector2 TriggerSize = new(14f, 1f);
    static readonly Vector2 LockSize = new(16f, 14f);
    static readonly Vector2 AreaSize = new(2f, 1f);

    public static string Run()
    {
        var flow = Object.FindAnyObjectByType<GameFlow>(FindObjectsInactive.Include);
        if (flow == null)
            return "No GameFlow in the open scene.";
        var report = new StringBuilder();
        foreach (Stage stage in flow.Stages)
        {
            Transform old = stage.transform.Find(ZoneName);
            if (old != null)
                Undo.DestroyObjectImmediate(old.gameObject);
            var spot = stage.GetComponentInChildren<SunlitSpot>(true);
            if (spot == null || stage.route == null || stage.route.Count == 0)
            {
                report.Append(stage.name).Append(": no sunlit spot, skipped. ");
                continue;
            }
            Vector3 at = stage.route[stage.route.Count - 1].position;

            var zone = new GameObject(ZoneName);
            Undo.RegisterCreatedObjectUndo(zone, "Add Sunlit Camera Zones");
            zone.transform.SetParent(stage.transform, false);
            zone.transform.position = at;
            var trigger = new GameObject("Trigger").transform;
            trigger.SetParent(zone.transform, false);
            trigger.position = at - Vector3.forward * TriggerBefore;

            var cameraZone = zone.AddComponent<CameraZone>();
            var so = new SerializedObject(cameraZone);
            so.FindProperty("trigger").objectReferenceValue = trigger;
            so.FindProperty("triggerSize").vector2Value = TriggerSize;
            so.FindProperty("lockSize").vector2Value = LockSize;
            so.FindProperty("areaSize").vector2Value = AreaSize;
            so.ApplyModifiedPropertiesWithoutUndo();
            report.Append(stage.name).Append(": zone at ").Append(at.ToString("F1")).Append(". ");
        }
        EditorSceneManager.MarkSceneDirty(flow.gameObject.scene);
        return report.ToString();
    }
}
