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
// which stays, so what refers to it keeps working; the DreamFog and HiddenUntilSeason set on that content are carried over. It also
// makes room on the ground past stage 2 (WorldGap), stretches the click plane out over the drop, and adds the girl's Run state.
// The stage's trees are the world's forest (TreeGenerator, ADR 0031): its zones here keep every way clear, with edges that wander,
// and thin the forest out toward the cliff, and it grows again.
// unity command script run --file AgentScripts/BuildStage3.cs --entry BuildStage3.Run
public static class BuildStage3
{
    const string MainPath = "Assets/00_Scenes/Main.unity";
    const string AnimalPrefabPath = "Assets/03_Prefabs/Stage3/FogForkAnimal.prefab";
    const string FogMaterialPath = "Assets/01_Arts/Materials/Fog.mat";
    const string StoneMaterialPath = "Assets/01_Arts/Materials/Watercolor/WC_Rock.mat";
    const string GirlControllerPath = "Assets/01_Arts/Models/Girl/Girl.controller";
    const string GirlModelPath = "Assets/01_Arts/Models/Girl/NPC-Girl-Animated.fbx";
    // The TreeGenerator's zones for this stage, under this child of it. The two before them are what earlier builds left.
    const string ZonesName = "Stage3 Zones";
    static readonly string[] OldZoneNames = { "Stage3 Maze", "Stage3 Ending" };

    // Fog the forest fades into, thinner over the ways (FogMaze), so both ways of every fork and the animals show through it.
    // Many thin layers up to about 2.6 m, so it reads as fog, not a ground sheet.
    const float FogDensity = 0.9f;
    const int FogResolution = 512;
    const int FogLayers = 7;
    const float FogLayerStep = 0.4f;
    // The forest keeps off every way in pieces this long, each edge of a piece this far from the way's middle, drawn anew for every
    // piece and side, so the edges wander instead of running straight.
    static readonly Vector2 WayPieceLength = new(3f, 6f);
    static readonly Vector2 WayHalfWidth = new(1.8f, 3.2f);
    // Where the ways meet or turn, and around each animal, a square this wide is kept clear.
    const float CornerClear = 5f;
    const float AnimalClear = 3f;
    // The fog covers this far either side of x 0.
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
    // Past the goal: the fog ends this far on, and thins out to none over its last metres. The forest starts thinning 2 m past that.
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
    // Toward the cliff the forest thins out to this share (a Thin zone), and keeps this far off the stone.
    const float EndingTreesAtCliff = 0.04f;
    const float StoneClear = 3.5f;
    // The ground is stretched here, a little into the straight way, where it is flat.
    const float JoinPastEntry = 4f;
    // Where the map starts, in the frame of the stage's root, which the stage spacing (Stage.AdjustSpacing) moves.
    static readonly Vector3 EntryInStage = new(0f, 0f, 38.5f);

    // In the quarter view (camera yaw 315) R runs straight right on the screen and U straight up, so the two ways of every fork
    // part at a right angle on the screen. Z runs up and to the right.
    static readonly Vector3 R = new Vector3(1f, 0f, 1f).normalized, U = new Vector3(-1f, 0f, 1f).normalized, Z = Vector3.forward;
    // Right ways 44 to 56 m, dead ends 21 to 27 m: twice and one and a half times the first map of ADR 0028, where they felt short
    // (ADR 0031).
    static readonly (Vector3, float)[][] RightMoves =
    {
        new[] { (R, 16f), (Z, 32f) },
        new[] { (U, 32f), (Z, 20f) },
        new[] { (U, 12f), (Z, 20f), (R, 24f) },
        new[] { (R, 20f), (Z, 28f) },
        new[] { (R, 12f), (Z, 20f), (U, 24f) },
        new[] { (U, 16f), (Z, 28f) },
    };
    static readonly (Vector3, float)[][] DeadMoves =
    {
        new[] { (U, 12f), (Z, 15f) },
        new[] { (R, 12f), (Z, 15f) },
        new[] { (R, 12f), (Z, 9f) },
        new[] { (U, 12f), (Z, 15f) },
        new[] { (U, 12f), (Z, 9f) },
        new[] { (R, 12f), (Z, 15f) },
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
        // Wide enough for the farthest way, with room for its forest edge.
        float halfWidth = Mathf.Max(MazeHalfWidth, rightWays.Concat(deadEnds).SelectMany(path => path).Max(point => Mathf.Abs(point.x)) + 4f);

        string dreamFog = Saved<DreamFog>(stage, "FogMaze/Fog");
        string mazeHidden = Saved<HiddenUntilSeason>(stage, "FogMaze");
        string endingHidden = Saved<HiddenUntilSeason>(stage, "CliffEnding");
        foreach (Transform child in stage.transform.Cast<Transform>().ToArray())
            UnityEngine.Object.DestroyImmediate(child.gameObject);
        GameObject root = stage.gameObject;
        stage.babyStart = Child(root, "BabyStart", wake).transform;
        stage.birdStart = Child(root, "BirdStart", wake + new Vector3(1.5f, 2.5f, -1f)).transform;
        stage.birdAltitude = 4f;
        float areaStart = entry.z - 9f;
        stage.area = new Bounds(new Vector3(0f, 0f, (areaStart + stageEnd) / 2f), new Vector3(2f * halfWidth - 3f, 1f, stageEnd - areaStart));

        GameObject mazeRoot = Child(root, "FogMaze", start);
        FogField field = Fog(mazeRoot, start.z, fogEnd, halfWidth);
        if (Restore<DreamFog>(field.gameObject, dreamFog) is DreamFog fog)
            Set(fog, so => so.FindProperty("field").objectReferenceValue = field);
        GameObject approachPath = Path(mazeRoot, "Approach", approach);
        var animalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AnimalPrefabPath);
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
        Restore<HiddenUntilSeason>(mazeRoot, mazeHidden);

