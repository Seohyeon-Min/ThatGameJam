using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Places the snow shelter in Main once (director's order: Snow Protection Interaction). Run with Main open in Edit Mode:
// unity command script run --file AgentScripts/SetupSnowShelter.cs --entry SetupSnowShelter.Run
public static class SetupSnowShelter
{
    public static string Run()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (Application.isPlaying || scene.path != "Assets/00_Scenes/Main.unity")
            throw new System.InvalidOperationException("Open Main in Edit Mode.");
        if (Object.FindAnyObjectByType<SnowShelter>(FindObjectsInactive.Include) != null)
            throw new System.InvalidOperationException("Main already has a SnowShelter.");

        var intro = GameObject.Find("Intro").GetComponent<Stage>();
        Set(intro, so => so.FindProperty("snowShelter").boolValue = true);

        var root = new GameObject("SnowShelter");
        SceneManager_Move(root, scene);
        var shelter = root.AddComponent<SnowShelter>();

        var block = new GameObject("ShelterSnowBlock");
        block.transform.SetParent(root.transform, false);
        block.layer = LayerMask.NameToLayer("Ignore Raycast");
        var box = block.AddComponent<BoxCollider>();
        box.isTrigger = true;
        // From her feet up to the bird's sheltering height (GameTuning.birdShelterHeight).
        box.center = new Vector3(0f, 1.4f, 0f);
        box.size = new Vector3(1.8f, 2.8f, 1.8f);
        block.SetActive(false);

        Set(shelter, so => so.FindProperty("snowBlock").objectReferenceValue = block.transform);

        var snowfall = GameObject.Find("World/Snowfall").GetComponent<ParticleSystem>();
        var trigger = snowfall.trigger;
        trigger.enabled = true;
        trigger.SetCollider(0, box);
        trigger.enter = ParticleSystemOverlapAction.Kill;
        trigger.inside = ParticleSystemOverlapAction.Kill;
        trigger.exit = ParticleSystemOverlapAction.Ignore;
        trigger.outside = ParticleSystemOverlapAction.Ignore;
        PrefabUtility.RecordPrefabInstancePropertyModifications(snowfall);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return "SnowShelter placed: Intro.snowShelter on, snow block, Snowfall trigger kills inside it.";
    }

    static void SceneManager_Move(GameObject go, UnityEngine.SceneManagement.Scene scene) =>
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);

    static void Set(Object target, System.Action<SerializedObject> change)
    {
        var so = new SerializedObject(target);
        change(so);
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
