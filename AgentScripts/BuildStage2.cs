using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;

// Updates only stage 2 in Sam_Test; keeps the terrain, forest, stage 1 and player/camera setup.
// After BuildSamTestScene.Build (full reset), run this pass to restore the rabbit stage:
// unity command script run --file AgentScripts/BuildStage2.cs --entry BuildStage2.Run
public static class BuildStage2
{
    const string RabbitFolder = "Assets/01_Arts/Models/Rabbits";
    const string PrefabFolder = "Assets/03_Prefabs/Stage2";
    // main's snowflake material (Snowfall), also used for the snow a branch sheds.
    const string SnowflakeMaterialPath = "Assets/01_Arts/Materials/Snow.mat";

    public static string Run()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (Application.isPlaying || scene.path != "Assets/00_Scenes/Sam_Test.unity")
            throw new InvalidOperationException("Open Sam_Test in Edit Mode.");
        Stage stage = GameObject.Find("Stage2").GetComponent<Stage>();
        GameObject oldGameplay = stage.transform.Find("RabbitStage")?.gameObject;
        if (oldGameplay != null) UnityEngine.Object.DestroyImmediate(oldGameplay);
        GameObject oldRest = stage.transform.Find("SunlitSpot")?.gameObject;
        if (oldRest != null) UnityEngine.Object.DestroyImmediate(oldRest);
        if (stage.route != null) UnityEngine.Object.DestroyImmediate(stage.route.gameObject);
        Configure(stage);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        return "Stage 2: demo bird, 2 snow branches, mother + 3 babies, bypass/natural routes and local snow melt.";
    }

    // Brings the saved Sam_Test in line without rebuilding stage 2: no branch sound sources, main's snowflake material on the shed snow.
    public static string UseMainSnowAssets()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (Application.isPlaying || scene.path != "Assets/00_Scenes/Sam_Test.unity")
            throw new InvalidOperationException("Open Sam_Test in Edit Mode.");
        SnowBranch[] branches = GameObject.Find("Stage2").GetComponentsInChildren<SnowBranch>(true);
        int sources = 0;
        foreach (SnowBranch branch in branches)
        {
            foreach (AudioSource source in branch.GetComponents<AudioSource>())
            {
                UnityEngine.Object.DestroyImmediate(source);
                sources++;
            }
            var powder = (ParticleSystem)new SerializedObject(branch).FindProperty("powder").objectReferenceValue;
            powder.GetComponent<ParticleSystemRenderer>().sharedMaterial = ParticleMat();
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return branches.Length + " branches: " + sources + " sound sources removed, shed snow uses " + SnowflakeMaterialPath;
    }

    public static void Configure(Stage stage)
    {
        if (Application.isPlaying || stage.gameObject.scene.path != "Assets/00_Scenes/Sam_Test.unity")
            throw new InvalidOperationException("Open Sam_Test in Edit Mode.");
        GameObject root = Child(stage.gameObject, "RabbitStage", Vector3.zero);
        var snowbanks = new List<Transform>();
        // An inner bank separates the sleeping family from the left bypass; its ends are the entrance and merge.
        Bank(root, "RightBank", new Vector3(2.75f, 0.45f, 33f), new Vector3(2.1f, 1.6f, 8.5f), snowbanks);
        Bank(root, "LeftOuterBank", new Vector3(-4.6f, 0.45f, 33f), new Vector3(1.7f, 1.7f, 8f), snowbanks);
        Bank(root, "Divider", new Vector3(-1.25f, 0.28f, 33f), new Vector3(1.1f, 1.1f, 3.2f), snowbanks);
        Bank(root, "LeftApproachBank", new Vector3(-1.4f, 0.25f, 28.6f), new Vector3(1.2f, 1.2f, 3.4f), snowbanks);
        Bank(root, "LeftExitBank", new Vector3(-1.4f, 0.25f, 37.2f), new Vector3(1.2f, 1.1f, 2f), snowbanks);

        SnowBranch demo = Branch(root, "DemoBranch", new Vector3(-3.2f, 0f, 28.5f));
        SnowBranch[] branches = {
            Branch(root, "BypassBranch_1", new Vector3(-4.3f, 0f, 32.1f)),
            Branch(root, "BypassBranch_2", new Vector3(-4.3f, 0f, 34f))
        };
        RabbitActor[] rabbits = {
            Rabbit(root, "Mother", false, new Vector3(0.1f, 0f, 33.4f), 200f, 0,
                new[]{new Vector3(0.9f,0f,34.5f),new Vector3(1.4f,0f,37f)}),
            Rabbit(root, "Kit_Flat", true, new Vector3(-0.36f, 0f, 32.6f), 160f, 1,
                new[]{new Vector3(0.7f,0f,34.1f),new Vector3(1.4f,0f,35.1f)}),
            Rabbit(root, "Kit_Side", true, new Vector3(0.55f, 0f, 32.75f), 250f, 2,
                new[]{new Vector3(0.85f,0f,34.4f),new Vector3(1.4f,0f,36f)}),
            Rabbit(root, "Kit_Loaf", true, new Vector3(-0.35f, 0f, 33.75f), 210f, 0,
                new[]{new Vector3(0.8f,0f,34.2f),new Vector3(1.4f,0f,34.3f)})
        };
        var familyRoot = Child(root, "SleepingFamily", new Vector3(0f, 0f, 33.3f));
        var timer = familyRoot.AddComponent<NaturalTimer>();
        Set(timer, so => so.FindProperty("kind").enumValueIndex = (int)NaturalTimerKind.RabbitFamily);
        var family = familyRoot.AddComponent<RabbitFamily>();
        Set(family, so =>
        {
            so.FindProperty("timer").objectReferenceValue = timer;
            Objects(so.FindProperty("branches"), branches);
            Objects(so.FindProperty("rabbits"), rabbits);
        });
        BabyRoute bypass = Route(root, "BypassRoute", new[]{new Vector3(-2.75f,0f,31f),new Vector3(-2.75f,0f,32.1f),
            new Vector3(-2.75f,0f,34f),new Vector3(-2.75f,0f,35.2f),new Vector3(0f,0f,35.9f)});
        stage.route = Route(stage.gameObject, "BabyRoute", new[]{new Vector3(0f,0f,27f),new Vector3(0f,0f,29.5f),
            new Vector3(0f,0f,31f),new Vector3(0f,0f,35.9f),new Vector3(0f,0f,38.5f)});
        var stop = stage.route[2].gameObject.AddComponent<StopPoint>();
        Set(stop, so =>
        {
            so.FindProperty("blocker").objectReferenceValue = family;
            so.FindProperty("pose").enumValueIndex = (int)WaitPose.Crouch;
            so.FindProperty("byBirdRoute").objectReferenceValue = bypass;
            so.FindProperty("observeTarget").objectReferenceValue = familyRoot.transform;
        });
        SetupDemo(root, demo);
        CameraZone(root, family);
        Prefab("Assets/03_Prefabs/SunlitSpot.prefab", root, "SunlitSpot", new Vector3(0f,0f,38.5f));
        var melt = root.AddComponent<Stage2SnowMelt>();
        Set(melt, so =>
        {
            so.FindProperty("season").objectReferenceValue = UnityEngine.Object.FindAnyObjectByType<Season>();
            Objects(so.FindProperty("snowbanks"), snowbanks.ToArray());
        });
    }

    static SnowBranch Branch(GameObject parent, string name, Vector3 position)
    {
        return Prefab(PrefabFolder+"/SnowBranch.prefab", parent, name, position).GetComponent<SnowBranch>();
    }

    static RabbitActor Rabbit(GameObject parent, string name, bool baby, Vector3 position, float yaw, int variant, Vector3[] exit)
    {
        GameObject root = baby ? Prefab(PrefabFolder+"/BabyRabbit.prefab", parent, name, position) : Child(parent, name, position);
        root.transform.rotation = Quaternion.Euler(0f,yaw,0f);
        if (baby) PrefabUtility.RecordPrefabInstancePropertyModifications(root.transform);
        string prefix = baby ? "BabyRabbit" : "Rabbit";
        string path = RabbitFolder+"/"+prefix+"-Animated.fbx";
        GameObject model;
        Animator animator;
        if (baby)
        {
            animator = root.GetComponentInChildren<Animator>();
            model = animator.gameObject;
        }
        else
        {
            model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), root.transform);
            animator = model.GetComponent<Animator>();
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(RabbitFolder+"/"+prefix+".controller");
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }
        string suffix = variant == 1 ? "_Flat" : variant == 2 ? "_Side" : "";
        var clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().ToDictionary(c=>c.name);
        clips[prefix+"_Sleep"+suffix].SampleAnimation(model,0f);
        if (baby)
            foreach (Transform pose in model.GetComponentsInChildren<Transform>())
                PrefabUtility.RecordPrefabInstancePropertyModifications(pose);
        var actor = baby ? root.GetComponent<RabbitActor>() : root.AddComponent<RabbitActor>();
        Transform[] exits = exit.Select((p,i)=>Child(parent,name+"_Exit_"+i,p).transform).ToArray();
        Set(actor, so=>
        {
            so.FindProperty("animator").objectReferenceValue = animator;
            so.FindProperty("wakeClip").objectReferenceValue = clips[prefix+"_WakeUp"+suffix];
            so.FindProperty("sleepVariant").intValue = variant;
            so.FindProperty("hopSpeed").floatValue = baby ? 0.34f : 0.61f;
            Objects(so.FindProperty("exitPoints"),exits);
        });
        return actor;
    }

    static void SetupDemo(GameObject parent, SnowBranch branch)
    {
        var demoRoot = Child(parent,"BranchDemonstration",Vector3.zero);
        var bird = Child(demoRoot,"DemoBird",new Vector3(-4f,3f,26f));
        var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/Bird-Animated.prefab"),bird.transform);
        model.transform.localScale = Vector3.one * 0.24f;
        model.transform.localRotation = Quaternion.identity;
        model.transform.localPosition = -new Vector3(0f,0.38f,-1.07f)*0.24f;
        var animator = model.GetComponent<Animator>();
        animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/01_Arts/Models/Bird/Bird.controller");
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        if (model.GetComponent<BirdAnimationEvents>() == null) model.AddComponent<BirdAnimationEvents>();
        foreach (Transform t in model.GetComponentsInChildren<Transform>()) t.gameObject.layer=LayerMask.NameToLayer("Bird");
        var demo = demoRoot.AddComponent<BranchDemo>();
        Set(demo,so=>
        {
            so.FindProperty("trigger").objectReferenceValue=Child(demoRoot,"Trigger",new Vector3(0f,0f,27.2f)).transform;
            so.FindProperty("branch").objectReferenceValue=branch;
            so.FindProperty("bird").objectReferenceValue=bird.transform;
            so.FindProperty("animator").objectReferenceValue=animator;
            so.FindProperty("start").objectReferenceValue=Child(demoRoot,"Start",bird.transform.position).transform;
            so.FindProperty("exit").objectReferenceValue=Child(demoRoot,"Exit",new Vector3(-7f,5f,32f)).transform;
        });
    }

    static void CameraZone(GameObject parent, RabbitFamily family)
    {
        GameObject root=Child(parent,"CameraZone_Rabbits",new Vector3(-0.3f,0f,31.8f));
        var zone=root.AddComponent<CameraZone>();
        Set(zone,so=>
        {
            so.FindProperty("blocker").objectReferenceValue=family;
            so.FindProperty("trigger").objectReferenceValue=Child(root,"Trigger",new Vector3(0f,0f,29.7f)).transform;
            so.FindProperty("triggerSize").vector2Value=new Vector2(16f,2f);
            so.FindProperty("lockSize").vector2Value=new Vector2(20f,13f);
            so.FindProperty("areaSize").vector2Value=new Vector2(3f,2f);
        });
    }

    static void Bank(GameObject parent,string name,Vector3 position,Vector3 scale,List<Transform> banks)
    {
        var bank=Prefab(PrefabFolder+"/Snowbank.prefab",parent,name,position);
        bank.transform.localScale=scale;
        PrefabUtility.RecordPrefabInstancePropertyModifications(bank.transform);
        banks.Add(bank.transform);
    }
    static BabyRoute Route(GameObject parent,string name,Vector3[] points)
    {
        var root=Child(parent,name,Vector3.zero);
        for(int i=0;i<points.Length;i++)Child(root,"Point_"+i,points[i]);
        return root.AddComponent<BabyRoute>();
    }
    static Material ParticleMat()=>AssetDatabase.LoadAssetAtPath<Material>(SnowflakeMaterialPath);
    static GameObject Prefab(string path,GameObject parent,string name,Vector3 position)
    {
        var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path),parent.transform);
        root.name=name;root.transform.position=position;
        PrefabUtility.RecordPrefabInstancePropertyModifications(root);
        PrefabUtility.RecordPrefabInstancePropertyModifications(root.transform);
        return root;
    }
    static GameObject Child(GameObject parent,string name,Vector3 position)
    {
        var root=new GameObject(name);root.transform.SetParent(parent.transform,false);root.transform.position=position;return root;
    }
    static void Set(UnityEngine.Object target,Action<SerializedObject> action)
    {var so=new SerializedObject(target);action(so);so.ApplyModifiedPropertiesWithoutUndo();}
    static void Objects(SerializedProperty property,UnityEngine.Object[] values)
    {property.arraySize=values.Length;for(int i=0;i<values.Length;i++)property.GetArrayElementAtIndex(i).objectReferenceValue=values[i];}
}
