using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Builds Assets/00_Scenes/Sam_Test.unity: a grey-box stage 1 (fallen log, leaning log, sunlit rest area)
// and a minimal stage 2 to test the stage transition. Also rebuilds Assets/03_Prefabs/Tree.prefab (decoration only).
// Run: unity command script run --file AgentScripts/BuildSamTestScene.cs --entry BuildSamTestScene.Build
public static class BuildSamTestScene
{
    const string ScenePath = "Assets/00_Scenes/Sam_Test.unity";
    const string MaterialFolder = "Assets/01_Arts/Materials/Test";
    const string TreePrefabPath = "Assets/03_Prefabs/Tree.prefab";
    const float BirdAltitude = 2.5f;
    // Seen from behind-right of the road, so the +Z road runs from lower-left to upper-right on screen (director's sketch).
    static readonly Vector3 CameraOffset = new(10f, 10f, -10f);
    // Section cameras aim this far above the road, so the leaning log's top stays in frame.
    const float CameraAimHeight = 1.5f;
    // Short enough that its top (the perch) stays low in the fixed camera's frame, long enough to span stream 2 when fallen.
    const float LeaningLogLength = 3.4f;

    static int groundLayer, interactableLayer, clickPlaneLayer;

    public static string Build()
    {
        foreach (string unused in new[] { "PerchIndicator", "Obstacle", "Goal", "Road" })
            AssetDatabase.DeleteAsset($"{MaterialFolder}/{unused}.mat");

        groundLayer = LayerMask.NameToLayer("Ground");
        interactableLayer = LayerMask.NameToLayer("Interactable");
        clickPlaneLayer = LayerMask.NameToLayer("ClickPlane");

        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        GameObject treePrefab = BuildTreePrefab();

        var clickPlane = new GameObject("ClickPlane", typeof(BoxCollider)) { layer = clickPlaneLayer };
        clickPlane.transform.position = new Vector3(30f, -1f, 15f);
        clickPlane.GetComponent<BoxCollider>().size = new Vector3(400f, 0.1f, 400f);

        Stage stage1 = BuildStage1(treePrefab);
        Stage stage2 = BuildStage2();

        Baby baby = BuildBaby();
        BirdController bird = BuildBird();
        bird.transform.position = stage1.birdStart.position;
        baby.transform.SetPositionAndRotation(stage1.babyStart.position, stage1.babyStart.rotation);

        Camera camera = Camera.main;
        var brain = camera.gameObject.AddComponent<CinemachineBrain>();
        brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 1f);

        ScreenFader fader = BuildFader();
        var flow = new GameObject("GameFlow");
        var gameFlow = flow.AddComponent<GameFlow>();
        flow.AddComponent<DevTools>();
        Set(gameFlow, so =>
        {
            SerializedProperty stages = so.FindProperty("stages");
            stages.arraySize = 2;
            stages.GetArrayElementAtIndex(0).objectReferenceValue = stage1;
            stages.GetArrayElementAtIndex(1).objectReferenceValue = stage2;
            so.FindProperty("fader").objectReferenceValue = fader;
            so.FindProperty("brain").objectReferenceValue = brain;
        });

        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
        AssetDatabase.SaveAssets();
        return ScenePath;
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

        CameraZone zone1 = Zone(root, "Zone_1-1", new Vector3(0f, 0f, 5.5f), 15f);   // z -2 .. 13
        Zone(root, "Zone_1-2", new Vector3(0f, 0f, 18f), 10f);                       // z 13 .. 23
        Zone(root, "Zone_1-3", new Vector3(0f, 0f, 28.5f), 11f);                     // z 23 .. 34

        return StageComponent(root, new Vector3(0f, 0f, 0f), new Vector3(1.5f, BirdAltitude, -1f), route, zone1);
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
        CameraZone zone = Zone(root, "Zone_2", new Vector3(60f, 0f, 5f), 14f);
        return StageComponent(root, new Vector3(60f, 0f, 0f), new Vector3(61.5f, BirdAltitude, -1f), route, zone);
    }

    static Stage StageComponent(GameObject root, Vector3 babyStart, Vector3 birdStart, BabyRoute route, CameraZone firstZone)
    {
        var stage = root.AddComponent<Stage>();
        stage.babyStart = Child(root, "BabyStart", babyStart).transform;
        stage.birdStart = Child(root, "BirdStart", birdStart).transform;
        stage.route = route;
        stage.birdAltitude = BirdAltitude;
        stage.firstZone = firstZone;
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

    // A camera section covering `length` along Z around `center`: trigger for the baby, bird area, fixed camera.
    static CameraZone Zone(GameObject parent, string name, Vector3 center, float length)
    {
        GameObject zoneObject = Child(parent, name, center);
        var trigger = zoneObject.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.center = new Vector3(0f, 1f, 0f);
        trigger.size = new Vector3(12f, 4f, length);

        GameObject cameraObject = Child(zoneObject, "Camera", center + Vector3.up * CameraAimHeight + CameraOffset);
        cameraObject.transform.rotation = Quaternion.LookRotation(-CameraOffset);
        var sectionCamera = cameraObject.AddComponent<CinemachineCamera>();
        sectionCamera.Priority = 0;

        var zone = zoneObject.AddComponent<CameraZone>();
        Set(zone, so =>
        {
            so.FindProperty("sectionCamera").objectReferenceValue = sectionCamera;
            so.FindProperty("birdArea").boundsValue = new Bounds(center + Vector3.up * 3f, new Vector3(10f, 6f, length));
        });
        return zone;
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
