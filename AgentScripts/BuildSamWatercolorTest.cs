using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Builds Seohyeon_SamWatercolor_Test.unity: a copy of Sam's game scene (Sam_Test.unity, as it is in the editor) painted
// with the watercolor look. Sam's scene and materials stay as they are; Sam's builder rebuilds Sam_Test.unity, so the
// look lives in this copy.
// - a Watercolor Manager with the values of the watercolor test scene, the watercolor Volume and the watercolor sky;
// - every tree, the baby, the bird and the stage props get a WatercolorPaintedModel and are baked (same models share a bake);
// - the terrain gets a watercolor floor material, the streams a WatercolorWater surface (flowing along +X).
// Run with Sam_Test.unity open: unity command script run --file AgentScripts/BuildSamWatercolorTest.cs --entry BuildSamWatercolorTest.Build
public static class BuildSamWatercolorTest
{
    const string SourceScenePath = "Assets/00_Scenes/Sam_Test.unity";
    const string ScenePath = "Assets/00_Scenes/Seohyeon_SamWatercolor_Test.unity";
    const string StyleScenePath = "Assets/00_Scenes/Seohyeon_Watercolor_Test.unity";
    const string MaterialFolder = "Assets/01_Arts/Materials/SamWatercolor";
    const string VolumeProfilePath = "Assets/04_Data/WatercolorVolumeProfile.asset";
    const string SkyPath = "Assets/01_Arts/Materials/WatercolorSky.mat";
    const string WaterPath = "Assets/01_Arts/Materials/WatercolorWater.mat";
    // Stage children that are not painted models (route points, triggers, the glowing rest light).
    static readonly HashSet<string> NotModels = new() { "BabyRoute", "SunlitSpot", "BabyStart", "BirdStart", "CameraZone_LeaningLog", "ClickMarker" };

    public static string Build()
    {
        var source = EditorSceneManager.GetActiveScene();
        if (source.path != SourceScenePath)
            return "Open " + SourceScenePath + " first";

        // The tuned manager values of the watercolor test scene.
        string managerJson = null;
        var styleScene = EditorSceneManager.OpenScene(StyleScenePath, OpenSceneMode.Additive);
        foreach (var root in styleScene.GetRootGameObjects())
            if (root.GetComponentInChildren<WatercolorManager>() is WatercolorManager tuned)
                managerJson = EditorJsonUtility.ToJson(tuned);
        EditorSceneManager.CloseScene(styleScene, true);

        EditorSceneManager.SaveScene(source, ScenePath, true);
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!AssetDatabase.IsValidFolder(MaterialFolder))
            AssetDatabase.CreateFolder("Assets/01_Arts/Materials", "SamWatercolor");

        var managerObject = new GameObject("Watercolor Manager");
        var manager = managerObject.AddComponent<WatercolorManager>();
        if (managerJson != null)
            EditorJsonUtility.FromJsonOverwrite(managerJson, manager);
        manager.settings.formLight = null;

        var volume = new GameObject("Watercolor Volume").AddComponent<Volume>();
        volume.isGlobal = true;
        volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
        Camera.main.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>(SkyPath);

        var models = new List<GameObject>();
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == "Baby" || root.name == "Bird")
                models.Add(root);
            else if (root.name == "Intro" || root.name.StartsWith("Stage") || root.name == "World")
                CollectModels(root.transform, models, root.name != "World");
        }
        foreach (var model in models)
        {
            var painted = model.GetComponent<WatercolorPaintedModel>() ?? model.AddComponent<WatercolorPaintedModel>();
            painted.patternResolution = 32;
        }

        string world = PaintWorld(scene);
        WatercolorBaker.BakeAll();
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        return $"{ScenePath}: {models.Count} models baked; {world}";
    }

    // Trees are the children of groups with "Trees" in the name (groups can nest). Other stage children with renderers are props.
    static void CollectModels(Transform parent, List<GameObject> models, bool props)
    {
        foreach (Transform child in parent)
        {
            if (child.name.Contains("Trees"))
                CollectModels(child, models, false);
            else if (parent.name.Contains("Trees"))
                models.Add(child.gameObject);
            else if (props && !NotModels.Contains(child.name) && child.GetComponentInChildren<Renderer>() != null)
                models.Add(child.gameObject);
        }
    }

    static string PaintWorld(UnityEngine.SceneManagement.Scene scene)
    {
        Shader flat = Shader.Find("ThatGameJam/WatercolorFlat");
        string log = "";
        foreach (var terrain in Object.FindObjectsByType<Terrain>())
        {
            Material template = terrain.materialTemplate;
            var floor = SaveMaterial(new Material(flat), "SnowFloor");
            floor.SetColor("_BaseColor", template != null && template.HasProperty("_BaseColor") ? template.GetColor("_BaseColor") : Color.white);
            floor.SetFloat("_Floor", 1f);
            // Set after CreateAsset, so mark it dirty or the values are lost on the next reload.
            EditorUtility.SetDirty(floor);
            terrain.materialTemplate = floor;
            terrain.drawInstanced = false;
            log += "terrain floor, ";
        }

        var water = SaveMaterial(new Material(AssetDatabase.LoadAssetAtPath<Material>(WaterPath)), "Water");
        water.SetVector("_FlowDirection", new Vector4(1f, 0f, 0f, 0f));
        water.SetFloat("_ShoreRagged", 0.5f);
        EditorUtility.SetDirty(water);
        foreach (var root in scene.GetRootGameObjects())
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
            {
                if (!renderer.name.StartsWith("Stream"))
                    continue;
                // The stream is a flat box; a plane on its top face carries the water (the box keeps its collider).
                Bounds bounds = renderer.bounds;
                var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
                plane.name = renderer.name + "_Water";
                Object.DestroyImmediate(plane.GetComponent<Collider>());
                plane.transform.SetParent(renderer.transform.parent, false);
                plane.transform.position = new Vector3(bounds.center.x, bounds.max.y + 0.01f, bounds.center.z);
                plane.transform.localScale = new Vector3(bounds.size.x / 10f, 1f, bounds.size.z / 10f);
                var planeRenderer = plane.GetComponent<MeshRenderer>();
                planeRenderer.sharedMaterial = water;
                planeRenderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.enabled = false;
                log += plane.name + ", ";
            }
        return log;
    }

    static Material SaveMaterial(Material material, string name)
    {
        string path = $"{MaterialFolder}/{name}.mat";
        AssetDatabase.DeleteAsset(path);
        material.name = name;
        AssetDatabase.CreateAsset(material, path);
        return material;
    }
}
