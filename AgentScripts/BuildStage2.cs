using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

// Builds stage 2 of Main as the blocked rabbit burrow (design doc §8, ADR 0036). Keeps the stage's route ends (the start and the
// sunlit spot) and the RabbitStage root (its HiddenUntilSeason and Stage2SnowMelt); replaces everything under it.
// Positions are in the Stage2 root's space, so they follow the stage spacing (Stage.AdjustSpacing).
// unity command script run --file AgentScripts/BuildStage2.cs --entry BuildStage2.Run
public static class BuildStage2
{
    const string PrefabFolder = "Assets/03_Prefabs/Stage2";
    const string RabbitFolder = "Assets/01_Arts/Models/Rabbits";
    // Components of the old stage 2, removed from the rabbit prefabs.
    static readonly string[] OldScripts = { "RabbitFamily", "RabbitActor", "SnowBranch", "BranchDemo" };

    static Transform stageRoot;
    static Material softSnow;

    public static string Run()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (Application.isPlaying || scene.path != "Assets/00_Scenes/Main.unity")
            throw new InvalidOperationException("Open Main in Edit Mode.");
        Stage stage = GameObject.Find("Stage2").GetComponent<Stage>();
        stageRoot = stage.transform;
        Transform root = stageRoot.Find("RabbitStage");
        foreach (Transform child in root.Cast<Transform>().ToArray())
            UnityEngine.Object.DestroyImmediate(child.gameObject);
        softSnow = AssetDatabase.LoadAssetAtPath<Material>("Assets/01_Arts/Materials/SoftSnow.mat");

        GameObject babyRabbit = RabbitPrefab(PrefabFolder + "/BabyRabbit.prefab", "BabyRabbit", 0.9f);
        GameObject rabbit = RabbitPrefab(PrefabFolder + "/Rabbit.prefab", "Rabbit", 1.1f);

        // Lumpy snowbanks along the road, each a few snow mounds (Stage2SnowMelt melts the groups).
        Transform leftBank = Bank(root, "LeftBank", new[]
        {
            new Vector4(-4.5f, 33.6f, 1.6f, 10f), new Vector4(-5f, 35.2f, 1.9f, -15f), new Vector4(-4.7f, 37.8f, 1.7f, 20f),
            new Vector4(-5.1f, 39.2f, 1.6f, 0f), new Vector4(-4.4f, 40.4f, 1.3f, -5f),
        });
        Transform rightBank = Bank(root, "RightBank", new[]
        {
            new Vector4(2.6f, 33.8f, 1.6f, -12f), new Vector4(3.0f, 36.2f, 1.9f, 8f), new Vector4(2.7f, 38.6f, 1.6f, -6f),
            new Vector4(2.9f, 40.6f, 1.3f, 15f),
        });

