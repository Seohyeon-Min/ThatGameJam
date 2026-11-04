using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

// Builds stage 3, the fog maze (ADR 0028), in Main after stage 2: a long straight way into the fog, then six forks, each with an
// animal on its right way. Past the maze, with no sunlit spot between, the ending (CliffEnding, ADR 0029): the forest thins out
// toward a cliff where the ground ends, and a big stone stands before it. Rerunning rebuilds the stage's content under its Stage,
// which stays, so what refers to it keeps working. It also makes room on the ground past stage 2 (WorldGap), clears the world's
// forest over the maze and the ending and grows it again, stretches the click plane out over the drop, and adds the girl's Run state.
// unity command script run --file AgentScripts/BuildStage3.cs --entry BuildStage3.Run
public static class BuildStage3
{
    const string MainPath = "Assets/00_Scenes/Main.unity";
    const string AnimalPrefabPath = "Assets/03_Prefabs/Stage3/FogForkAnimal.prefab";
    const string AnimalMaterialPath = "Assets/01_Arts/Materials/Test/Animal.mat";
    const string RingMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat";
    const string FogMaterialPath = "Assets/01_Arts/Materials/Fog.mat";
    const string StoneMaterialPath = "Assets/01_Arts/Materials/Watercolor/WC_Rock.mat";
    const string GirlControllerPath = "Assets/01_Arts/Models/Girl/Girl.controller";
    const string GirlModelPath = "Assets/01_Arts/Models/Girl/NPC-Girl-Animated.fbx";
    const string MazeZoneName = "Stage3 Maze";
    const string EndingZoneName = "Stage3 Ending";
    static readonly string[] TreePrefabPaths =
    {
        "Assets/03_Prefabs/Trees/Tree_1_Bare.prefab", "Assets/03_Prefabs/Trees/Tree_2_Bare.prefab", "Assets/03_Prefabs/Trees/Tree_3_Bare.prefab",
    };

    // Fog the forest fades into, thinner over the ways (FogMaze), so both ways of every fork and the animals show through it.
    // Many thin layers up to about 2.6 m, so it reads as fog, not a ground sheet.
    const float FogDensity = 0.9f;
    const int FogResolution = 256;
    const int FogLayers = 7;
    const float FogLayerStep = 0.4f;
    // Every way is lined on both sides with a row of trees this far from its middle, so the forks read clearly.
    const float EdgeOffset = 2.6f;
    const float EdgeStep = 1.5f;
    // The forest between the ways keeps this far from the middle of a way, so the edge rows stand out.
    const float WayClearance = 4.4f;
    const float MazeTreeSpacing = 3.2f;
    // The maze's own forest, and its fog, cover this far either side of x 0. The world's forest stands beyond.
    const float MazeHalfWidth = 21f;
    // The girl turns back this far before a wall.
    const float WallStop = 1f;
    // The straight way and its fog start this far past the stage's own start (EntryInStage).
    const float StartStep = 1f;
    // "A rather long straight way" at the start, where the fog thickens.
    const float StraightLength = 21.5f;
    // The animal stands on the right way this far from its fork, this far to the side away from the dead end.
    const float AnimalAlong = 3.5f;
    const float AnimalSide = 1.3f;
    // Past the goal: the maze's forest and the fog end this far on. The fog thins out to none over its last metres.
    const float FogPastGoal = 6.5f;
    const float FogFadeOut = 8f;
    // The ending. The ground ends this far past the goal, in the cliff; the big stone stands before it, and she rests beside it
    // on the camera's side, facing out over the drop.
    const float CliffPastGoal = 24f;
    const float StoneX = -2.5f;
    const float StoneBeforeCliff = 3f;
    static readonly Vector3 StoneSize = new(2.6f, 0.9f, 2f);
    static readonly Vector3 RestFromStone = new(1.6f, 0f, -0.4f);
    // The bird flies out over the drop this far: further than the screen shows, so the screen's edge is what stops it.
    const float AreaPastCliff = 40f;
    // Where the camera stays in the ending, from the ground it centres (in front of the stone at the edge): the quarter view at
    // the stage's altitude (4 m) sees the ground this far ahead of the bird.
    static readonly Vector3 CameraFromGround = new(4f, 0f, -4f);
    // The camera locks when the bird crosses this far before the edge, and unlocks when it flies back past 1.5 m before that.
    const float CameraTriggerBeforeCliff = 6f;
    // The ending's own trees, on the world forest's grid, thin out from all of them where the maze's forest ends to this share
    // at the cliff.
    const float EndingTreeSpacing = 3f;
    const float EndingTreesAtCliff = 0.04f;
    // The ground is stretched here, a little into the straight way, where it is flat.
    const float JoinPastEntry = 4f;
    // Where the map starts, in the frame of the stage's root, which the stage spacing (Stage.AdjustSpacing) moves.
    static readonly Vector3 EntryInStage = new(0f, 0f, 38.5f);

