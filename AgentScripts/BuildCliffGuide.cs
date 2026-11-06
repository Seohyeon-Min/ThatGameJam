using System.Text;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

// The end of the fog stage, in Main: the guide bird (Bird2) on the stump, the camera point she sits under, the ending (EndingFlight),
// and the flower generator's clear area over the cliff. Wires them into CliffEnding. Rerunning puts them in place again.

// unity command script run --file AgentScripts/BuildCliffGuide.cs --entry BuildCliffGuide.Run
public static class BuildCliffGuide
{
    const string ModelPath = "Assets/01_Arts/Models/Bird2/Bird2-Animated.fbx";
    const string ControllerPath = "Assets/01_Arts/Models/Bird2/Bird2.controller";
    const string PrefabPath = "Assets/03_Prefabs/Stage3/GuideBird.prefab";
    const string CryPrefabPath = "Assets/03_Prefabs/BirdCryPulse.prefab";
    const string TimelinePath = "Assets/04_Data/Timeline/Ending.playable";
    // The flowers' clear area over the cliff, in world X/Z: from a little before the stump out past the drop.
    static readonly Vector2 ClearMin = new(-80f, 243f);
    static readonly Vector2 ClearMax = new(80f, 320f);
    // Where the camera eases to once she sits: from the stump, out toward the drop and to the right on screen.
    const float ViewOut = 3f;
    const float ViewRight = 2.5f;

    public static string Run()
    {
        var report = new StringBuilder();
        SetUpImport(report);
        AnimatorController controller = MakeController(report);

        var cliff = Object.FindAnyObjectByType<CliffEnding>(FindObjectsInactive.Include);
        if (cliff == null)
            return report.Append("No CliffEnding in the open scene.").ToString();
        Transform stump = cliff.transform.Find("Stone");
        var cliffSo = new SerializedObject(cliff);
        var path = (Transform)cliffSo.FindProperty("path").objectReferenceValue;
        var edge = (Transform)cliffSo.FindProperty("edge").objectReferenceValue;

        GuideBird guide = PlaceGuide(cliff, stump, path, controller, report);
        Transform view = PlaceSittingView(cliff, stump, edge, report);
        EndingFlight ending = MakeEnding(cliff, edge, report);

        cliffSo.FindProperty("guide").objectReferenceValue = guide;
        cliffSo.FindProperty("sittingView").objectReferenceValue = view;
        cliffSo.FindProperty("ending").objectReferenceValue = ending;
        cliffSo.ApplyModifiedProperties();

        SetFlowerClearArea(report);
        EditorSceneManager.MarkSceneDirty(cliff.gameObject.scene);
        return report.ToString();
    }

