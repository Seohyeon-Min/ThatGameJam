using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEditor;
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

    // 1-1 fallen log over the first stream, 1-2 leaning log by the second stream, 1-3 sunlit rest area. The road runs along +Z.
    static Stage BuildStage1(GameObject treePrefab)
    {
        var root = new GameObject("Stage1");
        Material snow = Mat("Snow", new Color(0.92f, 0.95f, 1f));
        Material water = Mat("Water", new Color(0.35f, 0.55f, 0.8f));
        Material wood = Mat("Trunk", new Color(0.35f, 0.25f, 0.18f));

        Ground(root, "Bank_A", new Vector3(0f, 0f, 3f), 10f, snow);     // z -2 .. 8
        Ground(root, "Bank_B", new Vector3(0f, 0f, 16f), 8f, snow);     // z 12 .. 20
        Ground(root, "Bank_C", new Vector3(0f, 0f, 28.25f), 11.5f, snow); // z 22.5 .. 34
        Water(root, "Stream_1", new Vector3(0f, 0f, 10f), 4f, water);
        Water(root, "Stream_2", new Vector3(0f, 0f, 21.25f), 2.5f, water); // z 20 .. 22.5

        // 1-1: the fallen log is already a bridge; the stump shows it once stood.
        GameObject bridge = Primitive(PrimitiveType.Cylinder, "FallenLog", new Vector3(0f, 0.3f, 10f), new Vector3(0.6f, 2.5f, 0.6f), wood, root);
        bridge.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        bridge.layer = groundLayer;
        Primitive(PrimitiveType.Cylinder, "Stump", new Vector3(0f, 0.35f, 7.2f), new Vector3(0.65f, 0.35f, 0.65f), wood, root);

        LeaningLog leaningLog = BuildLeaningLog(root, new Vector3(0f, 0.3f, 19.6f), wood);

        GameObject restLight = Primitive(PrimitiveType.Cylinder, "RestArea", new Vector3(0f, 3f, 30f), new Vector3(3f, 3f, 3f),
            Mat("RestLight", new Color(1f, 0.9f, 0.5f, 0.25f), transparent: true), root);
        Object.DestroyImmediate(restLight.GetComponent<Collider>());

        // Only on the far side of the road, so they do not hide the road from the camera.
        foreach (float z in new[] { 2f, 15f, 28f })
            PlaceTree(treePrefab, root, new Vector3(-4.5f, 0f, z), 90f);

        // Points carry height: 0.6 is the top of a fallen log.
        BabyRoute route = Route(root,
            new Vector3(0f, 0f, 6.8f), new Vector3(0f, 0.6f, 8f), new Vector3(0f, 0.6f, 12f), new Vector3(0f, 0f, 13f),
            new Vector3(0f, 0f, 19f), new Vector3(0f, 0.6f, 20.2f), new Vector3(0f, 0.6f, 22.7f), new Vector3(0f, 0f, 23.5f),
            new Vector3(0f, 0f, 30f));
        StopPoint stop = route[4].gameObject.AddComponent<StopPoint>();
        Set(stop, so => so.FindProperty("blocker").objectReferenceValue = leaningLog);

        // The camera stops at z 19 until the leaning log has fallen.
        Gate(root, leaningLog, 19f);

        return StageComponent(root, new Vector3(0f, 0f, 0f), new Vector3(1.5f, BirdAltitude, -1f), route,
            new Bounds(new Vector3(0f, 0f, 16f), new Vector3(6f, 1f, 32f)));
    }

    // Just a start and a rest area, to see the transition from stage 1.
    static Stage BuildStage2()
    {
        var root = new GameObject("Stage2");
        root.transform.position = new Vector3(60f, 0f, 0f);
        Ground(root, "Bank", new Vector3(60f, 0f, 5f), 14f, Mat("Snow", new Color(0.92f, 0.95f, 1f)));
        GameObject restLight = Primitive(PrimitiveType.Cylinder, "RestArea", new Vector3(60f, 3f, 9f), new Vector3(3f, 3f, 3f),
            Mat("RestLight", new Color(1f, 0.9f, 0.5f, 0.25f), transparent: true), root);
        Object.DestroyImmediate(restLight.GetComponent<Collider>());
        BabyRoute route = Route(root, new Vector3(60f, 0f, 4f), new Vector3(60f, 0f, 9f));
        return StageComponent(root, new Vector3(60f, 0f, 0f), new Vector3(61.5f, BirdAltitude, -1f), route,
            new Bounds(new Vector3(60f, 0f, 5f), new Vector3(6f, 1f, 10f)));
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
        Vector3[] path32 = { new(0f, 0f, 20f), new(3f, 0f, 23f), new(3f, 0f, 27f), new(-2f, 0f, 30f), new(-2f, 0f, 34f), new(0f, 0f, 36f), new(0f, 0f, 41f) };
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

        GameObject restLight = Primitive(PrimitiveType.Cylinder, "RestArea", new Vector3(0f, 3f, 39f), new Vector3(3f, 3f, 3f),
            Mat("RestLight", new Color(1f, 0.9f, 0.5f, 0.25f), transparent: true), root);
        Object.DestroyImmediate(restLight.GetComponent<Collider>());

        BabyRoute route = Route(root,
            new Vector3(0f, 0f, 5.5f), new Vector3(0f, 0f, 18f), new Vector3(0f, 0f, 19.5f), new Vector3(0f, 0f, 36f), new Vector3(0f, 0f, 39f));
        StopAt(route[0], fog31);
        StopAt(route[2], fog32);

        // The camera stops short of the next fog until a section is through, so the bird cannot clear it early.
        Gate(root, fog31, 16f);
        Gate(root, fog32, 34f);

        return StageComponent(root, new Vector3(0f, 0f, -1f), new Vector3(1.5f, BirdAltitude, -2f), route,
            new Bounds(new Vector3(0f, 0f, 19.5f), new Vector3(8f, 1f, 39f)));
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

    static Stage StageComponent(GameObject root, Vector3 babyStart, Vector3 birdStart, BabyRoute route, Bounds cameraArea)
    {
        var stage = root.AddComponent<Stage>();
        stage.babyStart = Child(root, "BabyStart", babyStart).transform;
        stage.birdStart = Child(root, "BirdStart", birdStart).transform;
        stage.route = route;
        stage.birdAltitude = BirdAltitude;
        stage.area = cameraArea;
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
        perchPoint.transform.localPosition = new Vector3(0f, LeaningLogLength + 0.3f, 0f);
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

    static BirdController BuildBird()
    {
        GameObject bird = Primitive(PrimitiveType.Sphere, "Bird", Vector3.zero, Vector3.one * 0.5f, Mat("Bird", Color.white), null);
        Object.DestroyImmediate(bird.GetComponent<Collider>());
        GameObject beak = Primitive(PrimitiveType.Cube, "Beak", Vector3.zero, new Vector3(0.3f, 0.3f, 0.6f), Mat("Beak", new Color(1f, 0.75f, 0.2f)), bird);
        beak.transform.localPosition = new Vector3(0f, 0f, 0.55f);
        Object.DestroyImmediate(beak.GetComponent<Collider>());

        GameObject ring = Primitive(PrimitiveType.Cylinder, "ClickMarker", Vector3.zero, new Vector3(0.6f, 0.01f, 0.6f),
            Mat("ClickMarker", new Color(1f, 1f, 1f, 0.8f), transparent: true), null);
        Object.DestroyImmediate(ring.GetComponent<Collider>());
        var marker = ring.AddComponent<ClickMarker>();

        var controller = bird.AddComponent<BirdController>();
        Set(controller, so => so.FindProperty("clickMarker").objectReferenceValue = marker);
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

    // The camera cannot move past z until the blocker opens.
    static void Gate(GameObject parent, PathBlocker blocker, float z)
    {
        GameObject gateObject = Child(parent, $"CameraGate_{blocker.name}", new Vector3(parent.transform.position.x, 0f, z));
        var gate = gateObject.AddComponent<CameraGate>();
        Set(gate, so => so.FindProperty("blocker").objectReferenceValue = blocker);
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
