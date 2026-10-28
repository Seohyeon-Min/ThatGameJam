using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Builds Assets/00_Scenes/Seohyeon_Watercolor_Test.unity: a small forest path with the NPC girl, every object using
// ThatGameJam/WatercolorFlat materials, and a global Volume with the WatercolorBlur post effect on.
// The girl, trees, bushes and rocks get a WatercolorPaintedModel and are baked (mass normals + per-model pattern),
// so same-coloured pieces of one model read as one painted mass. Bake output goes to Assets/01_Arts/Baked/.
// Shared assets (Tree.prefab, Test materials, the girl FBX) are not modified; materials are overridden per instance.
// Run: unity command script run --file AgentScripts/BuildWatercolorTestScene.cs --entry BuildWatercolorTestScene.Build
public static class BuildWatercolorTestScene
{
    const string ScenePath = "Assets/00_Scenes/Seohyeon_Watercolor_Test.unity";
    const string MaterialFolder = "Assets/01_Arts/Materials/Watercolor";
    const string ProfilePath = "Assets/04_Data/WatercolorVolumeProfile.asset";

    static System.Random s_Random;
    static float Range(float a, float b) => a + (float)s_Random.NextDouble() * (b - a);

    public static string Build()
    {
        s_Random = new System.Random(1842);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        if (!AssetDatabase.IsValidFolder(MaterialFolder))
            AssetDatabase.CreateFolder("Assets/01_Arts/Materials", "Watercolor");

        var grass = Mat("Grass", new Color(0.55f, 0.68f, 0.38f), new Color(0.45f, 0.55f, 0.75f), floor: true);
        var path = Mat("Path", new Color(0.86f, 0.76f, 0.58f), new Color(0.6f, 0.58f, 0.75f), floor: true);
        var trunk = Mat("Trunk", new Color(0.5f, 0.36f, 0.26f), new Color(0.4f, 0.38f, 0.6f));
        var crown = Mat("Crown", new Color(0.36f, 0.6f, 0.36f), new Color(0.3f, 0.42f, 0.62f));
        var bush = Mat("Bush", new Color(0.42f, 0.66f, 0.4f), new Color(0.32f, 0.45f, 0.65f));
        var rock = Mat("Rock", new Color(0.72f, 0.72f, 0.76f), new Color(0.5f, 0.52f, 0.72f));
        var flowerColors = new[]
        {
            Mat("FlowerRed", new Color(0.92f, 0.42f, 0.4f), new Color(0.6f, 0.4f, 0.7f)),
            Mat("FlowerYellow", new Color(0.98f, 0.86f, 0.42f), new Color(0.7f, 0.6f, 0.7f)),
            Mat("FlowerViolet", new Color(0.66f, 0.52f, 0.9f), new Color(0.45f, 0.42f, 0.75f)),
            Mat("FlowerWhite", new Color(0.97f, 0.96f, 0.93f), new Color(0.65f, 0.68f, 0.85f)),
        };

        // Light and camera. The girl faces -X, so the camera stands on that side looking back at her and the path.
        var lightGo = new GameObject("Directional Light");
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.96f, 0.88f);
        light.intensity = 1.1f;
        light.shadows = LightShadows.Soft;
        lightGo.transform.rotation = Quaternion.Euler(45f, 60f, 0f);

        var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
        var camera = cameraGo.AddComponent<Camera>();
        camera.fieldOfView = 45f;
        cameraGo.AddComponent<AudioListener>();
        camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        cameraGo.transform.position = new Vector3(-8f, 3f, -3.5f);
        cameraGo.transform.LookAt(new Vector3(0f, 1.6f, 2f));

        // Ground and a winding path along +Z.
        var ground = Primitive(PrimitiveType.Plane, "Ground", new Vector3(0f, 0f, 6f), new Vector3(30f, 1f, 30f), grass);
        ground.isStatic = true;
        for (int i = 0; i < 9; i++)
        {
            float z = -4f + i * 2.4f;
            float x = Mathf.Sin(z * 0.25f) * 1.2f;
            var stone = Primitive(PrimitiveType.Cylinder, $"Path_{i}", new Vector3(x, 0.005f, z), new Vector3(2.4f, 0.005f, 2.8f), path);
            Object.DestroyImmediate(stone.GetComponent<Collider>());
        }

