using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FMODUnity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Builds the easter egg prefabs (Assets/03_Prefabs/EasterEggs/) and, with Main open, a clearing behind the intro's start (-Z) with one of
// each in it, for trying them before they get their places in the stages (docs/kor/easter_eggs.md). The models are placeholders where
// there is none yet (stump, snow piles, cliff). The puddle reflection is left out for now.
// In Main: the intro's area reaches back over the clearing, the world forest keeps out of it (a Clear zone, and its bottom padding only
// one row deep), and a second tree generator fills the ground behind it that the camera now sees. The snow cap goes under the bird.
// Run BuildEncyclopedia first: the eggs find their entries in Assets/04_Data/Encyclopedia.
// unity command script run --file AgentScripts/BuildEasterEggs.cs --entry BuildEasterEggs.Run
// Prefabs only (the open scene is left alone): --entry BuildEasterEggs.BuildPrefabs
public static class BuildEasterEggs
{
    const string Folder = "Assets/03_Prefabs/EasterEggs";
    const string EntryFolder = "Assets/04_Data/Encyclopedia";
    const string MainScene = "Assets/00_Scenes/Main.unity";
    const string Interactable = "Interactable";

    const string IcyTreeModel = "Assets/01_Arts/Mesh/icy_tree/icy tree.fbx";
    const string HollowTreeModel = "Assets/01_Arts/Mesh/thickTree/thickTree.obj";
    const string CherryBushModel = "Assets/01_Arts/Mesh/cornelian_cherry/berries/Tube.obj";
    const string CherryModel = "Assets/01_Arts/Mesh/cornelian_cherry/tree_base/UV Sphere 2.obj";
    const string LadybugPrefab = "Assets/03_Prefabs/Ladybug.prefab";
    const string SquirrelPrefab = "Assets/03_Prefabs/Squirrel.prefab";
    const string SnowbankPrefab = "Assets/03_Prefabs/Stage2/Snowbank.prefab";
    const string FlowerPrefab = "Assets/03_Prefabs/Flowers/Flower01.prefab";
    // The small, faint one the click eggs share (BuildEggSparkles).
    const string SparklePrefab = "Assets/03_Prefabs/EggSparkle.prefab";
    const string BirdPrefab = "Assets/03_Prefabs/Bird-Animated.prefab";
    const string BirdController = "Assets/01_Arts/Models/Bird/Bird.controller";
    const string ParticleMaterial = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat";
    // The SoftSnow shader's material, the snow on the girl. The snow piles of the whirl and the flower use it.
    const string SoftSnowMaterial = "Assets/01_Arts/Materials/SoftSnow.mat";
    const float CherryScale = 2.5f;
    // Where the bird's beak is at the bottom of a peck, from its feet: ahead and down, in metres.
    const float PeckReach = 0.5f, PeckDrop = 0.12f;
    // How far below an icicle's top the beak taps it, in metres.
    const float IcicleTapBelowTop = 0.15f;
    const string RockMaterial = "Assets/01_Arts/Materials/Test/Rock.mat";
    const string TrunkMaterial = "Assets/01_Arts/Materials/Test/Trunk.mat";

    // The clearing, in world X and Z. The bird reaches back to ClearingBirdZ (the intro's area), so every perch is in front of it.
    static readonly Rect Clearing = Rect.MinMaxRect(-14f, -36.5f, 14f, -24.5f);
    const float ClearingBirdZ = -35f;
    // The world forest's bottom padding: one row of its trees behind the clearing (its grid starts 9.5 m in from the terrain's edge).
    const float ForestBottomPadding = 1.5f;
    // The strip behind the clearing out to the terrain's edge, which the camera sees zoomed out with the bird at the back.
    static readonly Rect BackTrees = Rect.MinMaxRect(-25f, -48.2f, 35f, -38.6f);

    static int interactableLayer;

