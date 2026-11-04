using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Builds the encyclopedia: one EncyclopediaEntry asset (its name) per interaction in Assets/04_Data/Encyclopedia/, the blur Volume profile,
// and Assets/03_Prefabs/UI/Encyclopedia.prefab (page, cards, discovery popup). Existing assets are updated in place.
// If Sam_model_test is the open scene, also puts the prefab and a test rabbit (click it to find the next entry) in it and saves it.
// If Main is the open scene, puts only the prefab under 01_SYSTEM and saves it.
// Run after BuildSamModelTest, which rebuilds that scene without them:
// unity command script run --file AgentScripts/BuildEncyclopedia.cs --entry BuildEncyclopedia.Run
public static class BuildEncyclopedia
{
    const string EntryFolder = "Assets/04_Data/Encyclopedia";
    const string BlurProfilePath = "Assets/04_Data/EncyclopediaBlur.asset";
    const string PrefabPath = "Assets/03_Prefabs/UI/Encyclopedia.prefab";
    const string ModelTestScene = "Assets/00_Scenes/Sam_model_test.unity";
    const string MainScene = "Assets/00_Scenes/Main.unity";
    const string RabbitModelPath = "Assets/01_Arts/Models/Rabbits/Rabbit-Animated.fbx";
    const string RabbitControllerPath = "Assets/01_Arts/Models/Rabbits/Rabbit.controller";
    static readonly Vector3 RabbitPosition = new Vector3(2.5f, 0f, -2.5f);
    const float RabbitYaw = 135f;

    // The director's list (order_from_director.md), in its order.
    static readonly (string file, string title)[] Entries =
    {
        ("01_IcicleXylophone", "Icicle Xylophone"),
        ("02_HollowSquirrel", "Squirrel in the Hollow"),
        ("03_FrozenRiver", "Frozen River and Trout"),
        ("04_MigratingBirds", "Migrating Birds"),
        ("05_SnowFlower", "Flower in the Snow"),
        ("06_Swallowtail", "Swallowtail Butterfly"),
        ("07_CornelianCherry", "Cornelian Cherries"),
        ("08_PuddleReflection", "Puddle Reflection"),
        ("09_StumpLadybug", "Ladybug in the Stump"),
        ("10_CliffEcho", "Cliff Echo"),
        ("11_SnowWhirl", "Snow Whirl"),
    };

    const float CardWidth = 394f;
    const float LabelHeight = 48f;

    static Font font;

    public static string Run()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        DeleteDroppedEntries();
        EncyclopediaEntry[] entries = Entries.Select(e => Entry(e.file, e.title)).ToArray();
        VolumeProfile blurProfile = BlurProfile();
        GameObject prefab = BuildPrefab(entries, blurProfile);
        AssetDatabase.SaveAssets();

