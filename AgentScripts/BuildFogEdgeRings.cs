using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Faint cry rings at the edges of the screen while the baby is in the fog (FogEdgeRings), in Main: a manager-like object under
// 01_SYSTEM with a copy of the squirrel hollow's ring system (CryHint's Ring, simulated in the main camera's space so the rings
// stay on the screen, on the AboveFog layer, without the bird's CryTail). Where they come up along the edge is random each time
// (the component's own values; Preview In Editor shows them outside Play Mode). Rerunning makes it again.

// unity command script run --file AgentScripts/BuildFogEdgeRings.cs --entry BuildFogEdgeRings.Run
public static class BuildFogEdgeRings
{
    const string CryHintPath = "Assets/03_Prefabs/CryHint.prefab";
    const string ObjectName = "FogEdgeRings";

    public static string Run()
    {
        FogField field = Object.FindAnyObjectByType<FogField>(FindObjectsInactive.Include);
        if (field == null)
            return "No FogField in the open scene.";
        Camera view = Camera.main;
        if (view == null)
            return "No main camera in the open scene.";
        GameObject system = GameObject.Find("01_SYSTEM");
        Transform parent = system != null ? system.transform : null;
        foreach (FogEdgeRings existing in Object.FindObjectsByType<FogEdgeRings>(FindObjectsInactive.Include))
            Object.DestroyImmediate(existing.gameObject);

        var go = new GameObject(ObjectName);
        go.transform.SetParent(parent, false);
        go.layer = LayerMask.NameToLayer("AboveFog");

        // The hollow's ring, copied out of its prefab.
        var hint = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CryHintPath));
        PrefabUtility.UnpackPrefabInstance(hint, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        Transform ringObject = hint.transform.Find("Ring");
        ringObject.SetParent(go.transform, false);
        ringObject.localPosition = Vector3.zero;
        ringObject.localRotation = Quaternion.identity;
        ringObject.gameObject.layer = go.layer;
        if (ringObject.TryGetComponent(out CryTail tail))
            Object.DestroyImmediate(tail);
        Object.DestroyImmediate(hint);
        var ring = ringObject.GetComponent<ParticleSystem>();
        ParticleSystem.MainModule main = ring.main;
        main.simulationSpace = ParticleSystemSimulationSpace.Custom;
        main.customSimulationSpace = view.transform;
        main.playOnAwake = false;
        // Big rings may take up a good part of the screen.
        ringObject.GetComponent<ParticleSystemRenderer>().maxParticleSize = 1f;

        var rings = go.AddComponent<FogEdgeRings>();
        var so = new SerializedObject(rings);
        so.FindProperty("field").objectReferenceValue = field;
        so.FindProperty("ring").objectReferenceValue = ring;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(go.scene);
        bool saved = EditorSceneManager.SaveScene(go.scene);
        return $"{ObjectName} under {(parent != null ? parent.name : "the scene root")}, rings in {view.name}'s space, layer {LayerMask.LayerToName(go.layer)}; scene saved={saved}";
    }
}