    public static string Run()
    {
        Dictionary<string, GameObject> prefabs = BuildPrefabs();
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != MainScene)
            return "built the prefabs in " + Folder + "; open " + MainScene + " and run again to place them";
        PlaceInMain(prefabs);
        return EditorSceneManager.SaveScene(scene) ? "built the prefabs and saved " + scene.path : "FAILED to save " + scene.path;
    }

    // Only the prefabs, leaving the open scene alone. The entry for changing a prefab whose instances are already placed.
    public static Dictionary<string, GameObject> BuildPrefabs()
    {
        interactableLayer = LayerMask.NameToLayer(Interactable);
        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets/03_Prefabs", "EasterEggs");
        var prefabs = new Dictionary<string, GameObject>
        {
            ["StumpLadybug"] = StumpLadybug(),
            ["SnowWhirl"] = SnowWhirl(),
            ["SnowFlower"] = SnowFlower(),
            ["IcicleXylophone"] = IcicleXylophone(),
            ["HollowSquirrel"] = HollowSquirrel(),
            ["CornelianCherry"] = CornelianCherry(),
            ["CliffEcho"] = CliffEcho(),
            ["MigratingBirds"] = MigratingBirds(),
        };
        AssetDatabase.SaveAssets();
        return prefabs;
    }

    // ---- Prefabs ----

    static GameObject StumpLadybug()
    {
        var root = new GameObject("StumpLadybug");
        root.AddComponent<ClickCatcher>();
        // Placeholder stump: the broken log's stump once the log has fallen.
        GameObject stump = Primitive(PrimitiveType.Cylinder, "Stump", root.transform, new Vector3(0f, 0.35f, 0f), new Vector3(0.9f, 0.35f, 0.9f), TrunkMaterial, true);
        stump.layer = interactableLayer;
        Perchable perch = AddPerchable(stump, Child(root.transform, "PerchPoint", new Vector3(0f, 0.7f, 0f)));
        var ladybug = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(LadybugPrefab), root.transform);
        ladybug.transform.localScale = Vector3.one * 0.5f;
        ladybug.SetActive(false);
        Transform from = Child(root.transform, "CrawlFrom", new Vector3(0.45f, 0f, -0.1f));
        Transform to = Child(root.transform, "CrawlTo", new Vector3(1.4f, 0f, -0.5f));
        var discovery = AddDiscovery(root, "02_StumpLadybug", ladybug.transform);
        Set(root.AddComponent<StumpLadybug>(), so =>
        {
            so.FindProperty("discovery").objectReferenceValue = discovery;
            so.FindProperty("stump").objectReferenceValue = perch;
            so.FindProperty("ladybug").objectReferenceValue = ladybug.transform;
            so.FindProperty("ladybugAnimator").objectReferenceValue = ladybug.GetComponent<Animator>();
            so.FindProperty("from").objectReferenceValue = from;
            so.FindProperty("to").objectReferenceValue = to;
        });
        return Save(root);
    }

    static GameObject SnowWhirl()
    {
        var root = new GameObject("SnowWhirl");
        root.AddComponent<ClickCatcher>();
        GameObject pile = Snowbank(root.transform, "SnowPile", new Vector3(2.2f, 0.6f, 2.2f));
        Perchable perch = AddPerchable(ClickSphere(root.transform, new Vector3(0f, 0.15f, 0f), 1.2f), Child(root.transform, "PerchPoint", new Vector3(0f, 0.3f, 0f)));
        // Clicked, the bird goes on to the whirl even if the player clicks elsewhere on the way.
        Set(perch, so => so.FindProperty("locksClicks").boolValue = true);
        Transform centre = Child(root.transform, "CircleCentre", new Vector3(0f, 1f, 0f));
        ParticleSystem whirl = Whirl(root.transform);
        var discovery = AddDiscovery(root, "03_SnowWhirl", pile.transform);
        SnowWhirl egg = root.AddComponent<SnowWhirl>();
        Set(egg, so =>
        {
            so.FindProperty("discovery").objectReferenceValue = discovery;
            so.FindProperty("pile").objectReferenceValue = perch;
            so.FindProperty("whirl").objectReferenceValue = whirl;
            so.FindProperty("circleCentre").objectReferenceValue = centre;
        });
        SetSound(egg, "sound", "event:/SFX/spiral");
        return Save(root);
    }

    static GameObject SnowFlower()
    {
        var root = new GameObject("SnowFlower");
        root.AddComponent<ClickCatcher>();
        GameObject snow = Snowbank(root.transform, "GlowingSnow", new Vector3(0.9f, 0.35f, 0.9f));
        var sparkle = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(SparklePrefab), root.transform);
        sparkle.transform.localPosition = new Vector3(0f, 0.2f, 0f);
        Perchable perch = AddPerchable(ClickSphere(root.transform, new Vector3(0f, 0.1f, 0f), 0.6f), Child(root.transform, "PerchPoint", new Vector3(0f, 0.18f, 0f)));
        var flower = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(FlowerPrefab), root.transform);
        flower.SetActive(false);
        var discovery = AddDiscovery(root, "04_SnowFlower", flower.transform);
        Set(root.AddComponent<SnowFlower>(), so =>
        {
            so.FindProperty("discovery").objectReferenceValue = discovery;
            so.FindProperty("glowingSnow").objectReferenceValue = perch;
            so.FindProperty("snow").objectReferenceValue = snow.transform;
            so.FindProperty("glow").objectReferenceValue = sparkle.GetComponent<ParticleSystem>();
            so.FindProperty("flower").objectReferenceValue = flower.transform;
        });
        return Save(root);
    }

    static GameObject IcicleXylophone()
    {
        var root = new GameObject("IcicleXylophone");
        root.AddComponent<ClickCatcher>();
        root.transform.localScale = Vector3.one * 1.6f;
        var tree = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(IcyTreeModel), root.transform);
        // Longest is lowest: do re mi fa sol la ti do.
        int[] semitones = { 0, 2, 4, 5, 7, 9, 11, 12 };
        var icicles = tree.GetComponentsInChildren<MeshFilter>().Where(f => f.name.StartsWith("Cylinder"))
            .OrderByDescending(f => f.sharedMesh.bounds.size.y).ToArray();
        if (icicles.Length != semitones.Length)
            throw new Exception($"BuildEasterEggs: {IcyTreeModel} has {icicles.Length} icicles, not {semitones.Length}");
        // The trunk's axis, in the root's space: the middle of the foot of the tree.
        Vector3[] treePoints = tree.GetComponentsInChildren<MeshFilter>().Where(f => !icicles.Contains(f))
            .SelectMany(f => f.sharedMesh.vertices.Select(v => (root.transform.worldToLocalMatrix * f.transform.localToWorldMatrix).MultiplyPoint3x4(v))).ToArray();
        float treeBottom = treePoints.Min(p => p.y), treeHeight = treePoints.Max(p => p.y) - treeBottom;
        Vector3[] foot = treePoints.Where(p => p.y < treeBottom + treeHeight * 0.1f).ToArray();
        var axis = new Vector3(foot.Average(p => p.x), 0f, foot.Average(p => p.z));
        float scale = root.transform.localScale.x;
        var colliders = new List<Collider>();
        var perches = new List<Perchable>();
        foreach (MeshFilter icicle in icicles)
        {
            icicle.gameObject.layer = interactableLayer;
            var box = icicle.gameObject.AddComponent<BoxCollider>();
            Bounds b = icicle.sharedMesh.bounds;
            box.center = b.center;
            // Wider than the icicle, so it is easy to click.
            box.size = new Vector3(Mathf.Max(b.size.x, 0.3f), b.size.y, Mathf.Max(b.size.z, 0.3f));
            colliders.Add(box);
            // Where the bird stands to tap it: on the branch it hangs from, facing along it toward the icicle, so at the bottom of a peck
            // the beak is a little below the icicle's top. In toward the trunk, unless the icicle is so near the trunk that the bird
            // would stand in it: then out along the branch, facing in.
            Matrix4x4 toRoot = root.transform.worldToLocalMatrix * icicle.transform.localToWorldMatrix;
            Vector3[] corners = Corners(b).Select(c => toRoot.MultiplyPoint3x4(c)).ToArray();
            var top = new Vector3(corners.Average(c => c.x), corners.Max(c => c.y), corners.Average(c => c.z));
            Vector3 fromTrunk = Vector3.ProjectOnPlane(top - axis, Vector3.up);
            Vector3 along = BranchDirection(treePoints, top, scale);
            if (along == Vector3.zero)
                along = fromTrunk.normalized;
            if (Vector3.Dot(along, fromTrunk) < 0f)
                along = -along;
            Vector3 facing = fromTrunk.magnitude * scale > PeckReach + 0.3f ? along : -along;
            Vector3 beak = top + Vector3.down * (IcicleTapBelowTop / scale);
            Transform tapFrom = Child(root.transform, "TapFrom", beak - facing * (PeckReach / scale) + Vector3.up * (PeckDrop / scale));
            tapFrom.localRotation = Quaternion.LookRotation(facing);
            Perchable perch = AddPerchable(icicle.gameObject, tapFrom);
            Set(perch, so =>
            {
                so.FindProperty("alignBird").boolValue = true;
                so.FindProperty("faceForward").boolValue = true;
            });
            perches.Add(perch);
        }
        var discovery = AddDiscovery(root, "05_IcicleXylophone", tree.transform);
        IcicleXylophone egg = root.AddComponent<IcicleXylophone>();
        Set(egg, so =>
        {
            so.FindProperty("discovery").objectReferenceValue = discovery;
            SerializedProperty list = so.FindProperty("icicles");
            list.arraySize = colliders.Count;
            for (int i = 0; i < colliders.Count; i++)
            {
                list.GetArrayElementAtIndex(i).FindPropertyRelative("body").objectReferenceValue = colliders[i];
                list.GetArrayElementAtIndex(i).FindPropertyRelative("perch").objectReferenceValue = perches[i];
                list.GetArrayElementAtIndex(i).FindPropertyRelative("semitone").intValue = semitones[i];
            }
        });
        SetSound(egg, "note", "event:/SFX/highpitch_0");
        return Save(root);
    }

    static GameObject HollowSquirrel()
    {
        var root = new GameObject("HollowSquirrel");
        root.AddComponent<ClickCatcher>();
        root.transform.localScale = Vector3.one * 2f;
        var tree = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(HollowTreeModel), root.transform);
        // The hollow is on the model's +Z, about 0.6 m up the trunk. Turned around, it faces the prefab's -Z.
        var back = Quaternion.Euler(0f, 180f, 0f);
        tree.transform.localRotation = back;
        // The squirrel's feet: hidden below the hollow's lip, and sitting on it.
        Transform inside = Child(root.transform, "Inside", back * new Vector3(-0.02f, 0.3f, 0.12f));
        Transform peek = Child(root.transform, "Peek", back * new Vector3(-0.02f, 0.55f, 0.35f));
        inside.localRotation = peek.localRotation = back;
        var squirrel = new GameObject("Squirrel");
        squirrel.transform.SetParent(root.transform, false);
        squirrel.transform.SetLocalPositionAndRotation(inside.localPosition, inside.localRotation);
        var model = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(SquirrelPrefab), squirrel.transform);
        model.transform.localScale = Vector3.one * 0.5f;
        var discovery = AddDiscovery(root, "06_HollowSquirrel", squirrel.transform);
        Set(root.AddComponent<HollowSquirrel>(), so =>
        {
            so.FindProperty("discovery").objectReferenceValue = discovery;
            so.FindProperty("squirrel").objectReferenceValue = squirrel.transform;
            so.FindProperty("animator").objectReferenceValue = model.GetComponent<Animator>();
            so.FindProperty("inside").objectReferenceValue = inside;
            so.FindProperty("peek").objectReferenceValue = peek;
        });
        return Save(root);
    }

    static GameObject CornelianCherry()
    {
        var root = new GameObject("CornelianCherry");
        root.AddComponent<ClickCatcher>();
        root.transform.localScale = Vector3.one * CherryScale;
        var bush = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(CherryBushModel), root.transform);
        // The cluster model sits where it does on the bush in the model's space; each cluster is moved by a parent under a branch end.
        var clusterModel = Load<GameObject>(CherryModel);
        Mesh clusterMesh = clusterModel.GetComponentInChildren<MeshFilter>().sharedMesh;
        Vector3 clusterOffset = clusterMesh.bounds.center;
        float clusterTop = clusterMesh.bounds.extents.y * 0.5f;
        // The top of its stem, from its middle: on the model it touches the bottom of its branch.
        Vector3 stem = clusterMesh.vertices.OrderByDescending(v => v.y).First() - clusterOffset;
        var berries = new List<(GameObject cluster, Transform eatFrom)>();
        // Each cluster turned so its berries hang out along its branch, give or take a little, not all the same way round.
        float[] turns = { 0f, 35f, -30f, 20f, -40f, 10f };
        Vector3 hang = Vector3.ProjectOnPlane(-stem, Vector3.up);
        int index = 0;
        foreach ((Vector3 under, Vector3 outward) in BranchEnds(bush, 6))
        {
            Quaternion turn = Quaternion.AngleAxis(turns[index++ % turns.Length], Vector3.up) * Quaternion.FromToRotation(hang, outward);
            // Turned about the top of its stem, a little up into the branch so no gap shows where the stem meets it.
            Vector3 spot = under - turn * stem + Vector3.up * 0.01f;
            var cluster = new GameObject("Berry");
            cluster.transform.SetParent(root.transform, false);
            cluster.transform.SetLocalPositionAndRotation(spot, turn);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(clusterModel, cluster.transform);
            model.transform.localPosition = -clusterOffset;
            // Back along the branch, facing the cluster: at the bottom of a peck the beak is on the cluster's top.
            Vector3 beak = spot + Vector3.up * clusterTop;
            Transform eatFrom = Child(cluster.transform, "EatFrom", Vector3.zero);
            eatFrom.SetPositionAndRotation(root.transform.TransformPoint(beak - outward * (PeckReach / CherryScale) + Vector3.up * (PeckDrop / CherryScale)),
                root.transform.rotation * Quaternion.LookRotation(outward));
            berries.Add((cluster, eatFrom));
        }
        if (berries.Count == 0)
        {
            Object.DestroyImmediate(root);
            throw new Exception($"BuildEasterEggs: found no branch ends on {CherryBushModel}");
        }
        Bounds bounds = LocalBounds(bush);
        Transform perchPoint = Child(root.transform, "PerchPoint", Vector3.zero);
        perchPoint.SetPositionAndRotation(berries[0].eatFrom.position, berries[0].eatFrom.rotation);
        Perchable perch = AddPerchable(ClickSphere(root.transform, bounds.center, bounds.extents.y), perchPoint);
        Set(perch, so =>
        {
            so.FindProperty("alignBird").boolValue = true;
            so.FindProperty("faceForward").boolValue = true;
        });
        var discovery = AddDiscovery(root, "07_CornelianCherry", bush.transform);
        Set(root.AddComponent<CornelianCherry>(), so =>
        {
            so.FindProperty("discovery").objectReferenceValue = discovery;
            so.FindProperty("bush").objectReferenceValue = perch;
            SerializedProperty list = so.FindProperty("berries");
            list.arraySize = berries.Count;
            for (int i = 0; i < berries.Count; i++)
            {
                list.GetArrayElementAtIndex(i).FindPropertyRelative("cluster").objectReferenceValue = berries[i].cluster;
                list.GetArrayElementAtIndex(i).FindPropertyRelative("eatFrom").objectReferenceValue = berries[i].eatFrom;
            }
        });
        return Save(root);
    }

    static GameObject CliffEcho()
    {
        var root = new GameObject("CliffEcho");
        root.AddComponent<ClickCatcher>();
        // Placeholder for the cliff's edge: a low rock. The echo comes back from far out beyond it.
        GameObject edge = Primitive(PrimitiveType.Cube, "CliffEdge", root.transform, new Vector3(0f, 0.25f, 0f), new Vector3(3f, 0.5f, 1.5f), RockMaterial);
        Transform source = Child(root.transform, "EchoSource", new Vector3(0f, 0f, -20f));
        var discovery = AddDiscovery(root, "08_CliffEcho", edge.transform);
        CliffEcho egg = root.AddComponent<CliffEcho>();
        Set(egg, so =>
        {
            so.FindProperty("discovery").objectReferenceValue = discovery;
            so.FindProperty("source").objectReferenceValue = source;
        });
        SetSound(egg, "cry", "event:/SFX/Bird");
        return Save(root);
    }

    static GameObject MigratingBirds()
    {
        var root = new GameObject("MigratingBirds");
        var flock = new GameObject("Flock");
        flock.transform.SetParent(root.transform, false);
        var controller = Load<RuntimeAnimatorController>(BirdController);
        // A V, the leader ahead (+Z, the way it flies). The model faces -Z, so each bird is turned around.
        Vector3[] places = { Vector3.zero, new(-1.6f, 0.1f, -1.4f), new(1.6f, -0.1f, -1.4f), new(-3.2f, 0.2f, -2.8f), new(3.2f, 0f, -2.8f) };
        foreach (Vector3 place in places)
        {
            var bird = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(BirdPrefab), flock.transform);
            bird.transform.SetLocalPositionAndRotation(place, Quaternion.Euler(0f, 180f, 0f));
            bird.transform.localScale = Vector3.one * 0.4f;
            bird.GetComponent<Animator>().runtimeAnimatorController = controller;
        }
        flock.SetActive(false);
        var discovery = AddDiscovery(root, "10_MigratingBirds", flock.transform);
        Set(root.AddComponent<MigratingBirds>(), so =>
        {
            so.FindProperty("discovery").objectReferenceValue = discovery;
            so.FindProperty("flock").objectReferenceValue = flock.transform;
        });
        return Save(root);
    }

    // ---- Main ----

    static void PlaceInMain(Dictionary<string, GameObject> prefabs)
    {
        Stage intro = Object.FindObjectsByType<Stage>().First(s => s.name == "Intro");
        Undo.RecordObject(intro, "Easter egg clearing");
        Bounds area = intro.area;
        area.SetMinMax(new Vector3(area.min.x, area.min.y, ClearingBirdZ), area.max);
        intro.area = area;
        EditorUtility.SetDirty(intro);

        Terrain terrain = Terrain.activeTerrain;
        Transform old = intro.transform.Find("EasterEggs");
        if (old != null)
            Object.DestroyImmediate(old.gameObject);
        var eggs = new GameObject("EasterEggs").transform;
        eggs.SetParent(intro.transform, false);
        // Tall things toward the top of the screen (-X, +Z): the camera looks from +X, -Z.
        Place(prefabs["IcicleXylophone"], eggs, terrain, -11f, -27.5f, 0f);
        Place(prefabs["HollowSquirrel"], eggs, terrain, -11f, -33f, -45f);
        Place(prefabs["CornelianCherry"], eggs, terrain, -5f, -30.5f, 0f);
        Place(prefabs["SnowWhirl"], eggs, terrain, 0f, -27.5f, 0f);
        Place(prefabs["SnowFlower"], eggs, terrain, 0f, -33f, 0f);
        Place(prefabs["StumpLadybug"], eggs, terrain, 5.5f, -28f, 0f);
        Place(prefabs["CliffEcho"], eggs, terrain, 11f, -33f, 135f);
        GameObject birds = Place(prefabs["MigratingBirds"], eggs, terrain, 11f, -27.5f, 0f);
        // For trying it out here: it may fly in the intro too.
        Set(birds.GetComponent<MigratingBirds>(), so => so.FindProperty("fromStage").intValue = 0);

        RegrowForest(terrain);
    }

    static GameObject Place(GameObject prefab, Transform parent, Terrain terrain, float x, float z, float yaw)
    {
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        var position = new Vector3(x, 0f, z);
        position.y = terrain.transform.position.y + terrain.SampleHeight(position);
        instance.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        return instance;
    }

    static void RegrowForest(Terrain terrain)
    {
        var forest = GameObject.Find("01_SYSTEM/TreeGenerator").GetComponent<TreeGenerator>();
        Set(forest, so => so.FindProperty("bottomPadding").floatValue = ForestBottomPadding);
        Zone(forest.transform, "EasterEgg Clearing", TreeZone.Kind.Clear, Clearing);

        Transform system = GameObject.Find("01_SYSTEM").transform;
        Transform oldBack = system.Find("TreeGenerator_ClearingBack");
        if (oldBack != null)
        {
            oldBack.GetComponent<TreeGenerator>().Clear();
            Object.DestroyImmediate(oldBack.gameObject);
        }
        var backObject = new GameObject("TreeGenerator_ClearingBack");
        backObject.transform.SetParent(system, false);
        var back = backObject.AddComponent<TreeGenerator>();
        var from = new SerializedObject(forest);
        Set(back, so =>
        {
            foreach (string field in new[] { "bareTrees", "leafyTrees", "pines", "pineShare", "oakShareByLevel", "oakColour", "birchLeafBrightness",
                         "leafShareByLevel", "spacing", "jitter", "scaleRange", "randomRotation", "paint" })
                so.CopyFromSerializedProperty(from.FindProperty(field));
            so.FindProperty("ground").objectReferenceValue = terrain;
            so.FindProperty("coverGround").boolValue = false;
            so.FindProperty("seed").intValue = 7;
        });
        Zone(backObject.transform, "Behind The Clearing", TreeZone.Kind.Trees, BackTrees);

        forest.Generate();
        back.Generate();
    }

    static void Zone(Transform generator, string name, TreeZone.Kind kind, Rect rect)
    {
        Transform old = generator.Find(name);
        if (old != null)
            Object.DestroyImmediate(old.gameObject);
        var zone = new GameObject(name);
        zone.transform.SetParent(generator, false);
        zone.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        var box = zone.AddComponent<BoxCollider>();
        box.center = new Vector3(rect.center.x, 0f, rect.center.y);
        box.size = new Vector3(rect.width, 1f, rect.height);
        zone.AddComponent<TreeZone>().kind = kind;
    }

    // ---- Helpers ----

    static Discovery AddDiscovery(GameObject root, string entryFile, Transform subject = null)
    {
        var entry = Load<EncyclopediaEntry>(EntryFolder + "/" + entryFile + ".asset");
        var discovery = root.AddComponent<Discovery>();
        Set(discovery, so =>
        {
            so.FindProperty("entry").objectReferenceValue = entry;
            so.FindProperty("subject").objectReferenceValue = subject;
        });
        return discovery;
    }

    // The bird lands at the point when its collider (on the Interactable layer) is clicked. It stays seen through the camera fade.
    static Perchable AddPerchable(GameObject clickable, Transform point)
    {
        var perch = clickable.AddComponent<Perchable>();
        Set(perch, so =>
        {
            so.FindProperty("perchPoint").objectReferenceValue = point;
            so.FindProperty("alwaysExempt").boolValue = true;
        });
        return perch;
    }

    static GameObject ClickSphere(Transform parent, Vector3 centre, float radius)
    {
        var click = new GameObject("Click");
        click.layer = interactableLayer;
        click.transform.SetParent(parent, false);
        click.transform.localPosition = centre;
        click.AddComponent<SphereCollider>().radius = radius;
        return click;
    }

    static GameObject Snowbank(Transform parent, string name, Vector3 size)
    {
        var bank = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(SnowbankPrefab), parent);
        bank.name = name;
        bank.transform.localScale = size;
        foreach (Renderer renderer in bank.GetComponentsInChildren<Renderer>())
            renderer.sharedMaterial = Load<Material>(SoftSnowMaterial);
        return bank;
    }

    static ParticleSystem Whirl(Transform parent)
    {
        var whirlObject = new GameObject("Whirl");
        whirlObject.transform.SetParent(parent, false);
        var whirl = whirlObject.AddComponent<ParticleSystem>();
        whirl.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = whirl.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = 1f;
        main.startLifetime = 1.6f;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
        main.startColor = new Color(1f, 1f, 1f, 0.85f);
        main.maxParticles = 600;
        ParticleSystem.EmissionModule emission = whirl.emission;
        emission.rateOverTime = 160f;
        ParticleSystem.ShapeModule shape = whirl.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 1.1f;
        shape.rotation = new Vector3(90f, 0f, 0f);
        ParticleSystem.VelocityOverLifetimeModule velocity = whirl.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.orbitalY = 5f;
        velocity.radial = -0.4f;
        velocity.x = velocity.z = 0f;
        velocity.y = 1.4f;
        ParticleSystem.ColorOverLifetimeModule colour = whirl.colorOverLifetime;
        colour.enabled = true;
        var fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
        colour.color = fade;
        whirlObject.GetComponent<ParticleSystemRenderer>().sharedMaterial = Load<Material>(ParticleMaterial);
        return whirl;
    }

    // Up to count branch ends, the ones nearest the game camera's side first, in the root's space (model units): for each, a little in
    // from its tip, the bottom of the branch, where a cluster's stem hangs from (as the model's one cluster does), and the way along the
    // branch out to its tip.
    static List<(Vector3 spot, Vector3 outward)> BranchEnds(GameObject bush, int count)
    {
        var filter = bush.GetComponentInChildren<MeshFilter>();
        Matrix4x4 toRoot = bush.transform.parent.worldToLocalMatrix * filter.transform.localToWorldMatrix;
        Vector3[] points = filter.sharedMesh.vertices.Select(v => toRoot.MultiplyPoint3x4(v)).ToArray();
        Bounds bounds = LocalBounds(bush);
        // The trunk's axis: the middle of its foot.
        Vector3[] foot = points.Where(p => p.y < bounds.min.y + bounds.size.y * 0.1f).ToArray();
        var axis = new Vector3(foot.Average(p => p.x), 0f, foot.Average(p => p.z));
        Vector3 middle = axis + Vector3.up * (bounds.min.y + bounds.size.y * 0.5f);
        // The farthest point each way round the bush, from level to steeply up, is the tip of a branch. The trunk's top is not one.
        var tips = new List<Vector3>();
        for (int angle = 0; angle < 360; angle += 10)
            foreach (float rise in new[] { -0.2f, 0f, 0.3f, 0.6f })
            {
                Vector3 way = Quaternion.Euler(0f, angle, 0f) * new Vector3(Mathf.Cos(rise), Mathf.Sin(rise), 0f);
                Vector3 tip = points.OrderByDescending(p => Vector3.Dot(p - middle, way)).First();
                Vector3 off = tip - axis;
                if (new Vector2(off.x, off.z).magnitude > bounds.size.y * 0.25f && tips.All(t => Vector3.Distance(t, tip) > bounds.size.y * 0.1f))
                    tips.Add(tip);
            }
        var cameraSide = new Vector3(1f, 0f, -1f).normalized;
        var ends = new List<(Vector3 spot, Vector3 outward)>();
        foreach (Vector3 tip in tips.OrderByDescending(t => Vector3.Dot(Vector3.ProjectOnPlane(t - axis, Vector3.up).normalized, cameraSide)))
        {
            float reach = bounds.size.y * 0.12f;
            Vector3[] near = points.Where(p => Vector3.Distance(p, tip) > reach * 0.5f && Vector3.Distance(p, tip) < reach).ToArray();
            if (near.Length == 0)
                continue;
            var branch = new Vector3(near.Average(p => p.x), near.Average(p => p.y), near.Average(p => p.z));
            Vector3 outward = Vector3.ProjectOnPlane(tip - branch, Vector3.up).normalized;
            // The bottom of the branch there: the lowest point of the branch's surface around its middle.
            Vector3[] around = points.Where(p => Vector3.Distance(p, branch) < reach * 0.35f).ToArray();
            if (around.Length == 0)
                continue;
            ends.Add((around.OrderBy(p => p.y).First(), outward));
            if (ends.Count == count)
                break;
        }
        return ends;
    }

    // The level way the branch runs at a point on it: the long axis of the tree's surface near it, at about its height. In the space of
    // the points; scale is how many metres one unit of it is. Zero if there is too little surface there.
    static Vector3 BranchDirection(Vector3[] treePoints, Vector3 at, float scale)
    {
        Vector3[] near = treePoints.Where(p => Mathf.Abs(p.y - at.y) < 0.2f / scale
            && new Vector2(p.x - at.x, p.z - at.z).magnitude < 0.5f / scale).ToArray();
        if (near.Length < 2)
            return Vector3.zero;
        float meanX = near.Average(p => p.x), meanZ = near.Average(p => p.z);
        float xx = 0f, zz = 0f, xz = 0f;
        foreach (Vector3 p in near)
        {
            float dx = p.x - meanX, dz = p.z - meanZ;
            xx += dx * dx;
            zz += dz * dz;
            xz += dx * dz;
        }
        float angle = 0.5f * Mathf.Atan2(2f * xz, xx - zz);
        return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
    }

    static Bounds LocalBounds(GameObject model)
    {
        var bounds = new Bounds();
        bool first = true;
        foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>())
        {
            Bounds mesh = filter.sharedMesh.bounds;
            Matrix4x4 toRoot = model.transform.parent.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            foreach (Vector3 corner in Corners(mesh))
            {
                Vector3 point = toRoot.MultiplyPoint3x4(corner);
                if (first)
                    bounds = new Bounds(point, Vector3.zero);
                else
                    bounds.Encapsulate(point);
                first = false;
            }
        }
        return bounds;
    }

    static IEnumerable<Vector3> Corners(Bounds b)
    {
        for (int i = 0; i < 8; i++)
            yield return new Vector3(i % 2 == 0 ? b.min.x : b.max.x, i / 2 % 2 == 0 ? b.min.y : b.max.y, i / 4 == 0 ? b.min.z : b.max.z);
    }

    // Without its collider unless asked: a stray collider would catch clicks meant for the ground.
    static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale, string material, bool collider = false)
    {
        GameObject shape = GameObject.CreatePrimitive(type);
        shape.name = name;
        shape.transform.SetParent(parent, false);
        shape.transform.localPosition = position;
        shape.transform.localScale = scale;
        shape.GetComponent<Renderer>().sharedMaterial = Load<Material>(material);
        if (!collider)
            Object.DestroyImmediate(shape.GetComponent<Collider>());
        return shape;
    }

    static Transform Child(Transform parent, string name, Vector3 position)
    {
        var child = new GameObject(name).transform;
        child.SetParent(parent, false);
        child.localPosition = position;
        return child;
    }

    static void SetSound(Component component, string field, string path)
    {
        FieldInfo info = component.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
        info.SetValue(component, EventReference.Find(path));
        EditorUtility.SetDirty(component);
    }

    static T Load<T>(string path) where T : Object
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
            throw new Exception("BuildEasterEggs: missing " + path);
        return asset;
    }

    static GameObject Save(GameObject root)
    {
        try
        {
            return PrefabUtility.SaveAsPrefabAsset(root, Folder + "/" + root.name + ".prefab");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    static void Objects(SerializedProperty array, Object[] values)
    {
        array.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    static void Set(Object target, Action<SerializedObject> fill)
    {
        var so = new SerializedObject(target);
        fill(so);
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
