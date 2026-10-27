using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Builds the grey-box test scenes. Sam_Test.unity: stage 1 (fallen log, leaning log, sunlit rest area) and a minimal stage 2
// to test the stage transition; also rebuilds Assets/03_Prefabs/Tree.prefab (decoration only). Sam_Fog_Test.unity: the fog stage.
// Run: unity command script run --file AgentScripts/BuildSamTestScene.cs --entry BuildSamTestScene.Build
public static class BuildSamTestScene
{
    const string ScenePath = "Assets/00_Scenes/Sam_Test.unity";
    const string FogScenePath = "Assets/00_Scenes/Sam_Fog_Test.unity";
    const string FogMaterialPath = "Assets/01_Arts/Materials/Fog.mat";
    const string MaterialFolder = "Assets/01_Arts/Materials/Test";
    const string TreePrefabPath = "Assets/03_Prefabs/Tree.prefab";
    const string BirdModelPath = "Assets/01_Arts/Models/Bird/Bird-Animated.fbx";
    const string BirdModelPrefabPath = "Assets/03_Prefabs/Bird-Animated.prefab";
    const string BirdAnimatorPath = "Assets/01_Arts/Models/Bird/Bird.controller";
    // Wingspan about 2.2 (5.4 in the model), next to the baby's height of 1, so it reads from the quarter-view camera.
    const float BirdModelScale = 0.4f;
    // Where the toes are in the model while perched (Bird_PerchIdle), in model space.
    static readonly Vector3 BirdModelPerchedToes = new(0f, 0.38f, -1.07f);
    // Seconds of gliding between single wingbeats. Less than the Bird_Glide clip (2.5).
    const float BirdGlideFlapInterval = 2f;
    const float BirdAltitude = 2.5f;
    // Seen from behind-right of the road, so the +Z road runs from lower-left to upper-right on screen (director's sketch).
    static readonly Vector3 CameraOffset = new(10f, 10f, -10f);
    // The camera aims this far above the followed point, so the leaning log's top stays in frame.
    const float CameraAimHeight = 1.5f;
    // Short enough that its top (the perch) stays low in the fixed camera's frame, long enough to span stream 2 when fallen.
    const float LeaningLogLength = 3.4f;

    static int groundLayer, interactableLayer, clickPlaneLayer;

    public static string Build()
    {
        NewScene();
        GameObject treePrefab = BuildTreePrefab();
        return Finish(ScenePath, BuildStage1(treePrefab), BuildStage2());
    }

    // Step 2: the fog stage alone (3-0 start, 3-1 half fog, 3-2 full fog, a rest area).
    // Run: unity command script run --file AgentScripts/BuildSamTestScene.cs --entry BuildSamTestScene.BuildFog
    public static string BuildFog()
    {
        NewScene();
        return Finish(FogScenePath, BuildFogStage());
    }

    static void NewScene()
    {
        groundLayer = LayerMask.NameToLayer("Ground");
        interactableLayer = LayerMask.NameToLayer("Interactable");
        clickPlaneLayer = LayerMask.NameToLayer("ClickPlane");

        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        var clickPlane = new GameObject("ClickPlane", typeof(BoxCollider)) { layer = clickPlaneLayer };
        clickPlane.transform.position = new Vector3(30f, -1f, 15f);
        clickPlane.GetComponent<BoxCollider>().size = new Vector3(400f, 0.1f, 400f);
    }

