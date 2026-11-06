using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;

// The girl at the sunlit spots in Main (the director, 2026-11-06): play spots off the road where she plays until the bird comes,
// the Animator states for the plays, and the Zs rising off her head while she lies asleep (Ju's SleepZ.png).
// unity command script run --file AgentScripts/BuildGirlPlay.cs --entry BuildGirlPlay.Run
// Rerunning it rebuilds each stage's PlaySpots and the Zs in place. Run it again after the TreeGenerator replants the trees by the
// sunlit spots: the tree spots are put by the nearest trees.
public static class BuildGirlPlay
{
    const string ControllerPath = "Assets/01_Arts/Models/Girl/Girl.controller";
    const string ModelPath = "Assets/01_Arts/Models/Girl/NPC-Girl-Animated.fbx";
    const string TexturePath = "Assets/01_Arts/Textures/Particles/SleepZ.png";
    const string MaterialPath = "Assets/01_Arts/Materials/vfx/SleepZ.mat";
    const string PrefabPath = "Assets/03_Prefabs/SleepZzz.prefab";
    // Off the road, where the forest has not been planted: the road is clear to about 4.9 m either side.
    const float SnowSide = 3f;
    const float TreeSearch = 9f;
    const float TreeStandOff = 1.5f;
    const float TreeLookHeight = 3.5f;
    const float RunRadius = 2.2f;
    const string HeadPath = "NPC-Girl-Rig/ROOT/Hips/Spine/Chest/Neck/Head";
    // Where the Zs start, in metres from the head bone as she lies: along the head away from her body, and up.
    const float ZzzPastHead = 0.5f;
    const float ZzzAboveHead = 0.3f;

