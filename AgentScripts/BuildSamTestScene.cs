using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Builds the grey-box test scenes. Sam_Test.unity: the intro (a snowy walk to the first sunlit spot), stage 1 (fallen log, leaning
// log, sunlit rest area) and a minimal stage 2 on the same road, to test going on from one stage to the next (ADR 0010), all on one
// terrain whose edge the camera never shows (ADR 0019), with trees along the road (ADR 0020); also rebuilds the tree prefabs in
// Assets/03_Prefabs/Trees.
// Sam_Fog_Test.unity: the fog stage.
// Run: unity command script run --file AgentScripts/BuildSamTestScene.cs --entry BuildSamTestScene.Build
public static class BuildSamTestScene
{
    const string ScenePath = "Assets/00_Scenes/Sam_Test.unity";
    const string FogScenePath = "Assets/00_Scenes/Sam_Fog_Test.unity";
    const string FogMaterialPath = "Assets/01_Arts/Materials/Fog.mat";
    const string MaterialFolder = "Assets/01_Arts/Materials/Test";
    const string TreePrefabFolder = "Assets/03_Prefabs/Trees";
    const string TreeModelFolder = "Assets/01_Arts/Mesh";
    // Tommy's tree kinds: the bare model, the same tree with leaves, and where the bird perches on it, a low branch near the first fork
    // (from the model's vertices; the leafy model has the same wood).
    static readonly (string bare, string leafy, Vector3 perch)[] TreeModels =
    {
        ("tree01", "tree_l_01", new(0.17f, 2.18f, -0.59f)), ("tree02", "tree_l_02", new(0.30f, 2.30f, 0.28f)),
        ("tree03", "tree_l_03", new(0.08f, 2.43f, -0.20f)),
    };
    const string TerrainDataPath = "Assets/04_Data/SamTestTerrain.asset";
    // The streams cross the whole terrain, in Z. Stage 1 lays its logs over them.
    static readonly Vector2[] Streams = { new(8f, 12f), new(20f, 22.5f) };
    // How far the terrain dips under a stream; the water's surface is at -0.3.
    const float StreamDepth = 0.9f;
    // The terrain is flat where the bird flies and rolls into low hills farther out.
    const float HillHeight = 3f;
    // What the camera shows around the point it follows (the camera's own settings, at a 16:9 screen), and how far past that the
    // terrain and its trees go, so no edge of them comes into view (ADR 0019). A 21:9 screen shows about 6 more; the margin covers it.
    const float CameraFieldOfView = 40f;
    const float ScreenAspect = 16f / 9f;
    const float ViewMargin = 12f;
    const string SnowfallPrefabPath = "Assets/03_Prefabs/Snowfall.prefab";
    // The Snowfall prefab makes its flake material in the editor and does not save it, so a build would have none.
    const string SnowflakeTexturePath = MaterialFolder + "/Snowflake.png";
    // The snow starts above the camera (11.5 over the ground), so flakes fall past it too.
    const float SnowHeight = 14f;
    // New flakes per second on each square metre of the box they start from.
    const float SnowPerSquareMetre = 1.0f;
    // Metres per second along X and Z (ADR 0021). Falling about 8 s, a flake drifts about 4 m along X and 41 m along -Z.
    static readonly Vector2 SnowWind = new(0.5f, -5f);
    const float SnowFallSpeed = 1.5f;
    const float SnowFallSpeedVariation = 0.4f;
    // Trees stand this far past the road's edges (x -5 and 5) on both sides (ADR 0013, ADR 0020).
    const float ForestWidth = 17f;
    // Two dense rows of trees line the road this far past its edges.
    const float RoadsideRowsWidth = 4f;
    // Average distance between neighbouring trees by the road; dense like the director's reference pictures.
    const float TreeSpacing = 1.7f;
    // Farther out the trees stand sparser, with room to fly between them.
    const float OuterTreeSpacing = 3.2f;
    // Half the width of the area the bird flies over: the road and the terrain beside it, among the trees; 1.5 times the 13 of
    // ADR 0013 (ADR 0021).
    const float BirdAreaHalfWidth = 19.5f;
    const string GirlModelPath = "Assets/01_Arts/Models/Girl/NPC-Girl-Animated.fbx";
    const string GirlAnimatorPath = "Assets/01_Arts/Models/Girl/Girl.controller";
    // 1.3 tall in the model; about 1 here, the height the baby's collider and the levels were made for.
    const float BabyModelScale = 0.8f;
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
        // The leafy trees are made for the season when leaves come (step 3); every stage has the bare ones for now.
        var (bareTrees, _) = BuildTreePrefabs();
        Stage[] stages = { BuildIntro(), BuildStage1(), BuildStage2() };
        var (season, birdCamera) = Populate(stages);
        BuildWorld(stages, bareTrees, season, birdCamera);
        return Save(ScenePath);
    }

    // Step 2: the fog stage alone (3-0 start, 3-1 half fog, 3-2 full fog, a rest area).
    // Run: unity command script run --file AgentScripts/BuildSamTestScene.cs --entry BuildSamTestScene.BuildFog
    public static string BuildFog()
    {
        NewScene();
        Populate(BuildFogStage());
        return Save(FogScenePath);
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

    // Adds the baby, the bird, the camera brain and the game flow over the given stages.
    static (Season, BirdCamera) Populate(params Stage[] stages)
    {
        Baby baby = BuildBaby();
        BirdController bird = BuildBird();
        bird.transform.position = stages[0].birdStart.position;
        baby.transform.SetPositionAndRotation(stages[0].babyStart.position, stages[0].babyStart.rotation);

        Camera.main.gameObject.AddComponent<CinemachineBrain>();
        BirdCamera birdCamera = BuildBirdCamera();

        var flow = new GameObject("GameFlow");
        var gameFlow = flow.AddComponent<GameFlow>();
        flow.AddComponent<DevTools>();
        var season = flow.AddComponent<Season>();
        // Debug only, on its own object so deleting the object removes it.
        var tint = new GameObject("SeasonDebugTint").AddComponent<SeasonDebugTint>();
        Set(tint, so => so.FindProperty("season").objectReferenceValue = season);
        Set(gameFlow, so =>
        {
            SerializedProperty list = so.FindProperty("stages");
            list.arraySize = stages.Length;
            for (int i = 0; i < stages.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = stages[i];
            so.FindProperty("birdCamera").objectReferenceValue = birdCamera;
            so.FindProperty("season").objectReferenceValue = season;
        });
        return (season, birdCamera);
    }

    static string Save(string scenePath)
    {
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), scenePath);
        AssetDatabase.SaveAssets();
        return scenePath;
    }

    // The intro (the director's sketch): the baby sleeps at the start of the road while snow falls, wakes, and walks to the first
    // sunlit spot, where stage 1 begins. It has no device; the design doc's intro and snow walk are this one stage (ADR 0018).
    static Stage BuildIntro()
    {
        var root = new GameObject("Intro");
        BabyRoute route = Route(root, new Vector3(0f, 0f, -21f), new Vector3(0f, 0f, 0f));
        RestLight(root, route[route.Count - 1].position);
        // The road and the terrain beside it, z -26 .. -2 (stage 1's area starts there).
        return StageComponent(root, new Vector3(0f, 0f, -22f), new Vector3(1.5f, BirdAltitude, -23f), route,
            new Bounds(new Vector3(0f, 0f, -14f), new Vector3(2f * BirdAreaHalfWidth, 1f, 24f)));
    }

    // 1-1 fallen log over the first stream, 1-2 leaning log by the second stream; the stage ends in the sunlit spot right after it.
    // The road runs along +Z.
    static Stage BuildStage1()
    {
        var root = new GameObject("Stage1");
        Material wood = Mat("Trunk", new Color(0.35f, 0.25f, 0.18f));

        // The two streams (Streams, z 8 .. 12 and 20 .. 22.5) are in the terrain (BuildWorld).
        // 1-1: the fallen log is already a bridge; the stump shows it once stood.
        GameObject bridge = Primitive(PrimitiveType.Cylinder, "FallenLog", new Vector3(0f, 0.3f, 10f), new Vector3(0.6f, 2.5f, 0.6f), wood, root);
        bridge.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        bridge.layer = groundLayer;
        Primitive(PrimitiveType.Cylinder, "Stump", new Vector3(0f, 0.35f, 7.2f), new Vector3(0.65f, 0.35f, 0.65f), wood, root);

        LeaningLog leaningLog = BuildLeaningLog(root, new Vector3(0f, 0.3f, 19.6f), wood);

        // Points carry height: 0.6 is the top of a fallen log.
        BabyRoute route = Route(root,
            new Vector3(0f, 0f, 6.8f), new Vector3(0f, 0.6f, 8f), new Vector3(0f, 0.6f, 12f), new Vector3(0f, 0f, 13f),
            new Vector3(0f, 0f, 19f), new Vector3(0f, 0.6f, 20.2f), new Vector3(0f, 0.6f, 22.7f), new Vector3(0f, 0f, 23.5f),
            new Vector3(0f, 0f, 25f));
        // The sunlit spot is not a section of its own: the stage ends in it right after the last device (ADR 0006).
        RestLight(root, route[route.Count - 1].position);
        StopPoint stop = route[4].gameObject.AddComponent<StopPoint>();
        Set(stop, so => so.FindProperty("blocker").objectReferenceValue = leaningLog);

        // When the bird enters the band at z 15 .. 17, the camera eases onto the log and stream until the log has fallen or the bird
        // flies near the edge of the map: out of x ±10, z 10 .. 24 (the sunlit spot is at z 25). The band is 2 deep, so even a slow frame
        // (a third of a second at the bird's speed) does not skip it. While locked the camera may slide 1.5 sideways, so the bird, staying
        // on screen, can still reach the lock area's near edge (+X, toward the camera, where the quarter view shows least) (ADR 0014, ADR 0015).
        Zone(root, leaningLog, new Vector3(0f, 0f, 16f), new Vector2(16f, 2f), new Vector3(0f, 0f, 17f), new Vector2(3f, 1f), new Vector2(20f, 14f));

        // The road and the terrain beside it, z -2 .. 27.
        return StageComponent(root, new Vector3(0f, 0f, 0f), new Vector3(1.5f, BirdAltitude, -1f), route,
            new Bounds(new Vector3(0f, 0f, 12.5f), new Vector3(2f * BirdAreaHalfWidth, 1f, 29f)));
    }

    // Goes on from stage 1's sunlit spot (z 25) on the same road: just a walk to its own sunlit spot, to test going on to the next stage.
    static Stage BuildStage2()
    {
        var root = new GameObject("Stage2");
        BabyRoute route = Route(root, new Vector3(0f, 0f, 30f), new Vector3(0f, 0f, 38.5f));
        RestLight(root, route[route.Count - 1].position);
        // The baby starts where stage 1 left it asleep. The bird start is only for the dev tool's skip.
        return StageComponent(root, new Vector3(0f, 0f, 25f), new Vector3(1.5f, BirdAltitude, 24f), route,
            new Bounds(new Vector3(0f, 0f, 34f), new Vector3(2f * BirdAreaHalfWidth, 1f, 14f)));
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

        // When the bird nears a fog section, the camera eases onto it until the section is through.
        Zone(root, fog31, new Vector3(0f, 0f, 7f), new Vector2(10f, 1f), new Vector3(0f, 0f, 11.5f), new Vector2(6f, 9f), new Vector2(16f, 11f));
        Zone(root, fog32, new Vector3(0f, 0f, 21f), new Vector2(10f, 1f), new Vector3(0f, 0f, 28f), new Vector2(6f, 13f), new Vector2(16f, 15f));

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

    // The girl model from the art (Assets/01_Arts/Models/Girl, import settings by AgentScripts/SetupGirlModel.cs) under the Baby.
    // The model faces -X, so it is turned to face +Z. Its feet are at its origin.
    static Baby BuildBaby()
    {
        var root = new GameObject("Baby");
        var body = root.AddComponent<Rigidbody>();
        body.isKinematic = true;
        var capsule = root.AddComponent<CapsuleCollider>();
        capsule.center = new Vector3(0f, 0.5f, 0f);
        capsule.height = 1f;
        capsule.radius = 0.25f;

        var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(GirlModelPath), root.transform);
        model.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
        model.transform.localScale = Vector3.one * BabyModelScale;
        var animator = model.GetComponent<Animator>();
        animator.runtimeAnimatorController = BuildBabyAnimator(out AnimationClip getUp, out AnimationClip lieDown);
        animator.applyRootMotion = false;

        GameObject wake = Child(root, "WakeTimer", Vector3.zero);
        var timer = wake.AddComponent<NaturalTimer>();
        Set(timer, so => so.FindProperty("kind").enumValueIndex = (int)NaturalTimerKind.Wake);

        var baby = root.AddComponent<Baby>();
        Set(baby, so =>
        {
            so.FindProperty("wakeTimer").objectReferenceValue = timer;
            so.FindProperty("animator").objectReferenceValue = animator;
            so.FindProperty("getUpClip").objectReferenceValue = getUp;
            so.FindProperty("lieDownClip").objectReferenceValue = lieDown;
        });
        return baby;
    }

    // Rebuilds the baby's Animator Controller from the model's clips, along the art's flow (its README):
    // idles or Walk -> LieDown -> Sleep (loop) -> GetUp -> Idle. Parameters: Walking, Asleep, LookAround (Baby), IdleVariant (RandomIdle).
    // Standing still, the baby breathes, then hums or stretches at random. Run is not used: the baby only walks.
    static AnimatorController BuildBabyAnimator(out AnimationClip getUpClip, out AnimationClip lieDownClip)
    {
        var clips = new System.Collections.Generic.Dictionary<string, AnimationClip>();
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(GirlModelPath))
        {
            if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                clips[clip.name] = clip;
        }
        getUpClip = clips["Girl_GetUp"];
        lieDownClip = clips["Girl_LieDown"];

        // Rebuilt in place, so its GUID and the scenes' references to it stay.
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(GirlAnimatorPath)
            ?? AnimatorController.CreateAnimatorControllerAtPath(GirlAnimatorPath);
        foreach (AnimatorControllerParameter parameter in controller.parameters)
            controller.RemoveParameter(parameter);
        foreach (ChildAnimatorState child in controller.layers[0].stateMachine.states)
            controller.layers[0].stateMachine.RemoveState(child.state);
        controller.AddParameter("Walking", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Asleep", AnimatorControllerParameterType.Bool);
        controller.AddParameter("LookAround", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IdleVariant", AnimatorControllerParameterType.Int);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;

        AnimatorState State(string name, string clip, Vector2 position)
        {
            AnimatorState state = machine.AddState(name, position);
            state.motion = clips[clip];
            return state;
        }
        AnimatorState sleep = State("Sleep", "Girl_Sleep", new Vector2(0f, 0f));
        AnimatorState getUpState = State("GetUp", "Girl_GetUp", new Vector2(250f, -120f));
        AnimatorState lieDown = State("LieDown", "Girl_LieDown", new Vector2(250f, 120f));
        AnimatorState[] idles =
        {
            State("Idle", "Girl_Idle_Breathing", new Vector2(550f, -120f)),
            State("Hum", "Girl_Idle_Hum", new Vector2(550f, 0f)),
            State("Stretch", "Girl_Idle_Stretch", new Vector2(550f, 120f)),
        };
        AnimatorState lookAround = State("LookAround", "Girl_Idle_LookAround", new Vector2(850f, -120f));
        AnimatorState walk = State("Walk", "Girl_Walk", new Vector2(850f, 120f));
        machine.defaultState = sleep;

        AnimatorStateTransition Go(AnimatorState from, AnimatorState to, float duration, float? exitTime = null)
        {
            AnimatorStateTransition transition = from.AddTransition(to);
            transition.duration = duration;
            transition.hasExitTime = exitTime.HasValue;
            transition.exitTime = exitTime ?? 0f;
            return transition;
        }

        // The README's poses match exactly: GetUp ends standing, LieDown ends in Sleep's pose.
        Go(sleep, getUpState, 0.2f).AddCondition(AnimatorConditionMode.IfNot, 0f, "Asleep");
        Go(getUpState, idles[0], 0.2f, 1f);
        Go(lieDown, sleep, 0f, 1f);
        foreach (AnimatorState idle in idles)
        {
            idle.AddStateMachineBehaviour<RandomIdle>();
            for (int i = 0; i < idles.Length; i++)
                Go(idle, idles[i], 0.3f, 1f).AddCondition(AnimatorConditionMode.Equals, i, "IdleVariant");
            Go(idle, lookAround, 0.3f).AddCondition(AnimatorConditionMode.If, 0f, "LookAround");
        }
        Go(lookAround, idles[0], 0.3f).AddCondition(AnimatorConditionMode.IfNot, 0f, "LookAround");
        // Lying down is listed first: the baby stops walking and falls asleep in the same frame, and the first true transition wins.
        // Baby waits the LieDown clip's length from that frame.
        foreach (AnimatorState standing in new[] { idles[0], idles[1], idles[2], lookAround })
        {
            Go(standing, lieDown, 0.3f).AddCondition(AnimatorConditionMode.If, 0f, "Asleep");
            Go(standing, walk, 0.2f).AddCondition(AnimatorConditionMode.If, 0f, "Walking");
        }
        Go(walk, lieDown, 0.3f).AddCondition(AnimatorConditionMode.If, 0f, "Asleep");
        Go(walk, idles[0], 0.2f).AddCondition(AnimatorConditionMode.IfNot, 0f, "Walking");

        AssetDatabase.SaveAssets();
        return controller;
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

        // The Bird layer gets the see-through silhouette (AgentScripts/SetupBirdSilhouette.cs).
        int birdLayer = LayerMask.NameToLayer("Bird");
        if (birdLayer < 0)
            throw new System.InvalidOperationException("No Bird layer. Run AgentScripts/SetupBirdSilhouette.cs first.");
        foreach (Transform part in bird.GetComponentsInChildren<Transform>(true))
            part.gameObject.layer = birdLayer;

        var controller = bird.AddComponent<BirdController>();
        Set(controller, so =>
        {
            so.FindProperty("animator").objectReferenceValue = animator;
            so.FindProperty("clickMarker").objectReferenceValue = marker;
        });
        return controller;
    }

    // Rebuilds the bird's Animator Controller from the model's clips, along the art's flow (its README):
    // Glide <-> Hover (in place), Flap -> Landing -> a random perch idle -> Takeoff -> Flap. While gliding, one wingbeat every BirdGlideFlapInterval.
    // Perched, Refuse shakes the head once (it starts and ends in the perch idle pose). Parameters: Moving, Perched and Refuse (BirdController),
    // IdleVariant (RandomIdle).
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
        controller.AddParameter("Refuse", AnimatorControllerParameterType.Trigger);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;

        AnimatorState State(string clip, Vector2 position)
        {
            AnimatorState state = machine.AddState(clip.Replace("Bird_", ""), position);
            state.motion = clips[clip];
            return state;
        }
        AnimatorState hover = State("Bird_Hover", new Vector2(300f, -120f));
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
        AnimatorState refuse = State("Bird_Refuse", new Vector2(1200f, 60f));
        AnimatorState takeoff = State("Bird_Takeoff", new Vector2(600f, 300f));
        machine.defaultState = hover;

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
        // Cross-fades of 0.3 s between gliding and hovering, as the README suggests.
        Fly(Go(glide, hover, 0.3f)).AddCondition(AnimatorConditionMode.IfNot, 0f, "Moving");
        Fly(Go(hover, glide, 0.3f)).AddCondition(AnimatorConditionMode.If, 0f, "Moving");
        // After taking off: gliding at once if it moves, else one Flap cycle and then hovering.
        Fly(Go(flap, glide, 0.25f)).AddCondition(AnimatorConditionMode.If, 0f, "Moving");
        Fly(Go(flap, hover, 0.3f, 1f)).AddCondition(AnimatorConditionMode.IfNot, 0f, "Moving");
        // One Flap cycle, then back to gliding, which starts its clip again.
        Fly(Go(glide, glideFlap, 0.25f, BirdGlideFlapInterval / clips["Bird_Glide"].length));
        Fly(Go(glideFlap, glide, 0.25f, 1f)).AddCondition(AnimatorConditionMode.If, 0f, "Moving");
        Fly(Go(glideFlap, hover, 0.3f)).AddCondition(AnimatorConditionMode.IfNot, 0f, "Moving");
        Go(glideFlap, landing, 0.2f).AddCondition(AnimatorConditionMode.If, 0f, "Perched");
        // Glide ends in Landing's first pose; from the others it blends.
        Go(glide, landing, 0.1f).AddCondition(AnimatorConditionMode.If, 0f, "Perched");
        Go(hover, landing, 0.2f).AddCondition(AnimatorConditionMode.If, 0f, "Perched");
        Go(flap, landing, 0.2f).AddCondition(AnimatorConditionMode.If, 0f, "Perched");
        // The perched poses match exactly, so these cut.
        Go(landing, idles[0], 0f, 1f);
        foreach (AnimatorState idle in idles)
        {
            idle.AddStateMachineBehaviour<RandomIdle>();
            Go(idle, takeoff, 0.1f).AddCondition(AnimatorConditionMode.IfNot, 0f, "Perched");
            Go(idle, refuse, 0.1f).AddCondition(AnimatorConditionMode.If, 0f, "Refuse");
            for (int i = 0; i < idles.Length; i++)
                Go(idle, idles[i], 0f, 1f).AddCondition(AnimatorConditionMode.Equals, i, "IdleVariant");
        }
        Go(refuse, takeoff, 0.1f).AddCondition(AnimatorConditionMode.IfNot, 0f, "Perched");
        Go(refuse, idles[0], 0f, 1f);
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
        followCamera.Lens.FieldOfView = CameraFieldOfView;
        followCamera.Target.TrackingTarget = point.transform;
        var follow = cameraObject.AddComponent<CinemachineFollow>();
        follow.FollowOffset = CameraOffset + Vector3.up * CameraAimHeight;
        follow.TrackerSettings.BindingMode = BindingMode.WorldSpace;
        follow.TrackerSettings.PositionDamping = Vector3.one * 0.4f;

        var birdCamera = point.AddComponent<BirdCamera>();
        Set(birdCamera, so => so.FindProperty("followCamera").objectReferenceValue = followCamera);
        return birdCamera;
    }

    // When the bird enters the trigger band (across the road), the camera stays inside the area until the bird leaves the lock area
    // or the blocker opens (ADR 0014).
    static void Zone(GameObject parent, PathBlocker blocker, Vector3 trigger, Vector2 triggerSize, Vector3 areaCenter, Vector2 areaSize, Vector2 lockSize)
    {
        GameObject zoneObject = Child(parent, $"CameraZone_{blocker.name}", areaCenter);
        GameObject triggerObject = Child(zoneObject, "Trigger", trigger);
        var zone = zoneObject.AddComponent<CameraZone>();
        Set(zone, so =>
        {
            so.FindProperty("blocker").objectReferenceValue = blocker;
            so.FindProperty("trigger").objectReferenceValue = triggerObject.transform;
            so.FindProperty("triggerSize").vector2Value = triggerSize;
            so.FindProperty("areaSize").vector2Value = areaSize;
            so.FindProperty("lockSize").vector2Value = lockSize;
        });
    }

    static BabyRoute Route(GameObject parent, params Vector3[] points)
    {
        GameObject routeObject = Child(parent, "BabyRoute", points[0]);
        for (int i = 0; i < points.Length; i++)
            Child(routeObject, $"Point_{i}", points[i]);
        return routeObject.AddComponent<BabyRoute>();
    }

    // One terrain under every stage, long and wide enough that the camera, wherever the bird takes it, never shows its edge; the streams
    // across it; each stage's trees; and the snow of the intro (ADR 0018, ADR 0019, ADR 0020).
    static void BuildWorld(Stage[] stages, GameObject[] trees, Season season, BirdCamera birdCamera)
    {
        // The camera follows a point inside the stages' areas (BirdCamera).
        Bounds flown = stages[0].area;
        foreach (Stage stage in stages)
            flown.Encapsulate(stage.area);
        var (reachMin, reachMax) = Extent(CameraGround());
        Rect map = Rect.MinMaxRect(flown.min.x + reachMin.x - ViewMargin, flown.min.z + reachMin.y - ViewMargin,
            flown.max.x + reachMax.x + ViewMargin, flown.max.z + reachMax.y + ViewMargin);

        var world = new GameObject("World");
        Terrain terrain = BuildTerrain(world, map);
        Material water = Mat("Water", new Color(0.35f, 0.55f, 0.8f));
        for (int i = 0; i < Streams.Length; i++)
            Water(world, $"Stream_{i + 1}", map, Streams[i], water);
        // Behind where the baby starts, the first stage's trees reach back as far as the camera sees, also on the road, which starts at
        // the baby (ADR 0021).
        float behindStart = flown.min.z + reachMin.y;
        Forest(stages[0].gameObject, trees, terrain, behindStart, stages[0].area.max.z);
        Trees(trees, stages[0].gameObject, terrain, "TreesBehindTheStart", OuterTreeSpacing, 3,
            new[] { new Vector2(behindStart, stages[0].babyStart.position.z - 3f) }, new Vector2(-4.6f, 4.6f));
        for (int i = 1; i < stages.Length; i++)
            Forest(stages[i].gameObject, trees, terrain, stages[i].area.min.z, stages[i].area.max.z);
        Snow(world, season, birdCamera);
    }

    // The corners of the ground the camera shows, as X and Z offsets from the point it follows (BuildBirdCamera's camera).
    static Vector2[] CameraGround()
    {
        Vector3 eye = CameraOffset + Vector3.up * CameraAimHeight;
        Quaternion look = Quaternion.LookRotation(-CameraOffset);
        float up = Mathf.Tan(CameraFieldOfView * 0.5f * Mathf.Deg2Rad);
        float side = up * ScreenAspect;
        var corners = new Vector2[4];
        (float x, float y)[] screen = { (-1f, -1f), (1f, -1f), (1f, 1f), (-1f, 1f) };
        for (int i = 0; i < 4; i++)
        {
            Vector3 ray = look * new Vector3(screen[i].x * side, screen[i].y * up, 1f);
            Vector3 ground = eye + ray * (-eye.y / ray.y);
            corners[i] = new Vector2(ground.x, ground.z);
        }
        return corners;
    }

    static (Vector2 min, Vector2 max) Extent(Vector2[] points)
    {
        Vector2 min = points[0], max = points[0];
        foreach (Vector2 point in points)
        {
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }
        return (min, max);
    }

    static Terrain BuildTerrain(GameObject parent, Rect map)
    {
        const int resolution = 513;
        var data = new TerrainData { heightmapResolution = resolution };
        data.size = new Vector3(map.width, StreamDepth + HillHeight, map.height);
        var heights = new float[resolution, resolution];
        for (int row = 0; row < resolution; row++)
            for (int column = 0; column < resolution; column++)
            {
                float x = Mathf.Lerp(map.xMin, map.xMax, column / (resolution - 1f));
                float z = Mathf.Lerp(map.yMin, map.yMax, row / (resolution - 1f));
                heights[row, column] = (GroundHeight(x, z) + StreamDepth) / data.size.y;
            }
        data.SetHeights(0, 0, heights);
        AssetDatabase.DeleteAsset(TerrainDataPath);
        AssetDatabase.CreateAsset(data, TerrainDataPath);

        GameObject terrainObject = Terrain.CreateTerrainGameObject(data);
        terrainObject.name = "Terrain";
        terrainObject.layer = groundLayer;
        terrainObject.transform.SetParent(parent.transform, false);
        terrainObject.transform.position = new Vector3(map.xMin, -StreamDepth, map.yMin);
        var terrain = terrainObject.GetComponent<Terrain>();
        terrain.materialTemplate = Mat("Snow", new Color(0.92f, 0.95f, 1f));
        // The plain Lit snow material does not place the terrain's vertices itself, which instanced drawing needs.
        terrain.drawInstanced = false;
        return terrain;
    }

    // Flat at 0 where the bird flies (the road and the land beside it), low hills farther out, and a steep dip under each stream.
    // Near a stream the hills flatten out, so the streams run through level ground.
    static float GroundHeight(float x, float z)
    {
        const float bankWidth = 0.3f;
        float toStream = float.MaxValue;
        foreach (Vector2 stream in Streams)
            toStream = Mathf.Min(toStream, Mathf.Max(0f, Mathf.Max(stream.x - z, z - stream.y)));
        float hills = HillHeight * Mathf.PerlinNoise(x * 0.06f + 50f, z * 0.06f + 50f)
            * Mathf.SmoothStep(0f, 1f, (Mathf.Abs(x) - BirdAreaHalfWidth - 3f) / 15f)
            * Mathf.SmoothStep(0f, 1f, (toStream - 2f) / 8f);
        return Mathf.Lerp(-StreamDepth, hills, toStream / bankWidth);
    }

    // Across the whole terrain, between the stream's Z edges, its surface at -0.3. Water is not walkable ground, but clicking it sends
    // the bird there: its collider is on the click plane's layer.
    static void Water(GameObject parent, string name, Rect map, Vector2 stream, Material material)
    {
        GameObject water = Primitive(PrimitiveType.Cube, name, new Vector3(map.center.x, -0.45f, (stream.x + stream.y) / 2f),
            new Vector3(map.width, 0.3f, stream.y - stream.x), material, parent);
        water.layer = clickPlaneLayer;
    }

    // Each of Tommy's tree kinds comes bare and with leaves. The bird can perch on one branch of every tree: clicking near the perch
    // point lands it there; left idle, the bird lands on the nearest one by itself (ADR 0009).
    static (GameObject[] bare, GameObject[] leafy) BuildTreePrefabs()
    {
        if (!AssetDatabase.IsValidFolder(TreePrefabFolder))
            AssetDatabase.CreateFolder("Assets/03_Prefabs", "Trees");
        var bare = new GameObject[TreeModels.Length];
        var leafy = new GameObject[TreeModels.Length];
        for (int i = 0; i < TreeModels.Length; i++)
        {
            bare[i] = SaveTree($"Tree_{i + 1}_Bare", TreeModels[i].bare, TreeModels[i].perch);
            leafy[i] = SaveTree($"Tree_{i + 1}_Leafy", TreeModels[i].leafy, TreeModels[i].perch);
        }
        return (bare, leafy);
    }

    static GameObject SaveTree(string name, string model, Vector3 perch)
    {
        var root = new GameObject(name);
        PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>($"{TreeModelFolder}/{model}.fbx"), root.transform);
        GameObject spot = Child(root, "PerchPoint", perch);
        spot.layer = interactableLayer;
        var clickArea = spot.AddComponent<SphereCollider>();
        clickArea.isTrigger = true;
        clickArea.radius = 0.6f;
        var perchable = spot.AddComponent<Perchable>();
        Set(perchable, so => so.FindProperty("idleSpot").boolValue = true);
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{TreePrefabFolder}/{name}.prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    // A stage's trees, over its Z span but the streams: two dense rows along both edges of the road (also on the camera side; the
    // director accepts that they hide the road), and sparser trees on the terrain beyond, out to ForestWidth (ADR 0013, ADR 0020).
    static void Forest(GameObject parent, GameObject[] prefabs, Terrain ground, float zMin, float zMax)
    {
        var land = new System.Collections.Generic.List<Vector2>();
        float from = zMin;
        foreach (Vector2 stream in Streams)
            if (stream.y > zMin && stream.x < zMax)
            {
                if (stream.x > from)
                    land.Add(new Vector2(from, stream.x));
                from = stream.y;
            }
        if (from < zMax)
            land.Add(new Vector2(from, zMax));
        Vector2[] landRanges = land.ToArray();

        const float rowsEnd = 5f + RoadsideRowsWidth - 0.6f;
        Trees(prefabs, parent, ground, "RoadsideTrees", TreeSpacing, 1, landRanges, new Vector2(-rowsEnd, -4.6f), new Vector2(4.6f, rowsEnd));
        Trees(prefabs, parent, ground, "TerrainTrees", OuterTreeSpacing, 2, landRanges,
            new Vector2(-5f - ForestWidth + 0.6f, -rowsEnd - 0.6f), new Vector2(rowsEnd + 0.6f, 5f + ForestWidth - 0.6f));
    }

    // Falls over the ground the camera shows, in the intro only (SeasonSnow): a box turned with the camera, following the camera's
    // point. The wind carries a flake while it falls, so the box lies upwind: over the shown ground moved back by a whole fall's
    // drift (flakes landing there), and over the camera moved back by the drift down to its height (flakes passing close to it).
    // Flakes pass through the ground instead of colliding, which is cheaper; the ground hides them.
    static void Snow(GameObject parent, Season season, BirdCamera birdCamera)
    {
        Quaternion turn = Quaternion.Euler(0f, Quaternion.LookRotation(-CameraOffset).eulerAngles.y, 0f);
        // A whole fall's drift for the slowest and the fastest flakes.
        Vector2[] drifts = { SnowWind * (SnowHeight / SnowFallSpeed), SnowWind * (SnowHeight / (SnowFallSpeed + SnowFallSpeedVariation)) };
        Vector2[] ground = CameraGround();
        Vector2 eye = new(CameraOffset.x, CameraOffset.z);
        float aboveEye = (SnowHeight - (CameraOffset.y + CameraAimHeight)) / SnowHeight;
        var local = new System.Collections.Generic.List<Vector2>();
        foreach (Vector2 drift in drifts)
        {
            foreach (Vector2 corner in ground)
                local.Add(Turned(corner - drift));
            local.Add(Turned(eye - drift * aboveEye));
        }
        Vector2 Turned(Vector2 flat)
        {
            Vector3 point = Quaternion.Inverse(turn) * new Vector3(flat.x, 0f, flat.y);
            return new Vector2(point.x, point.z);
        }
        var (min, max) = Extent(local.ToArray());
        Vector3 middle = turn * new Vector3((min.x + max.x) / 2f, 0f, (min.y + max.y) / 2f);
        GameObject center = Child(birdCamera.gameObject, "SnowCenter", birdCamera.transform.position + middle);

        var snow = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(SnowfallPrefabPath), parent.transform);
        snow.transform.SetPositionAndRotation(new Vector3(0f, SnowHeight, 0f), turn);
        snow.transform.localScale = Vector3.one;
        var snowfall = snow.GetComponent<Snowfall>();
        Set(snowfall, so =>
        {
            so.FindProperty("followTarget").objectReferenceValue = center.transform;
            so.FindProperty("areaSize").vector2Value = max - min;
            so.FindProperty("spawnRate").floatValue = SnowPerSquareMetre * (max.x - min.x) * (max.y - min.y);
            so.FindProperty("fallSpeed").floatValue = SnowFallSpeed;
            so.FindProperty("fallSpeedVariation").floatValue = SnowFallSpeedVariation;
            so.FindProperty("wind").vector2Value = SnowWind;
            so.FindProperty("flakeSize").floatValue = 0.26f;
            // Long enough for the slowest flake to reach the ground.
            so.FindProperty("maxLifetime").floatValue = SnowHeight / SnowFallSpeed + 0.5f;
            so.FindProperty("collideWithWorld").boolValue = false;
            so.FindProperty("snowMaterial").objectReferenceValue = SnowflakeMaterial();
        });
        snowfall.Configure();
        var seasonSnow = snow.AddComponent<SeasonSnow>();
        Set(seasonSnow, so => so.FindProperty("season").objectReferenceValue = season);
    }

    // A white round flake, solid in the middle and soft at the edge, unlit and see-through.
    static Material SnowflakeMaterial()
    {
        const int size = 32;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), Vector2.one * (size / 2f)) / (size / 2f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, 1f - Mathf.SmoothStep(0.5f, 1f, distance)));
            }
        System.IO.File.WriteAllBytes(SnowflakeTexturePath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(SnowflakeTexturePath);
        var importer = (TextureImporter)AssetImporter.GetAtPath(SnowflakeTexturePath);
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();

        Material material = Mat("Snowflake", Color.white, transparent: true, shader: "Universal Render Pipeline/Particles/Unlit");
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(SnowflakeTexturePath));
        return material;
    }

    // A TreeGenerator over each X range times each Z range, on the ground, with the seed fixed so rebuilding gives the same trees.
    static void Trees(GameObject[] prefabs, GameObject parent, Terrain ground, string name, float spacing, int seed, Vector2[] zRanges,
        params Vector2[] xRanges)
    {
        var generator = Child(parent, name, parent.transform.position).AddComponent<TreeGenerator>();
        Set(generator, so =>
        {
            so.FindProperty("ground").objectReferenceValue = ground;
            SerializedProperty kinds = so.FindProperty("prefabs");
            kinds.arraySize = prefabs.Length;
            for (int i = 0; i < prefabs.Length; i++)
                kinds.GetArrayElementAtIndex(i).objectReferenceValue = prefabs[i];
            SerializedProperty areas = so.FindProperty("areas");
            areas.arraySize = xRanges.Length * zRanges.Length;
            int index = 0;
            foreach (Vector2 x in xRanges)
            foreach (Vector2 z in zRanges)
            {
                SerializedProperty area = areas.GetArrayElementAtIndex(index++);
                area.FindPropertyRelative("min").vector2Value = new Vector2(x.x, z.x);
                area.FindPropertyRelative("max").vector2Value = new Vector2(x.y, z.y);
            }
            so.FindProperty("spacing").floatValue = spacing;
            so.FindProperty("jitter").floatValue = 0.3f;
            so.FindProperty("scaleRange").vector2Value = new Vector2(0.85f, 1.15f);
            so.FindProperty("randomRotation").boolValue = true;
            so.FindProperty("seed").intValue = seed;
        });
        generator.Generate();
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

    static Material Mat(string name, Color color, bool transparent = false, string shader = "Universal Render Pipeline/Lit")
    {
        string path = $"{MaterialFolder}/{name}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find(shader));
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
