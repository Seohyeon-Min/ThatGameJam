using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class SetupRabbitModels
{
    public const string Folder = "Assets/01_Arts/Models/Rabbits";
    public static string Run()
    {
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/01_Arts/Models", "Rabbits");
        string report = "";
        foreach (string name in new[]{"Rabbit-Animated", "BabyRabbit-Animated"})
        {
            string path = Folder + "/" + name + ".fbx";
            // One-time ZIP import; the FBX + meta are versioned in Assets afterwards.
            if (!File.Exists(path))
            {
                File.Copy("Temp/RabbitPrepared/" + name + ".fbx", path);
                File.Copy("Temp/RabbitPrepared/" + name + ".fbx.meta", path + ".meta");
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.motionNodeName = "Root";
            importer.SaveAndReimport();
            var clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__")).ToArray();
            report += name + " avatar=" + AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<Animator>()?.avatar?.isValid + "\n";
            report += string.Join("\n", clips.Select(c => c.name + " " + c.length + " loop=" + c.isLooping)) + "\n";
            var model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            foreach (var r in model.GetComponentsInChildren<Renderer>())
                report += r.name + " " + r.bounds + " mats=" + string.Join(",", r.sharedMaterials.Select(m => m.name + ":" + m.shader.name)) + "\n";
            UnityEngine.Object.DestroyImmediate(model);
            BuildController(name.StartsWith("Baby") ? "BabyRabbit" : "Rabbit", clips);
        }
        foreach (string file in new[]{"README_KO.md", "Rabbit_clips.json", "BabyRabbit_clips.json", "unity_validation.txt"})
            if (!File.Exists(Folder + "/" + file)) File.Copy("Temp/RabbitZipExtract/RabbitUnity-Animations/" + file, Folder + "/" + file);
        AssetDatabase.Refresh();
        AssetDatabase.SaveAssets();
        return report;
    }
    static void BuildController(string prefix, AnimationClip[] clips)
    {
        string path = Folder + "/" + prefix + ".controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path) ?? AnimatorController.CreateAnimatorControllerAtPath(path);
        var machine = controller.layers[0].stateMachine;
        foreach (var child in machine.states) machine.RemoveState(child.state);
        foreach (var clip in clips)
        {
            var state = machine.AddState(clip.name.Substring(prefix.Length + 1));
            state.motion = clip;
            if (state.name == "Sleep") machine.defaultState = state;
        }
        EditorUtility.SetDirty(controller);
    }
}
