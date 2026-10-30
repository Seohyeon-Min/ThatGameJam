#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// One-off: swaps the old cylinder SunlitSpots in the open scene for the SunlitSpot prefab, with its foot on the ground where
// the cylinder's bottom was. Scenes the builder made before it used the prefab need this; undo with Ctrl+Z.
// The second menu adds the ring child to the prefab, since prefab files aren't edited by hand while the Editor is open.
// Delete this file once every scene is done.
public static class SunlitSpotReplacer
{
    const string PrefabPath = "Assets/03_Prefabs/SunlitSpot.prefab";
    const string RingMaterialPath = "Assets/01_Arts/Materials/vfx/SpringRing.mat";
    const string SnowfallPrefabPath = "Assets/03_Prefabs/Snowfall.prefab";
    const string SnowMaterialPath = "Assets/01_Arts/Materials/Snow.mat";
    const string OldWispMaterialPath = "Assets/01_Arts/Resources/SnowWisps.mat";
    const string WispMaterialPath = "Assets/01_Arts/Materials/vfx/SnowWisps.mat";

    // Moves the wisp material out of Resources, sets the snow's materials on the Snowfall prefab, and in the open scene removes
    // a SeasonSnow the builder added twice and fills in what's still empty. Save the scene afterwards.
    [MenuItem("Tools/Connect Snow Clearing")]
    static void ConnectSnowClearing()
    {
        if (AssetDatabase.LoadAssetAtPath<Material>(OldWispMaterialPath) != null)
        {
            string error = AssetDatabase.MoveAsset(OldWispMaterialPath, WispMaterialPath);
            if (!string.IsNullOrEmpty(error))
            {
                Debug.LogError("SunlitSpotReplacer: couldn't move the wisp material: " + error);
                return;
            }
        }
        if (AssetDatabase.IsValidFolder("Assets/01_Arts/Resources") && AssetDatabase.FindAssets("", new[] { "Assets/01_Arts/Resources" }).Length == 0)
            AssetDatabase.DeleteAsset("Assets/01_Arts/Resources");

        var wispMaterial = AssetDatabase.LoadAssetAtPath<Material>(WispMaterialPath);
        var snowMaterial = AssetDatabase.LoadAssetAtPath<Material>(SnowMaterialPath);
        if (wispMaterial == null || snowMaterial == null)
        {
            Debug.LogError("SunlitSpotReplacer: wisp or snow material not found.");
            return;
        }

        using (var scope = new PrefabUtility.EditPrefabContentsScope(SnowfallPrefabPath))
        {
            foreach (SeasonSnow seasonSnow in scope.prefabContentsRoot.GetComponentsInChildren<SeasonSnow>(true))
                SetObject(seasonSnow, "wispMaterial", wispMaterial);
            foreach (Snowfall snowfall in scope.prefabContentsRoot.GetComponentsInChildren<Snowfall>(true))
                SetObject(snowfall, "snowMaterial", snowMaterial);
        }

        var scene = EditorSceneManager.GetActiveScene();
        Season season = Object.FindAnyObjectByType<Season>();
        int removed = 0;
        int found = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Snowfall snowfall in root.GetComponentsInChildren<Snowfall>(true))
            {
                SeasonSnow[] seasonSnows = snowfall.GetComponents<SeasonSnow>();
                int left = seasonSnows.Length;
                foreach (SeasonSnow extra in seasonSnows)
                {
                    if (left > 1 && PrefabUtility.IsAddedComponentOverride(extra))
                    {
                        Undo.DestroyObjectImmediate(extra);
                        left--;
                        removed++;
                    }
                }
                SeasonSnow kept = snowfall.GetComponent<SeasonSnow>();
                if (kept == null)
                    kept = Undo.AddComponent<SeasonSnow>(snowfall.gameObject);
                found++;
                FillIfEmpty(kept, "season", season);
                FillIfEmpty(kept, "wispMaterial", wispMaterial);
                FillIfEmpty(snowfall, "snowMaterial", snowMaterial);
            }
        }
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"SunlitSpotReplacer: connected {found} snowfall(s) in {scene.name}, removed {removed} extra SeasonSnow. Save the scene to keep it.");
    }

    static void SetObject(Object target, string property, Object value)
    {
        var serialized = new SerializedObject(target);
        serialized.FindProperty(property).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static void FillIfEmpty(Object target, string property, Object value)
    {
        var serialized = new SerializedObject(target);
        SerializedProperty field = serialized.FindProperty(property);
        if (field.objectReferenceValue != null)
            return;
        field.objectReferenceValue = value;
        serialized.ApplyModifiedProperties();
    }

    // Registers SeasonWipeFeature on the PC and Mobile renderers. Mobile is the WebGL quality level.
    [MenuItem("Tools/Add Season Wipe To Renderers")]
    static void AddSeasonWipe()
    {
        Shader shader = Shader.Find("Hidden/ThatGameJam/SeasonWipe");
        if (shader == null)
        {
            Debug.LogError("SunlitSpotReplacer: SeasonWipe shader not found.");
            return;
        }
        foreach (string path in new[] { "Assets/Settings/PC_Renderer.asset", "Assets/Settings/Mobile_Renderer.asset" })
        {
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
            if (data == null || data.rendererFeatures.Exists(feature => feature is SeasonWipeFeature))
                continue;

            var added = ScriptableObject.CreateInstance<SeasonWipeFeature>();
            added.name = "SeasonWipe";
            AssetDatabase.AddObjectToAsset(added, data);
            SetObject(added, "shader", shader);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(added, out _, out long localId);

            var serialized = new SerializedObject(data);
            SerializedProperty features = serialized.FindProperty("m_RendererFeatures");
            SerializedProperty map = serialized.FindProperty("m_RendererFeatureMap");
            features.arraySize++;
            features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = added;
            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            data.SetDirty();
            EditorUtility.SetDirty(data);
            Debug.Log("SunlitSpotReplacer: added the season wipe to " + path);
        }
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Tools/Add Ring To SunlitSpot Prefab")]
    static void AddRing()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(RingMaterialPath);
        if (material == null)
        {
            Debug.LogError("SunlitSpotReplacer: ring material not found at " + RingMaterialPath);
            return;
        }

        using var scope = new PrefabUtility.EditPrefabContentsScope(PrefabPath);
        if (scope.prefabContentsRoot.GetComponentInChildren<SpringRing>(true) != null)
        {
            Debug.Log("SunlitSpotReplacer: the prefab already has a ring.");
            return;
        }

        var ringObject = new GameObject("Ring", typeof(MeshFilter), typeof(MeshRenderer));
        ringObject.transform.SetParent(scope.prefabContentsRoot.transform, false);
        var meshRenderer = ringObject.GetComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = material;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        var ring = ringObject.AddComponent<SpringRing>();
        var serialized = new SerializedObject(ring);
        serialized.FindProperty("groundLayers").intValue = LayerMask.GetMask("Ground");
        serialized.ApplyModifiedPropertiesWithoutUndo();
        ring.enabled = false;
        Debug.Log("SunlitSpotReplacer: added the ring to the prefab.");
    }

    [MenuItem("Tools/Replace SunlitSpots With Prefab")]
    static void Replace()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError("SunlitSpotReplacer: prefab not found at " + PrefabPath);
            return;
        }

        var scene = EditorSceneManager.GetActiveScene();
        int count = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform old in root.GetComponentsInChildren<Transform>(true))
            {
                if (old.name != "SunlitSpot" || PrefabUtility.IsPartOfPrefabInstance(old.gameObject))
                    continue;

                // A cylinder primitive is 2 units tall, so its bottom is its Y scale below its centre.
                Vector3 foot = old.position + Vector3.down * old.lossyScale.y;
                var spot = (GameObject)PrefabUtility.InstantiatePrefab(prefab, old.parent);
                Undo.RegisterCreatedObjectUndo(spot, "Replace SunlitSpots");
                spot.transform.position = foot;
                spot.transform.SetSiblingIndex(old.GetSiblingIndex());
                Undo.DestroyObjectImmediate(old.gameObject);
                count++;
            }
        }

        if (count > 0)
            EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"SunlitSpotReplacer: replaced {count} in {scene.name}. Save the scene to keep it.");
    }
}
#endif
