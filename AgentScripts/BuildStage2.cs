using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

// Builds stage 2 of Main as the blocked rabbit burrow (design doc §8, ADR 0036). Keeps the stage's route ends (the start and the
// sunlit spot), the RabbitStage root (its HiddenUntilSeason and Stage2SnowMelt) and the two roadside snowbanks; replaces the rest.
// Positions are in the Stage2 root's space, so they follow the stage spacing (Stage.AdjustSpacing).
// unity command script run --file AgentScripts/BuildStage2.cs --entry BuildStage2.Run
public static class BuildStage2
{
    const string PrefabFolder = "Assets/03_Prefabs/Stage2";
    const string RabbitFolder = "Assets/01_Arts/Models/Rabbits";
    static readonly string[] OldScripts = { "RabbitFamily", "RabbitActor", "SnowBranch", "BranchDemo" };

    static Transform stageRoot;

    public static string Run()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (Application.isPlaying || scene.path != "Assets/00_Scenes/Main.unity")
            throw new InvalidOperationException("Open Main in Edit Mode.");
        Stage stage = GameObject.Find("Stage2").GetComponent<Stage>();
        stageRoot = stage.transform;
        Transform root = stageRoot.Find("RabbitStage");
        foreach (Transform child in root.Cast<Transform>().ToArray())
            if (child.name != "RightBank" && child.name != "LeftOuterBank")
                UnityEngine.Object.DestroyImmediate(child.gameObject);

        GameObject babyRabbit = RabbitPrefab(PrefabFolder + "/BabyRabbit.prefab", "BabyRabbit", 0.9f);
        GameObject rabbit = RabbitPrefab(PrefabFolder + "/Rabbit.prefab", "Rabbit", 1.1f);

        GameObject burrowRoot = Child(root.gameObject, "RabbitBurrow", P(-3.3f, 0f, 36f));
        Transform hole = Child(burrowRoot, "Hole", P(-3.45f, 0f, 36f)).transform;
        GameObject holeLook = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        holeLook.name = "HoleLook";
        UnityEngine.Object.DestroyImmediate(holeLook.GetComponent<Collider>());
        holeLook.transform.SetParent(burrowRoot.transform, false);
        holeLook.transform.position = P(-3.6f, 0.3f, 36f);
        holeLook.transform.localScale = new Vector3(0.2f, 0.6f, 0.75f);
        holeLook.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/01_Arts/Materials/Test/Face.mat");

        // The bird stands on the ground in front of the snow, facing it.
        Transform perchPoint = Child(burrowRoot, "PerchPoint", P(-2.35f, 0f, 36f)).transform;
        perchPoint.rotation = stageRoot.rotation * Quaternion.Euler(0f, -90f, 0f);
        GameObject snow = Prefab(PrefabFolder + "/Snowbank.prefab", burrowRoot, "EntranceSnow", P(-3.05f, 0.2f, 36f));
        snow.transform.localScale = new Vector3(1.3f, 1f, 1.5f);
        snow.layer = LayerMask.NameToLayer("Interactable");
        PrefabUtility.RecordPrefabInstancePropertyModifications(snow);
        PrefabUtility.RecordPrefabInstancePropertyModifications(snow.transform);
        var perch = snow.AddComponent<Perchable>();
        Set(perch, so =>
        {
            so.FindProperty("perchPoint").objectReferenceValue = perchPoint;
            so.FindProperty("alwaysExempt").boolValue = true;
            so.FindProperty("alignBird").boolValue = true;
            so.FindProperty("faceForward").boolValue = true;
        });
        GameObject sparkle = Prefab("Assets/03_Prefabs/InteractiveSparkle.prefab", burrowRoot, "Sparkle", P(-3.05f, 0.9f, 36f));
        var indicator = snow.AddComponent<InteractiveIndicator>();
        Set(indicator, so => so.FindProperty("sparkle").objectReferenceValue = sparkle.GetComponent<ParticleSystem>());
        GameObject answer = Prefab("Assets/03_Prefabs/BirdCryPulse.prefab", burrowRoot, "Answer", P(-3.3f, 0.5f, 36f));

        BurrowRabbit[] road =
        {
            Rabbit(babyRabbit, root.gameObject, "RoadRabbit_1", P(-0.55f, 0f, 35.6f), 250f),
            Rabbit(babyRabbit, root.gameObject, "RoadRabbit_2", P(0.1f, 0f, 36.2f), 215f),
            Rabbit(babyRabbit, root.gameObject, "RoadRabbit_3", P(0.7f, 0f, 35.5f), 290f),
        };
        BurrowRabbit[] inside =
        {
            Rabbit(rabbit, burrowRoot, "BurrowRabbit_1", hole.position, 90f),
            Rabbit(rabbit, burrowRoot, "BurrowRabbit_2", hole.position, 90f),
        };
        foreach (BurrowRabbit r in inside)
            r.gameObject.SetActive(false);
        Transform[] waits = { Child(burrowRoot, "Wait_1", P(-2.45f, 0f, 35.3f)).transform, Child(burrowRoot, "Wait_2", P(-2.45f, 0f, 36.8f)).transform };

