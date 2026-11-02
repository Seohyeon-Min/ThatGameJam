using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Builds stage 3, the fog maze (ADR 0027), in Main after stage 2, from the director's sketch (20261101_215022.jpg).
// The sketch is turned so its start-to-goal line runs along +Z. Rerunning replaces the stage.
// unity command script run --file AgentScripts/BuildStage3.cs --entry BuildStage3.Run
public static class BuildStage3
{
    const string MainPath = "Assets/00_Scenes/Main.unity";
    const string RabbitPrefabPath = "Assets/03_Prefabs/Stage3/FogRabbit.prefab";
    const string RabbitModelPath = "Assets/01_Arts/Models/Rabbits/BabyRabbit-Animated.fbx";
    const string RabbitControllerPath = "Assets/01_Arts/Models/Rabbits/BabyRabbit.controller";
    const string FogMaterialPath = "Assets/01_Arts/Materials/Fog.mat";
    const string SunlitSpotPrefabPath = "Assets/03_Prefabs/SunlitSpot.prefab";
    static readonly string[] TreePrefabPaths =
    {
        "Assets/03_Prefabs/Trees/Tree_1_Bare.prefab", "Assets/03_Prefabs/Trees/Tree_2_Bare.prefab", "Assets/03_Prefabs/Trees/Tree_3_Bare.prefab",
    };

    // Light fog: the rabbits and the paths show through it. Many thin layers up to about 2.6 m, so it reads as fog, not a ground sheet.
    const float FogDensity = 0.22f;
    const int FogLayers = 7;
    const float FogLayerStep = 0.4f;
    // Every path is lined on both sides with a row of trees this far from its middle, so the forks read clearly.
    const float EdgeOffset = 2.2f;
    const float EdgeStep = 1.5f;
    // The forest between the paths keeps this far from the middle of a path, so the edge rows stand out.
    const float PathClearance = 3.8f;
    const float MazeTreeSpacing = 3.2f;
    // The girl turns back this far before a wall.
    const float WallStop = 1f;
    const float StageEndZ = 88f;

    // The sketch's corners below are stretched from the fog edge by these, so the paths lie further apart and read more clearly.
    // The goal stays inside the terrain, which ends at z 91.6.
    const float StretchX = 1.4f;
    const float StretchZ = 1.22f;

    static readonly Vector3 Entry = new(0f, 0f, 38.5f);  // stage 2's sunlit spot, where the girl wakes
    static readonly Vector3 Edge = new(0f, 0f, 40.5f);   // the fog edge
    // Path corners (x, z), from the sketch.
    static readonly Vector3 J1 = P(-0.35f, 50.23f), J1a = P(-2.64f, 52.26f), J2 = P(-2.47f, 59.87f), J3 = P(7.23f, 64.52f),
        J4 = P(2.19f, 69.05f), Goal = P(0f, 77.09f);
    static readonly Vector3[][] DeadEnds =
    {
        new[] { J1, P(2.82f, 51.96f), P(3.97f, 56.3f) },
        new[] { J2, P(-4.08f, 60.88f), P(-6.49f, 59.94f), P(-7.66f, 56.54f) },
        new[] { J3, P(11.08f, 71.06f) },
        new[] { J4, P(-3.29f, 70.75f) },
    };
    static readonly Vector3[][] RightWays =
    {
        new[] { J1, J1a, J2 },
        new[] { J2, J3 },
        new[] { J3, J4 },
        new[] { J4, Goal },
    };

    static Vector3 P(float x, float z) => new(x * StretchX, 0f, Edge.z + (z - Edge.z) * StretchZ);

    public static string Run()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (Application.isPlaying || scene.path != MainPath)
            throw new InvalidOperationException("Open Main in Edit Mode.");
        Transform stages = GameObject.Find("Stage2").transform.parent;
        GameObject old = stages.Find("Stage3")?.gameObject;
        if (old != null)
            UnityEngine.Object.DestroyImmediate(old);