        (CliffEnding ending, Vector3[] endingPath, Vector3 stone) = Ending(root, goal, cliffZ, stageEnd);
        Restore<HiddenUntilSeason>(ending.gameObject, endingHidden);
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

        var ways = new List<Vector3[]> { approach };
        ways.AddRange(rightWays);
        ways.AddRange(deadEnds);
        ways.Add(endingPath);
        int zones = Zones(ways, forks.Select(f => f.Animal.transform.position), stone, fogEnd + 2f, cliffZ);
        string road = EndRoad(start.z);
        string world = RegrowWorld();
        string clicks = StretchClickPlane(stageEnd);
        string run = AddRunState();

        EditorUtility.SetDirty(stage);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        float right = approach.Length > 0 ? Vector3.Distance(approach[0], approach[^1]) + rightWays.Sum(Length) : 0f;
        return $"Stage 3: {forks.Length} forks, right route {right:0} m, dead ends {deadEnds.Sum(Length):0} m, goal z {goal.z:0.0}, "
            + $"half width {halfWidth:0.0}; cliff z {cliffZ:0.0}, ending walk {Length(endingPath):0.0} m; {zones} tree zones. "
            + $"Carried over: DreamFog {dreamFog != null}, hidden maze {mazeHidden != null}, hidden ending {endingHidden != null}. "
            + $"{room} {road} {world} {clicks} Run state {run}.";
    }

    // A component on the stage's content as JSON, to put back on the rebuilt content (Restore). Null if it isn't there.
    static string Saved<T>(Stage stage, string path) where T : Component
    {
        Transform target = stage.transform.Find(path);
        return target != null && target.TryGetComponent(out T component) ? EditorJsonUtility.ToJson(component) : null;
    }

    static T Restore<T>(GameObject target, string json) where T : Component
    {
        if (json == null)
            return null;
        T component = target.AddComponent<T>();
        EditorJsonUtility.FromJsonOverwrite(json, component);
        return component;
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

    static FogField Fog(GameObject parent, float fromZ, float toZ, float halfWidth)
    {
        var center = new Vector3(0f, 0f, (fromZ + toZ) / 2f);
        var size = new Vector2(halfWidth * 2f, toZ - fromZ);
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

    // The TreeGenerator's zones for this stage, made again under ZonesName: every way kept clear in pieces whose edges wander (each edge
    // of each piece drawn anew), a square at every corner and fork and around each animal and the stone, and from where the fog ends
    // the forest thinning out across the whole ground to the cliff. Returns how many zones.
    static int Zones(List<Vector3[]> ways, IEnumerable<Vector3> animals, Vector3 stone, float thinFrom, float cliffZ)
    {
        Transform trees = GameObject.Find("01_SYSTEM/TreeGenerator").transform;
        foreach (string name in OldZoneNames.Append(ZonesName))
        {
            Transform old = trees.Find(name);
            if (old != null)
                UnityEngine.Object.DestroyImmediate(old.gameObject);
        }
        GameObject root = Child(trees.gameObject, ZonesName, Vector3.zero);
        var random = new System.Random(31);
        float Draw(Vector2 range) => Mathf.Lerp(range.x, range.y, (float)random.NextDouble());
        foreach (Vector3[] way in ways)
            for (int i = 1; i < way.Length; i++)
            {
                Vector3 a = way[i - 1], b = way[i];
                Vector3 along = Vector3.ProjectOnPlane(b - a, Vector3.up).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, along);
                float length = Flat(a, b);
                for (float from = 0f; from < length;)
                {
                    float to = Mathf.Min(from + Draw(WayPieceLength), length);
                    float left = Draw(WayHalfWidth), right = Draw(WayHalfWidth);
                    Vector3 middle = a + along * ((from + to) / 2f) + side * ((right - left) / 2f);
                    // A little longer than the piece, so no tree slips in between two pieces.
                    Zone(root, TreeZone.Kind.Clear, middle, along, new Vector3(left + right, 1f, to - from + 0.5f));
                    from = to;
                }
            }
        foreach (Vector3 corner in ways.SelectMany(way => way))
            Zone(root, TreeZone.Kind.Clear, corner, Vector3.forward, new Vector3(CornerClear, 1f, CornerClear));
        foreach (Vector3 animal in animals)
            Zone(root, TreeZone.Kind.Clear, animal, Vector3.forward, new Vector3(AnimalClear, 1f, AnimalClear));
        Zone(root, TreeZone.Kind.Clear, stone, Vector3.forward, new Vector3(StoneClear * 2f, 1f, StoneClear * 2f));
        TerrainData data = terrain.terrainData;
        float middleX = terrain.transform.position.x + data.size.x / 2f;
        TreeZone thin = Zone(root, TreeZone.Kind.Thin, new Vector3(middleX, 0f, (thinFrom + cliffZ) / 2f), Vector3.forward,
            new Vector3(data.size.x + 2f, 1f, cliffZ - thinFrom));
        thin.farShare = EndingTreesAtCliff;
        return root.transform.childCount;
    }

    // The road's Clear zone, from the stages before, ends where the maze starts: past it the stage's own zones keep the ways clear.
    // Opening the ground (WorldGap) stretches it across the join, so it would run on straight through the maze.
    static string EndRoad(float endZ)
    {
        var box = GameObject.Find("01_SYSTEM/TreeGenerator/Road").GetComponent<BoxCollider>();
        float nearZ = box.transform.TransformPoint(box.center - Vector3.forward * box.size.z / 2f).z;
        float farZ = box.transform.TransformPoint(box.center + Vector3.forward * box.size.z / 2f).z;
        if (farZ <= endZ)
            return $"Road zone ends at z {farZ:0.0}.";
        Vector3 size = box.size;
        size.z = endZ - nearZ;
        box.size = size;
        box.center = box.transform.InverseTransformPoint(new Vector3(0f, 0f, (nearZ + endZ) / 2f)).z * Vector3.forward + new Vector3(box.center.x, box.center.y, 0f);
        EditorUtility.SetDirty(box);
        return $"Road zone cut from z {farZ:0.0} to {endZ:0.0}.";
    }

    static TreeZone Zone(GameObject parent, TreeZone.Kind kind, Vector3 center, Vector3 forward, Vector3 size)
    {
        var zoneObject = new GameObject(kind.ToString(), typeof(BoxCollider), typeof(TreeZone));
        zoneObject.transform.SetParent(parent.transform);
        zoneObject.transform.SetPositionAndRotation(new Vector3(center.x, 0f, center.z), Quaternion.LookRotation(forward));
        zoneObject.GetComponent<BoxCollider>().size = size;
        var zone = zoneObject.GetComponent<TreeZone>();
        zone.kind = kind;
        return zone;
    }

    // The world's forest grows again over the stage's new zones: the ground opened past stage 2 is bare until it does. The cliff closes
    // the road off now, so the forest no longer closes its top end (its top padding). The flowers and grass of stage 3 follow its new
    // area; past the cliff there is no ground for them.
    static string RegrowWorld()
    {
        var trees = GameObject.Find("01_SYSTEM/TreeGenerator").GetComponent<TreeGenerator>();
        Set(trees, so => so.FindProperty("topPadding").floatValue = 0f);
        trees.Generate();
        var parts = new List<string> { $"world trees {trees.TreeCount} ({trees.CanLeafCount} can leaf, {trees.OakCount} oaks)" };
        foreach (string name in new[] { "01_SYSTEM/FlowerGenerator", "01_SYSTEM/GrassGenerator" })
        {
            var scatter = GameObject.Find(name).GetComponent<ScatterGenerator>();
            scatter.Generate();
            parts.Add($"{scatter.name} {scatter.MadeCount}");
        }
        return string.Join(", ", parts) + ".";
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
