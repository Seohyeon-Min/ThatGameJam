using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Builds Assets/00_Scenes/Sam_model_test.unity: a small snowy patch to see the models at game size.
// A girl walks slowly forward and back past the old tree, the bird flies where you click, the wheel zooms the camera and moves the bird's height as in the game, two ladybugs (one resting, one crawling), a few trees.
// Camera, light, watercolor look and the girl's, bird's and old tree's baked watercolor materials are copied from Main.unity,
// which must be the open scene. Run:
// unity command script run --file AgentScripts/BuildSamModelTest.cs --entry BuildSamModelTest.Run
public static class BuildSamModelTest
{
    const string ScenePath = "Assets/00_Scenes/Sam_model_test.unity";
    const string GirlModelPath = "Assets/01_Arts/Models/Girl/NPC-Girl-Animated.fbx";
    const string LadybugPrefabPath = "Assets/03_Prefabs/Ladybug.prefab";
    const string BirdPrefabPath = "Assets/03_Prefabs/Bird-Animated.prefab";
    const float GirlSpeed = 0.6f;
    static readonly Vector3 BirdPosition = new Vector3(-3f, 2.5f, -4f);

    static readonly (string prefab, Vector3 position, float yaw, float scale)[] Trees =
    {
        ("Assets/03_Prefabs/Trees/Tree_1_Bare.prefab", new Vector3(-6f, 0f, 4f), 20f, 1f),
        ("Assets/03_Prefabs/Trees/Tree_2_Bare.prefab", new Vector3(-7f, 0f, -3f), 130f, 0.9f),
        ("Assets/03_Prefabs/Trees/Tree_3_Bare.prefab", new Vector3(6f, 0f, -4f), 250f, 1f),
        ("Assets/03_Prefabs/Trees/Tree_1_Bare.prefab", new Vector3(7f, 0f, 5f), 300f, 1.1f),
        ("Assets/03_Prefabs/Trees/Tree_2_Bare.prefab", new Vector3(-3f, 0f, 10f), 70f, 1f),
        ("Assets/03_Prefabs/Trees/Tree_3_Bare.prefab", new Vector3(4f, 0f, 11f), 190f, 0.95f),
    };
    static readonly Vector3[] GirlPoints = { new Vector3(0f, 0f, 5f), new Vector3(0f, 0f, -5f) };
    static readonly Vector3[] CrawlPoints =
    {
        new Vector3(-1.2f, 0f, -0.6f),
        new Vector3(-2.2f, 0f, -0.4f),
        new Vector3(-2.4f, 0f, 0.6f),
        new Vector3(-1.4f, 0f, 0.8f),
    };

    public static string Run()
    {
        Scene main = SceneManager.GetActiveScene();
        GameObject mainGirl = GameObject.Find("02_WORLD/Baby/NPC-Girl-Animated");
        GameObject mainBirdModel = GameObject.Find("02_WORLD/Bird/Bird-Animated");
        Transform mainBaby = mainGirl.transform.parent;
        Transform mainLog = GameObject.Find("02_WORLD/Stages/Stage1/LeaningLog").transform;
        GameObject mainCamera = Camera.main.gameObject;
        GameObject mainLight = GameObject.Find("00_CAMERA&LIT/Directional Light");
        GameObject mainVolume = GameObject.Find("00_CAMERA&LIT/Watercolor Volume");
        GameObject mainWatercolor = GameObject.Find("01_SYSTEM/Watercolor Manager");
        Material floorMaterial = Terrain.activeTerrain.materialTemplate;
        var look = RenderLook.From();

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(scene);
            look.Apply();
            Fill(scene, mainGirl, mainBaby, mainBirdModel, mainLog, mainCamera, mainLight, mainVolume, mainWatercolor, floorMaterial);
            return EditorSceneManager.SaveScene(scene, ScenePath) ? "saved " + ScenePath : "FAILED to save " + ScenePath;
        }
        finally
        {
            SceneManager.SetActiveScene(main);
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    static void Fill(Scene scene, GameObject mainGirl, Transform mainBaby, GameObject mainBirdModel, Transform mainLog, GameObject mainCamera,
        GameObject mainLight, GameObject mainVolume, GameObject mainWatercolor, Material floorMaterial)
    {

        var camera = CopyInto(scene, mainCamera, "Main Camera");
        Object.DestroyImmediate(camera.GetComponent("CinemachineBrain"));
        camera.transform.SetPositionAndRotation(new Vector3(10.5f, 10.5f, -10.5f), Quaternion.Euler(35f, 315f, 0f));
        CopyInto(scene, mainLight, "Directional Light");
        CopyInto(scene, mainVolume, "Watercolor Volume");
        var watercolor = CopyInto(scene, mainWatercolor, "Watercolor Manager");
        Object.DestroyImmediate(watercolor.GetComponent<WatercolorSeasonLooks>());

        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = new Vector3(10f, 1f, 10f);
        ground.GetComponent<MeshRenderer>().sharedMaterial = floorMaterial;

        BuildGirl(mainGirl, mainBaby);
        BuildBird(mainBirdModel, camera.AddComponent<ModelTestCamera>());
        BuildOldTree(mainLog);
        BuildLadybugs();
        var trees = new GameObject("Trees").transform;
        foreach (var t in Trees)
        {
            var tree = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(t.prefab), trees);
            tree.transform.SetPositionAndRotation(t.position, Quaternion.Euler(0f, t.yaw, 0f));
            tree.transform.localScale = Vector3.one * t.scale;
        }
    }