        // Trees from the shared prefab, with watercolor materials overridden on each instance.
        var treePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/Tree.prefab");
        var treeSpots = new[]
        {
            new Vector3(3.2f, 0f, 1.5f), new Vector3(-3.4f, 0f, 4.5f), new Vector3(3.8f, 0f, 7f), new Vector3(-2.8f, 0f, 10f),
            new Vector3(4.5f, 0f, 12.5f), new Vector3(-4.6f, 0f, 14f), new Vector3(2.2f, 0f, 16f), new Vector3(6.5f, 0f, 4f),
        };
        var treesRoot = new GameObject("Trees");
        foreach (var spot in treeSpots)
        {
            var tree = (GameObject)PrefabUtility.InstantiatePrefab(treePrefab);
            tree.transform.SetParent(treesRoot.transform);
            tree.transform.position = spot;
            tree.transform.rotation = Quaternion.Euler(0f, Range(0f, 360f), 0f);
            tree.transform.localScale = Vector3.one * Range(0.85f, 1.15f);
            foreach (var r in tree.GetComponentsInChildren<Renderer>())
            {
                if (r.name == "Crown")
                    r.sharedMaterial = crown;
                else if (r.name == "Indicator")
                    r.enabled = false; // Gameplay perch marker; hidden in this look-dev scene.
                else
                    r.sharedMaterial = trunk;
            }
        }

        // Bushes, rocks and flowers along the path edges.
        var propsRoot = new GameObject("Props");
        for (int i = 0; i < 14; i++)
        {
            var p = EdgePoint();
            var b = Primitive(PrimitiveType.Sphere, $"Bush_{i}", p + Vector3.up * 0.25f, new Vector3(Range(0.8f, 1.4f), Range(0.5f, 0.8f), Range(0.8f, 1.4f)), bush);
            b.transform.SetParent(propsRoot.transform);
        }
        for (int i = 0; i < 8; i++)
        {
            var p = EdgePoint();
            var r = Primitive(PrimitiveType.Sphere, $"Rock_{i}", p + Vector3.up * 0.08f, new Vector3(Range(0.4f, 0.8f), Range(0.25f, 0.4f), Range(0.4f, 0.7f)), rock);
            r.transform.rotation = Quaternion.Euler(0f, Range(0f, 360f), Range(-10f, 10f));
            r.transform.SetParent(propsRoot.transform);
        }
        var flowersRoot = new GameObject("Flowers");
        for (int i = 0; i < 70; i++)
        {
            var p = EdgePoint();
            var f = Primitive(PrimitiveType.Sphere, $"Flower_{i}", p + Vector3.up * 0.08f, Vector3.one * Range(0.12f, 0.2f), flowerColors[s_Random.Next(flowerColors.Length)]);
            Object.DestroyImmediate(f.GetComponent<Collider>());
            f.transform.SetParent(flowersRoot.transform);
        }

        // The NPC girl on the path, facing the camera side (-X).
        string girlInfo = AddGirl();

        // One master manager for the watercolor style of every WatercolorFlat material.
        new GameObject("Watercolor Manager").AddComponent<WatercolorManager>();

