using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

// Imports the ladybug FBX (Blender, Generic rig, Idle/Crawl in place), builds its Animator Controller
// and saves the prefab. Materials stay the FBX's own solid colors (URP Lit), like the rabbits and the bird.
// Run: unity command script run --file AgentScripts/SetupLadybugModel.cs --entry SetupLadybugModel.Run
public static class SetupLadybugModel
{
    public const string Folder = "Assets/01_Arts/Models/Ladybug";
    const string ModelPath = Folder + "/Ladybug-Animated.fbx";
    const string ControllerPath = Folder + "/Ladybug.controller";
    const string PrefabPath = "Assets/03_Prefabs/Ladybug.prefab";
    const string Prepared = "Temp/LadybugPrepared/";

    public static string Run()
    {
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/01_Arts/Models", "Ladybug");
        // One-time handoff copy from Temp; the FBX and docs are versioned in Assets afterwards.
        foreach (string file in new[] { "Ladybug-Animated.fbx", "README_KO.md", "Ladybug_clips.json" })
            if (File.Exists(Prepared + file)) File.Copy(Prepared + file, Folder + "/" + file, true);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.motionNodeName = "Root";
        importer.importBlendShapes = false;
        importer.importCameras = false;
        importer.importLights = false;
        var clipDefs = importer.defaultClipAnimations;
        foreach (var c in clipDefs)
        {
            c.name = c.takeName.Contains("Crawl") ? "Ladybug_Crawl" : "Ladybug_Idle";
            c.loopTime = true;
        }
        importer.clipAnimations = clipDefs;
        importer.SaveAndReimport();

        var clips = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__")).ToDictionary(c => c.name);
        var controller = BuildController(clips["Ladybug_Idle"], clips["Ladybug_Crawl"]);
        BuildPrefab(controller);
        AssetDatabase.SaveAssets();
        return Report();
    }

    static AnimatorController BuildController(AnimationClip idle, AnimationClip crawl)
    {
        // Keep the asset (and its GUID) if it exists; empty it and refill it in place.
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath)
                         ?? AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        foreach (var p in controller.parameters) controller.RemoveParameter(p);
        controller.AddParameter("Crawl", AnimatorControllerParameterType.Bool);
        var machine = controller.layers[0].stateMachine;
        foreach (var child in machine.states) machine.RemoveState(child.state);
        var idleState = machine.AddState("Idle");
        idleState.motion = idle;
        var crawlState = machine.AddState("Crawl");
        crawlState.motion = crawl;
        machine.defaultState = idleState;
        Transition(idleState, crawlState, AnimatorConditionMode.If);
        Transition(crawlState, idleState, AnimatorConditionMode.IfNot);
        EditorUtility.SetDirty(controller);
        return controller;
    }

    static void Transition(AnimatorState from, AnimatorState to, AnimatorConditionMode mode)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = false;
        t.duration = 0.15f;
        t.AddCondition(mode, 0f, "Crawl");
    }

    static void BuildPrefab(AnimatorController controller)
    {
        // Build in a preview scene so the open scene is not touched.
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
            go.name = "Ladybug";
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
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        string report = "motionNode=" + importer.motionNodeName + " avatarValid=" + model.GetComponent<Animator>()?.avatar?.isValid + "\n";
        foreach (var c in AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__")))
            report += "clip " + c.name + " len=" + c.length.ToString("0.###") + " loop=" + c.isLooping + " fps=" + c.frameRate + "\n";
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            foreach (var r in inst.GetComponentsInChildren<Renderer>())
                report += "renderer " + r.name + " bounds center=" + r.bounds.center.ToString("F3") + " size=" + r.bounds.size.ToString("F3")
                          + " mats=" + string.Join(",", r.sharedMaterials.Select(m => m.name + ":" + m.shader.name)) + "\n";
            var anim = inst.GetComponent<Animator>();
            report += "prefab animator controller=" + anim.runtimeAnimatorController?.name + " rootMotion=" + anim.applyRootMotion + "\n";
            // which way is the head? eyes are the most forward part of the mesh
            var smr = inst.GetComponentInChildren<SkinnedMeshRenderer>();
            var mesh = new Mesh();
            smr.BakeMesh(mesh);
            var verts = mesh.vertices.Select(v => smr.transform.TransformPoint(v)).ToArray();
            report += "baked z range " + verts.Min(v => v.z).ToString("F3") + " .. " + verts.Max(v => v.z).ToString("F3")
                      + ", y range " + verts.Min(v => v.y).ToString("F3") + " .. " + verts.Max(v => v.y).ToString("F3") + "\n";
            var head = inst.GetComponentsInChildren<Transform>().First(t => t.name == "Head");
            report += "Head bone world pos " + head.position.ToString("F3") + " (expected +Z forward)\n";
            Object.DestroyImmediate(mesh);
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
        return report;
    }
}
