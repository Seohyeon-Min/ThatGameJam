using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Adds a stream with the ThatGameJam/WatercolorWater shader to the open watercolor test scene, beside the existing set,
// and a "Water Camera" (off) looking at it with the main camera's settings. Run again to rebuild it; the rest of the
// scene and the main camera are left alone. The scene is marked dirty, not saved.
// Run: unity command script run --file AgentScripts/BuildWaterTest.cs --entry BuildWaterTest.Build
public static class BuildWaterTest
{
    const string ScenePath = "Assets/00_Scenes/Seohyeon_Watercolor_Test.unity";
    const string MaterialPath = "Assets/01_Arts/Materials/WatercolorWater.mat";
    static readonly Vector3 Center = new(14f, 0.02f, 6f);
    // A Unity Plane is 10 x 10; this makes the stream 4 m wide and 16 m long, running along +Z.
    static readonly Vector3 PlaneScale = new(0.4f, 1f, 1.6f);
    // On the far side from the test set (no trees in the way), a little upstream, so the whole length is in view.
    static readonly Vector3 CameraOffset = new(8f, 4.5f, -4f);

    public static string Build()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
            return "Open " + ScenePath + " first";

        foreach (var root in scene.GetRootGameObjects())
            if (root.name == "Water Test")
                Object.DestroyImmediate(root);

        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("ThatGameJam/WatercolorWater"));
            AssetDatabase.CreateAsset(material, MaterialPath);
        }

        var group = new GameObject("Water Test");
        var water = GameObject.CreatePrimitive(PrimitiveType.Plane);
        water.name = "Stream";
        Object.DestroyImmediate(water.GetComponent<Collider>());
        water.transform.SetParent(group.transform, false);
        water.transform.position = Center;
        water.transform.localScale = PlaneScale;
        var renderer = water.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // Same lens and post-processing as the main camera; off, so the main camera keeps the Game view.
        var cameraObject = new GameObject("Water Camera");
        cameraObject.transform.SetParent(group.transform, false);
        var camera = cameraObject.AddComponent<Camera>();
        camera.CopyFrom(Camera.main);
        camera.tag = "Untagged";
        cameraObject.transform.position = Center + CameraOffset;
        cameraObject.transform.LookAt(Center + Vector3.up * 0.5f);
        camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        camera.enabled = false;

        EditorSceneManager.MarkSceneDirty(scene);
        return "built " + group.name;
    }
}