        var timer = burrowRoot.AddComponent<NaturalTimer>();
        Set(timer, so => so.FindProperty("kind").enumValueIndex = (int)NaturalTimerKind.RabbitBurrow);
        var burrow = burrowRoot.AddComponent<RabbitBurrow>();
        Set(burrow, so =>
        {
            so.FindProperty("timer").objectReferenceValue = timer;
            so.FindProperty("snowPerch").objectReferenceValue = perch;
            so.FindProperty("snow").objectReferenceValue = snow.transform;
            so.FindProperty("answer").objectReferenceValue = answer;
            so.FindProperty("hole").objectReferenceValue = hole;
            Objects(so.FindProperty("roadRabbits"), road);
            Objects(so.FindProperty("burrowRabbits"), inside);
            Objects(so.FindProperty("waitPoints"), waits);
        });

        // The baby stops a little before the road's rabbits and watches the burrow.
        BabyRoute route = stage.route;
        route[2].position = P(0f, 0f, 34f);
        route[3].position = P(0f, 0f, 38f);
        var stop = route[2].GetComponent<StopPoint>();
        Set(stop, so =>
        {
            so.FindProperty("blocker").objectReferenceValue = burrow;
            so.FindProperty("pose").enumValueIndex = (int)WaitPose.Stand;
            so.FindProperty("byBirdRoute").objectReferenceValue = null;
            so.FindProperty("observeTarget").objectReferenceValue = hole;
        });

        GameObject zoneRoot = Child(root.gameObject, "CameraZone_Burrow", P(-1f, 0f, 35.5f));
        var zone = zoneRoot.AddComponent<CameraZone>();
        Set(zone, so =>
        {
            so.FindProperty("blocker").objectReferenceValue = burrow;
            so.FindProperty("trigger").objectReferenceValue = Child(zoneRoot, "Trigger", P(0f, 0f, 32.5f)).transform;
            so.FindProperty("triggerSize").vector2Value = new Vector2(16f, 2f);
            so.FindProperty("lockSize").vector2Value = new Vector2(20f, 13f);
            so.FindProperty("areaSize").vector2Value = new Vector2(3f, 2f);
        });

        var melt = root.GetComponent<Stage2SnowMelt>();
        Set(melt, so => Objects(so.FindProperty("snowbanks"), new UnityEngine.Object[] { root.Find("RightBank"), root.Find("LeftOuterBank") }));

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        foreach (string name in OldScripts)
            AssetDatabase.DeleteAsset("Assets/02_Scripts/Stage2/" + name + ".cs");
        AssetDatabase.DeleteAsset(PrefabFolder + "/SnowBranch.prefab");
        AssetDatabase.SaveAssets();

        var pulse = answer.GetComponentsInChildren<ParticleSystem>(true);
        return $"Stage 2: burrow at {hole.position}, stop at {route[2].position}, {road.Length} road rabbits, {inside.Length} inside. " +
            $"Answer playOnAwake: {string.Join(",", pulse.Select(p => p.main.playOnAwake))}";
    }

    static Vector3 P(float x, float y, float z) => stageRoot.TransformPoint(new Vector3(x, y, z));

    // The rabbit prefab: the model under a root with a BurrowRabbit (BabyRabbit.prefab exists and loses its old RabbitActor).
    static GameObject RabbitPrefab(string path, string modelName, float hopSpeed)
    {
        bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
        GameObject contents = exists ? PrefabUtility.LoadPrefabContents(path) : new GameObject(modelName);
        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(contents);
        foreach (MonoBehaviour behaviour in contents.GetComponents<MonoBehaviour>())
            if (Array.IndexOf(OldScripts, behaviour.GetType().Name) >= 0)
                UnityEngine.Object.DestroyImmediate(behaviour);
        Animator animator = contents.GetComponentInChildren<Animator>();
        if (animator == null)
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(RabbitFolder + "/" + modelName + "-Animated.fbx"), contents.transform);
            animator = model.GetComponent<Animator>();
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(RabbitFolder + "/" + modelName + ".controller");
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }
        var rabbit = contents.GetComponent<BurrowRabbit>() ?? contents.AddComponent<BurrowRabbit>();
        Set(rabbit, so =>
        {
            so.FindProperty("animator").objectReferenceValue = animator;
            so.FindProperty("hopSpeed").floatValue = hopSpeed;
        });
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(contents, path);
        if (exists)
            PrefabUtility.UnloadPrefabContents(contents);
        else
            UnityEngine.Object.DestroyImmediate(contents);
        return saved;
    }

    static BurrowRabbit Rabbit(GameObject prefab, GameObject parent, string name, Vector3 position, float yaw)
    {
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.transform);
        instance.name = name;
        instance.transform.SetPositionAndRotation(position, stageRoot.rotation * Quaternion.Euler(0f, yaw, 0f));
        PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
        return instance.GetComponent<BurrowRabbit>();
    }

    static GameObject Prefab(string path, GameObject parent, string name, Vector3 position)
    {
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), parent.transform);
        instance.name = name;
        instance.transform.position = position;
        PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
        PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
        return instance;
    }

    static GameObject Child(GameObject parent, string name, Vector3 position)
    {
        var child = new GameObject(name);
        child.transform.SetParent(parent.transform, false);
        child.transform.position = position;
        return child;
    }

    static void Set(UnityEngine.Object target, Action<SerializedObject> action)
    {
        var so = new SerializedObject(target);
        action(so);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void Objects(SerializedProperty property, UnityEngine.Object[] values)
    {
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }
}