    // In the quarter view (camera yaw 315) R runs straight right on the screen and U straight up, so the two ways of every fork
    // part at a right angle on the screen. Z runs up and to the right.
    static readonly Vector3 R = new Vector3(1f, 0f, 1f).normalized, U = new Vector3(-1f, 0f, 1f).normalized, Z = Vector3.forward;
    // Every way at least twice as long as in ADR 0027's map: right ways 22 to 28 m, dead ends 14 to 18 m.
    static readonly (Vector3, float)[][] RightMoves =
    {
        new[] { (R, 8f), (Z, 16f) },
        new[] { (U, 16f), (Z, 10f) },
        new[] { (U, 6f), (Z, 10f), (R, 12f) },
        new[] { (R, 10f), (Z, 14f) },
        new[] { (R, 6f), (Z, 10f), (U, 12f) },
        new[] { (U, 8f), (Z, 14f) },
    };
    static readonly (Vector3, float)[][] DeadMoves =
    {
        new[] { (U, 8f), (Z, 10f) },
        new[] { (R, 8f), (Z, 10f) },
        new[] { (R, 8f), (Z, 6f) },
        new[] { (U, 8f), (Z, 10f) },
        new[] { (U, 8f), (Z, 6f) },
        new[] { (R, 8f), (Z, 10f) },
    };
    // One animal per fork, in order. Placeholder capsules sized roughly like each.
    static readonly (string name, float scale)[] Animals =
    {
        ("Squirrel", 0.3f), ("Bird", 0.25f), ("Fox", 0.45f), ("Deer", 0.8f), ("Elk", 1f), ("Bear", 1f),
    };

    static Terrain terrain;

    public static string Run()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (Application.isPlaying || scene.path != MainPath)
            throw new InvalidOperationException("Open Main in Edit Mode.");
        terrain = Terrain.activeTerrain;
        Stage stage = GameObject.Find("02_WORLD/Stages/Stage3").GetComponent<Stage>();
        Vector3 entry = Ground(stage.transform.TransformPoint(EntryInStage));
        // The girl wakes at stage 2's sunlit spot, and the maze leads her from there: a cry calls her as soon as she is up.
        BabyRoute stage2Route = GameObject.Find("02_WORLD/Stages/Stage2").GetComponent<Stage>().route;
        Vector3 wake = stage2Route[stage2Route.Count - 1].position;

        // The ways, flat at first; set on the ground once the ground is there.
        Vector3 start = entry + Z * StartStep;
        Vector3 fork = start + Z * StraightLength;
        var rightWays = new List<Vector3[]>();
        var deadEnds = new List<Vector3[]>();
        for (int i = 0; i < RightMoves.Length; i++)
        {
            rightWays.Add(Walk(fork, RightMoves[i]));
            deadEnds.Add(Walk(fork, DeadMoves[i]));
            fork = rightWays[i][^1];
        }
        Vector3 goal = fork;
        string room = MakeRoom(entry.z + JoinPastEntry, goal.z + CliffPastGoal);

        Vector3[] approach = OnGround(new[] { wake, start, rightWays[0][0] });
        rightWays = rightWays.Select(OnGround).ToList();
        deadEnds = deadEnds.Select(OnGround).ToList();
        goal = rightWays[^1][^1];
        // The ground ends in the cliff.
        float cliffZ = terrain.transform.position.z + terrain.terrainData.size.z;
        float fogEnd = goal.z + FogPastGoal;
        float stageEnd = cliffZ + AreaPastCliff;

