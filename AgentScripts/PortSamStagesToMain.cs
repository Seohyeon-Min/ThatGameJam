using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// One-time move of sam's stage 1 (Tommy's broken tree) and stage 2 (rabbit family) from Sam_Test into Main.
// The repeated stage 2 objects become prefabs first, in Sam_Test too. Run with Main open in Edit Mode:
// unity command script run --file AgentScripts/PortSamStagesToMain.cs --entry PortSamStagesToMain.Run
public static class PortSamStagesToMain
{
    const string SamPath = "Assets/00_Scenes/Sam_Test.unity";
    const string MainPath = "Assets/00_Scenes/Main.unity";
    const string PrefabFolder = "Assets/03_Prefabs/Stage2";

    public static string Run()
    {
        Scene main = SceneManager.GetActiveScene();
        if (Application.isPlaying || main.path != MainPath || main.isDirty)
            throw new System.InvalidOperationException("Open Main in Edit Mode with no unsaved changes.");
        var log = new List<string>();

        Scene sam = EditorSceneManager.OpenScene(SamPath, OpenSceneMode.Additive);
        if (!PrefabUtility.IsPartOfPrefabInstance(Find(sam, "Stage2/RabbitStage/DemoBranch").gameObject))
        {
            MakeStage2Prefabs(sam, log);
            EditorSceneManager.SaveScene(sam);
        }

        // Main's stage 1 devices and both routes give way to sam's.
        var oldRoots = new[] { "Stage1/FallenLog", "Stage1/Stump", "Stage1/LeaningLog", "Stage1/BabyRoute", "Stage2/BabyRoute" }
            .Select(p => Find(main, p)).ToArray();
        var incoming = CollectReferencesInto(main, oldRoots);
        foreach (Transform old in oldRoots) Object.DestroyImmediate(old.gameObject);

        // Main keeps its SunlitSpot prefab at the end of stage 2.
        Object.DestroyImmediate(Find(sam, "Stage2/RabbitStage/SunlitSpot").gameObject);
        var moved = new List<Transform>();
        foreach (string path in new[] { "Stage1/FallenLog_Art", "Stage1/LeaningLog", "Stage1/BabyRoute", "Stage2/BabyRoute", "Stage2/RabbitStage" })
            moved.Add(Move(Find(sam, path), sam, main, Find(main, path.Substring(0, path.IndexOf('/')))));

        int relinked = Relink(moved, sam, main, log);
        foreach (var (target, property, path, type) in incoming)
        {
            Object replacement = Resolve(main, path, type);
            var so = new SerializedObject(target);
            so.FindProperty(property).objectReferenceValue = replacement;
            so.ApplyModifiedPropertiesWithoutUndo();
            log.Add((replacement != null ? "relinked " : "UNRESOLVED ") + Path(((Component)target).transform) + "." + property + " -> " + path);
        }

        // The old logs were landmarks the baby glances at (PointOfInterest, weight 2); the new ones stay so.
        foreach (string path in new[] { "Stage1/FallenLog_Art", "Stage1/LeaningLog" })
        {
            var poi = Find(main, path).gameObject.AddComponent<PointOfInterest>();
            var so = new SerializedObject(poi);
            so.FindProperty("weight").floatValue = 2f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // The demonstrating bird looks like Main's bird.
        Transform bird = Find(main, "Bird/Bird-Animated");
        Transform demo = Find(main, "Stage2/RabbitStage/BranchDemonstration/DemoBird/Bird-Animated");
        foreach (var source in bird.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            var copy = demo.Find(source.transform.name).GetComponent<SkinnedMeshRenderer>();
            copy.sharedMesh = source.sharedMesh;
            copy.sharedMaterials = source.sharedMaterials;
        }

        // One-time move: if anything stayed unresolved, leave Main unsaved and restore it from git.
        if (log.Any(line => line.StartsWith("UNRESOLVED")))
            throw new System.InvalidOperationException("Unresolved references; Main not saved.\n" + string.Join("\n", log));
        EditorSceneManager.SaveScene(main);
        EditorSceneManager.CloseScene(sam, true);
        return "relinked " + relinked + " cross-scene references\n" + string.Join("\n", log);
    }

    static void MakeStage2Prefabs(Scene sam, List<string> log)
    {
        if (!AssetDatabase.IsValidFolder(PrefabFolder)) AssetDatabase.CreateFolder("Assets/03_Prefabs", "Stage2");
        string stage = "Stage2/RabbitStage/";
        GameObject branch = Prefab(Find(sam, stage + "DemoBranch").gameObject, "SnowBranch");
        GameObject bank = Prefab(Find(sam, stage + "Divider").gameObject, "Snowbank");
        GameObject kit = Prefab(Find(sam, stage + "Kit_Loaf").gameObject, "BabyRabbit");
        foreach (string name in new[] { "DemoBranch", "BypassBranch_1", "BypassBranch_2" }) Convert(Find(sam, stage + name).gameObject, branch, log);
        foreach (string name in new[] { "RightBank", "LeftOuterBank", "Divider", "LeftApproachBank", "LeftExitBank" }) Convert(Find(sam, stage + name).gameObject, bank, log);
        foreach (string name in new[] { "Kit_Flat", "Kit_Loaf", "Kit_Side" }) Convert(Find(sam, stage + name).gameObject, kit, log);
    }

    // Saves the object as a prefab whose root sits at the origin; each placed copy keeps its own transform.
    static GameObject Prefab(GameObject source, string name)
    {
        string path = PrefabFolder + "/" + name + ".prefab";
        PrefabUtility.SaveAsPrefabAsset(source, path);
        GameObject contents = PrefabUtility.LoadPrefabContents(path);
        contents.name = name;
        contents.transform.localPosition = Vector3.zero;
        contents.transform.localRotation = Quaternion.identity;
        contents.transform.localScale = Vector3.one;
        PrefabUtility.SaveAsPrefabAsset(contents, path);
        PrefabUtility.UnloadPrefabContents(contents);
        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    static void Convert(GameObject plain, GameObject prefab, List<string> log)
    {
        var settings = new ConvertToPrefabInstanceSettings
        {
            recordPropertyOverridesOfMatches = true,
            componentsNotMatchedBecomesOverride = true,
            gameObjectsNotMatchedBecomesOverride = true,
            changeRootNameToAssetName = false,
            objectMatchMode = ObjectMatchMode.ByHierarchy,
        };
        PrefabUtility.ConvertToPrefabInstance(plain, prefab, settings, InteractionMode.AutomatedAction);
        log.Add("prefab " + prefab.name + " <- " + plain.name);
    }

    static Transform Move(Transform source, Scene from, Scene to, Transform parent)
    {
        source.SetParent(null, true);
        SceneManager.MoveGameObjectToScene(source.gameObject, to);
        source.SetParent(parent, true);
        return source;
    }

    // References from Main into the objects about to be replaced, remembered by path to point at the new objects of the same name.
    static List<(Object, string, string, System.Type)> CollectReferencesInto(Scene scene, Transform[] roots)
    {
        var found = new List<(Object, string, string, System.Type)>();
        foreach (var component in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Component>(true)))
        {
            // Transforms list their children; the hierarchy itself is rebuilt by the move.
            if (component == null || component is Transform || roots.Any(r => component.transform.IsChildOf(r))) continue;
            var it = new SerializedObject(component).GetIterator();
            while (it.Next(true))
            {
                if (it.propertyType != SerializedPropertyType.ObjectReference || it.objectReferenceValue == null) continue;
                Transform target = it.objectReferenceValue is Component c ? c.transform : (it.objectReferenceValue as GameObject)?.transform;
                if (target != null && roots.Any(r => target.IsChildOf(r)))
                    found.Add((component, it.propertyPath, Path(target), it.objectReferenceValue.GetType()));
            }
        }
        return found;
    }

    // Moved objects that still point into Sam_Test (GameFlow's Season, for example) are pointed at Main's object of the same path.
    static int Relink(List<Transform> moved, Scene sam, Scene main, List<string> log)
    {
        int count = 0;
        foreach (var component in moved.SelectMany(t => t.GetComponentsInChildren<Component>(true)))
        {
            if (component == null) continue;
            var so = new SerializedObject(component);
            var it = so.GetIterator();
            while (it.Next(true))
            {
                if (it.propertyType != SerializedPropertyType.ObjectReference || it.objectReferenceValue == null) continue;
                Object value = it.objectReferenceValue;
                GameObject owner = value is Component c ? c.gameObject : value as GameObject;
                if (owner == null || owner.scene != sam) continue;
                Object replacement = Resolve(main, Path(owner.transform), value.GetType());
                it.objectReferenceValue = replacement;
                log.Add((replacement != null ? "relinked " : "UNRESOLVED ") + Path(component.transform) + "." + it.propertyPath + " -> " + Path(owner.transform));
                count++;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        return count;
    }

    static Object Resolve(Scene scene, string path, System.Type type)
    {
        Transform t = Find(scene, path, false);
        if (t == null) return null;
        if (type == typeof(GameObject)) return t.gameObject;
        if (type == typeof(Transform)) return t;
        return t.GetComponent(type);
    }

    static Transform Find(Scene scene, string path, bool required = true)
    {
        string[] parts = path.Split(new[] { '/' }, 2);
        GameObject root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == parts[0]);
        Transform found = root == null ? null : parts.Length == 1 ? root.transform : root.transform.Find(parts[1]);
        if (found == null && required) throw new System.InvalidOperationException(scene.name + " has no " + path);
        return found;
    }

    static string Path(Transform t)
    {
        string path = t.name;
        while (t.parent != null) { t = t.parent; path = t.name + "/" + path; }
        return path;
    }
}