        GameObject root = Child(stages.gameObject, "Stage3", Vector3.zero);
        var stage = root.AddComponent<Stage>();
        stage.babyStart = Child(root, "BabyStart", Entry).transform;
        stage.birdStart = Child(root, "BirdStart", Entry + new Vector3(1.5f, 2.5f, -1f)).transform;
        stage.birdAltitude = 4f;
        stage.area = new Bounds(new Vector3(0f, 0f, (41f + StageEndZ) / 2f), new Vector3(39f, 1f, StageEndZ - 41f));

        GameObject mazeRoot = Child(root, "FogMaze", Edge);
        FogField field = Fog(mazeRoot);
        GameObject approach = Path(mazeRoot, "Approach", new[] { J1 });
        GameObject rabbitPrefab = RabbitPrefab();
        var forks = new FogFork[RightWays.Length];
        for (int i = 0; i < forks.Length; i++)
            forks[i] = Fork(mazeRoot, i, rabbitPrefab);
        var maze = mazeRoot.AddComponent<FogMaze>();
        Set(maze, so =>
        {
            so.FindProperty("field").objectReferenceValue = field;
            so.FindProperty("approach").objectReferenceValue = approach.transform;
            Objects(so.FindProperty("forks"), forks);
        });

        GameObject route = Path(root, "BabyRoute", new[] { Edge, Goal });
        stage.route = route.AddComponent<BabyRoute>();
        var stop = route.transform.GetChild(0).gameObject.AddComponent<StopPoint>();
        Set(stop, so => so.FindProperty("blocker").objectReferenceValue = maze);
        var sun = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(SunlitSpotPrefabPath), root.transform);
        sun.name = "SunlitSpot";
        sun.transform.position = Goal;

        int mazeTrees = Trees(root);
        string ahead = MoveTreesAhead();
        TerrainTrees(root);

        var flow = UnityEngine.Object.FindAnyObjectByType<GameFlow>();
        Set(flow, so =>
        {
            SerializedProperty list = so.FindProperty("stages");
            var current = Enumerable.Range(0, list.arraySize).Select(i => (Stage)list.GetArrayElementAtIndex(i).objectReferenceValue)
                .Where(s => s != null).ToList();
            current.Add(stage);
            Objects(list, current.ToArray());
        });
        EditorUtility.SetDirty(stage);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return $"Stage 3: 4 forks, {mazeTrees} maze trees, trees ahead {ahead}.";
    }

    static FogField Fog(GameObject parent)
    {
        var center = new Vector3(2.5f, 0f, 63.5f);
        var size = new Vector2(36f, 50f);
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
            Objects(so.FindProperty("layers"), layers);
        });
        return field;
    }

    static FogFork Fork(GameObject parent, int index, GameObject rabbitPrefab)
    {
        Vector3[] right = RightWays[index];
        Vector3[] dead = DeadEnds[index].ToArray();
        GameObject root = Child(parent, $"Fork_{index + 1}", right[0]);
        GameObject rightWay = Path(root, "RightWay", right.Skip(1).ToArray());
        // The last dead-end point is the wall; the girl stops short of it.
        Vector3 wall = dead[^1];
        Vector3 back = (dead[^2] - wall).normalized;
        dead[^1] = wall + back * WallStop;
        GameObject deadEnd = Path(root, "DeadEnd", dead.Skip(1).ToArray());
        WallTrees(root, wall, back, index);

        // Beside the fork, on the far side from the dead end, facing the right way.
        Vector3 along = (right[1] - right[0]).normalized;
        Vector3 side = Vector3.Cross(Vector3.up, along);
        if (Vector3.Dot(side, (dead[1] - dead[0]).normalized) > 0f)
            side = -side;
        var rabbit = (GameObject)PrefabUtility.InstantiatePrefab(rabbitPrefab, root.transform);
        rabbit.name = "Rabbit";
        rabbit.transform.SetPositionAndRotation(right[0] + along * 0.8f + side * 0.9f, Quaternion.LookRotation(along));
        GameObject run = Child(root, "RabbitRun", rabbit.transform.position);
        var runPoints = new[] { 2.5f, 5f }.Select((d, i) => Child(run, $"Point_{i}", PointAlong(right, d) + side * 0.6f).transform).ToArray();
        Set(rabbit.GetComponent<FogRabbit>(), so => Objects(so.FindProperty("runPoints"), runPoints));

        var fork = root.AddComponent<FogFork>();
        Set(fork, so =>
        {
            so.FindProperty("rabbit").objectReferenceValue = rabbit.GetComponent<FogRabbit>();
            so.FindProperty("rightWay").objectReferenceValue = rightWay.transform;
            so.FindProperty("deadEnd").objectReferenceValue = deadEnd.transform;
        });
        return fork;
    }

    // The wall closing a dead end: five trees close together across its end. Trees for now; maybe rocks later (ADR 0027).
    static void WallTrees(GameObject parent, Vector3 wall, Vector3 back, int index)
    {
        GameObject root = Child(parent, "Wall", wall);
        Vector3 across = Vector3.Cross(Vector3.up, back);
        GameObject[] prefabs = TreePrefabs();
        for (int i = -2; i <= 2; i++)
        {
            var tree = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[(index + i + 6) % prefabs.Length], root.transform);
            tree.transform.SetPositionAndRotation(wall + across * (0.9f * i) + back * (0.25f * Mathf.Abs(i)), Quaternion.Euler(0f, 90f * (index + i), 0f));
        }
    }

    static Vector3 PointAlong(Vector3[] path, float distance)
    {
        for (int i = 1; i < path.Length; i++)
        {
            float length = Vector3.Distance(path[i - 1], path[i]);
            if (distance <= length)
                return Vector3.Lerp(path[i - 1], path[i], distance / length);
            distance -= length;
        }
        return path[^1];
    }

    // The forest between the paths. Every path is lined on both sides with a row of trees; behind the rows a looser jittered grid
    // fills the rest, kept off the paths and away from the walls and the sunlit spot.
    static int Trees(GameObject parent)
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
            AddPath(list, new[] { Entry, Edge, J1 });
            foreach (Vector3[] path in RightWays)
                AddPath(list, path);
        }
        foreach (Vector3[] path in DeadEnds)
        {
            Vector3 wall = path[^1];
            AddPath(walked, path.Take(path.Length - 1).Append(wall + (path[^2] - wall).normalized * WallStop).ToArray());
            AddPath(lined, path);
        }
        var walls = DeadEnds.Select(path => path[^1]).ToArray();
        // The wall trees stand about 1.8 m to either side of a wall point.
        var placed = new List<Vector3>(walls);

        GameObject[] prefabs = TreePrefabs();
        var random = new System.Random(27);
        bool Free(Vector3 position, float clearance, float gap) =>
            !walked.Any(s => DistanceToSegment(position, s.Item1, s.Item2) < clearance)
            && !walls.Any(w => Vector3.Distance(position, w) < 1.6f)
            && Vector3.Distance(position, Goal) >= 4f
            && !placed.Any(p => Vector3.Distance(position, p) < gap);
        void Plant(Vector3 position)
        {
            int kind = random.Next(prefabs.Length);
            var tree = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[kind], root.transform);
            tree.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f));
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
        // Forks 3 and 4 get a wedge of trees between their two ways, so the split shows (user feedback).
        foreach (int fork in new[] { 2, 3 })
        {
            Vector3 origin = RightWays[fork][0];
            Vector3 toRight = (RightWays[fork][1] - origin).normalized;
            Vector3 toDead = (DeadEnds[fork][1] - origin).normalized;
            Vector3 bisector = (toRight + toDead).normalized;
            Vector3 across = Vector3.Cross(Vector3.up, bisector);
            for (float d = 1f; d <= 7f; d += 1.1f)
                foreach (float sideways in new[] { 0f, -0.9f, 0.9f })
                {
                    Vector3 position = origin + bisector * d + across * sideways;
                    if (Free(position, EdgeOffset - 0.3f, 0.9f))
                        Plant(position);
                }
        }
        for (float x = -17f; x <= 19f; x += MazeTreeSpacing)
            for (float z = 41f; z <= StageEndZ; z += MazeTreeSpacing)
            {
                Vector3 position = new Vector3(x, 0f, z) + Jitter(random, MazeTreeSpacing * 0.3f);
                if (Free(position, PathClearance, 1.2f))
                    Plant(position);
            }
        return placed.Count - walls.Length;
    }

    static Vector3 Jitter(System.Random random, float amount) =>
        new(((float)random.NextDouble() * 2f - 1f) * amount, 0f, ((float)random.NextDouble() * 2f - 1f) * amount);

    static float DistanceToSegment(Vector3 point, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float t = Mathf.Clamp01(Vector3.Dot(point - a, ab) / ab.sqrMagnitude);
        return Vector3.Distance(point, a + ab * t);
    }

    // The forest past the last stage now starts past stage 3.
    static string MoveTreesAhead()
    {
        var parts = new List<string>();
        foreach (TreeGenerator generator in GameObject.Find("TreesAhead").GetComponentsInChildren<TreeGenerator>())
        {
            Set(generator, so =>
            {
                SerializedProperty areas = so.FindProperty("areas");
                for (int i = 0; i < areas.arraySize; i++)
                {
                    SerializedProperty min = areas.GetArrayElementAtIndex(i).FindPropertyRelative("min");
                    min.vector2Value = new Vector2(min.vector2Value.x, StageEndZ);
                }
            });
            generator.Generate();
            parts.Add($"{generator.name} {generator.TreeCount}");
        }
        return string.Join(", ", parts);
    }

    // The sparse forest on either side of the maze, as stage 2 has.
    static void TerrainTrees(GameObject parent)
    {
        TreeGenerator stage2 = GameObject.Find("Stage2/TerrainTrees").GetComponent<TreeGenerator>();
        GameObject root = Child(parent, "TerrainTrees", Vector3.zero);
        var generator = root.AddComponent<TreeGenerator>();
        var source = new SerializedObject(stage2);
        Set(generator, so =>
        {
            so.CopyFromSerializedProperty(source.FindProperty("prefabs"));
            so.CopyFromSerializedProperty(source.FindProperty("ground"));
            so.CopyFromSerializedProperty(source.FindProperty("spacing"));
            so.FindProperty("seed").intValue = 3;
            SerializedProperty areas = so.FindProperty("areas");
            areas.arraySize = 2;
            areas.GetArrayElementAtIndex(0).FindPropertyRelative("min").vector2Value = new Vector2(-60.48f, 41f);
            areas.GetArrayElementAtIndex(0).FindPropertyRelative("max").vector2Value = new Vector2(-19f, StageEndZ);
            areas.GetArrayElementAtIndex(1).FindPropertyRelative("min").vector2Value = new Vector2(21f, 41f);
            areas.GetArrayElementAtIndex(1).FindPropertyRelative("max").vector2Value = new Vector2(32.4f, StageEndZ);
        });
        generator.Generate();
    }

    // A prefab, as there are four: the baby rabbit model, sitting, with the fork's cry reaction.
    static GameObject RabbitPrefab()
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(RabbitPrefabPath);
        if (existing != null)
            return existing;
        if (!AssetDatabase.IsValidFolder("Assets/03_Prefabs/Stage3"))
            AssetDatabase.CreateFolder("Assets/03_Prefabs", "Stage3");
        var root = new GameObject("FogRabbit");
        var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(RabbitModelPath), root.transform);
        var animator = model.GetComponent<Animator>();
        animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(RabbitControllerPath);
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        var rabbit = root.AddComponent<FogRabbit>();
        Set(rabbit, so => so.FindProperty("animator").objectReferenceValue = animator);
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, RabbitPrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    static GameObject[] TreePrefabs() => TreePrefabPaths.Select(AssetDatabase.LoadAssetAtPath<GameObject>).ToArray();

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