        foreach (Transform child in stage.transform.Cast<Transform>().ToArray())
            UnityEngine.Object.DestroyImmediate(child.gameObject);
        GameObject root = stage.gameObject;
        stage.babyStart = Child(root, "BabyStart", wake).transform;
        stage.birdStart = Child(root, "BirdStart", wake + new Vector3(1.5f, 2.5f, -1f)).transform;
        stage.birdAltitude = 4f;
        float areaStart = entry.z - 9f;
        stage.area = new Bounds(new Vector3(0f, 0f, (areaStart + stageEnd) / 2f), new Vector3(39f, 1f, stageEnd - areaStart));

        GameObject mazeRoot = Child(root, "FogMaze", start);
        FogField field = Fog(mazeRoot, start.z, fogEnd);
        GameObject approachPath = Path(mazeRoot, "Approach", approach);
        GameObject animalPrefab = AnimalPrefab();
        var forks = new FogFork[rightWays.Count];
        for (int i = 0; i < forks.Length; i++)
            forks[i] = Fork(mazeRoot, i, rightWays[i], deadEnds[i], animalPrefab);
        var maze = mazeRoot.AddComponent<FogMaze>();
        Set(maze, so =>
        {
            so.FindProperty("field").objectReferenceValue = field;
            so.FindProperty("approach").objectReferenceValue = approachPath.transform;
            Objects(so.FindProperty("forks"), forks);
        });

        (CliffEnding ending, Vector3[] endingPath, Vector3 stone) = Ending(root, goal, cliffZ, stageEnd);
        // The maze leads her to its goal, and the ending from there to the stone.
        GameObject route = Path(root, "BabyRoute", new[] { approach[0], goal });
        stage.route = route.AddComponent<BabyRoute>();
        var stop = route.transform.GetChild(0).gameObject.AddComponent<StopPoint>();
        Set(stop, so => so.FindProperty("blocker").objectReferenceValue = maze);
        var endStop = route.transform.GetChild(1).gameObject.AddComponent<StopPoint>();
        Set(endStop, so =>
        {
            so.FindProperty("blocker").objectReferenceValue = ending;
            so.FindProperty("pose").enumValueIndex = (int)WaitPose.Sit;
        });

        float mazeFrom = entry.z - 1f;
        float mazeTo = fogEnd + 2f;
        int mazeTrees = Trees(root, approach, rightWays, deadEnds, forks, goal, endingPath, mazeFrom, mazeTo);
        int endingTrees = EndingTrees(ending.gameObject, endingPath, stone, mazeTo, cliffZ);
        string world = RegrowWorld(mazeFrom, mazeTo, cliffZ);
        string clicks = StretchClickPlane(stageEnd);
        string run = AddRunState();

