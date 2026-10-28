using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// Import settings for the art's girl model (the baby), per its README: Generic rig with an avatar from this model,
// clips named as in the README with Loop Time on the looping ones (Walk, Run, Sleep, the four idles), and the embedded textures extracted. Run once after the FBX is (re)imported.
// Run: unity command script run --file AgentScripts/SetupGirlModel.cs --entry SetupGirlModel.Run
public static class SetupGirlModel
{
    public const string ModelPath = "Assets/01_Arts/Models/Girl/NPC-Girl-Animated.fbx";
    const string TextureFolder = "Assets/01_Arts/Models/Girl/Textures";
    static readonly string[] OneShotClips = { "Girl_GetUp", "Girl_LieDown" };

    public static string Run()
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        ModelImporterClipAnimation[] clips = importer.clipAnimations.Length > 0 ? importer.clipAnimations : importer.defaultClipAnimations;
        foreach (ModelImporterClipAnimation clip in clips)
        {
            // Takes come as "NPC-Girl-Rig|Girl_Walk"; the clip keeps the README's name.
            clip.name = clip.takeName.Substring(clip.takeName.LastIndexOf('|') + 1);
            clip.loopTime = !OneShotClips.Contains(clip.name);
        }
        importer.clipAnimations = clips;
        importer.SaveAndReimport();
        // The skin, eye and limb textures are embedded in the FBX; out here the materials find them on reimport.
        if (!AssetDatabase.IsValidFolder(TextureFolder))
        {
            AssetDatabase.CreateFolder("Assets/01_Arts/Models/Girl", "Textures");
            importer.ExtractTextures(TextureFolder);
            AssetDatabase.Refresh();
            AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceUpdate);
        }

        var report = new StringBuilder();
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(ModelPath))
            if (asset is AnimationClip c && !c.name.StartsWith("__preview__"))
                report.Append($"{c.name} {c.length:0.###}s loop={c.isLooping}; ");
        return report.ToString();
    }
}
