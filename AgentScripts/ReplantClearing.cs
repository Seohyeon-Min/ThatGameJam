
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Undoes the easter egg clearing behind the intro's start (ADR 0034) now that the eggs stand in the stages: the world forest loses its
// "EasterEgg Clearing" zone and gets its bottom padding back, the generator behind the clearing goes, and the intro's area ends at z -26 again.
// The scene is left unsaved.
// unity command script run --file AgentScripts/ReplantClearing.cs --entry ReplantClearing.Run
public static class ReplantClearing
{
    const float BottomPadding = 13.878445f;
    const float IntroBackZ = -26f;

    public static string Run()
    {
        Stage intro = Object.FindObjectsByType<Stage>().First(s => s.name == "Intro");
        Undo.RecordObject(intro, "Replant the clearing");
        Bounds area = intro.area;
        area.SetMinMax(new Vector3(area.min.x, area.min.y, IntroBackZ), area.max);
        intro.area = area;

        Transform back = GameObject.Find("01_SYSTEM").transform.Find("TreeGenerator_ClearingBack");
        if (back != null)
        {
            back.GetComponent<TreeGenerator>().Clear();
            Undo.DestroyObjectImmediate(back.gameObject);
        }

        var forest = GameObject.Find("01_SYSTEM/TreeGenerator").GetComponent<TreeGenerator>();
        Transform zone = forest.transform.Find("EasterEgg Clearing");
        if (zone != null)
            Undo.DestroyObjectImmediate(zone.gameObject);
        var so = new SerializedObject(forest);
        so.FindProperty("bottomPadding").floatValue = BottomPadding;
        so.ApplyModifiedProperties();
        int before = forest.TreeCount;
        forest.Generate();

        EditorSceneManager.MarkSceneDirty(intro.gameObject.scene);
        return $"intro area z {intro.area.min.z} .. {intro.area.max.z}; forest {before} -> {forest.TreeCount} trees; back generator removed: {back != null}";
    }
}
