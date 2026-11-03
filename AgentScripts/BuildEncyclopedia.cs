using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Builds the encyclopedia: one EncyclopediaEntry asset per interaction in Assets/04_Data/Encyclopedia/, the blur Volume profile,
// and Assets/03_Prefabs/UI/Encyclopedia.prefab (page, cards, discovery popup). Existing assets are updated in place.
// If Sam_model_test is the open scene, also puts the prefab and a test rabbit (click it to find the next entry) in it and saves it.
// Run after BuildSamModelTest, which rebuilds that scene without them:
// unity command script run --file AgentScripts/BuildEncyclopedia.cs --entry BuildEncyclopedia.Run
public static class BuildEncyclopedia
{
    const string EntryFolder = "Assets/04_Data/Encyclopedia";
    const string BlurProfilePath = "Assets/04_Data/EncyclopediaBlur.asset";
    const string PrefabPath = "Assets/03_Prefabs/UI/Encyclopedia.prefab";
    const string ModelTestScene = "Assets/00_Scenes/Sam_model_test.unity";
    const string RabbitModelPath = "Assets/01_Arts/Models/Rabbits/Rabbit-Animated.fbx";
    const string RabbitControllerPath = "Assets/01_Arts/Models/Rabbits/Rabbit.controller";
    static readonly Vector3 RabbitPosition = new Vector3(2.5f, 0f, -2.5f);
    const float RabbitYaw = 135f;

    // The director's list (order_from_director.md), in its order.
    static readonly (string file, string title, string description)[] Entries =
    {
        ("01_IcicleXylophone", "Icicle Xylophone", "Peck the icicles on a tree and they ring as they fall. Peck them in order to play the main melody."),
        ("02_HollowSquirrel", "Squirrel in the Hollow", "Cry near a tree hollow and a squirrel peeks out, then hides again."),
        ("03_FrozenRiver", "Frozen River and Trout", "Peck the frozen river until the ice cracks. Wait, and a trout appears."),
        ("04_MigratingBirds", "Migrating Birds", "Far away, migrating birds fly in, their shadows sliding over the ground."),
        ("05_SnowFlower", "Flower in the Snow", "Peck the snow to find a flower blooming underneath."),
        ("06_Swallowtail", "Swallowtail Butterfly", "Once the snow has melted, click a flower and a swallowtail flies to it."),
        ("07_CornelianCherry", "Cornelian Cherries", "Eat the cornelian cherries in the bush and the bird puffs up its chest."),
        ("08_PuddleReflection", "Puddle Reflection", "Pass over a meltwater puddle and something other than yourself looks back."),
        ("09_SnowOnTheBird", "Snow on the Bird", "Keep still and snow piles up on the bird. Move to shake it off and fly."),
        ("10_StumpLadybug", "Ladybug in the Stump", "Peck the stump of the fallen tree and a ladybug crawls out."),
        ("11_CliffEcho", "Cliff Echo", "Cry at the edge of the cliff and hear your echo."),
        ("12_SnowWhirl", "Snow Whirl", "Circle round and round over the snow to stir up a whirl."),
        ("13_FeatherForTheBaby", "Feather for the Baby", "Turn sharply near the baby. A feather falls, and she reaches for it."),
    };

    static Font font;

