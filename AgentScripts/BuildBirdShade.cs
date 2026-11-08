using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// The soft dark spot behind the bird at the cliff (BirdShade): a quad with the SoftShade material, beside the bird under 02_WORLD
// (not under it, so the ending's ScaleBy leaves it alone), wired to CliffEnding. Rerunning keeps the material's values and
// replaces the object.
// unity command script run --file AgentScripts/BuildBirdShade.cs --entry BuildBirdShade.Run
public static class BuildBirdShade
{
    const string ShaderName = "ThatGameJam/SoftShade";
    const string MaterialPath = "Assets/01_Arts/Materials/BirdShade.mat";

    public static string Run()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
                return "no shader " + ShaderName;
            material = new Material(shader) { name = "BirdShade" };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }

        BirdController bird = Object.FindAnyObjectByType<BirdController>(FindObjectsInactive.Include);
        CliffEnding cliff = Object.FindAnyObjectByType<CliffEnding>(FindObjectsInactive.Include);
        if (bird == null || cliff == null)
            return "no bird or no cliff ending in the open scene";
        Transform parent = bird.transform.parent;

        Transform old = parent.Find("BirdShade");
        if (old != null)
            Object.DestroyImmediate(old.gameObject);

        var go = new GameObject("BirdShade", typeof(MeshFilter), typeof(MeshRenderer), typeof(BirdShade));
        go.transform.SetParent(parent, false);
        go.transform.SetSiblingIndex(bird.transform.GetSiblingIndex() + 1);
        go.transform.position = bird.transform.position;
        go.GetComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.enabled = false;

        var cliffSo = new SerializedObject(cliff);
        cliffSo.FindProperty("birdShade").objectReferenceValue = go.GetComponent<BirdShade>();
        cliffSo.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(go.scene);
        return "ok: BirdShade under " + parent.name + ", material " + MaterialPath;
    }
}
