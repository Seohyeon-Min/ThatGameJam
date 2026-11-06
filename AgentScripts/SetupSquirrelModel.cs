using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

// Imports the squirrel FBX (Blender, Generic rig, Hop/SitIdle/SitIdle_LookAround in place), builds its Animator Controller and saves the
// prefab. The FBX comes with the .meta from the artist's unitypackage (clip names, loops). Materials stay the FBX's own solid colors.
// Run: unity command script run --file AgentScripts/SetupSquirrelModel.cs --entry SetupSquirrelModel.Run
public static class SetupSquirrelModel
{
    public const string Folder = "Assets/01_Arts/Models/Squirrel";
    const string ModelPath = Folder + "/Squirrel-Animated.fbx";
    const string ControllerPath = Folder + "/Squirrel.controller";
    const string PrefabPath = "Assets/03_Prefabs/Squirrel.prefab";
    const string Prepared = "Temp/SquirrelPrepared/";

    public static string Run()
    {
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/01_Arts/Models", "Squirrel");
        // One-time handoff copy from Temp; the FBX and docs are versioned in Assets afterwards.
        foreach (string file in new[] { "Squirrel-Animated.fbx", "Squirrel-Animated.fbx.meta", "README_KO.md", "Squirrel_clips.json", "unity_validation.txt" })
            if (File.Exists(Prepared + file) && !File.Exists(Folder + "/" + file)) File.Copy(Prepared + file, Folder + "/" + file);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
        importer.importBlendShapes = false;
        importer.importCameras = false;
        importer.importLights = false;
        importer.SaveAndReimport();

        var clips = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__")).ToArray();
        var controller = BuildController(clips);
        BuildPrefab(controller);
        AssetDatabase.SaveAssets();
        return Report();
    }

    // One state per clip. It peeks out of the hollow and looks around, so LookAround is the default.
    static AnimatorController BuildController(AnimationClip[] clips)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath)
                         ?? AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        var machine = controller.layers[0].stateMachine;
        foreach (var child in machine.states) machine.RemoveState(child.state);
        foreach (var clip in clips)
        {
            var state = machine.AddState(clip.name.Substring("Squirrel_".Length));
            state.motion = clip;
            if (state.name == "SitIdle_LookAround") machine.defaultState = state;
        }
        EditorUtility.SetDirty(controller);
        return controller;
    }

    static void BuildPrefab(AnimatorController controller)
    {
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath), scene);
            go.name = "Squirrel";
            var animator = go.GetComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    static string Report()
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        string report = "type=" + importer.animationType + " motionNode=" + importer.motionNodeName + " avatarValid=" + model.GetComponent<Animator>()?.avatar?.isValid + "\n";
        foreach (var c in AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__")))
            report += "clip " + c.name + " len=" + c.length.ToString("0.###") + " loop=" + c.isLooping + "\n";
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), scene);
            var anim = inst.GetComponent<Animator>();
            report += "controller=" + anim.runtimeAnimatorController?.name + " default=" + ((AnimatorController)anim.runtimeAnimatorController).layers[0].stateMachine.defaultState.name + "\n";
            var smr = inst.GetComponentInChildren<SkinnedMeshRenderer>();
            report += "mats=" + string.Join(",", smr.sharedMaterials.Select(m => m.name + ":" + m.shader.name)) + "\n";
            var mesh = new Mesh();
            smr.BakeMesh(mesh, true);
            var verts = mesh.vertices.Select(v => smr.transform.TransformPoint(v)).ToArray();
            report += "baked x " + verts.Min(v => v.x).ToString("F3") + ".." + verts.Max(v => v.x).ToString("F3")
                      + " y " + verts.Min(v => v.y).ToString("F3") + ".." + verts.Max(v => v.y).ToString("F3")
                      + " z " + verts.Min(v => v.z).ToString("F3") + ".." + verts.Max(v => v.z).ToString("F3") + "\n";
            Object.DestroyImmediate(mesh);
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
        return report;
    }
}
