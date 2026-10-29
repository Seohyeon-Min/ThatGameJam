using System.Collections.Generic;
using System.Linq;
using UnityEditor;

// Adds the clips of a newer Bird-Animated.fbx that its import settings do not list yet (Bird_Hover, Bird_Refuse from 2026-10-28),
// keeping the listed clips' settings and events. Bird_Hover loops (the art's README); Bird_Refuse plays once.
// Run after replacing the FBX: unity command script run --file AgentScripts/SetupBirdModel.cs --entry SetupBirdModel.Run
public static class SetupBirdModel
{
    const string ModelPath = "Assets/01_Arts/Models/Bird/Bird-Animated.fbx";
    static readonly string[] LoopingNewClips = { "Bird_Hover" };

    public static string Run()
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
        var clips = new List<ModelImporterClipAnimation>(importer.clipAnimations);
        var added = new List<string>();
        foreach (ModelImporterClipAnimation clip in importer.defaultClipAnimations)
        {
            if (clips.Any(c => c.takeName == clip.takeName))
                continue;
            clip.loopTime = LoopingNewClips.Contains(clip.name);
            clips.Add(clip);
            added.Add($"{clip.name} loop={clip.loopTime}");
        }
        importer.clipAnimations = clips.ToArray();
        importer.SaveAndReimport();
        return "added: " + string.Join(", ", added);
    }
}