    public static string Run()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (Application.isPlaying || scene.path != "Assets/00_Scenes/Main.unity")
            throw new InvalidOperationException("Open Main in Edit Mode.");
        string states = SetupAnimator();
        string spots = PlaceSpots();
        string zzz = PlaceZzz();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        return states + "\n" + spots + "\n" + zzz;
    }

    static string SetupAnimator()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        var clips = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>().ToDictionary(c => c.name);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        // Stand-in clips until the art comes: the director makes the plays' own animations.
        AddState(machine, "PlaySnow", clips["Girl_Idle_Hum"], new Vector3(650f, 300f));
        AddState(machine, "PlayTree", clips["Girl_Idle_Breathing"], new Vector3(650f, 360f));
        EditorUtility.SetDirty(controller);
        return "Animator: PlaySnow, PlayTree";
    }

    // Played directly by Baby (CrossFade), so no transitions. An existing state keeps the clip set on it.
    static void AddState(AnimatorStateMachine machine, string name, AnimationClip clip, Vector3 position)
    {
        if (machine.states.Any(s => s.state.name == name))
            return;
        AnimatorState state = machine.AddState(name, position);
        state.motion = clip;
    }

    static string PlaceSpots()
    {
        var flow = UnityEngine.Object.FindAnyObjectByType<GameFlow>();
        List<Vector3> trees = UnityEngine.Object.FindObjectsByType<TreeGenerator>()
            .SelectMany(g => g.transform.Cast<Transform>())
            .Where(t => t.name.StartsWith("Tree_"))
            .Select(t => t.position)
            .ToList();
        Terrain terrain = Terrain.activeTerrain;
        var report = new List<string>();
        foreach (Stage stage in flow.Stages)
        {
            Transform end = stage.route[stage.route.Count - 1];
            // Only stages that end in a sunlit spot: the fog stage ends at the cliff.
            if (end.GetComponentInChildren<SunlitSpot>() == null)
                continue;
            Transform old = end.Find("PlaySpots");
            if (old != null)
                Undo.DestroyObjectImmediate(old.gameObject);
            var root = new GameObject("PlaySpots").transform;
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Play Spots");
            root.SetParent(end, false);
            Vector3 c = end.position;

            Spot(root, "SnowPlay_Left", PlayKind.SnowPlay, c + new Vector3(-SnowSide, 0f, -1.5f));
            Spot(root, "SnowPlay_Right", PlayKind.SnowPlay, c + new Vector3(SnowSide, 0f, 1.5f));
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 tree = trees.Where(t => Mathf.Sign(t.x - c.x) == side && Flat(t - c).magnitude < TreeSearch)
                    .OrderBy(t => Flat(t - c).sqrMagnitude).First();
                Vector3 toRoad = Flat(c - tree).normalized;
                Transform spot = Spot(root, side < 0f ? "LookAtTree_Left" : "LookAtTree_Right", PlayKind.LookAtTree, tree + toRoad * TreeStandOff);
                var look = new GameObject("LookAt").transform;
                look.SetParent(spot, false);
                look.position = new Vector3(tree.x, tree.y + TreeLookHeight, tree.z);
                spot.GetComponent<PlaySpot>().lookAt = look;
            }
            Transform run = Spot(root, "RunAround", PlayKind.RunAround, c + new Vector3(0f, 0f, -3f));
            for (int i = 0; i < 4; i++)
            {
                float angle = i * Mathf.PI * 0.5f;
                var point = new GameObject("Point_" + i).transform;
                point.SetParent(run, false);
                point.position = Ground(terrain, run.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * RunRadius);
            }
            foreach (PlaySpot spot in root.GetComponentsInChildren<PlaySpot>())
            {
                Vector3 p = spot.transform.position;
                if (!stage.area.Contains(new Vector3(p.x, stage.area.center.y, p.z)))
                    throw new Exception(stage.name + ": " + spot.name + " is outside the bird's area");
                float nearest = trees.Min(t => Flat(t - p).magnitude);
                report.Add($"{stage.name}/{spot.name} {p} nearest tree {nearest:F1} m");
            }
        }
        return string.Join("\n", report);
    }

    static Transform Spot(Transform root, string name, PlayKind kind, Vector3 position)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        go.transform.position = Ground(Terrain.activeTerrain, position);
        go.AddComponent<PlaySpot>().kind = kind;
        return go.transform;
    }

    static Vector3 Ground(Terrain terrain, Vector3 p) => new(p.x, terrain.SampleHeight(p) + terrain.transform.position.y, p.z);

    static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);

    static string PlaceZzz()
    {
        GameObject prefab = BuildZzzPrefab();
        Baby baby = UnityEngine.Object.FindAnyObjectByType<Baby>();
        Transform head = baby.transform.Find("NPC-Girl-Animated/" + HeadPath);
        Transform old = head.Find(prefab.name);
        if (old != null)
            Undo.DestroyObjectImmediate(old.gameObject);
        var zzz = (GameObject)PrefabUtility.InstantiatePrefab(prefab, head);
        Undo.RegisterCreatedObjectUndo(zzz, "Sleep Zzz");
        zzz.transform.localPosition = ZzzLocalPosition(baby);
        zzz.transform.localRotation = Quaternion.identity;
        var so = new SerializedObject(baby);
        so.FindProperty("sleepZzz").objectReferenceValue = zzz.GetComponent<ParticleSystem>();
        so.ApplyModifiedProperties();
        return "Zzz under " + head.name + ", head lying at " + head.position;
    }

    // Past the top of her head as she lies asleep (Girl_Sleep), away from her body and a little up: the head bone's local position for
    // that point, found on a copy of her model posed that way.
    static Vector3 ZzzLocalPosition(Baby baby)
    {
        Transform model = baby.transform.Find("NPC-Girl-Animated");
        AnimationClip sleep = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>().First(c => c.name == "Girl_Sleep");
        var copy = (GameObject)UnityEngine.Object.Instantiate(model.gameObject, model.position, model.rotation);
        copy.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            copy.transform.localScale = model.lossyScale;
            sleep.SampleAnimation(copy, 0f);
            Transform head = copy.transform.Find(HeadPath);
            return head.InverseTransformVector(head.up * ZzzPastHead + Vector3.up * ZzzAboveHead);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(copy);
        }
    }

    static GameObject BuildZzzPrefab()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath));
        material.SetColor("_BaseColor", Color.white);
        BaseShaderGUI.SetMaterialKeywords(material);
        EditorUtility.SetDirty(material);

        var go = new GameObject("SleepZzz");
        try
        {
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 2f;
            main.startLifetime = 3f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 0.75f);
            // Blue, to read on the snow and in the sunlit spot's light.
            main.startColor = new Color(0.72f, 0.82f, 1f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            // The head bone carries the rig's and the girl's scale: the Zs ignore it and keep their own size and spread.
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.maxParticles = 8;

            var emission = ps.emission;
            emission.rateOverTime = 0.75f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.03f;

            // Up, and drifting right on the screen.
            Vector3 right = Camera.main.transform.right;
            right.y = 0f;
            right = right.normalized * 0.12f;
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = right.x;
            velocity.y = 0.45f;
            velocity.z = right.z;

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.08f;
            noise.frequency = 0.6f;
            noise.scrollSpeed = 0.3f;

            var color = ps.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.1f));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
