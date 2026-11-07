using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// The light breeze at the cliff, in Main: blows past the girl, from behind where she sits out over the drop, only while she
// sits there (CliffEnding plays and stops it). Thin curling wind lines (particle trails) and a few pale petals. Makes the
// material and the prefab, places it under CliffEnding and wires it in. Rerunning makes them again and puts it in place again.

// unity command script run --file AgentScripts/BuildCliffBreeze.cs --entry BuildCliffBreeze.Run
public static class BuildCliffBreeze
{
    const string MaterialPath = "Assets/01_Arts/Materials/vfx/BreezeWisp.mat";
    const string TexturePath = "Assets/01_Arts/Textures/SnowFlake.png";
    const string PrefabPath = "Assets/03_Prefabs/Stage3/CliffBreeze.prefab";
    // The box the breeze starts in: centred this far behind where she sits (toward the maze), this wide, deep and tall.
    const float Behind = 3f;
    static readonly Vector3 BoxSize = new(14f, 1.6f, 7f);
    const float BoxHeight = 1.1f;

    public static string Run()
    {
        var report = new StringBuilder();
        var cliff = Object.FindAnyObjectByType<CliffEnding>(FindObjectsInactive.Include);
        if (cliff == null)
            return "No CliffEnding in the open scene.";
        var cliffSo = new SerializedObject(cliff);
        var path = (Transform)cliffSo.FindProperty("path").objectReferenceValue;
        var edge = (Transform)cliffSo.FindProperty("edge").objectReferenceValue;

        Material material = MakeMaterial(report);
        GameObject prefab = MakePrefab(material, report);

        Transform old = cliff.transform.Find("CliffBreeze");
        if (old != null)
            Object.DestroyImmediate(old.gameObject);
        Vector3 seat = path.GetChild(path.childCount - 1).position;
        Vector3 forward = Vector3.ProjectOnPlane(edge.forward, Vector3.up).normalized;
        Vector3 at = seat - forward * Behind;
        Terrain terrain = Terrain.activeTerrain;
        if (terrain != null)
            at.y = terrain.SampleHeight(at) + terrain.transform.position.y;
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, cliff.transform);
        instance.name = "CliffBreeze";
        instance.transform.SetPositionAndRotation(at, Quaternion.LookRotation(forward));
        PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
        report.AppendLine($"Placed CliffBreeze at {at}, blowing along {forward}.");

        cliffSo.FindProperty("breeze").objectReferenceValue = instance.GetComponent<ParticleSystem>();
        cliffSo.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(cliff.gameObject.scene);
        return report.ToString();
    }

    static Material MakeMaterial(StringBuilder report)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("ThatGameJam/SnowParticle"));
            AssetDatabase.CreateAsset(material, MaterialPath);
            report.AppendLine("Made " + MaterialPath);
        }
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath));
        material.SetColor("_BaseColor", Color.white);
        // No distance tint: the breeze is near the camera's point; only fade what comes right up to the lens.
        material.SetFloat("_FarStart", 80f);
        material.SetFloat("_FarEnd", 160f);
        material.SetFloat("_FarAlpha", 1f);
        material.SetFloat("_NearFadeStart", 1f);
        material.SetFloat("_NearFadeEnd", 3f);
        EditorUtility.SetDirty(material);
        return material;
    }

    static GameObject MakePrefab(Material material, StringBuilder report)
    {
        var root = new GameObject("CliffBreeze");
        ParticleSystem lines = root.AddComponent<ParticleSystem>();
        SetUpLines(lines, material);
        var petalsGo = new GameObject("Petals");
        petalsGo.transform.SetParent(root.transform, false);
        SetUpPetals(petalsGo.AddComponent<ParticleSystem>(), material);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        report.AppendLine("Saved " + PrefabPath);
        return prefab;
    }

    // Shared by both: starts nowhere until played, lives in the world, starts anywhere in the box behind her and moves along
    // the prefab's forward (out over the drop), wandering on a slow noise.
    static void SetUpCommon(ParticleSystem system, int maxParticles, float rate, float noise)
    {
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = system.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = 5f;
        main.prewarm = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = maxParticles;

        var emission = system.emission;
        emission.rateOverTime = rate;

        var shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = BoxSize;
        shape.position = new Vector3(0f, BoxHeight, 0f);

        var drift = system.noise;
        drift.enabled = true;
        drift.strength = noise;
        drift.frequency = 0.3f;
        drift.scrollSpeed = 0.25f;
        drift.octaveCount = 1;
        drift.quality = ParticleSystemNoiseQuality.Medium;

        // A slight lift as it goes, more so out past the edge.
        var velocity = system.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(0f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.4f, AnimationCurve.EaseInOut(0f, 0f, 1f, 1f));
        velocity.z = new ParticleSystem.MinMaxCurve(0f);
    }

    static Gradient FadeInOut(Color a, float alpha)
    {
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(a, 0f), new GradientColorKey(a, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(alpha, 0.25f), new GradientAlphaKey(alpha, 0.7f), new GradientAlphaKey(0f, 1f) });
        return gradient;
    }

    // Thin white wind lines: the particles themselves are not drawn, only their trails, which curl with the noise.
    static void SetUpLines(ParticleSystem system, Material material)
    {
        SetUpCommon(system, 24, 2.5f, 0.6f);
        var main = system.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 3.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.6f, 3.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.13f);
        main.startColor = Color.white;

        var fade = system.colorOverLifetime;
        fade.enabled = true;
        fade.color = FadeInOut(Color.white, 0.7f);

        var trails = system.trails;
        trails.enabled = true;
        trails.mode = ParticleSystemTrailMode.PerParticle;
        trails.ratio = 1f;
        trails.lifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.28f);
        trails.minVertexDistance = 0.12f;
        trails.worldSpace = true;
        trails.dieWithParticles = true;
        trails.sizeAffectsWidth = true;
        trails.inheritParticleColor = true;
        trails.textureMode = ParticleSystemTrailTextureMode.Stretch;
        trails.widthOverTrail = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0f), new Keyframe(0.35f, 1f), new Keyframe(1f, 0f)));
        var trailFade = new Gradient();
        trailFade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.4f), new GradientAlphaKey(0f, 1f) });
        trails.colorOverTrail = trailFade;

        var renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.None;
        renderer.sharedMaterial = material;
        renderer.trailMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    // A few pale petals tumbling along with it.
    static void SetUpPetals(ParticleSystem system, Material material)
    {
        SetUpCommon(system, 30, 2f, 0.45f);
        var main = system.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.4f, 2.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.09f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 2f * Mathf.PI);
        main.startColor = new ParticleSystem.MinMaxGradient(Color.white, new Color(1f, 0.86f, 0.9f));
        main.gravityModifier = 0.01f;

        var spin = system.rotationOverLifetime;
        spin.enabled = true;
        spin.z = new ParticleSystem.MinMaxCurve(-1.6f, 1.6f);

        var fade = system.colorOverLifetime;
        fade.enabled = true;
        fade.color = FadeInOut(Color.white, 0.9f);

        var renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }
}