    static GameObject CopyInto(Scene scene, GameObject source, string name)
    {
        var copy = Object.Instantiate(source);
        copy.name = name;
        SceneManager.MoveGameObjectToScene(copy, scene);
        copy.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
        return copy;
    }

    // Same root scale and model offset as Main's Baby, the FBX kept as a prefab instance,
    // with Main's baked watercolor meshes and materials put on its renderers.
    static void BuildGirl(GameObject mainGirl, Transform mainBaby)
    {
        var root = new GameObject("Girl").transform;
        root.position = GirlPoints[GirlPoints.Length - 1];
        root.localScale = mainBaby.localScale;
        var girl = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(GirlModelPath), root);
        girl.transform.localPosition = mainGirl.transform.localPosition;
        girl.transform.localRotation = mainGirl.transform.localRotation;
        girl.transform.localScale = mainGirl.transform.localScale;
        var animator = CopyLook(mainGirl, girl);
        girl.AddComponent<BabyAnimationEvents>();

        var path = Points("GirlPath", GirlPoints);
        var walker = root.gameObject.AddComponent<WaypointWalker>();
        Wire(walker, animator, "Walking", "Idle", path, GirlSpeed);
    }

    // Main's bird model at its starting height above the ground. ClickFlyer moves it with the mouse and the wheel zoom;
    // Main's BirdController is left out, because it needs the game flow and the baby.
    static void BuildBird(GameObject mainBirdModel, ModelTestCamera zoomCamera)
    {
        var root = new GameObject("Bird").transform;
        root.position = BirdPosition;
        var bird = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(BirdPrefabPath), root);
        bird.transform.localPosition = mainBirdModel.transform.localPosition;
        bird.transform.localRotation = mainBirdModel.transform.localRotation;
        bird.transform.localScale = mainBirdModel.transform.localScale;
        var flyer = root.gameObject.AddComponent<ClickFlyer>();
        var so = new SerializedObject(flyer);
        so.FindProperty("animator").objectReferenceValue = CopyLook(mainBirdModel, bird);
        so.FindProperty("zoomCamera").objectReferenceValue = zoomCamera;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Puts the source's baked watercolor meshes, materials and Animator Controller on a fresh instance of the same model.
    static Animator CopyLook(GameObject source, GameObject target)
    {
        foreach (var from in source.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            var to = target.GetComponentsInChildren<SkinnedMeshRenderer>().First(r => r.name == from.name);
            to.sharedMesh = from.sharedMesh;
            to.sharedMaterials = from.sharedMaterials;
        }
        var animator = target.GetComponent<Animator>();
        animator.runtimeAnimatorController = source.GetComponent<Animator>().runtimeAnimatorController;
        animator.applyRootMotion = false;
        return animator;
    }

    // The standing old tree from Stage 1 (LeaningLog's art) without its gameplay parts.
    static void BuildOldTree(Transform mainLog)
    {
        var root = new GameObject("OldTree").transform;
        root.SetPositionAndRotation(new Vector3(2.5f, 0f, 2f), mainLog.rotation);
        root.localScale = mainLog.localScale;
        var art = Object.Instantiate(mainLog.Find("broken tree").gameObject, root, false);
        art.name = "broken tree";
        Object.DestroyImmediate(art.GetComponent<Perchable>());
        Object.DestroyImmediate(art.GetComponent<Animator>());
        Object.DestroyImmediate(art.transform.Find("Tube/PerchPoint").gameObject);
    }

    static void BuildLadybugs()
    {
        var group = new GameObject("Ladybugs").transform;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LadybugPrefabPath);
        var resting = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group);
        resting.name = "Ladybug_Resting";
        resting.transform.SetPositionAndRotation(new Vector3(0.9f, 0f, -1.5f), Quaternion.Euler(0f, -110f, 0f));

        var path = Points("CrawlPath", CrawlPoints);
        path.SetParent(group, false);
        var crawling = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group);
        crawling.name = "Ladybug_Crawling";
        Vector3 start = CrawlPoints[CrawlPoints.Length - 1];
        crawling.transform.SetPositionAndRotation(start, Quaternion.LookRotation(CrawlPoints[0] - start));
        var walker = crawling.AddComponent<WaypointWalker>();
        Wire(walker, crawling.GetComponent<Animator>(), "Crawl", "", path, 0.2f);
    }

    static Transform Points(string name, Vector3[] positions)
    {
        var path = new GameObject(name).transform;
        for (int i = 0; i < positions.Length; i++)
        {
            var point = new GameObject("Point_" + i).transform;
            point.SetParent(path, false);
            point.position = positions[i];
        }
        return path;
    }

    static void Wire(WaypointWalker walker, Animator animator, string moveParameter, string startState, Transform path, float speed)
    {
        var so = new SerializedObject(walker);
        so.FindProperty("animator").objectReferenceValue = animator;
        so.FindProperty("moveParameter").stringValue = moveParameter;
        so.FindProperty("startState").stringValue = startState;
        so.FindProperty("speed").floatValue = speed;
        var points = so.FindProperty("points");
        points.arraySize = path.childCount;
        for (int i = 0; i < path.childCount; i++)
            points.GetArrayElementAtIndex(i).objectReferenceValue = path.GetChild(i);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // The sky, ambient light and fog of the scene that is active when From() runs.
    struct RenderLook
    {
        Material skybox;
        UnityEngine.Rendering.AmbientMode ambientMode;
        Color ambientSky, ambientEquator, ambientGround;
        float ambientIntensity;
        bool fog;
        FogMode fogMode;
        Color fogColor;
        float fogDensity, fogStart, fogEnd;

        public static RenderLook From() => new RenderLook
        {
            skybox = RenderSettings.skybox,
            ambientMode = RenderSettings.ambientMode,
            ambientSky = RenderSettings.ambientSkyColor,
            ambientEquator = RenderSettings.ambientEquatorColor,
            ambientGround = RenderSettings.ambientGroundColor,
            ambientIntensity = RenderSettings.ambientIntensity,
            fog = RenderSettings.fog,
            fogMode = RenderSettings.fogMode,
            fogColor = RenderSettings.fogColor,
            fogDensity = RenderSettings.fogDensity,
            fogStart = RenderSettings.fogStartDistance,
            fogEnd = RenderSettings.fogEndDistance,
        };

        public void Apply()
        {
            RenderSettings.skybox = skybox;
            RenderSettings.ambientMode = ambientMode;
            RenderSettings.ambientSkyColor = ambientSky;
            RenderSettings.ambientEquatorColor = ambientEquator;
            RenderSettings.ambientGroundColor = ambientGround;
            RenderSettings.ambientIntensity = ambientIntensity;
            RenderSettings.fog = fog;
            RenderSettings.fogMode = fogMode;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogDensity = fogDensity;
            RenderSettings.fogStartDistance = fogStart;
            RenderSettings.fogEndDistance = fogEnd;
        }
    }
}