        // The burrow is the hollow at the foot of an old tree on the left bank (the hollow tree of the squirrel egg), sunk so the
        // hollow is low. Its mouth looks out toward the road and the camera (out, in stage space).
        Vector3 out_ = new Vector3(0.86f, 0f, -0.51f);
        Vector3 across = new Vector3(-out_.z, 0f, out_.x);
        Vector3 trunk = new Vector3(-3.9f, 0f, 36.3f);
        Vector3 mouth = trunk + out_ * 0.7f;
        GameObject burrowRoot = Child(root.gameObject, "RabbitBurrow", P(mouth.x, 0f, mouth.z));
        GameObject tree = Child(burrowRoot, "BurrowTree", P(trunk.x, -0.65f, trunk.z));
        // Its root's -Z is where the hollow looks (HollowSquirrel.prefab).
        tree.transform.rotation = stageRoot.rotation * Quaternion.LookRotation(-out_);
        tree.transform.localScale = Vector3.one * 2f;
        var treeModel = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/01_Arts/Mesh/thickTree/thickTree.obj"), tree.transform);
        treeModel.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        PrefabUtility.RecordPrefabInstancePropertyModifications(treeModel.transform);
        // Darker deeper in the hollow.
        Vector3 deep = trunk + out_ * 0.15f;
        Ball(burrowRoot, "HoleDeep", P(deep.x, 0.55f, deep.z), new Vector3(0.55f, 0.55f, 0.55f), "Assets/01_Arts/Materials/Test/Face.mat");
        Transform hole = Child(burrowRoot, "Hole", P(mouth.x, 0f, mouth.z)).transform;
        Vector3 rim1 = mouth + across * 0.8f, rim2 = mouth - across * 0.8f;
        Snow(burrowRoot, "RimSnow_1", P(rim1.x, 0.05f, rim1.z), new Vector3(0.8f, 0.4f, 0.7f));
        Snow(burrowRoot, "RimSnow_2", P(rim2.x, 0.05f, rim2.z), new Vector3(0.7f, 0.35f, 0.8f));
        // Snow on the hollow's upper lip: a pale arch over the dark, which the dark trunk alone doesn't give.
        Vector3 lip = mouth + out_ * 0.05f;
        GameObject lipSnow = Snow(burrowRoot, "LipSnow", P(lip.x, 1.0f, lip.z), new Vector3(0.9f, 0.28f, 0.45f));
        lipSnow.transform.rotation = stageRoot.rotation * Quaternion.LookRotation(out_) * Quaternion.Euler(0f, 90f, 0f);
        PrefabUtility.RecordPrefabInstancePropertyModifications(lipSnow.transform);

        // The bird stands on the ground in front of the snow, facing it.
        Vector3 stand = mouth + out_ * 1.2f, mound = mouth + out_ * 0.4f;
        Transform perchPoint = Child(burrowRoot, "PerchPoint", P(stand.x, 0f, stand.z)).transform;
        perchPoint.rotation = stageRoot.rotation * Quaternion.LookRotation(-out_);
        GameObject snow = Snow(burrowRoot, "EntranceSnow", P(mound.x, 0.15f, mound.z), new Vector3(1.0f, 0.8f, 1.2f));
        snow.transform.rotation = stageRoot.rotation * Quaternion.LookRotation(across);
        PrefabUtility.RecordPrefabInstancePropertyModifications(snow.transform);
        snow.layer = LayerMask.NameToLayer("Interactable");
        PrefabUtility.RecordPrefabInstancePropertyModifications(snow);
        var perch = snow.AddComponent<Perchable>();
        Set(perch, so =>
        {
            so.FindProperty("perchPoint").objectReferenceValue = perchPoint;
            so.FindProperty("alwaysExempt").boolValue = true;
            so.FindProperty("alignBird").boolValue = true;
            so.FindProperty("faceForward").boolValue = true;
        });
        GameObject sparkle = Prefab("Assets/03_Prefabs/InteractiveSparkle.prefab", burrowRoot, "Sparkle", P(mound.x, 0.75f, mound.z));
        var indicator = snow.AddComponent<InteractiveIndicator>();
        Set(indicator, so => so.FindProperty("sparkle").objectReferenceValue = sparkle.GetComponent<ParticleSystem>());
        GameObject answer = Prefab("Assets/03_Prefabs/BirdCryPulse.prefab", burrowRoot, "Answer", P(mouth.x, 0.7f, mouth.z));
        ParticleSystem digSnow = DigSnow(burrowRoot, P(mound.x, 0.35f, mound.z), out_);
        Transform pacePoint = Child(burrowRoot, "PacePoint", P(-2.0f, 0f, 36f)).transform;
        pacePoint.rotation = stageRoot.rotation;