        EditorUtility.SetDirty(stage);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        float right = approach.Length > 0 ? Vector3.Distance(approach[0], approach[^1]) + rightWays.Sum(Length) : 0f;
        return $"Stage 3: {forks.Length} forks, right route {right:0} m, dead ends {deadEnds.Sum(Length):0} m, goal z {goal.z:0.0}, "
            + $"{mazeTrees} maze trees; cliff z {cliffZ:0.0}, ending walk {Length(endingPath):0.0} m, {endingTrees} ending trees. "
            + $"{room} {world} {clicks} Run state {run}.";
    }

    static Vector3[] Walk(Vector3 from, (Vector3 direction, float length)[] moves)
    {
        var points = new List<Vector3> { from };
        foreach ((Vector3 direction, float length) in moves)
            points.Add(points[^1] + direction * length);
        return points.ToArray();
    }

    static float Length(Vector3[] path)
    {
        float length = 0f;
        for (int i = 1; i < path.Length; i++)
            length += Vector3.Distance(path[i - 1], path[i]);
        return length;
    }

    static Vector3 Ground(Vector3 point) =>
        new(point.x, terrain.transform.position.y + terrain.SampleHeight(point), point.z);

    static Vector3[] OnGround(Vector3[] points) => points.Select(Ground).ToArray();

    // Opens the ground at joinZ (WorldGap) until it reaches endZ. Nothing when it already does, so rerunning keeps the ground.
    static string MakeRoom(float joinZ, float endZ)
    {
        float far = terrain.transform.position.z + terrain.terrainData.size.z;
        if (endZ <= far + 0.01f)
            return $"Ground already reaches z {far:0.0}.";
        WorldGap.Open(joinZ, endZ - far);
        return $"Ground opened by {endZ - far:0.0} m at z {joinZ:0.0}, now to z {endZ:0.0}.";
    }

    static FogField Fog(GameObject parent, float fromZ, float toZ)
    {
        var center = new Vector3(0f, 0f, (fromZ + toZ) / 2f);
        var size = new Vector2(MazeHalfWidth * 2f, toZ - fromZ);
        GameObject root = Child(parent, "Fog", center);
        var material = AssetDatabase.LoadAssetAtPath<Material>(FogMaterialPath);
        var layers = new Renderer[FogLayers];
        for (int i = 0; i < layers.Length; i++)
        {
            GameObject layer = GameObject.CreatePrimitive(PrimitiveType.Quad);
            layer.name = $"FogLayer_{i}";
            layer.transform.SetParent(root.transform);
            layer.transform.SetPositionAndRotation(center + Vector3.up * (0.2f + FogLayerStep * i), Quaternion.Euler(90f, 0f, 0f));
            layer.transform.localScale = new Vector3(size.x, size.y, 1f);
            UnityEngine.Object.DestroyImmediate(layer.GetComponent<Collider>());
            layers[i] = layer.GetComponent<Renderer>();
            layers[i].sharedMaterial = material;
        }
        var field = root.AddComponent<FogField>();
        Set(field, so =>
        {
            so.FindProperty("size").vector2Value = size;
            so.FindProperty("density").floatValue = FogDensity;
            so.FindProperty("resolution").intValue = FogResolution;
            so.FindProperty("rampLength").floatValue = StraightLength;
            so.FindProperty("fadeOutLength").floatValue = FogFadeOut;
            Objects(so.FindProperty("layers"), layers);
        });
        return field;
    }

    static FogFork Fork(GameObject parent, int index, Vector3[] right, Vector3[] dead, GameObject animalPrefab)
    {
        GameObject root = Child(parent, $"Fork_{index + 1}", right[0]);
        GameObject rightWay = Path(root, "RightWay", right.Skip(1).ToArray());
        // The last dead-end point is the wall; the girl stops short of it.
        Vector3 wall = dead[^1];
        Vector3 back = (dead[^2] - wall).normalized;
        Vector3[] walked = dead.ToArray();
        walked[^1] = wall + back * WallStop;
        GameObject deadEnd = Path(root, "DeadEnd", walked.Skip(1).ToArray());
        WallTrees(root, wall, back, index);

        // On the right way, to the side away from the dead end, facing along the right way.
        Vector3 along = (right[1] - right[0]).normalized;
        Vector3 side = Vector3.Cross(Vector3.up, along);
        if (Vector3.Dot(side, (dead[1] - dead[0]).normalized) > 0f)
            side = -side;
        var animal = (GameObject)PrefabUtility.InstantiatePrefab(animalPrefab, root.transform);
        (string name, float scale) = Animals[index];
        animal.name = $"Animal_{name}";
        animal.transform.SetPositionAndRotation(Ground(right[0] + along * AnimalAlong + side * AnimalSide), Quaternion.LookRotation(along));
        animal.transform.localScale = Vector3.one * scale;

        var fork = root.AddComponent<FogFork>();
        Set(fork, so =>
        {
            so.FindProperty("animal").objectReferenceValue = animal.GetComponent<FogForkAnimal>();
            so.FindProperty("rightWay").objectReferenceValue = rightWay.transform;
            so.FindProperty("deadEnd").objectReferenceValue = deadEnd.transform;
        });
        return fork;
    }

    // The wall closing a dead end: five trees close together across its end. Trees for now; maybe rocks later.
    static void WallTrees(GameObject parent, Vector3 wall, Vector3 back, int index)
    {
        GameObject root = Child(parent, "Wall", wall);
        Vector3 across = Vector3.Cross(Vector3.up, back);
        GameObject[] prefabs = TreePrefabs();
        for (int i = -2; i <= 2; i++)
        {
            GameObject tree = Tree(prefabs[(index + i + 6) % prefabs.Length], root);
            tree.transform.SetPositionAndRotation(Ground(wall + across * (EdgeOffset / 2f * i) + back * (0.25f * Mathf.Abs(i))), Quaternion.Euler(0f, 90f * (index + i), 0f));
        }
    }

    // The forest of the maze. Every way is lined on both sides with a row of trees, and every fork has a wedge of trees between its
    // two ways; behind them a looser jittered grid fills the rest, kept off the ways and away from the walls, animals and the goal.
    static int Trees(GameObject parent, Vector3[] approach, List<Vector3[]> rightWays, List<Vector3[]> deadEnds, FogFork[] forks,
        Vector3 goal, Vector3[] endingPath, float fromZ, float toZ)
    {
        GameObject root = Child(parent, "MazeTrees", Vector3.zero);
        // Where the girl walks: the dead ends only up to where she turns back.
        var walked = new List<(Vector3, Vector3)>();
        // What gets lined with trees: the dead ends all the way to their walls.
        var lined = new List<(Vector3, Vector3)>();
        void AddPath(List<(Vector3, Vector3)> list, IList<Vector3> path)
        {
            for (int i = 1; i < path.Count; i++)
                list.Add((path[i - 1], path[i]));
        }
        foreach (var list in new[] { walked, lined })
        {
            AddPath(list, approach);
            foreach (Vector3[] path in rightWays)
                AddPath(list, path);
        }
        foreach (Vector3[] path in deadEnds)
        {
            Vector3 wall = path[^1];
            AddPath(walked, path.Take(path.Length - 1).Append(wall + (path[^2] - wall).normalized * WallStop).ToArray());
            AddPath(lined, path);
        }
        // Out of the maze toward the stone, through its last trees: kept clear but not lined.
        AddPath(walked, endingPath);
        var walls = deadEnds.Select(path => path[^1]).ToArray();
        var animals = forks.Select(fork => fork.Animal.transform.position).ToArray();
        // The wall trees stand across the dead end's width at a wall point.
        var placed = new List<Vector3>(walls);

        GameObject[] prefabs = TreePrefabs();
        var random = new System.Random(28);
        bool Free(Vector3 position, float clearance, float gap) =>
            !walked.Any(s => DistanceToSegment(position, s.Item1, s.Item2) < clearance)
            && !walls.Any(w => Flat(position, w) < EdgeOffset + 0.4f)
            && !animals.Any(a => Flat(position, a) < 1.5f)
            && Flat(position, goal) >= 4f
            && !placed.Any(p => Flat(position, p) < gap);
        void Plant(Vector3 position)
        {
            GameObject tree = Tree(prefabs[random.Next(prefabs.Length)], root);
            tree.transform.SetPositionAndRotation(Ground(position), Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f));
            tree.transform.localScale = Vector3.one * Mathf.Lerp(0.85f, 1.15f, (float)random.NextDouble());
            placed.Add(position);
        }

        foreach ((Vector3 a, Vector3 b) in lined)
        {
            Vector3 along = (b - a).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, along);
            float length = Vector3.Distance(a, b);
            for (float d = 0f; d <= length; d += EdgeStep)
                foreach (float sign in new[] { -1f, 1f })
                {
                    Vector3 position = a + along * d + side * (sign * EdgeOffset) + Jitter(random, 0.15f);
                    if (Free(position, EdgeOffset - 0.3f, 1f))
                        Plant(position);
                }
        }
        // A wedge between the two ways of every fork, so the split shows.
        for (int i = 0; i < rightWays.Count; i++)
        {
            Vector3 origin = rightWays[i][0];
            Vector3 bisector = ((rightWays[i][1] - origin).normalized + (deadEnds[i][1] - origin).normalized).normalized;
            Vector3 across = Vector3.Cross(Vector3.up, bisector);
            for (float d = 1f; d <= 7f; d += 1.1f)
                foreach (float sideways in new[] { 0f, -0.9f, 0.9f })
                {
                    Vector3 position = origin + bisector * d + across * sideways;
                    if (Free(position, EdgeOffset - 0.3f, 0.9f))
                        Plant(position);
                }
        }
        for (float x = -MazeHalfWidth; x <= MazeHalfWidth; x += MazeTreeSpacing)
            for (float z = fromZ; z <= toZ; z += MazeTreeSpacing)
            {
                Vector3 position = new Vector3(x, 0f, z) + Jitter(random, MazeTreeSpacing * 0.3f);
                if (Mathf.Abs(position.x) <= MazeHalfWidth && Free(position, WayClearance, 1.2f))
                    Plant(position);
            }
        return placed.Count - walls.Length;
    }

    // The ending past the maze: from its goal she walks to the big stone before the cliff and rests beside it (CliffEnding). The
    // camera stays there once the bird comes near, so the bird can fly out over the drop to the edge of the screen. Returns the
    // ending, the way she walks (from the goal), and the stone.
    static (CliffEnding, Vector3[], Vector3) Ending(GameObject parent, Vector3 goal, float cliffZ, float stageEnd)
    {
        GameObject root = Child(parent, "CliffEnding", goal);
        Vector3 stone = Ground(new Vector3(StoneX, 0f, cliffZ - StoneBeforeCliff));
        Vector3 rest = Ground(stone + RestFromStone);
        // A gentle bend, not a ruler line.
        Vector3[] walk = OnGround(new[]
        {
            goal,
            Vector3.Lerp(goal, rest, 0.35f) + Vector3.right * 1.2f,
            Vector3.Lerp(goal, rest, 0.7f) - Vector3.right * 0.8f,
            rest,
        });
        GameObject path = Path(root, "Path", walk.Skip(1).ToArray());
        GameObject view = Child(root, "View", Ground(rest + Vector3.forward * 6f));
        GameObject edge = Child(root, "Edge", Ground(new Vector3(0f, 0f, cliffZ - 0.01f)));

        GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        rock.name = "Stone";
        UnityEngine.Object.DestroyImmediate(rock.GetComponent<Collider>());
        rock.transform.SetParent(root.transform);
        // Sunk a little, so it sits in the ground.
        rock.transform.SetPositionAndRotation(stone + Vector3.up * (StoneSize.y * 0.35f), Quaternion.Euler(0f, 20f, 0f));
        rock.transform.localScale = StoneSize;
        rock.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(StoneMaterialPath);

        var ending = root.AddComponent<CliffEnding>();
        Set(ending, so =>
        {
            so.FindProperty("path").objectReferenceValue = path.transform;
            so.FindProperty("view").objectReferenceValue = view.transform;
            so.FindProperty("edge").objectReferenceValue = edge.transform;
        });

        // Centred on the edge in front of the stone. The bird locks it crossing a band before the stone and unlocks it flying back
        // past the band toward the maze; out over the drop the lock reaches past what the screen shows.
        GameObject zoneObject = Child(root, "CameraZone", new Vector3(stone.x, 0f, cliffZ) + CameraFromGround);
        GameObject trigger = Child(zoneObject, "Trigger", new Vector3(0f, 0f, cliffZ - CameraTriggerBeforeCliff));
        float lockFrom = trigger.transform.position.z - 1.5f;
        Vector3 zone = zoneObject.transform.position;
        var cameraZone = zoneObject.AddComponent<CameraZone>();
        Set(cameraZone, so =>
        {
            so.FindProperty("blocker").objectReferenceValue = ending;
            so.FindProperty("trigger").objectReferenceValue = trigger.transform;
            so.FindProperty("triggerSize").vector2Value = new Vector2(60f, 2f);
            so.FindProperty("lockSize").vector2Value = new Vector2(60f, stageEnd - lockFrom);
            so.FindProperty("lockOffset").vector2Value = new Vector2(-zone.x, (lockFrom + stageEnd) / 2f - zone.z);
            so.FindProperty("areaSize").vector2Value = new Vector2(1f, 1f);
        });
        return (ending, walk, stone);
    }

    // The forest past the maze, on the world forest's grid across the whole ground, thinning out from all of it where the maze's
    // forest ends to almost none at the cliff, so the forest fades out instead of stopping. Kept off her walk and the stone.
    static int EndingTrees(GameObject parent, Vector3[] walk, Vector3 stone, float fromZ, float cliffZ)
    {
        GameObject root = Child(parent, "Trees", Vector3.zero);
        GameObject[] prefabs = TreePrefabs();
        var random = new System.Random(29);
        float padding = new SerializedObject(GameObject.Find("01_SYSTEM/TreeGenerator").GetComponent<TreeGenerator>())
            .FindProperty("edgePadding").floatValue;
        float minX = terrain.transform.position.x + padding;
        float maxX = terrain.transform.position.x + terrain.terrainData.size.x - padding;
        int count = 0;
        for (float x = minX; x <= maxX; x += EndingTreeSpacing)
            for (float z = fromZ; z <= cliffZ; z += EndingTreeSpacing)
            {
                Vector3 position = new Vector3(x, 0f, z) + Jitter(random, EndingTreeSpacing * 0.3f);
                // Drawn for every grid point, so a change in what is kept clear doesn't move the trees after it.
                float keep = (float)random.NextDouble();
                float yaw = (float)random.NextDouble() * 360f;
                float scale = Mathf.Lerp(0.85f, 1.15f, (float)random.NextDouble());
                int which = random.Next(prefabs.Length);
                float share = Mathf.Lerp(1f, EndingTreesAtCliff, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(fromZ, cliffZ - 4f, position.z)));
                bool clear = position.z < cliffZ - 1.2f && Flat(position, stone) >= 3.5f
                    && Enumerable.Range(1, walk.Length - 1).All(i => DistanceToSegment(position, walk[i - 1], walk[i]) >= 2.5f);
                if (keep > share || !clear)
                    continue;
                GameObject tree = Tree(prefabs[which], root);
                tree.transform.SetPositionAndRotation(Ground(position), Quaternion.Euler(0f, yaw, 0f));
                tree.transform.localScale = Vector3.one * scale;
                count++;
            }
        return count;
    }

    // The world's click plane, under the ground, reaches out over the drop past where the bird may fly, so a click there takes it.
    static string StretchClickPlane(float toZ)
    {
        var box = GameObject.Find("02_WORLD/ClickPlane").GetComponent<BoxCollider>();
        Bounds bounds = box.bounds;
        if (bounds.max.z >= toZ)
            return $"Click plane already reaches z {bounds.max.z:0}.";
        float minZ = bounds.min.z;
        Vector3 size = box.size;
        size.z += (toZ - bounds.max.z) / box.transform.lossyScale.z;
        box.size = size;
        Vector3 center = box.center;
        center.z += (toZ - bounds.max.z) / 2f / box.transform.lossyScale.z;
        box.center = center;
        EditorUtility.SetDirty(box);
        return $"Click plane z {minZ:0} to {box.bounds.max.z:0}.";
    }

    static float Flat(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

    static Vector3 Jitter(System.Random random, float amount) =>
        new(((float)random.NextDouble() * 2f - 1f) * amount, 0f, ((float)random.NextDouble() * 2f - 1f) * amount);

    static float DistanceToSegment(Vector3 point, Vector3 a, Vector3 b)
    {
        point.y = a.y = b.y = 0f;
        Vector3 ab = b - a;
        float t = Mathf.Clamp01(Vector3.Dot(point - a, ab) / ab.sqrMagnitude);
        return Vector3.Distance(point, a + ab * t);
    }

    // The world's forest stays off the maze (a Clear zone over it, as the road has) and off the ending, across the whole ground there
    // (its own trees thin out to the cliff), and grows again: the ground opened past stage 2 is bare until it does. The cliff closes
    // the road off now, so the forest no longer closes its top end (its top padding). The flowers and grass of stage 3 follow its new
    // area; past the cliff there is no ground for them.
    static string RegrowWorld(float fromZ, float toZ, float cliffZ)
    {
        var trees = GameObject.Find("01_SYSTEM/TreeGenerator").GetComponent<TreeGenerator>();
        ClearZone(trees, MazeZoneName, new Vector3(0f, 0f, (fromZ + toZ) / 2f), new Vector3(MazeHalfWidth * 2f, 1f, toZ - fromZ));
        TerrainData data = terrain.terrainData;
        float middleX = terrain.transform.position.x + data.size.x / 2f;
        ClearZone(trees, EndingZoneName, new Vector3(middleX, 0f, (toZ + cliffZ + 1f) / 2f), new Vector3(data.size.x + 2f, 1f, cliffZ + 1f - toZ));
        Set(trees, so => so.FindProperty("topPadding").floatValue = 0f);
        trees.Generate();
        var parts = new List<string> { $"world trees {trees.TreeCount}" };
        foreach (string name in new[] { "01_SYSTEM/FlowerGenerator", "01_SYSTEM/GrassGenerator" })
        {
            var scatter = GameObject.Find(name).GetComponent<ScatterGenerator>();
            scatter.Generate();
            parts.Add($"{scatter.name} {scatter.MadeCount}");
        }
        return string.Join(", ", parts) + ".";
    }

    static void ClearZone(TreeGenerator trees, string name, Vector3 center, Vector3 size)
    {
        Transform zoneTransform = trees.transform.Find(name);
        if (zoneTransform == null)
        {
            var zoneObject = new GameObject(name, typeof(BoxCollider), typeof(TreeZone));
            zoneObject.transform.SetParent(trees.transform);
            zoneTransform = zoneObject.transform;
        }
        zoneTransform.GetComponent<TreeZone>().kind = TreeZone.Kind.Clear;
        zoneTransform.SetPositionAndRotation(center, Quaternion.identity);
        zoneTransform.localScale = Vector3.one;
        var box = zoneTransform.GetComponent<BoxCollider>();
        box.center = Vector3.zero;
        box.size = size;
    }

    // Run, played directly by Baby (CrossFade) as BalanceWalk and Jump are, so without transitions. Kept if it is there.
    static string AddRunState()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(GirlControllerPath);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        if (machine.states.Any(child => child.state.name == "Run"))
            return "kept";
        AnimatorState run = machine.AddState("Run", new Vector2(1150f, 0f));
        run.motion = AssetDatabase.LoadAllAssetsAtPath(GirlModelPath).OfType<AnimationClip>().First(clip => clip.name == "Girl_Run");
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return "added";
    }

    // The fork's animal: a placeholder capsule on the ground, and the ring of sound it shows when it answers. Saved over the old
    // prefab, so its GUID stays.
    static GameObject AnimalPrefab()
    {
        if (!AssetDatabase.IsValidFolder("Assets/03_Prefabs/Stage3"))
            AssetDatabase.CreateFolder("Assets/03_Prefabs", "Stage3");
        var root = new GameObject("FogForkAnimal");
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>());
        body.transform.SetParent(root.transform);
        body.transform.SetLocalPositionAndRotation(new Vector3(0f, 0.5f, 0f), Quaternion.Euler(90f, 0f, 0f));
        body.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(AnimalMaterialPath);

        var ringObject = new GameObject("CryRing", typeof(LineRenderer), typeof(CryRing));
        ringObject.transform.SetParent(root.transform);
        var line = ringObject.GetComponent<LineRenderer>();
        line.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(RingMaterialPath);
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.alignment = LineAlignment.View;
        line.numCapVertices = 0;
        line.numCornerVertices = 2;
        // After the fog, which is in the same transparent queue at order 0.
        line.sortingOrder = 1;

        var animal = root.AddComponent<FogForkAnimal>();
        Set(animal, so =>
        {
            so.FindProperty("body").objectReferenceValue = body.GetComponent<Renderer>();
            so.FindProperty("ring").objectReferenceValue = ringObject.GetComponent<CryRing>();
        });
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, AnimalPrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    static GameObject[] TreePrefabs() => TreePrefabPaths.Select(AssetDatabase.LoadAssetAtPath<GameObject>).ToArray();

    // A tree painted in watercolor as the world's trees are: the world's TreeGenerator bakes every model of a kind together, so
    // these take its bake (and its fade when they stand between the camera and the bird or the girl) when it grows again.
    static GameObject Tree(GameObject prefab, GameObject parent)
    {
        var tree = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.transform);
        tree.AddComponent<WatercolorPaintedModel>();
        return tree;
    }

    static GameObject Path(GameObject parent, string name, Vector3[] points)
    {
        GameObject root = Child(parent, name, points[0]);
        for (int i = 0; i < points.Length; i++)
            Child(root, $"Point_{i}", points[i]);
        return root;
    }

    static GameObject Child(GameObject parent, string name, Vector3 position)
    {
        var child = new GameObject(name);
        child.transform.SetParent(parent.transform);
        child.transform.position = position;
        return child;
    }

    static void Set(UnityEngine.Object target, Action<SerializedObject> edit)
    {
        var so = new SerializedObject(target);
        edit(so);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void Objects(SerializedProperty list, UnityEngine.Object[] values)
    {
        list.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }
}