    // Adds the baby, the bird, the camera brain, the fader and the game flow over the given stages, and saves the scene.
    static string Finish(string scenePath, params Stage[] stages)
    {
        Baby baby = BuildBaby();
        BirdController bird = BuildBird();
        bird.transform.position = stages[0].birdStart.position;
        baby.transform.SetPositionAndRotation(stages[0].babyStart.position, stages[0].babyStart.rotation);

        Camera.main.gameObject.AddComponent<CinemachineBrain>();
        BirdCamera birdCamera = BuildBirdCamera();

        ScreenFader fader = BuildFader();
        var flow = new GameObject("GameFlow");
        var gameFlow = flow.AddComponent<GameFlow>();
        flow.AddComponent<DevTools>();
        Set(gameFlow, so =>
        {
            SerializedProperty list = so.FindProperty("stages");
            list.arraySize = stages.Length;
            for (int i = 0; i < stages.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = stages[i];
            so.FindProperty("fader").objectReferenceValue = fader;
            so.FindProperty("birdCamera").objectReferenceValue = birdCamera;
        });

        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), scenePath);
        AssetDatabase.SaveAssets();
        return scenePath;
    }

    // 1-1 fallen log over the first stream, 1-2 leaning log by the second stream; the stage ends in the sunlit spot right after it.
    // The road runs along +Z.
    static Stage BuildStage1(GameObject treePrefab)
    {
        var root = new GameObject("Stage1");
        Material snow = Mat("Snow", new Color(0.92f, 0.95f, 1f));
        Material water = Mat("Water", new Color(0.35f, 0.55f, 0.8f));
        Material wood = Mat("Trunk", new Color(0.35f, 0.25f, 0.18f));

        Ground(root, "Bank_A", new Vector3(0f, 0f, 3f), 10f, snow);     // z -2 .. 8
        Ground(root, "Bank_B", new Vector3(0f, 0f, 16f), 8f, snow);     // z 12 .. 20
        Ground(root, "Bank_C", new Vector3(0f, 0f, 24.75f), 4.5f, snow);  // z 22.5 .. 27
        Water(root, "Stream_1", new Vector3(0f, 0f, 10f), 4f, water);
        Water(root, "Stream_2", new Vector3(0f, 0f, 21.25f), 2.5f, water); // z 20 .. 22.5

        // 1-1: the fallen log is already a bridge; the stump shows it once stood.
        GameObject bridge = Primitive(PrimitiveType.Cylinder, "FallenLog", new Vector3(0f, 0.3f, 10f), new Vector3(0.6f, 2.5f, 0.6f), wood, root);
        bridge.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        bridge.layer = groundLayer;
        Primitive(PrimitiveType.Cylinder, "Stump", new Vector3(0f, 0.35f, 7.2f), new Vector3(0.65f, 0.35f, 0.65f), wood, root);

        LeaningLog leaningLog = BuildLeaningLog(root, new Vector3(0f, 0.3f, 19.6f), wood);


        // Only on the far side of the road, so they do not hide the road from the camera.
        foreach (float z in new[] { 2f, 15f, 24f })
            PlaceTree(treePrefab, root, new Vector3(-4.5f, 0f, z), 90f);

        // Points carry height: 0.6 is the top of a fallen log.
        BabyRoute route = Route(root,
            new Vector3(0f, 0f, 6.8f), new Vector3(0f, 0.6f, 8f), new Vector3(0f, 0.6f, 12f), new Vector3(0f, 0f, 13f),
            new Vector3(0f, 0f, 19f), new Vector3(0f, 0.6f, 20.2f), new Vector3(0f, 0.6f, 22.7f), new Vector3(0f, 0f, 23.5f),
            new Vector3(0f, 0f, 25f));
        // The sunlit spot is not a section of its own: the stage ends in it right after the last device (ADR 0006).
        RestLight(root, route[route.Count - 1].position);
        StopPoint stop = route[4].gameObject.AddComponent<StopPoint>();
        Set(stop, so => so.FindProperty("blocker").objectReferenceValue = leaningLog);

        // When the baby nears the log, the camera eases onto the log and stream until the log has fallen.
        // The wall at z 21.5 is just past the perch on the log's top (z 20.9), so the bird cannot cross the stream before.
        Zone(root, leaningLog, new Vector3(0f, 0f, 16f), new Vector3(0f, 0f, 19.5f), new Vector2(4f, 5f), 21.5f);

        // The road is the ground strip, z -2 .. 27.
        return StageComponent(root, new Vector3(0f, 0f, 0f), new Vector3(1.5f, BirdAltitude, -1f), route,
            new Bounds(new Vector3(0f, 0f, 12.5f), new Vector3(10f, 1f, 29f)));
    }

    // Just a start and a rest area, to see the transition from stage 1.
    static Stage BuildStage2()
    {
        var root = new GameObject("Stage2");
        root.transform.position = new Vector3(60f, 0f, 0f);
        Ground(root, "Bank", new Vector3(60f, 0f, 5f), 14f, Mat("Snow", new Color(0.92f, 0.95f, 1f)));
        RestLight(root, new Vector3(60f, 0f, 9f));
        BabyRoute route = Route(root, new Vector3(60f, 0f, 4f), new Vector3(60f, 0f, 9f));
        return StageComponent(root, new Vector3(60f, 0f, 0f), new Vector3(61.5f, BirdAltitude, -1f), route,
            new Bounds(new Vector3(60f, 0f, 5f), new Vector3(10f, 1f, 14f)));
    }

    // The ground is only paths (correct ones and dead ends); everything else is dark water hidden under the fog.
    // 3-1: the fog starts at z 6; the left fork is the way (an animal sits on it), the right fork ends at a hidden rock.
    // 3-2: fully fogged, a winding way with two dead ends (a rock, and open water).
    static Stage BuildFogStage()
    {
        var root = new GameObject("Stage3_Fog");
        Material snow = Mat("Snow", new Color(0.92f, 0.95f, 1f));
        Material rock = Mat("Rock", new Color(0.45f, 0.45f, 0.48f));

        GameObject water = Primitive(PrimitiveType.Cube, "Water", new Vector3(0f, -0.6f, 19f), new Vector3(30f, 0.2f, 50f), Mat("Water", new Color(0.35f, 0.55f, 0.8f)), root);
        Object.DestroyImmediate(water.GetComponent<Collider>());

        // 3-0 and 3-1
        PathTile(root, new Vector3(0f, 0f, -2f), new Vector3(0f, 0f, 6f), snow);
        Vector3[] path31 = { new(0f, 0f, 6f), new(-3f, 0f, 9f), new(-3f, 0f, 14f), new(0f, 0f, 17f), new(0f, 0f, 20f) };
        PathTiles(root, path31, snow);
        PathTile(root, new Vector3(0f, 0f, 6f), new Vector3(3f, 0f, 9f), snow);
        PathTile(root, new Vector3(3f, 0f, 9f), new Vector3(3f, 0f, 12f), snow);
        HiddenRock(root, new Vector3(3f, 0f, 10f), rock);

        // 3-2
        Vector3[] path32 = { new(0f, 0f, 20f), new(3f, 0f, 23f), new(3f, 0f, 27f), new(-2f, 0f, 30f), new(-2f, 0f, 34f), new(0f, 0f, 36f), new(0f, 0f, 38.5f) };
        PathTiles(root, path32, snow);
        PathTile(root, new Vector3(3f, 0f, 27f), new Vector3(6f, 0f, 30f), snow);
        HiddenRock(root, new Vector3(5.2f, 0f, 29.2f), rock);
        PathTile(root, new Vector3(-2f, 0f, 30f), new Vector3(-5f, 0f, 28.5f), snow);

        FogSection fog31 = BuildFogSection(root, "Fog_3-1", new Vector3(0f, 0f, 11.5f), new Vector2(16f, 11f),
            new[] { new Vector3(0f, 0f, 6f), new Vector3(-3f, 0f, 9f), new Vector3(-3f, 0f, 14f), new Vector3(0f, 0f, 17f) }, new Vector3(0f, 0f, 18f),
            new[] { new Vector3(-3f, 0f, 10.5f) });
        FogSection fog32 = BuildFogSection(root, "Fog_3-2", new Vector3(0f, 0f, 27.5f), new Vector2(16f, 15f),
            new[] { new Vector3(0f, 0f, 20f), new Vector3(3f, 0f, 23f), new Vector3(3f, 0f, 27f), new Vector3(-2f, 0f, 30f), new Vector3(-2f, 0f, 34f) }, new Vector3(0f, 0f, 36f),
            new[] { new Vector3(3f, 0f, 25f), new Vector3(-2f, 0f, 32f) });


        BabyRoute route = Route(root,
            new Vector3(0f, 0f, 5.5f), new Vector3(0f, 0f, 18f), new Vector3(0f, 0f, 19.5f), new Vector3(0f, 0f, 36f), new Vector3(0f, 0f, 37.5f));
        StopAt(route[0], fog31);
        StopAt(route[2], fog32);
        RestLight(root, route[route.Count - 1].position);

        // When the baby nears a fog section, the camera eases onto it until the section is through.
        // Each wall is just past the section's exit, so the bird can lead the baby out but cannot clear the next fog early.
        Zone(root, fog31, new Vector3(0f, 0f, 4f), new Vector3(0f, 0f, 11.5f), new Vector2(6f, 9f), 19f);
        Zone(root, fog32, new Vector3(0f, 0f, 18.5f), new Vector3(0f, 0f, 28f), new Vector2(6f, 13f), 37f);

        // The road covers every path tile, dead ends included: x -6.25 .. 7.25, z -2 .. 39.75.
        return StageComponent(root, new Vector3(0f, 0f, -1f), new Vector3(1.5f, BirdAltitude, -2f), route,
            new Bounds(new Vector3(0.5f, 0f, 18.875f), new Vector3(13.5f, 1f, 41.75f)));
    }

    // The sunlit spot at the end of a route: a narrow shaft of light, like a portal.
    static void RestLight(GameObject parent, Vector3 position)
    {
        GameObject light = Primitive(PrimitiveType.Cylinder, "SunlitSpot", position + Vector3.up * 3f, new Vector3(1.6f, 3f, 1.6f),
            Mat("RestLight", new Color(1f, 0.9f, 0.5f, 0.25f), transparent: true), parent);
        Object.DestroyImmediate(light.GetComponent<Collider>());
    }

    static void StopAt(Transform point, PathBlocker blocker)
    {
        StopPoint stop = point.gameObject.AddComponent<StopPoint>();
        Set(stop, so => so.FindProperty("blocker").objectReferenceValue = blocker);
    }

    // A fog field with stacked layers, its timer, the authored correct path, the exit and the animals.
    static FogSection BuildFogSection(GameObject parent, string name, Vector3 center, Vector2 size, Vector3[] correctPath, Vector3 exit, Vector3[] animals)
    {
        GameObject root = Child(parent, name, center);
        var renderers = new Renderer[4];
        for (int i = 0; i < renderers.Length; i++)
        {
            GameObject layer = Primitive(PrimitiveType.Quad, $"FogLayer_{i}", center + Vector3.up * (0.15f + 0.35f * i), new Vector3(size.x, size.y, 1f), FogMaterial(), root);
            layer.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            Object.DestroyImmediate(layer.GetComponent<Collider>());
            renderers[i] = layer.GetComponent<Renderer>();
        }
        var field = root.AddComponent<FogField>();
        Set(field, so =>
        {
            so.FindProperty("size").vector2Value = size;
            SerializedProperty layers = so.FindProperty("layers");
            layers.arraySize = renderers.Length;
            for (int i = 0; i < renderers.Length; i++)
                layers.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
        });

        var timer = root.AddComponent<NaturalTimer>();
        Set(timer, so => so.FindProperty("kind").enumValueIndex = (int)NaturalTimerKind.FogSection);

        GameObject path = Child(root, "CorrectPath", correctPath[0]);
        for (int i = 0; i < correctPath.Length; i++)
            Child(path, $"Point_{i}", correctPath[i]);
        GameObject exitPoint = Child(root, "Exit", exit);

        Material animalMat = Mat("Animal", new Color(0.55f, 0.45f, 0.35f));
        Material glowMat = Mat("AnimalGlow", new Color(1f, 0.95f, 0.6f, 0.8f), transparent: true);
        for (int i = 0; i < animals.Length; i++)
        {
            GameObject animal = Primitive(PrimitiveType.Sphere, $"Animal_{i}", animals[i] + Vector3.up * 0.2f, new Vector3(0.5f, 0.4f, 0.6f), animalMat, root);
            Object.DestroyImmediate(animal.GetComponent<Collider>());
            GameObject glow = Primitive(PrimitiveType.Sphere, "Glow", animals[i] + Vector3.up * 1.9f, Vector3.one * 0.5f, glowMat, animal);
            glow.transform.localScale = Vector3.one * 1.2f;
            Object.DestroyImmediate(glow.GetComponent<Collider>());
            glow.SetActive(false);
            var fogAnimal = animal.AddComponent<FogAnimal>();
            Set(fogAnimal, so => so.FindProperty("glow").objectReferenceValue = glow);
        }

        var section = root.AddComponent<FogSection>();
        Set(section, so =>
        {
            so.FindProperty("field").objectReferenceValue = field;
            so.FindProperty("timer").objectReferenceValue = timer;
            so.FindProperty("correctPath").objectReferenceValue = path.transform;
            so.FindProperty("exit").objectReferenceValue = exitPoint.transform;
        });
        return section;
    }

    static Material FogMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(FogMaterialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("ThatGameJam/Fog"));
            AssetDatabase.CreateAsset(material, FogMaterialPath);
        }
        // Four layers at 0.75 cover over 99 %, enough to hide the paths and rocks under full fog.
        material.SetFloat("_Alpha", 0.75f);
        material.SetColor("_Color", new Color(0.8f, 0.83f, 0.88f));
        EditorUtility.SetDirty(material);
        return material;
    }

    static void HiddenRock(GameObject parent, Vector3 position, Material material)
    {
        GameObject rock = Primitive(PrimitiveType.Sphere, "HiddenRock", position + Vector3.up * 0.3f, new Vector3(1.6f, 0.8f, 1.2f), material, parent);
        rock.AddComponent<HiddenObstacle>();
    }

    static void PathTiles(GameObject parent, Vector3[] points, Material material)
    {
        for (int i = 1; i < points.Length; i++)
            PathTile(parent, points[i - 1], points[i], material);
    }

    // A 2.5 m wide walkable strip from a to b, with its top at y = 0. Ends overlap so joints have no gaps.
    static void PathTile(GameObject parent, Vector3 a, Vector3 b, Material material)
    {
        const float width = 2.5f;
        Vector3 direction = b - a;
        GameObject tile = Primitive(PrimitiveType.Cube, "Path", (a + b) * 0.5f + Vector3.down * 0.25f,
            new Vector3(width, 0.5f, direction.magnitude + width), material, parent);
        tile.transform.rotation = Quaternion.LookRotation(direction);
        tile.layer = groundLayer;
    }

    static Stage StageComponent(GameObject root, Vector3 babyStart, Vector3 birdStart, BabyRoute route, Bounds roadArea)
    {
        var stage = root.AddComponent<Stage>();
        stage.babyStart = Child(root, "BabyStart", babyStart).transform;
        stage.birdStart = Child(root, "BirdStart", birdStart).transform;
        stage.route = route;
        stage.birdAltitude = BirdAltitude;
        stage.area = roadArea;
        return stage;
    }

    // The pivot sits at the base and leans toward the water (+Z); fallen, the log lies across the stream as a bridge.
    static LeaningLog BuildLeaningLog(GameObject parent, Vector3 basePosition, Material wood)
    {
        GameObject root = Child(parent, "LeaningLog", basePosition);
        GameObject pivot = Child(root, "Pivot", basePosition);
        pivot.transform.localRotation = Quaternion.Euler(20f, 0f, 0f);

        GameObject trunk = Primitive(PrimitiveType.Cylinder, "Trunk", Vector3.zero, new Vector3(0.6f, LeaningLogLength / 2f, 0.6f), wood, pivot);
        trunk.transform.localPosition = new Vector3(0f, LeaningLogLength / 2f, 0f);
        trunk.layer = interactableLayer;
        GameObject snowCap = Primitive(PrimitiveType.Sphere, "SnowOnTop", Vector3.zero, new Vector3(0.7f, 0.35f, 0.7f), Mat("Snow", new Color(0.92f, 0.95f, 1f)), pivot);
        snowCap.transform.localPosition = new Vector3(0f, LeaningLogLength, -0.1f);
        Object.DestroyImmediate(snowCap.GetComponent<Collider>());

        GameObject perchPoint = Child(pivot, "PerchPoint", Vector3.zero);
        // On the snow cap: the bird's feet stand here.
        perchPoint.transform.localPosition = new Vector3(0f, LeaningLogLength + 0.15f, 0f);
        var top = trunk.AddComponent<Perchable>();
        Set(top, so => so.FindProperty("perchPoint").objectReferenceValue = perchPoint.transform);

        var timer = root.AddComponent<NaturalTimer>();
        Set(timer, so => so.FindProperty("kind").enumValueIndex = (int)NaturalTimerKind.LeaningLog);

        var log = root.AddComponent<LeaningLog>();
        Set(log, so =>
        {
            so.FindProperty("timer").objectReferenceValue = timer;
            so.FindProperty("top").objectReferenceValue = top;
            so.FindProperty("pivot").objectReferenceValue = pivot.transform;
            so.FindProperty("fallenEuler").vector3Value = new Vector3(90f, 0f, 0f);
        });
        return log;
    }

    static Baby BuildBaby()
    {
        var root = new GameObject("Baby");
        var body = root.AddComponent<Rigidbody>();
        body.isKinematic = true;
        var capsule = root.AddComponent<CapsuleCollider>();
        capsule.center = new Vector3(0f, 0.5f, 0f);
        capsule.height = 1f;
        capsule.radius = 0.25f;

        GameObject visual = Primitive(PrimitiveType.Capsule, "Visual", Vector3.zero, Vector3.one * 0.5f, Mat("Child", new Color(1f, 0.6f, 0.4f)), root);
        visual.transform.localPosition = new Vector3(0f, 0.5f, 0f);
        Object.DestroyImmediate(visual.GetComponent<Collider>());
        // Shows which way the baby faces; the capsule alone is symmetric.
        GameObject face = Primitive(PrimitiveType.Cube, "Face", Vector3.zero, new Vector3(0.3f, 0.15f, 0.2f), Mat("Face", new Color(0.3f, 0.2f, 0.2f)), visual);
        face.transform.localPosition = new Vector3(0f, 0.5f, 0.45f);
        Object.DestroyImmediate(face.GetComponent<Collider>());

        GameObject wake = Child(root, "WakeTimer", Vector3.zero);
        var timer = wake.AddComponent<NaturalTimer>();
        Set(timer, so => so.FindProperty("kind").enumValueIndex = (int)NaturalTimerKind.Wake);

        var baby = root.AddComponent<Baby>();
        Set(baby, so =>
        {
            so.FindProperty("wakeTimer").objectReferenceValue = timer;
            so.FindProperty("visual").objectReferenceValue = visual.transform;
        });
        return baby;
    }

    // The bird model from the art (Assets/03_Prefabs/Bird-Animated.prefab) under the controller. The controller's transform is
    // the feet: the model is turned to face +Z (it faces -Z) and shifted so its perched toes sit on that point.
    static BirdController BuildBird()
    {
        var bird = new GameObject("Bird");
        var modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BirdModelPrefabPath);
        var model = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab, bird.transform);
        model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        model.transform.localScale = Vector3.one * BirdModelScale;
        model.transform.localPosition = -(model.transform.localRotation * BirdModelPerchedToes) * BirdModelScale;
        var animator = model.GetComponent<Animator>();
        animator.runtimeAnimatorController = BuildBirdAnimator();
        animator.applyRootMotion = false;

        GameObject ring = Primitive(PrimitiveType.Cylinder, "ClickMarker", Vector3.zero, new Vector3(0.6f, 0.01f, 0.6f),
            Mat("ClickMarker", new Color(1f, 1f, 1f, 0.8f), transparent: true), null);
        Object.DestroyImmediate(ring.GetComponent<Collider>());
        var marker = ring.AddComponent<ClickMarker>();

        var controller = bird.AddComponent<BirdController>();
        Set(controller, so =>
        {
            so.FindProperty("animator").objectReferenceValue = animator;
            so.FindProperty("clickMarker").objectReferenceValue = marker;
        });
        return controller;
    }

    // Rebuilds the bird's Animator Controller from the model's clips, along the art's flow (its README):
    // Glide <-> Flap -> Landing -> a random perch idle -> Takeoff -> Flap. While gliding, one wingbeat every BirdGlideFlapInterval. Parameters: Moving and Perched (BirdController),
    // IdleVariant (RandomPerchIdle).
    static AnimatorController BuildBirdAnimator()
    {
        var clips = new System.Collections.Generic.Dictionary<string, AnimationClip>();
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(BirdModelPath))
        {
            if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                clips[clip.name] = clip;
        }

        // Rebuilt in place, so its GUID and the scenes' references to it stay.
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(BirdAnimatorPath)
            ?? AnimatorController.CreateAnimatorControllerAtPath(BirdAnimatorPath);
        foreach (AnimatorControllerParameter parameter in controller.parameters)
            controller.RemoveParameter(parameter);
        foreach (ChildAnimatorState child in controller.layers[0].stateMachine.states)
            controller.layers[0].stateMachine.RemoveState(child.state);
        controller.AddParameter("Moving", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Perched", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IdleVariant", AnimatorControllerParameterType.Int);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;

        AnimatorState State(string clip, Vector2 position)
        {
            AnimatorState state = machine.AddState(clip.Replace("Bird_", ""), position);
            state.motion = clips[clip];
            return state;
        }
        AnimatorState flap = State("Bird_Flap", new Vector2(300f, 0f));
        AnimatorState glide = State("Bird_Glide", new Vector2(300f, 120f));
        AnimatorState glideFlap = State("Bird_Flap", new Vector2(300f, 240f));
        glideFlap.name = "GlideFlap";
        AnimatorState landing = State("Bird_Landing", new Vector2(600f, 60f));
        AnimatorState[] idles =
        {
            State("Bird_PerchIdle", new Vector2(900f, -60f)),
            State("Bird_PerchIdle_Preen", new Vector2(900f, 60f)),
            State("Bird_PerchIdle_LeanBack", new Vector2(900f, 180f)),
        };
        AnimatorState takeoff = State("Bird_Takeoff", new Vector2(600f, 300f));
        machine.defaultState = flap;

        AnimatorStateTransition Go(AnimatorState from, AnimatorState to, float duration, float? exitTime = null)
        {
            AnimatorStateTransition transition = from.AddTransition(to);
            transition.duration = duration;
            transition.hasExitTime = exitTime.HasValue;
            transition.exitTime = exitTime ?? 0f;
            return transition;
        }

        // Flight changes only while not perched, so arriving at a perch (Moving and Perched change together) goes straight to Landing.
        // A flight blend already under way is cut by the destination's Landing transition.
        AnimatorStateTransition Fly(AnimatorStateTransition transition)
        {
            transition.AddCondition(AnimatorConditionMode.IfNot, 0f, "Perched");
            transition.interruptionSource = TransitionInterruptionSource.Destination;
            return transition;
        }
        Fly(Go(glide, flap, 0.25f)).AddCondition(AnimatorConditionMode.IfNot, 0f, "Moving");
        Fly(Go(flap, glide, 0.25f)).AddCondition(AnimatorConditionMode.If, 0f, "Moving");
        // One Flap cycle, then back to gliding, which starts its clip again.
        Fly(Go(glide, glideFlap, 0.25f, BirdGlideFlapInterval / clips["Bird_Glide"].length));
        Fly(Go(glideFlap, glide, 0.25f, 1f)).AddCondition(AnimatorConditionMode.If, 0f, "Moving");
        Fly(Go(glideFlap, flap, 0.1f)).AddCondition(AnimatorConditionMode.IfNot, 0f, "Moving");
        Go(glideFlap, landing, 0.2f).AddCondition(AnimatorConditionMode.If, 0f, "Perched");
        // Glide ends in Landing's first pose; from Flap it blends.
        Go(glide, landing, 0.1f).AddCondition(AnimatorConditionMode.If, 0f, "Perched");
        Go(flap, landing, 0.2f).AddCondition(AnimatorConditionMode.If, 0f, "Perched");
        // The perched poses match exactly, so these cut.
        Go(landing, idles[0], 0f, 1f);
        foreach (AnimatorState idle in idles)
        {
            idle.AddStateMachineBehaviour<RandomPerchIdle>();
            for (int i = 0; i < idles.Length; i++)
                Go(idle, idles[i], 0f, 1f).AddCondition(AnimatorConditionMode.Equals, i, "IdleVariant");
            Go(idle, takeoff, 0.1f).AddCondition(AnimatorConditionMode.IfNot, 0f, "Perched");
        }
        Go(landing, takeoff, 0.1f).AddCondition(AnimatorConditionMode.IfNot, 0f, "Perched");
        // Takeoff ends in Flap's first pose.
        Go(takeoff, flap, 0.15f, 1f);

        AssetDatabase.SaveAssets();
        return controller;
    }

    // The point the camera follows, and one camera at a fixed quarter-view offset from it.
    static BirdCamera BuildBirdCamera()
    {
        var point = new GameObject("CameraPoint");
        var cameraObject = new GameObject("FollowCamera");
        cameraObject.transform.SetPositionAndRotation(CameraOffset, Quaternion.LookRotation(-CameraOffset));
        var followCamera = cameraObject.AddComponent<CinemachineCamera>();
        followCamera.Target.TrackingTarget = point.transform;
        var follow = cameraObject.AddComponent<CinemachineFollow>();
        follow.FollowOffset = CameraOffset + Vector3.up * CameraAimHeight;
        follow.TrackerSettings.BindingMode = BindingMode.WorldSpace;
        follow.TrackerSettings.PositionDamping = Vector3.one * 0.4f;

        var birdCamera = point.AddComponent<BirdCamera>();
        Set(birdCamera, so => so.FindProperty("followCamera").objectReferenceValue = followCamera);
        return birdCamera;
    }

    static ScreenFader BuildFader()
    {
        var canvasObject = new GameObject("FadeCanvas", typeof(Canvas), typeof(CanvasScaler));
        canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var imageObject = new GameObject("Fade", typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(canvasObject.transform, false);
        var rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        var image = imageObject.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0f);
        image.raycastTarget = false;
        var fader = canvasObject.AddComponent<ScreenFader>();
        Set(fader, so => so.FindProperty("image").objectReferenceValue = image);
        return fader;
    }

    // Once the baby reaches the trigger, the camera stays inside the area until the blocker opens.
    // Until then the bird cannot fly past wallZ either.
    static void Zone(GameObject parent, PathBlocker blocker, Vector3 trigger, Vector3 areaCenter, Vector2 areaSize, float wallZ)
    {
        GameObject zoneObject = Child(parent, $"CameraZone_{blocker.name}", areaCenter);
        GameObject triggerObject = Child(zoneObject, "Trigger", trigger);
        GameObject wallObject = Child(zoneObject, "BirdWall", new Vector3(areaCenter.x, 0f, wallZ));
        var zone = zoneObject.AddComponent<CameraZone>();
        Set(zone, so =>
        {
            so.FindProperty("blocker").objectReferenceValue = blocker;
            so.FindProperty("trigger").objectReferenceValue = triggerObject.transform;
            so.FindProperty("areaSize").vector2Value = areaSize;
            so.FindProperty("wall").objectReferenceValue = wallObject.transform;
        });
    }

    static BabyRoute Route(GameObject parent, params Vector3[] points)
    {
        GameObject routeObject = Child(parent, "BabyRoute", points[0]);
        for (int i = 0; i < points.Length; i++)
            Child(routeObject, $"Point_{i}", points[i]);
        return routeObject.AddComponent<BabyRoute>();
    }

    // A bank of snow whose top is at y = 0, `length` long along Z.
    static void Ground(GameObject parent, string name, Vector3 center, float length, Material material)
    {
        GameObject ground = Primitive(PrimitiveType.Cube, name, center + Vector3.down * 0.25f, new Vector3(10f, 0.5f, length), material, parent);
        ground.layer = groundLayer;
    }

    // Water is not walkable ground; clicks pass through to the click plane.
    static void Water(GameObject parent, string name, Vector3 center, float length, Material material)
    {
        GameObject water = Primitive(PrimitiveType.Cube, name, center + Vector3.down * 0.45f, new Vector3(10f, 0.3f, length), material, parent);
        Object.DestroyImmediate(water.GetComponent<Collider>());
    }

    // Trunk and crown. Decoration only (plan Q7: no perching on roadside trees).
    static GameObject BuildTreePrefab()
    {
        var root = new GameObject("Tree");
        GameObject trunk = Primitive(PrimitiveType.Cylinder, "Trunk", new Vector3(0f, 2f, 0f), new Vector3(0.4f, 2f, 0.4f), Mat("Trunk", new Color(0.35f, 0.25f, 0.18f)), root);
        Object.DestroyImmediate(trunk.GetComponent<Collider>());
        GameObject crown = Primitive(PrimitiveType.Sphere, "Crown", new Vector3(0f, 4.6f, 0f), Vector3.one * 2.4f, Mat("Leaves", new Color(0.3f, 0.45f, 0.35f)), root);
        Object.DestroyImmediate(crown.GetComponent<Collider>());
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, TreePrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    static void PlaceTree(GameObject prefab, GameObject parent, Vector3 position, float yaw)
    {
        var tree = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.transform);
        tree.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
    }

    static GameObject Child(GameObject parent, string name, Vector3 position)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.transform.position = position;
        return go;
    }

    static GameObject Primitive(PrimitiveType type, string name, Vector3 position, Vector3 scale, Material material, GameObject parent)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        if (parent != null)
            go.transform.SetParent(parent.transform, false);
        go.transform.position = position;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = material;
        return go;
    }

    static Material Mat(string name, Color color, bool transparent = false)
    {
        string path = $"{MaterialFolder}/{name}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetColor("_BaseColor", color);
        if (transparent)
        {
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static void Set(Object component, System.Action<SerializedObject> edit)
    {
        var so = new SerializedObject(component);
        edit(so);
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
