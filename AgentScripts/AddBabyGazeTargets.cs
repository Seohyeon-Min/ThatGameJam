using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Adds PointOfInterest to things the baby can glance at while walking (see PointOfInterest.cs / Baby.cs
// PickNearbyPointOfInterest): the roadside trees it already passes by design, and the two trail landmarks.
// The bird is deliberately excluded — looking up at the bird is a separate, reserved gesture.
// Run: unity command script run --file AgentScripts/AddBabyGazeTargets.cs --entry AddBabyGazeTargets.Run
public static class AddBabyGazeTargets
{
    // Trees: default weight (1). Landmarks get a higher weight so they stand out from a crowd of trees.
    static readonly string[] TreeGroups = { "Stage1/RoadsideTrees" };
    static readonly (string path, float weight)[] Landmarks =
    {
        ("Stage1/FallenLog", 2f),
        ("Stage1/Stump", 2f),
        ("Stage1/LeaningLog", 2f),
    };

    public static string Run()
    {
        var report = new StringBuilder();
        int added = 0, skipped = 0;

        foreach (string groupPath in TreeGroups)
        {
            Transform group = GameObject.Find(groupPath)?.transform;
            if (group == null)
            {
                report.AppendLine($"Group not found: {groupPath}");
                continue;
            }
            foreach (Transform tree in group)
            {
                if (tree.GetComponent<PointOfInterest>() != null) { skipped++; continue; }
                Undo.AddComponent<PointOfInterest>(tree.gameObject);
                added++;
            }
        }

        foreach ((string path, float weight) in Landmarks)
        {
            GameObject go = GameObject.Find(path);
            if (go == null)
            {
                report.AppendLine($"Landmark not found: {path}");
                continue;
            }
            if (go.GetComponent<PointOfInterest>() != null) { skipped++; continue; }
            var poi = Undo.AddComponent<PointOfInterest>(go);
            var so = new SerializedObject(poi);
            so.FindProperty("weight").floatValue = weight;
            so.ApplyModifiedPropertiesWithoutUndo();
            added++;
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        report.AppendLine($"Added {added} PointOfInterest components, skipped {skipped} already present.");
        return report.ToString();
    }
}