        // Global Volume with the bilateral blur on.
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
            var blur = profile.Add<WatercolorBlur>(true);
            blur.enableBlur.value = true;
            AssetDatabase.AddObjectToAsset(blur, profile);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
        }
        var volumeGo = new GameObject("Global Volume");
        var volume = volumeGo.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.sharedProfile = profile;

        // Soft ambient so watercolor shadows keep their colour instead of going grey.
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.62f, 0.68f, 0.78f);
        RenderSettings.ambientEquatorColor = new Color(0.55f, 0.58f, 0.52f);
        RenderSettings.ambientGroundColor = new Color(0.38f, 0.37f, 0.32f);

        // Save first: bake folders are named after scene object ids, which are only stable once the scene is saved.
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.SaveScene(scene, ScenePath);
        var baked = new System.Text.StringBuilder();
        var models = new List<GameObject>();
        foreach (Transform t in treesRoot.transform) models.Add(t.gameObject);
        foreach (Transform t in propsRoot.transform) models.Add(t.gameObject);
        models.Add(GameObject.Find("NPC-Girl-Unity-Skirt"));
        foreach (var go in models)
        {
            var model = go.GetComponent<WatercolorPaintedModel>() ?? go.AddComponent<WatercolorPaintedModel>();
            baked.AppendLine(WatercolorBaker.Bake(model));
        }
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        return $"saved {ScenePath}; {girlInfo}" + System.Environment.NewLine + baked;
    }

    static Vector3 EdgePoint()
    {
        float z = Range(-3f, 17f);
        float pathX = Mathf.Sin(z * 0.25f) * 1.2f;
        float side = s_Random.Next(2) == 0 ? -1f : 1f;
        return new Vector3(pathX + side * Range(1.5f, 4f), 0f, z);
    }

    static string AddGirl()
    {
        var girlAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/01_Arts/NPCGirl/NPC-Girl-Unity-Skirt.fbx");
        if (girlAsset == null)
            return "girl FBX not found";
        var girl = (GameObject)PrefabUtility.InstantiatePrefab(girlAsset);
        girl.transform.position = new Vector3(0f, 0f, 0.4f);
        girl.transform.localScale = Vector3.one * 1.4f;
        // The FBX carries Blender's camera and a point light (intensity 1000). Left enabled, the camera also renders
        // to the Game view and the light blows nearby objects out to white.
        foreach (var c in girl.GetComponentsInChildren<Camera>())
            c.enabled = false;
        foreach (var l in girl.GetComponentsInChildren<Light>())
            l.enabled = false;

        // The FBX ships without its textures, so each part gets a flat watercolor colour by name.
        var skin = Mat("GirlSkin", new Color(0.98f, 0.84f, 0.74f), new Color(0.85f, 0.6f, 0.65f));
        var dress = Mat("GirlDress", new Color(0.95f, 0.62f, 0.6f), new Color(0.6f, 0.45f, 0.7f));
        var sleeve = Mat("GirlSleeve", new Color(0.98f, 0.95f, 0.88f), new Color(0.7f, 0.7f, 0.85f));
        var sash = Mat("GirlSash", new Color(0.45f, 0.62f, 0.88f), new Color(0.35f, 0.4f, 0.7f));
        var hair = Mat("GirlHair", new Color(0.45f, 0.3f, 0.22f), new Color(0.35f, 0.3f, 0.5f));
        var face = Mat("GirlFace", new Color(0.18f, 0.14f, 0.14f), new Color(0.3f, 0.25f, 0.35f));

        int count = 0;
        foreach (var r in girl.GetComponentsInChildren<Renderer>())
        {
            string n = r.name;
            if (n.StartsWith("WGT_"))
            {
                r.enabled = false; // Rig control widgets exported from Blender.
                continue;
            }
            Material m =
                n.StartsWith("NurbsPath") ? hair :
                n == "Dress_Petal" ? dress :
                n.StartsWith("Puff_Sleeve") ? sleeve :
                n == "Waist_Blue" ? sash :
                n == "Circle" || n.StartsWith("Plane") ? face :
                skin;
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
                mats[i] = m;
            r.sharedMaterials = mats;
            count++;
        }
        return $"girl renderers: {count}";
    }

    // floor: wide flat surfaces (no paper area, outline or boundary band; see WatercolorFlat).
    // shadowColor is unused now that shadows are derived from the base colour; kept so the colour table stays readable.
    static Material Mat(string name, Color baseColor, Color shadowColor, bool floor = false)
    {
        string path = $"{MaterialFolder}/WC_{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("ThatGameJam/WatercolorFlat"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.shader = Shader.Find("ThatGameJam/WatercolorFlat");
        m.SetColor("_BaseColor", baseColor);
        m.SetColor("_ShadowColor", shadowColor);
        m.SetFloat("_Floor", floor ? 1f : 0f);
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        return m;
    }

    static GameObject Primitive(PrimitiveType type, string name, Vector3 position, Vector3 scale, Material material)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.position = position;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = material;
        return go;
    }
}