        foreach (Vector2 at in new[] { new Vector2(-2.5f, 34.5f), new Vector2(-2.3f, 37.5f), new Vector2(-3.8f, 34.0f), new Vector2(-3.7f, 38.1f) })
            Prefab("Assets/03_Prefabs/Grass/Grass01.prefab", burrowRoot, "Grass", P(at.x, 0f, at.y));

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
        Transform[] waits = { Child(burrowRoot, "Wait_1", P(-2.4f, 0f, 35.2f)).transform, Child(burrowRoot, "Wait_2", P(-2.4f, 0f, 36.9f)).transform };

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
            so.FindProperty("digSnow").objectReferenceValue = digSnow;
            so.FindProperty("pacePoint").objectReferenceValue = pacePoint;
        });

        // The baby stops a little before the road's rabbits and watches the burrow.
        BabyRoute route = stage.route;
        route[2].position = P(0f, 0f, 33.3f);
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
        Set(melt, so => Objects(so.FindProperty("snowbanks"), new UnityEngine.Object[] { leftBank, rightBank }));

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        return $"Stage 2: burrow at {hole.position}, stop at {route[2].position}, {road.Length} road rabbits, {inside.Length} inside.";
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
        // Small tracks from all four feet.
        var tracks = contents.GetComponent<FootprintMaker>() ?? contents.AddComponent<FootprintMaker>();
        Transform[] bones = contents.GetComponentsInChildren<Transform>(true);
        Set(tracks, so =>
        {
            Objects(so.FindProperty("feet"), new[] { "Foot.L", "Foot.R", "Paw.L", "Paw.R" }.Select(n => (UnityEngine.Object)bones.First(t => t.name == n)).ToArray());
            so.FindProperty("printSize").vector2Value = new Vector2(0.05f, 0.08f);
            so.FindProperty("minSwing").floatValue = 0.006f;
        });
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


    // A group of snow mounds: x, z (stage space), size, yaw.
    static Transform Bank(Transform parent, string name, Vector4[] mounds)
    {
        GameObject bank = Child(parent.gameObject, name, P(mounds[0].x, 0f, mounds[0].y));
        for (int i = 0; i < mounds.Length; i++)
        {
            Vector4 m = mounds[i];
            GameObject mound = Snow(bank, "Mound_" + i, P(m.x, 0.12f * m.z, m.y), new Vector3(m.z, m.z * 0.55f, m.z * 1.7f));
            mound.transform.rotation = stageRoot.rotation * Quaternion.Euler(0f, m.w, 0f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(mound.transform);
        }
        return bank.transform;
    }

    static GameObject Snow(GameObject parent, string name, Vector3 position, Vector3 scale)
    {
        GameObject snow = Prefab(PrefabFolder + "/Snowbank.prefab", parent, name, position);
        snow.transform.localScale = scale;
        snow.GetComponent<MeshRenderer>().sharedMaterial = softSnow;
        PrefabUtility.RecordPrefabInstancePropertyModifications(snow.transform);
        PrefabUtility.RecordPrefabInstancePropertyModifications(snow.GetComponent<MeshRenderer>());
        return snow;
    }

    static void Ball(GameObject parent, string name, Vector3 position, Vector3 scale, string materialPath)
    {
        GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ball.name = name;
        UnityEngine.Object.DestroyImmediate(ball.GetComponent<Collider>());
        ball.transform.SetParent(parent.transform, false);
        ball.transform.position = position;
        ball.transform.localScale = scale;
        ball.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
    }

    // Snow thrown up by the digging, emitted from code (RabbitBurrow).
    static ParticleSystem DigSnow(GameObject parent, Vector3 position, Vector3 outward)
    {
        GameObject go = Child(parent, "DigSnow", position);
        go.transform.rotation = stageRoot.rotation * Quaternion.LookRotation((outward + Vector3.up * 1.7f).normalized);
        var system = go.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = system.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
        main.gravityModifier = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 200;
        var emission = system.emission;
        emission.rateOverTime = 0f;
        var shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 35f;
        shape.radius = 0.2f;
        var fade = system.colorOverLifetime;
        fade.enabled = true;
        var alpha = new Gradient();
        alpha.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
        fade.color = alpha;
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/01_Arts/Materials/Snow.mat");
        return system;
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