        Scene scene = SceneManager.GetActiveScene();
        if (scene.path == ModelTestScene)
            PlaceInModelTest(scene, prefab);
        else if (scene.path == MainScene)
            PlaceInMain(scene, prefab);
        else
            return "built " + PrefabPath + "; open " + ModelTestScene + " or " + MainScene + " and run again to place it there";
        return EditorSceneManager.SaveScene(scene) ? "built " + PrefabPath + " and saved " + scene.path : "FAILED to save " + scene.path;
    }

    // Entry assets in the folder that are no longer in the list above.
    static void DeleteDroppedEntries()
    {
        if (!AssetDatabase.IsValidFolder(EntryFolder))
            return;
        foreach (string guid in AssetDatabase.FindAssets("t:EncyclopediaEntry", new[] { EntryFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!Entries.Any(e => path == EntryFolder + "/" + e.file + ".asset"))
                AssetDatabase.DeleteAsset(path);
        }
    }

    static EncyclopediaEntry Entry(string file, string title)
    {
        string path = EntryFolder + "/" + file + ".asset";
        var entry = AssetDatabase.LoadAssetAtPath<EncyclopediaEntry>(path);
        if (entry == null)
        {
            if (!AssetDatabase.IsValidFolder(EntryFolder))
                AssetDatabase.CreateFolder("Assets/04_Data", "Encyclopedia");
            entry = ScriptableObject.CreateInstance<EncyclopediaEntry>();
            AssetDatabase.CreateAsset(entry, path);
        }
        entry.title = title;
        EditorUtility.SetDirty(entry);
        return entry;
    }

    // Bokeh depth of field focused right in front of the lens, as strong as it goes: everything behind the page is out of focus.
    static VolumeProfile BlurProfile()
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(BlurProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, BlurProfilePath);
        }
        if (!profile.TryGet(out DepthOfField dof))
        {
            dof = profile.Add<DepthOfField>();
            dof.name = nameof(DepthOfField);
            AssetDatabase.AddObjectToAsset(dof, profile);
        }
        dof.mode.Override(DepthOfFieldMode.Bokeh);
        dof.focusDistance.Override(0.1f);
        dof.focalLength.Override(300f);
        dof.aperture.Override(1f);
        EditorUtility.SetDirty(dof);
        EditorUtility.SetDirty(profile);
        return profile;
    }

    static GameObject BuildPrefab(EncyclopediaEntry[] entries, VolumeProfile blurProfile)
    {
        var root = new GameObject("Encyclopedia");
        try
        {
            var blurObject = new GameObject("Blur");
            blurObject.transform.SetParent(root.transform, false);
            var blur = blurObject.AddComponent<Volume>();
            blur.isGlobal = true;
            blur.priority = 100f;
            blur.weight = 0f;
            blur.sharedProfile = blurProfile;

            var photoObject = new GameObject("PhotoCamera");
            photoObject.transform.SetParent(root.transform, false);
            var photoCamera = photoObject.AddComponent<Camera>();
            photoCamera.enabled = false;
            photoObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;

            var canvasObject = new GameObject("Canvas", typeof(RectTransform));
            canvasObject.transform.SetParent(root.transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Above Main's intro canvas (title, white fade), which is at 100.
            canvas.sortingOrder = 110;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            // Expand keeps the whole 1920 x 1080 layout on screen at any aspect ratio.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            RectTransform page = Rect("Page", canvasObject.transform, Vector2.zero, Vector2.one, new Vector2(96f, 54f), new Vector2(-96f, -54f));
            AddImage(page, new Color(0.08f, 0.1f, 0.14f, 0.88f));
            RectTransform grid = Rect("Grid", page, Vector2.zero, Vector2.one, new Vector2(40f, 40f), new Vector2(-40f, -40f));
            var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(CardWidth, LabelHeight + CardWidth * 9f / 16f);
            layout.spacing = new Vector2(24f, 24f);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 4;
            layout.childAlignment = TextAnchor.MiddleCenter;
            EncyclopediaCard[] cards = entries.Select((_, i) => Card(grid, i)).ToArray();

            DiscoveryPopup popup = Popup(canvasObject.transform);

            var encyclopedia = root.AddComponent<Encyclopedia>();
            Set(encyclopedia, so =>
            {
                Objects(so.FindProperty("entries"), entries);
                Objects(so.FindProperty("cards"), cards);
                so.FindProperty("popup").objectReferenceValue = popup;
                so.FindProperty("page").objectReferenceValue = page;
                so.FindProperty("blur").objectReferenceValue = blur;
                so.FindProperty("photoCamera").objectReferenceValue = photoCamera;
            });
            page.gameObject.SetActive(false);

            if (!AssetDatabase.IsValidFolder("Assets/03_Prefabs/UI"))
                AssetDatabase.CreateFolder("Assets/03_Prefabs", "UI");
            return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    // Number and name on one line, the 16:9 photo below.
    static EncyclopediaCard Card(Transform grid, int index)
    {
        RectTransform rect = Rect($"Card_{index + 1:00}", grid, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        AddImage(rect, new Color(0.17f, 0.19f, 0.23f, 1f));
        Text label = Label("Label", rect, new Vector2(0f, 1f), Vector2.one, new Vector2(14f, -LabelHeight), new Vector2(-14f, 0f), $"No. {index + 1:00}  ???", 28, FontStyle.Bold, TextAnchor.MiddleLeft);
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = 16;
        label.resizeTextMaxSize = 28;
        RectTransform frame = Rect("PhotoFrame", rect, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0f, -LabelHeight));
        AddImage(frame, new Color(0.1f, 0.11f, 0.14f, 1f));
        RectTransform photoRect = Rect("Photo", frame, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var photo = photoRect.gameObject.AddComponent<RawImage>();
        photo.raycastTarget = false;
        photo.enabled = false;
        var card = rect.gameObject.AddComponent<EncyclopediaCard>();
        Set(card, so =>
        {
            so.FindProperty("label").objectReferenceValue = label;
            so.FindProperty("photo").objectReferenceValue = photo;
        });
        return card;
    }

    static DiscoveryPopup Popup(Transform canvas)
    {
        RectTransform area = Rect("DiscoveryPopup", canvas, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        RectTransform box = Rect("Box", area, new Vector2(1f, 0f), new Vector2(1f, 0f), Vector2.zero, Vector2.zero);
        box.pivot = new Vector2(1f, 0f);
        box.sizeDelta = new Vector2(520f, 110f);
        box.anchoredPosition = new Vector2(-40f, 40f);
        AddImage(box, new Color(0.08f, 0.1f, 0.14f, 0.9f));
        var group = box.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;
        Text label = Label("Label", box, new Vector2(0f, 1f), Vector2.one, new Vector2(24f, -44f), new Vector2(-24f, -12f), "New discovery", 22, FontStyle.Normal, TextAnchor.MiddleLeft);
        label.color = new Color(1f, 0.85f, 0.5f, 1f);
        Text title = Label("Title", box, Vector2.zero, Vector2.one, new Vector2(24f, 12f), new Vector2(-24f, -44f), "", 30, FontStyle.Bold, TextAnchor.MiddleLeft);
        var popup = area.gameObject.AddComponent<DiscoveryPopup>();
        Set(popup, so =>
        {
            so.FindProperty("box").objectReferenceValue = box;
            so.FindProperty("group").objectReferenceValue = group;
            so.FindProperty("title").objectReferenceValue = title;
        });
        return popup;
    }

    // Under 01_SYSTEM. No test rabbit: Main gets only the encyclopedia until the easter eggs exist.
    static void PlaceInMain(Scene scene, GameObject prefab)
    {
        Transform system = scene.GetRootGameObjects().First(g => g.name == "01_SYSTEM").transform;
        Transform old = system.Find("Encyclopedia");
        if (old != null)
            Object.DestroyImmediate(old.gameObject);
        PrefabUtility.InstantiatePrefab(prefab, system);
    }

    static void PlaceInModelTest(Scene scene, GameObject prefab)
    {
        foreach (GameObject old in scene.GetRootGameObjects().Where(g => g.name == "Encyclopedia" || g.name == "TestRabbit"))
            Object.DestroyImmediate(old);

        var encyclopedia = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);

        var rabbit = new GameObject("TestRabbit");
        SceneManager.MoveGameObjectToScene(rabbit, scene);
        var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(RabbitModelPath), rabbit.transform);
        var animator = model.GetComponent<Animator>();
        animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(RabbitControllerPath);
        animator.applyRootMotion = false;
        Bounds bounds = model.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
        var collider = rabbit.AddComponent<BoxCollider>();
        collider.center = bounds.center;
        collider.size = bounds.size;
        rabbit.transform.SetPositionAndRotation(RabbitPosition, Quaternion.Euler(0f, RabbitYaw, 0f));
        // An example photo view: low, from the rabbit's side. Move or turn it in the editor; clear the field to frame it from the game camera's angle.
        var view = new GameObject("PhotoView").AddComponent<Camera>();
        view.enabled = false;
        view.fieldOfView = 40f;
        view.transform.SetParent(rabbit.transform, false);
        Vector3 centre = rabbit.transform.TransformPoint(bounds.center);
        view.transform.position = centre + Quaternion.Euler(0f, RabbitYaw + 60f, 0f) * new Vector3(0f, 0.5f, 2.6f);
        view.transform.LookAt(centre);
        var target = rabbit.AddComponent<DiscoveryTestTarget>();
        Set(target, so =>
        {
            so.FindProperty("encyclopedia").objectReferenceValue = encyclopedia.GetComponent<Encyclopedia>();
            so.FindProperty("photoView").objectReferenceValue = view;
        });
    }

    static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        return rect;
    }

    static Image AddImage(RectTransform rect, Color color)
    {
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    static Text Label(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax,
        string text, int size, FontStyle style, TextAnchor alignment)
    {
        var label = Rect(name, parent, anchorMin, anchorMax, offsetMin, offsetMax).gameObject.AddComponent<Text>();
        label.font = font;
        label.text = text;
        label.fontSize = size;
        label.fontStyle = style;
        label.alignment = alignment;
        label.color = Color.white;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        label.raycastTarget = false;
        return label;
    }

    static void Objects(SerializedProperty array, Object[] values)
    {
        array.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    static void Set(Object target, System.Action<SerializedObject> fill)
    {
        var so = new SerializedObject(target);
        fill(so);
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