    public static string Run()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        EncyclopediaEntry[] entries = Entries.Select(e => Entry(e.file, e.title, e.description)).ToArray();
        VolumeProfile blurProfile = BlurProfile();
        GameObject prefab = BuildPrefab(entries, blurProfile);
        AssetDatabase.SaveAssets();

        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ModelTestScene)
            return "built " + PrefabPath + "; open " + ModelTestScene + " and run again to place it there";
        PlaceInModelTest(scene, prefab);
        return EditorSceneManager.SaveScene(scene) ? "built " + PrefabPath + " and saved " + ModelTestScene : "FAILED to save " + ModelTestScene;
    }

    static EncyclopediaEntry Entry(string file, string title, string description)
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
        entry.description = description;
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

            var canvasObject = new GameObject("Canvas", typeof(RectTransform));
            canvasObject.transform.SetParent(root.transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            // Expand keeps the whole 1920 x 1080 layout on screen at any aspect ratio.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            RectTransform page = Rect("Page", canvasObject.transform, Vector2.zero, Vector2.one, new Vector2(96f, 54f), new Vector2(-96f, -54f));
            AddImage(page, new Color(0.08f, 0.1f, 0.14f, 0.88f));
            Label("Header", page, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(60f, -110f), new Vector2(-60f, -30f), "Encyclopedia", 56, FontStyle.Bold, TextAnchor.MiddleLeft);
            Text counter = Label("Counter", page, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(60f, -110f), new Vector2(-60f, -30f), "0 / 0", 36, FontStyle.Normal, TextAnchor.MiddleRight);
            Label("Hint", page, Vector2.zero, new Vector2(1f, 0f), new Vector2(60f, 20f), new Vector2(-60f, 70f), "Press ` to close", 24, FontStyle.Italic, TextAnchor.MiddleCenter);

            RectTransform grid = Rect("Grid", page, Vector2.zero, Vector2.one, new Vector2(40f, 90f), new Vector2(-40f, -130f));
            var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(310f, 230f);
            layout.spacing = new Vector2(24f, 24f);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 5;
            layout.childAlignment = TextAnchor.UpperCenter;
            EncyclopediaCard[] cards = entries.Select((_, i) => Card(grid, i)).ToArray();

            DiscoveryPopup popup = Popup(canvasObject.transform);

            var encyclopedia = root.AddComponent<Encyclopedia>();
            Set(encyclopedia, so =>
            {
                Objects(so.FindProperty("entries"), entries);
                Objects(so.FindProperty("cards"), cards);
                so.FindProperty("popup").objectReferenceValue = popup;
                so.FindProperty("page").objectReferenceValue = page;
                so.FindProperty("counter").objectReferenceValue = counter;
                so.FindProperty("blur").objectReferenceValue = blur;
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

    static EncyclopediaCard Card(Transform grid, int index)
    {
        RectTransform rect = Rect($"Card_{index + 1:00}", grid, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Image background = AddImage(rect, Color.gray);
        Text number = Label("Number", rect, new Vector2(0f, 1f), Vector2.one, new Vector2(18f, -46f), new Vector2(-18f, -12f), $"No. {index + 1:00}", 22, FontStyle.Normal, TextAnchor.MiddleLeft);
        number.color = new Color(1f, 1f, 1f, 0.6f);
        Text title = Label("Title", rect, new Vector2(0f, 1f), Vector2.one, new Vector2(18f, -90f), new Vector2(-18f, -44f), "???", 28, FontStyle.Bold, TextAnchor.MiddleLeft);
        title.resizeTextForBestFit = true;
        title.resizeTextMinSize = 18;
        title.resizeTextMaxSize = 28;
        Text description = Label("Description", rect, Vector2.zero, Vector2.one, new Vector2(18f, 12f), new Vector2(-18f, -96f), "", 22, FontStyle.Normal, TextAnchor.UpperLeft);
        description.resizeTextForBestFit = true;
        description.resizeTextMinSize = 16;
        description.resizeTextMaxSize = 22;
        var card = rect.gameObject.AddComponent<EncyclopediaCard>();
        Set(card, so =>
        {
            so.FindProperty("number").objectReferenceValue = number;
            so.FindProperty("title").objectReferenceValue = title;
            so.FindProperty("description").objectReferenceValue = description;
            so.FindProperty("background").objectReferenceValue = background;
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
        var target = rabbit.AddComponent<DiscoveryTestTarget>();
        Set(target, so => so.FindProperty("encyclopedia").objectReferenceValue = encyclopedia.GetComponent<Encyclopedia>());
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