    static void SetUpImport(StringBuilder report)
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.importAnimation = true;
        importer.optimizeGameObjects = false;
        ModelImporterClipAnimation[] clips = importer.clipAnimations.Length > 0 ? importer.clipAnimations : importer.defaultClipAnimations;
        foreach (ModelImporterClipAnimation clip in clips)
        {
            // Blender names the takes "Bird2-Rig|Bird2_Fly"; the clips go by the action's name.
            int bar = clip.name.LastIndexOf('|');
            if (bar >= 0)
                clip.name = clip.name.Substring(bar + 1);
            // PerchLook plays once when it answers, then goes back to PerchIdle.
            bool loop = clip.name.EndsWith("Fly") || clip.name.EndsWith("PerchIdle");
            clip.loopTime = loop;
            clip.loopPose = false;
        }
        importer.clipAnimations = clips;
        importer.SaveAndReimport();
        report.Append("Imported Bird2 with ").Append(clips.Length).Append(" clips. ");
    }

    static AnimationClip Clip(string name)
    {
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(ModelPath))
            if (asset is AnimationClip clip && clip.name == name)
                return clip;
        throw new System.Exception("No clip " + name + " in " + ModelPath);
    }

    static AnimatorController MakeController(StringBuilder report)
    {
        AssetDatabase.DeleteAsset(ControllerPath);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState idle = machine.AddState("PerchIdle");
        idle.motion = Clip("Bird2_PerchIdle");
        AnimatorState look = machine.AddState("PerchLook");
        look.motion = Clip("Bird2_PerchLook");
        AnimatorState takeoff = machine.AddState("Takeoff");
        takeoff.motion = Clip("Bird2_Takeoff");
        AnimatorState fly = machine.AddState("Fly");
        fly.motion = Clip("Bird2_Fly");
        AnimatorState landing = machine.AddState("Landing");
        landing.motion = Clip("Bird2_Landing");
        machine.defaultState = idle;
        // GuideBird plays these by name; each one-off goes on by itself when it ends.
        Then(look, idle, 0.25f);
        Then(takeoff, fly, 0.1f);
        Then(landing, idle, 0.2f);
        report.Append("Made Bird2.controller. ");
        return controller;
    }

    static void Then(AnimatorState from, AnimatorState to, float blend)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = true;
        transition.exitTime = 1f - blend / Mathf.Max(((AnimationClip)from.motion).length, 0.01f);
        transition.duration = blend;
        transition.hasFixedDuration = true;
    }

    static GuideBird PlaceGuide(CliffEnding cliff, Transform stump, Transform path, AnimatorController controller, StringBuilder report)
    {
        Transform old = cliff.transform.Find("GuideBird");
        if (old != null)
            Undo.DestroyObjectImmediate(old.gameObject);

        var root = new GameObject("GuideBird");
        Undo.RegisterCreatedObjectUndo(root, "Build Cliff Guide");
        root.transform.SetParent(cliff.transform, false);

        var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath));
        model.name = "Bird2";
        model.transform.SetParent(root.transform, false);
        // The export faces -Z: turned so its head points along the root's forward.
        model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        Animator animator = model.GetComponent<Animator>();
        if (animator == null)
            animator = model.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        var painted = model.AddComponent<WatercolorPaintedModel>();

        var cry = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CryPrefabPath));
        cry.transform.SetParent(root.transform, false);
        cry.transform.localPosition = new Vector3(0f, 0.3f, 0f);
        int aboveFog = LayerMask.NameToLayer("AboveFog");
        foreach (Transform t in cry.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = aboveFog;

        var guide = root.AddComponent<GuideBird>();
        var so = new SerializedObject(guide);
        so.FindProperty("animator").objectReferenceValue = animator;
        so.FindProperty("cry").objectReferenceValue = cry;
        so.FindProperty("answersPast").objectReferenceValue = LastFogAnimal();
        so.ApplyModifiedPropertiesWithoutUndo();

        // On top of the stump, sitting as it likes: side on to the camera, not watching for her.
        Bounds top = stump.GetComponent<Renderer>().bounds;
        root.transform.position = new Vector3(top.center.x, top.max.y, top.center.z);
        root.transform.rotation = Quaternion.LookRotation(SittingFacing);

        report.Append(WatercolorBaker.Bake(painted)).Append(' ');
        PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabPath, InteractionMode.AutomatedAction);
        report.Append("Guide bird on the stump at ").Append(root.transform.position.ToString("F2")).Append(", prefab ").Append(PrefabPath).Append(". ");
        return guide;
    }

    // The guide bird's facing on the stump (world X/Z).
    static readonly Vector3 SittingFacing = new(1f, 0f, 0.35f);

    // The maze's animal furthest along the road: past it, the guide bird answers.
    public static Transform LastFogAnimal()
    {
        Transform last = null;
        foreach (FogForkAnimal animal in Object.FindObjectsByType<FogForkAnimal>(FindObjectsInactive.Include))
            if (last == null || animal.transform.position.z > last.position.z)
                last = animal.transform;
        return last;
    }

    // Only the guide bird's answering and facing, on the one already in the scene.
    // unity command script run --file AgentScripts/BuildCliffGuide.cs --entry BuildCliffGuide.SetGuide
    public static string SetGuide()
    {
        var guide = Object.FindAnyObjectByType<GuideBird>(FindObjectsInactive.Include);
        var so = new SerializedObject(guide);
        so.FindProperty("answersPast").objectReferenceValue = LastFogAnimal();
        so.ApplyModifiedProperties();
        Undo.RecordObject(guide.transform, "Set Guide");
        guide.transform.rotation = Quaternion.LookRotation(SittingFacing);
        var cliff = Object.FindAnyObjectByType<CliffEnding>(FindObjectsInactive.Include);
        var cliffSo = new SerializedObject(cliff);
        cliffSo.FindProperty("guideFliesAt").floatValue = 6f;
        cliffSo.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(guide.gameObject.scene);
        return "Guide answers past " + LastFogAnimal().name + " (z " + LastFogAnimal().position.z.ToString("F1") + "), faces " + guide.transform.forward.ToString("F2") + ", flies at 6 m.";
    }

    static Transform PlaceSittingView(CliffEnding cliff, Transform stump, Transform edge, StringBuilder report)
    {
        Transform view = cliff.transform.Find("SittingView");
        if (view == null)
        {
            view = new GameObject("SittingView").transform;
            Undo.RegisterCreatedObjectUndo(view.gameObject, "Build Cliff Guide");
            view.SetParent(cliff.transform, false);
        }
        Camera main = Camera.main;
        Vector3 right = main != null ? Vector3.ProjectOnPlane(main.transform.right, Vector3.up).normalized : Vector3.right;
        Vector3 at = stump.position + edge.forward * ViewOut + right * ViewRight;
        view.position = new Vector3(at.x, 0f, at.z);
        report.Append("Sitting view at ").Append(view.position.ToString("F1")).Append(". ");
        return view;
    }

    // The ending (EndingFlight) under 04_CUTSCENE: its two shots and the bird's two paths, laid out from the cliff's edge.
    // Replaces the placeholder Timeline ending (its object and Ending.playable).
    // unity command script run --file AgentScripts/BuildCliffGuide.cs --entry BuildCliffGuide.BuildEnding
    public static string BuildEnding()
    {
        var report = new StringBuilder();
        var cliff = Object.FindAnyObjectByType<CliffEnding>(FindObjectsInactive.Include);
        var cliffSo = new SerializedObject(cliff);
        EndingFlight ending = MakeEnding(cliff, (Transform)cliffSo.FindProperty("edge").objectReferenceValue, report);
        cliffSo.FindProperty("ending").objectReferenceValue = ending;
        cliffSo.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(cliff.gameObject.scene);
        return report.ToString();
    }

    static EndingFlight MakeEnding(CliffEnding cliff, Transform edge, StringBuilder report)
    {
        var cutscenes = GameObject.Find("04_CUTSCENE");
        Transform old = cutscenes.transform.Find("Ending");
        if (old != null)
            Undo.DestroyObjectImmediate(old.gameObject);
        if (AssetDatabase.LoadAssetAtPath<Object>(TimelinePath) != null)
            AssetDatabase.DeleteAsset(TimelinePath);

        var root = new GameObject("Ending");
        Undo.RegisterCreatedObjectUndo(root, "Build Ending");
        root.transform.SetParent(cutscenes.transform, false);
        var flight = root.AddComponent<EndingFlight>();

        // Laid out from the edge: out = over the drop, side = along the edge, up.
        Vector3 e = edge.position;
        Vector3 out_ = Vector3.ProjectOnPlane(edge.forward, Vector3.up).normalized;
        Vector3 side = Vector3.Cross(Vector3.up, out_);
        Vector3 At(float o, float s, float u) => e + out_ * o + side * s + Vector3.up * u;
        Transform path = (Transform)new SerializedObject(cliff).FindProperty("path").objectReferenceValue;
        Vector3 girl = path.GetChild(path.childCount - 1).position;

        Camera main = Camera.main;
        // 1. Up in the air over the drop, looking back down at the cliff and the girl; the bird comes up round under it and past it.
        CinemachineCamera rise = Shot(root.transform, "RiseShot", At(7f, 1f, 13f), girl + Vector3.up * 1f, main, main != null ? main.fieldOfView : 40f);
        Transform risePath = Path(root.transform, "RisePath", new[]
        {
            At(3f, 1f, 3f), At(3.5f, -2f, 6f), At(1.5f, -1.5f, 9f), At(2.5f, 1.5f, 11.5f), At(5.5f, 2.5f, 14.5f), At(8f, 2.5f, 18f),
        });
        // 2. Far off to the side of the cliff (the drop on the right of the picture), the bird climbing away high over the edge.
        Vector3 farAt = At(4f, 90f, 0f);
        Terrain ground = Terrain.activeTerrain;
        float groundY = ground != null ? ground.transform.position.y + ground.SampleHeight(farAt) : 0f;
        farAt.y = Mathf.Max(groundY, 0f) + 4f;
        CinemachineCamera far = Shot(root.transform, "FarShot", farAt, At(3f, 0f, 9f), main, FarFieldOfView);
        FrameFarShot(far, e);
        AddHandheld(far);
        Transform farPath = Path(root.transform, "FarPath", new[] { At(3f, 0f, 8f), At(4f, 0f, 16f), At(6f, 0f, 26f), At(9f, 0f, 40f) });

        var so = new SerializedObject(flight);
        so.FindProperty("riseShot").objectReferenceValue = rise;
        so.FindProperty("risePath").objectReferenceValue = risePath;
        so.FindProperty("farShot").objectReferenceValue = far;
        so.FindProperty("farPath").objectReferenceValue = farPath;
        so.ApplyModifiedPropertiesWithoutUndo();
        report.Append("Ending under 04_CUTSCENE: RiseShot at ").Append(rise.transform.position.ToString("F1"))
            .Append(", FarShot at ").Append(far.transform.position.ToString("F1")).Append(" (ground there ").Append(groundY.ToString("F1")).Append("). Removed the placeholder Timeline. ");
        return flight;
    }

    // The far shot, zoomed in, turned so the cliff's edge sits low and right of the middle (the cliff's mass on the left, the
    // sky over the drop on the right, where the bird climbs).
    const float FarFieldOfView = 14f;
    static readonly Vector2 FarEdgeOnScreen = new(0.68f, 0.28f);

    static void FrameFarShot(CinemachineCamera shot, Vector3 edge)
    {
        Camera main = Camera.main;
        float aspect = main != null ? main.aspect : 16f / 9f;
        float tanHalfV = Mathf.Tan(shot.Lens.FieldOfView * 0.5f * Mathf.Deg2Rad);
        float tanHalfH = tanHalfV * aspect;
        Vector3 to = edge - shot.transform.position;
        float yaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
        float rise = Mathf.Atan2(to.y, new Vector2(to.x, to.z).magnitude) * Mathf.Rad2Deg;
        // Turned away from the edge so it lands at that place on the screen.
        yaw -= Mathf.Atan((FarEdgeOnScreen.x - 0.5f) * 2f * tanHalfH) * Mathf.Rad2Deg;
        rise -= Mathf.Atan((FarEdgeOnScreen.y - 0.5f) * 2f * tanHalfV) * Mathf.Rad2Deg;
        shot.transform.rotation = Quaternion.Euler(-rise, yaw, 0f);
    }

    // A slight sway, as if held by hand (Cinemachine's telephoto handheld noise: the far shot is zoomed in).
    const string HandheldNoisePath = "Packages/com.unity.cinemachine/Presets/Noise/Handheld_tele_mild.asset";

    static void AddHandheld(CinemachineCamera shot)
    {
        var noise = shot.GetComponent<CinemachineBasicMultiChannelPerlin>();
        if (noise == null)
            noise = Undo.AddComponent<CinemachineBasicMultiChannelPerlin>(shot.gameObject);
        noise.NoiseProfile = AssetDatabase.LoadAssetAtPath<NoiseSettings>(HandheldNoisePath);
        noise.AmplitudeGain = 0.2f;
        noise.FrequencyGain = 0.5f;
    }

    // Only the handheld sway on the far shot already in the scene.
    // unity command script run --file AgentScripts/BuildCliffGuide.cs --entry BuildCliffGuide.AddFarShake
    public static string AddFarShake()
    {
        var far = GameObject.Find("04_CUTSCENE").transform.Find("Ending/FarShot").GetComponent<CinemachineCamera>();
        AddHandheld(far);
        EditorSceneManager.MarkSceneDirty(far.gameObject.scene);
        var noise = far.GetComponent<CinemachineBasicMultiChannelPerlin>();
        return "FarShot handheld noise: " + (noise.NoiseProfile != null ? noise.NoiseProfile.name : "missing profile");
    }

    // A slight handheld sway on the low shot already in the scene, a little stronger than the far shot's as its lens is wider.
    // unity command script run --file AgentScripts/BuildCliffGuide.cs --entry BuildCliffGuide.AddLowShake
    public static string AddLowShake()
    {
        var low = GameObject.Find("04_CUTSCENE").transform.Find("Ending/LowShot").GetComponent<CinemachineCamera>();
        AddHandheld(low);
        var noise = low.GetComponent<CinemachineBasicMultiChannelPerlin>();
        noise.AmplitudeGain = 0.35f;
        noise.FrequencyGain = 0.4f;
        EditorSceneManager.MarkSceneDirty(low.gameObject.scene);
        return "LowShot handheld noise: " + (noise.NoiseProfile != null ? noise.NoiseProfile.name : "missing profile");
    }

    // Clouds for the far shot (Cloud_01 instances): a row across the picture just over its top edge, over the drop and the cliff,
    // so only their undersides show and the bird climbs up into them. Off until the shot (EndingFlight turns them on).
    // unity command script run --file AgentScripts/BuildCliffGuide.cs --entry BuildCliffGuide.AddFarClouds
    const string CloudPrefabPath = "Assets/03_Prefabs/Clouds/Cloud_01.prefab";
    // The clouds' height over the drop: the far shot sees their undersides just over the top of its picture, the top shot their tops.
    const float CloudsY = 18.8f;

    const string EndingCloudMaterialPath = "Assets/01_Arts/Materials/CloudPuff_Ending.mat";

    static Material EndingCloudMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(EndingCloudMaterialPath);
        if (material != null)
            return material;
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(CloudPrefabPath).GetComponentInChildren<Renderer>(true).sharedMaterial;
        material = new Material(source) { name = "CloudPuff_Ending" };
        AssetDatabase.CreateAsset(material, EndingCloudMaterialPath);
        LightenForSideView(material);
        return material;
    }

    // Seen from the side and below, nearly all of a puff is in its Shade tone, which is dark for clouds seen from above: one step
    // lighter each (Shade halfway from Middle to Light, Middle takes Light's), and no darkening away from the intro.
    static void LightenForSideView(Material material)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(CloudPrefabPath).GetComponentInChildren<Renderer>(true).sharedMaterial;
        Color light = source.GetColor("_LightColor"), middle = source.GetColor("_MidColor");
        material.SetColor("_ShadeColor", Color.Lerp(middle, light, 0.5f));
        material.SetColor("_MidColor", light);
        material.SetFloat("_DarkStrength", 0f);
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
    }

    public static string AddFarClouds()
    {
        var ending = GameObject.Find("04_CUTSCENE").transform.Find("Ending");
        Transform old = ending.Find("FarClouds");
        if (old != null)
            Undo.DestroyObjectImmediate(old.gameObject);
        var clouds = new GameObject("FarClouds");
        Undo.RegisterCreatedObjectUndo(clouds, "Add Far Clouds");
        clouds.transform.SetParent(ending, false);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CloudPrefabPath);
        // Along the picture (Z), alternating a little nearer and farther (X), at about the height of the picture's top.
        float[] alongZ = { 218f, 229f, 240f, 251f, 262f, 273f };
        for (int i = 0; i < alongZ.Length; i++)
        {
            var cloud = (GameObject)PrefabUtility.InstantiatePrefab(prefab, clouds.transform);
            cloud.transform.position = new Vector3(i % 2 == 0 ? -3f : 4f, CloudsY + (i % 3) * 0.4f, alongZ[i]);
        }
        // The puffs' material darkens away from the intro (Dark Away From A Point), and these are far from it: a copy without that.
        Material material = EndingCloudMaterial();
        LightenForSideView(material);
        foreach (Renderer puff in clouds.GetComponentsInChildren<Renderer>(true))
            puff.sharedMaterial = material;
        clouds.SetActive(false);
        CloudGenerator.SortAllPuffs();
        var flight = ending.GetComponent<EndingFlight>();
        var so = new SerializedObject(flight);
        so.FindProperty("farClouds").objectReferenceValue = clouds;
        so.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(ending.gameObject.scene);
        return "Far clouds: " + alongZ.Length + " Cloud_01 under Ending/FarClouds, off until the far shot.";
    }

    // The top shot: a sea of clouds (Cloud_01) over the drop at the far shot's clouds' height, seen from just over it, with the
    // point on its top where the bird bursts out. The sea has its own material: the clouds' own tones (made for seeing them from
    // above) without the darkening away from the intro. Off until the shot.
    // unity command script run --file AgentScripts/BuildCliffGuide.cs --entry BuildCliffGuide.AddTopShot
    const string TopCloudMaterialPath = "Assets/01_Arts/Materials/CloudPuff_EndingTop.mat";
    const float TopTurnLeft = 12f;
    const float TopTurnUp = 6f;

    // Whiter than the clouds' own tones, with enough shade left for the bird to show against: Middle most of the way to Light,
    // Shade most of the way to Middle, from the source's tones.
    static void WhitenTop(Material material)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(CloudPrefabPath).GetComponentInChildren<Renderer>(true).sharedMaterial;
        Color light = source.GetColor("_LightColor"), middle = source.GetColor("_MidColor"), shade = source.GetColor("_ShadeColor");
        material.SetColor("_MidColor", Color.Lerp(middle, light, 0.6f));
        material.SetColor("_ShadeColor", Color.Lerp(shade, middle, 0.6f));
        material.SetFloat("_DarkStrength", 0f);
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
    }

    // Only the top shot's clouds whiter, on the material already made.
    // unity command script run --file AgentScripts/BuildCliffGuide.cs --entry BuildCliffGuide.WhitenTopClouds
    public static string WhitenTopClouds()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(TopCloudMaterialPath);
        WhitenTop(material);
        return "Top clouds: middle " + material.GetColor("_MidColor") + ", shade " + material.GetColor("_ShadeColor");
    }

    public static string AddTopShot()
    {
        var ending = GameObject.Find("04_CUTSCENE").transform.Find("Ending");
        foreach (string name in new[] { "TopClouds", "TopShot", "CloudSurface" })
        {
            Transform old = ending.Find(name);
            if (old != null)
                Undo.DestroyObjectImmediate(old.gameObject);
        }
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CloudPrefabPath);
        var material = AssetDatabase.LoadAssetAtPath<Material>(TopCloudMaterialPath);
        if (material == null)
        {
            material = new Material(prefab.GetComponentInChildren<Renderer>(true).sharedMaterial) { name = "CloudPuff_EndingTop" };
            material.SetFloat("_DarkStrength", 0f);
            AssetDatabase.CreateAsset(material, TopCloudMaterialPath);
        }
        WhitenTop(material);

        var centre = new Vector3(0f, CloudsY, 256f);
        var clouds = new GameObject("TopClouds");
        Undo.RegisterCreatedObjectUndo(clouds, "Add Top Shot");
        clouds.transform.SetParent(ending, false);
        // 3 across, 4 deep, every other row shifted, so the sea has no gaps or lines in it.
        for (int ix = -1; ix <= 1; ix++)
            for (int iz = -1; iz <= 2; iz++)
            {
                var cloud = (GameObject)PrefabUtility.InstantiatePrefab(prefab, clouds.transform);
                cloud.transform.position = centre + new Vector3(ix * 26f + (iz % 2 == 0 ? 0f : 9f), ((ix + iz) & 1) * 0.4f, iz * 26f);
            }
        float top = float.MinValue;
        foreach (Renderer puff in clouds.GetComponentsInChildren<Renderer>(true))
        {
            puff.sharedMaterial = material;
            top = Mathf.Max(top, puff.transform.position.y + puff.transform.lossyScale.y * 0.3f);
        }

        var surface = new GameObject("CloudSurface").transform;
        surface.SetParent(ending, false);
        surface.position = new Vector3(centre.x, top, centre.z);
        CinemachineCamera shot = Shot(ending, "TopShot", surface.position + new Vector3(0f, 2.5f, -13f), surface.position + Vector3.up * 1f,
            Camera.main, 35f);
        // Turned a little left and up from the spot: the bird comes out on the right, with more sky over the clouds.
        shot.transform.rotation = Quaternion.Euler(0f, -TopTurnLeft, 0f) * shot.transform.rotation * Quaternion.Euler(-TopTurnUp, 0f, 0f);

        clouds.SetActive(false);
        CloudGenerator.SortAllPuffs();
        var so = new SerializedObject(ending.GetComponent<EndingFlight>());
        so.FindProperty("topShot").objectReferenceValue = shot;
        so.FindProperty("topClouds").objectReferenceValue = clouds;
        so.FindProperty("cloudSurface").objectReferenceValue = surface;
        so.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(ending.gameObject.scene);
        return "Top shot: 12 Cloud_01 under Ending/TopClouds (top at y " + top.ToString("F1") + "), TopShot at " + shot.transform.position.ToString("F1") + ".";
    }

    // The low shot: a camera down by the girl's feet looking up past her (her on the right, facing left), the gap's centre in the
    // top shot's clouds up on the left of the picture, and shafts of light (EndingLightShaft prefab) coming down through it beside
    // her, not on her. The shafts are off until the shot.
    // unity command script run --file AgentScripts/BuildCliffGuide.cs --entry BuildCliffGuide.AddLowShot
    const string ShaftPrefabPath = "Assets/03_Prefabs/Stage3/EndingLightShaft.prefab";
    const string ShaftMaterialPath = "Assets/01_Arts/Materials/EndingLightBeam.mat";
    // The sunlit spots' beam material, which the shafts' own is a brighter copy of.
    const string SunBeamMaterialGuid = "09e1ff68552e4be0a7ead4e174c8a091";
    const float LowFieldOfView = 45f;
    // Where the gap is on the low shot's picture.
    static readonly Vector2 GapOnScreen = new(0.3f, 0.8f);
    // Where the shafts land, from where she sits, in the order they come: beside her and ahead, on the ground before the edge,
    // never on her (metres, world X and Z). And where each starts in the gap, from its centre: all from inside it.
    static readonly Vector2[] ShaftFeet = { new(-0.6f, 1.7f), new(0.5f, 2.8f), new(-1.8f, 2.4f), new(1.3f, 1.9f), new(-2.7f, 1.3f) };
    static readonly Vector2[] ShaftTops = { new(0.5f, -0.5f), new(2f, 1f), new(-1.5f, 0.5f), new(2.5f, -2f), new(-2.5f, -1.5f) };

    public static string AddLowShot()
    {
        var ending = GameObject.Find("04_CUTSCENE").transform.Find("Ending");
        foreach (string name in new[] { "LowShot", "GapCentre", "LowLight" })
        {
            Transform old = ending.Find(name);
            if (old != null)
                Undo.DestroyObjectImmediate(old.gameObject);
        }
        var cliff = Object.FindAnyObjectByType<CliffEnding>(FindObjectsInactive.Include);
        var path = (Transform)new SerializedObject(cliff).FindProperty("path").objectReferenceValue;
        Vector3 girl = path.GetChild(path.childCount - 1).position;

        // Low, to her side and a little ahead, looking across past her (she faces +Z, so she faces left on the picture), tilted up
        // at the clouds; the stump behind her stays just off the picture's right.
        Vector3 at = girl + new Vector3(-2.5f, 0.3f, 2.25f);
        Quaternion rotation = Quaternion.Euler(-26f, 110f, 0f);
        CinemachineCamera shot = Shot(ending, "LowShot", at, at + rotation * Vector3.forward, Camera.main, LowFieldOfView);
        shot.transform.rotation = rotation;

        // The gap: where that place on the picture looks up into the clouds.
        float aspect = Camera.main != null ? Camera.main.aspect : 16f / 9f;
        float tanV = Mathf.Tan(LowFieldOfView * 0.5f * Mathf.Deg2Rad), tanH = tanV * aspect;
        Vector3 ray = rotation * new Vector3((GapOnScreen.x - 0.5f) * 2f * tanH, (GapOnScreen.y - 0.5f) * 2f * tanV, 1f).normalized;
        float cloudsMiddle = CloudsY + 0.7f;
        Vector3 gap = at + ray * ((cloudsMiddle - at.y) / ray.y);
        var gapCentre = new GameObject("GapCentre").transform;
        gapCentre.SetParent(ending, false);
        gapCentre.position = gap;

        // The sea seen from below has gaps of its own: a few more clouds over where this one opens, so it opens from a full sky.
        Transform topClouds = ending.Find("TopClouds");
        for (int i = 0; ; i++)
        {
            Transform old = topClouds.Find("GapCloud_" + i);
            if (old == null)
                break;
            Undo.DestroyObjectImmediate(old.gameObject);
        }
        var cloudPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CloudPrefabPath);
        var cloudMaterial = AssetDatabase.LoadAssetAtPath<Material>(TopCloudMaterialPath);
        Vector2[] around = { new(0f, 0f), new(-11f, 7f), new(10f, -6f) };
        for (int i = 0; i < around.Length; i++)
        {
            var cloud = (GameObject)PrefabUtility.InstantiatePrefab(cloudPrefab, topClouds);
            cloud.name = "GapCloud_" + i;
            cloud.transform.position = new Vector3(gap.x + around[i].x, CloudsY + 0.2f * i, gap.z + around[i].y);
            foreach (Renderer puff in cloud.GetComponentsInChildren<Renderer>(true))
                puff.sharedMaterial = cloudMaterial;
        }
        CloudGenerator.SortAllPuffs();

        GameObject prefab = ShaftPrefab();
        var light = new GameObject("LowLight");
        Undo.RegisterCreatedObjectUndo(light, "Add Low Shot");
        light.transform.SetParent(ending, false);
        var shafts = new LightBeam[ShaftFeet.Length];
        for (int i = 0; i < ShaftFeet.Length; i++)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, light.transform);
            Vector3 foot = girl + new Vector3(ShaftFeet[i].x, 0f, ShaftFeet[i].y);
            Vector3 start = gap + new Vector3(ShaftTops[i].x, 0f, ShaftTops[i].y);
            instance.transform.position = foot;
            var beam = instance.GetComponent<LightBeam>();
            var so = new SerializedObject(beam);
            // Thin shafts, from the gap's middle height (inside the clouds) down to the ground.
            so.FindProperty("toSky").vector3Value = (start - foot).normalized;
            so.FindProperty("height").floatValue = Vector3.Distance(start, foot);
            so.FindProperty("footWidth").floatValue = i % 2 == 0 ? 0.45f : 0.3f;
            so.FindProperty("topWidth").floatValue = i % 2 == 0 ? 0.9f : 0.6f;
            so.FindProperty("poolRadius").floatValue = i % 2 == 0 ? 0.6f : 0.45f;
            so.ApplyModifiedPropertiesWithoutUndo();
            instance.SetActive(false);
            shafts[i] = beam;
        }

        var flight = new SerializedObject(ending.GetComponent<EndingFlight>());
        flight.FindProperty("lowShot").objectReferenceValue = shot;
        flight.FindProperty("gapCentre").objectReferenceValue = gapCentre;
        SerializedProperty list = flight.FindProperty("lightShafts");
        list.arraySize = shafts.Length;
        for (int i = 0; i < shafts.Length; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = shafts[i];
        flight.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(ending.gameObject.scene);
        return "Low shot at " + at.ToString("F1") + ", gap at " + gap.ToString("F1") + ", " + shafts.Length + " shafts beside her.";
    }

    // The last two shots: Sleep Shot from in front of her, out over the drop looking back at her and the light she walks into, and Back Shot
    // behind where she lies in it, looking out with her over the cliff. The light (Sleep Shaft) is one more, wider, shaft from the
    // gap, the last of the low shot's to come.
    // unity command script run --file AgentScripts/BuildCliffGuide.cs --entry BuildCliffGuide.AddSleepShots
    static readonly Vector2 SleepShaftFoot = new(2f, 0.3f);

    public static string AddSleepShots()
    {
        var ending = GameObject.Find("04_CUTSCENE").transform.Find("Ending");
        foreach (string name in new[] { "SleepShot", "BackShot" })
        {
            Transform old = ending.Find(name);
            if (old != null)
                Undo.DestroyObjectImmediate(old.gameObject);
        }
        Transform light = ending.Find("LowLight");
        Transform oldShaft = light.Find("SleepShaft");
        if (oldShaft != null)
            Undo.DestroyObjectImmediate(oldShaft.gameObject);
        var cliff = Object.FindAnyObjectByType<CliffEnding>(FindObjectsInactive.Include);
        var path = (Transform)new SerializedObject(cliff).FindProperty("path").objectReferenceValue;
        Vector3 girl = path.GetChild(path.childCount - 1).position;
        Vector3 gap = ending.Find("GapCentre").position;

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(ShaftPrefab(), light);
        instance.name = "SleepShaft";
        Vector3 foot = girl + new Vector3(SleepShaftFoot.x, 0f, SleepShaftFoot.y);
        instance.transform.position = foot;
        var beam = instance.GetComponent<LightBeam>();
        var so = new SerializedObject(beam);
        so.FindProperty("toSky").vector3Value = (gap - foot).normalized;
        so.FindProperty("height").floatValue = Vector3.Distance(gap, foot);
        so.FindProperty("footWidth").floatValue = 1.1f;
        so.FindProperty("topWidth").floatValue = 2f;
        so.FindProperty("poolRadius").floatValue = 1f;
        so.ApplyModifiedPropertiesWithoutUndo();
        instance.SetActive(false);

        Vector3 middle = (girl + foot) / 2f;
        CinemachineCamera sleep = Shot(ending, "SleepShot", middle + new Vector3(0f, 1.5f, 6.5f), middle + Vector3.up * 0.7f, Camera.main, 35f);
        CinemachineCamera back = Shot(ending, "BackShot", foot + new Vector3(0.4f, 1.1f, -3.2f), foot + new Vector3(0.4f, 0.6f, 3f), Camera.main, 40f);
        Undo.RegisterCreatedObjectUndo(sleep.gameObject, "Add Sleep Shots");
        Undo.RegisterCreatedObjectUndo(back.gameObject, "Add Sleep Shots");

        var flight = new SerializedObject(ending.GetComponent<EndingFlight>());
        flight.FindProperty("sleepShot").objectReferenceValue = sleep;
        flight.FindProperty("backShot").objectReferenceValue = back;
        flight.FindProperty("sleepShaft").objectReferenceValue = beam;
        SerializedProperty list = flight.FindProperty("lightShafts");
        bool listed = false;
        for (int i = 0; i < list.arraySize; i++)
        {
            Object item = list.GetArrayElementAtIndex(i).objectReferenceValue;
            if (item == null || item == beam)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = beam;
                listed = true;
            }
        }
        if (!listed)
        {
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = beam;
        }
        flight.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(ending.gameObject.scene);
        return "Sleep shaft at " + foot.ToString("F1") + ", " + list.arraySize + " shafts; sleep shot " + sleep.transform.position.ToString("F1") + ", back shot " + back.transform.position.ToString("F1");
    }

    // One shaft of light (LightBeam), its material a brighter copy of the sunlit spots' beam.
    static GameObject ShaftPrefab()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ShaftPrefabPath);
        if (prefab != null)
            return prefab;
        var material = AssetDatabase.LoadAssetAtPath<Material>(ShaftMaterialPath);
        if (material == null)
        {
            var source = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(SunBeamMaterialGuid));
            material = new Material(source) { name = "EndingLightBeam" };
            material.SetFloat("_Intensity", Mathf.Max(0.45f, source.GetFloat("_Intensity")));
            AssetDatabase.CreateAsset(material, ShaftMaterialPath);
        }
        var shaft = new GameObject("EndingLightShaft");
        shaft.AddComponent<MeshFilter>();
        var renderer = shaft.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        shaft.AddComponent<LightBeam>();
        prefab = PrefabUtility.SaveAsPrefabAsset(shaft, ShaftPrefabPath);
        Object.DestroyImmediate(shaft);
        return prefab;
    }

    // Only the far shot's lens and framing, on the one already in the scene.
    // unity command script run --file AgentScripts/BuildCliffGuide.cs --entry BuildCliffGuide.ReframeFarShot
    public static string ReframeFarShot()
    {
        var cliff = Object.FindAnyObjectByType<CliffEnding>(FindObjectsInactive.Include);
        var edge = (Transform)new SerializedObject(cliff).FindProperty("edge").objectReferenceValue;
        var far = GameObject.Find("04_CUTSCENE").transform.Find("Ending/FarShot").GetComponent<CinemachineCamera>();
        Undo.RecordObjects(new Object[] { far, far.transform }, "Reframe Far Shot");
        LensSettings lens = far.Lens;
        lens.FieldOfView = FarFieldOfView;
        far.Lens = lens;
        FrameFarShot(far, edge.position);
        var flight = Object.FindAnyObjectByType<EndingFlight>(FindObjectsInactive.Include);
        var so = new SerializedObject(flight);
        so.FindProperty("farSpeed").floatValue = 3.5f;
        so.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(far.gameObject.scene);
        return "FarShot fov " + FarFieldOfView + " at " + far.transform.position.ToString("F1") + " rot " + far.transform.eulerAngles.ToString("F1");
    }

    static CinemachineCamera Shot(Transform parent, string name, Vector3 at, Vector3 lookAt, Camera main, float fieldOfView)
    {
        var shot = new GameObject(name);
        shot.transform.SetParent(parent, false);
        shot.transform.position = at;
        shot.transform.rotation = Quaternion.LookRotation(lookAt - at);
        var camera = shot.AddComponent<CinemachineCamera>();
        if (main != null)
            camera.Lens = LensSettings.FromCamera(main);
        LensSettings lens = camera.Lens;
        lens.FieldOfView = fieldOfView;
        camera.Lens = lens;
        camera.Priority.Value = 0;
        shot.SetActive(false);
        return camera;
    }

    static Transform Path(Transform parent, string name, Vector3[] points)
    {
        var path = new GameObject(name).transform;
        path.SetParent(parent, false);
        for (int i = 0; i < points.Length; i++)
        {
            var point = new GameObject("Point_" + i).transform;
            point.SetParent(path, false);
            point.position = points[i];
        }
        return path;
    }

    static void SetFlowerClearArea(StringBuilder report)
    {
        var flowers = Object.FindAnyObjectByType<FlowerGenerator>(FindObjectsInactive.Include);
        if (flowers == null)
        {
            report.Append("No FlowerGenerator. ");
            return;
        }
        Vector3 a = flowers.transform.InverseTransformPoint(new Vector3(ClearMin.x, 0f, ClearMin.y));
        Vector3 b = flowers.transform.InverseTransformPoint(new Vector3(ClearMax.x, 0f, ClearMax.y));
        var so = new SerializedObject(flowers);
        SerializedProperty areas = so.FindProperty("clearAreas");
        areas.arraySize = 1;
        SerializedProperty area = areas.GetArrayElementAtIndex(0);
        area.FindPropertyRelative("min").vector2Value = new Vector2(Mathf.Min(a.x, b.x), Mathf.Min(a.z, b.z));
        area.FindPropertyRelative("max").vector2Value = new Vector2(Mathf.Max(a.x, b.x), Mathf.Max(a.z, b.z));
        so.ApplyModifiedProperties();
        report.Append("Flower clear area set on ").Append(flowers.name).Append(" (not generated). ");
    }
}
