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
    const string SparklePrefab = "Assets/03_Prefabs/InteractiveSparkle.prefab";
    const string BirdPrefab = "Assets/03_Prefabs/Bird-Animated.prefab";
    const string BirdController = "Assets/01_Arts/Models/Bird/Bird.controller";
    const string ParticleMaterial = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat";
    const string SnowMaterial = "Assets/01_Arts/Materials/Test/Snow.mat";
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
        interactableLayer = LayerMask.NameToLayer(Interactable);
        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets/03_Prefabs", "EasterEggs");
        var prefabs = new Dictionary<string, GameObject>
        {
            ["SnowOnBird"] = SnowOnBird(),
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

        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != MainScene)
            return "built the prefabs in " + Folder + "; open " + MainScene + " and run again to place them";
        PlaceInMain(prefabs);
        return EditorSceneManager.SaveScene(scene) ? "built the prefabs and saved " + scene.path : "FAILED to save " + scene.path;
    }

    // ---- Prefabs ----

    static GameObject SnowOnBird()
    {
        var root = new GameObject("SnowOnBird");
        var discovery = AddDiscovery(root, "01_SnowOnBird");
        // On the bird's back. Its root is at the feet.
        GameObject snow = Primitive(PrimitiveType.Sphere, "Snow", root.transform, new Vector3(0f, 0.36f, -0.1f), new Vector3(0.35f, 0.12f, 0.45f), SnowMaterial);
        Set(root.AddComponent<SnowOnBird>(), so =>
        {
            so.FindProperty("discovery").objectReferenceValue = discovery;
            so.FindProperty("snow").objectReferenceValue = snow.transform;
        });
        return Save(root);
    }

    static GameObject StumpLadybug()
    {
        var root = new GameObject("StumpLadybug");
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
        GameObject pile = Snowbank(root.transform, "SnowPile", new Vector3(2.2f, 0.6f, 2.2f));
        Perchable perch = AddPerchable(ClickSphere(root.transform, new Vector3(0f, 0.15f, 0f), 1.2f), Child(root.transform, "PerchPoint", new Vector3(0f, 0.3f, 0f)));
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
        var tree = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(IcyTreeModel), root.transform);
        // The rock the icicles ring on (placeholder), under all eight.
        Primitive(PrimitiveType.Cube, "Rock", root.transform, new Vector3(0.05f, 0.18f, 0.15f), new Vector3(3.8f, 0.36f, 1.1f), RockMaterial, true);
        // Longest is lowest: do re mi fa sol la ti do.
        int[] scale = { 0, 2, 4, 5, 7, 9, 11, 12 };
        var icicles = tree.GetComponentsInChildren<MeshFilter>().Where(f => f.name.StartsWith("Cylinder"))
            .OrderByDescending(f => f.sharedMesh.bounds.size.y).ToArray();
        if (icicles.Length != scale.Length)
            throw new Exception($"BuildEasterEggs: {IcyTreeModel} has {icicles.Length} icicles, not {scale.Length}");
        var colliders = new List<Collider>();
        foreach (MeshFilter icicle in icicles)
        {
            icicle.gameObject.layer = interactableLayer;
            var box = icicle.gameObject.AddComponent<BoxCollider>();
            Bounds b = icicle.sharedMesh.bounds;
            box.center = b.center;
            // Wider than the icicle, so it is easy to click.
            box.size = new Vector3(Mathf.Max(b.size.x, 0.3f), b.size.y, Mathf.Max(b.size.z, 0.3f));
            colliders.Add(box);
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
                list.GetArrayElementAtIndex(i).FindPropertyRelative("semitone").intValue = scale[i];
            }
        });
        SetSound(egg, "note", "event:/SFX/highpitch_0");
        return Save(root);
    }

    static GameObject HollowSquirrel()
    {
        var root = new GameObject("HollowSquirrel");
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
        var bush = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(CherryBushModel), root.transform);
        // The berry model sits where it does on the bush in the model's space; each berry is moved by a parent at a spot on the bush.
        var berryModel = Load<GameObject>(CherryModel);
        Vector3 berryOffset = berryModel.GetComponentInChildren<MeshFilter>().sharedMesh.bounds.center;
        var berries = new List<GameObject>();
        foreach (Vector3 spot in BushSpots(bush, 6))
        {
            var berry = new GameObject("Berry");
            berry.transform.SetParent(root.transform, false);
            berry.transform.localPosition = spot;
            var model = (GameObject)PrefabUtility.InstantiatePrefab(berryModel, berry.transform);
            model.transform.localPosition = -berryOffset;
            berries.Add(berry);
        }
        Bounds bounds = LocalBounds(bush);
        Perchable perch = AddPerchable(ClickSphere(root.transform, bounds.center, bounds.extents.y),
            Child(root.transform, "PerchPoint", new Vector3(bounds.center.x, bounds.max.y - 0.15f, bounds.center.z)));
        var discovery = AddDiscovery(root, "07_CornelianCherry", bush.transform);
        Set(root.AddComponent<CornelianCherry>(), so =>
        {
            so.FindProperty("discovery").objectReferenceValue = discovery;
            so.FindProperty("bush").objectReferenceValue = perch;
            Objects(so.FindProperty("berries"), berries.ToArray());
        });
        return Save(root);
    }

    static GameObject CliffEcho()
    {
        var root = new GameObject("CliffEcho");
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
        // A V, the leader ahead (+Z, the way it flies).
        Vector3[] places = { Vector3.zero, new(-1.6f, 0.1f, -1.4f), new(1.6f, -0.1f, -1.4f), new(-3.2f, 0.2f, -2.8f), new(3.2f, 0f, -2.8f) };
        foreach (Vector3 place in places)
        {
            var bird = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(BirdPrefab), flock.transform);
            bird.transform.localPosition = place;
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

        GameObject bird = GameObject.Find("02_WORLD/Bird");
        Transform oldSnow = bird.transform.Find("SnowOnBird");
        if (oldSnow != null)
            Object.DestroyImmediate(oldSnow.gameObject);
        var snow = (GameObject)PrefabUtility.InstantiatePrefab(prefabs["SnowOnBird"], bird.transform);
        Set(snow.GetComponent<Discovery>(), so => so.FindProperty("subject").objectReferenceValue = bird.transform);

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

    // Spots on the bush's surface, found by rays toward its middle from around it, in the root's space.
    static IEnumerable<Vector3> BushSpots(GameObject bush, int count)
    {
        var filter = bush.GetComponentInChildren<MeshFilter>();
        var collider = filter.gameObject.AddComponent<MeshCollider>();
        collider.sharedMesh = filter.sharedMesh;
        Bounds bounds = LocalBounds(bush);
        var spots = new List<Vector3>();
        try
        {
            for (int i = 0; i < count; i++)
            {
                float angle = (i * 360f / count + 20f) * Mathf.Deg2Rad;
                float height = Mathf.Lerp(bounds.min.y + bounds.size.y * 0.35f, bounds.max.y - bounds.size.y * 0.15f, i % 3 / 2f);
                var target = new Vector3(bounds.center.x, height, bounds.center.z);
                Vector3 start = target + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (bounds.extents.magnitude + 1f);
                Vector3 worldStart = bush.transform.parent.TransformPoint(start), worldTarget = bush.transform.parent.TransformPoint(target);
                if (collider.Raycast(new Ray(worldStart, worldTarget - worldStart), out RaycastHit hit, 100f))
                    spots.Add(bush.transform.parent.InverseTransformPoint(hit.point));
            }
        }
        finally
        {
            Object.DestroyImmediate(collider);
        }
        return spots;
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
