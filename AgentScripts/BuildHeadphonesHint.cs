using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

// Under the intro's title: "Best experienced with headphones", small, white, with the headset icon, clear (IntroSequence fades it
// in once the logo has played). Wires it and the title's video to IntroSequence. Rerunning replaces it.
// unity command script run --file AgentScripts/BuildHeadphonesHint.cs --entry BuildHeadphonesHint.Run
public static class BuildHeadphonesHint
{
    const string IconPath = "Assets/01_Arts/Textures/UI/Headset.png";
    const string Words = "Best experienced with headphones";
    const int FontSize = 24;
    const float IconHeight = 34f;
    // Below the title's middle, in the canvas's units (1920 x 1080).
    const float BelowTitle = 130f;

    public static string Run()
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(IconPath);
        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
        Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);

        IntroSequence intro = Object.FindAnyObjectByType<IntroSequence>(FindObjectsInactive.Include);
        var introSo = new SerializedObject(intro);
        var title = (Graphic)introSo.FindProperty("title").objectReferenceValue;
        Transform canvas = title.canvas != null ? title.canvas.transform : title.transform.parent;

        Transform old = canvas.Find("Headphones");
        if (old != null)
            Object.DestroyImmediate(old.gameObject);

        var hint = new GameObject("Headphones", typeof(RectTransform), typeof(CanvasGroup), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        hint.transform.SetParent(canvas, false);
        hint.transform.SetSiblingIndex(title.transform.GetSiblingIndex() + 1);
        var titleRect = (RectTransform)title.transform;
        var rect = (RectTransform)hint.transform;
        rect.anchorMin = rect.anchorMax = titleRect.anchorMin;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = titleRect.anchoredPosition + Vector2.down * BelowTitle;
        var group = hint.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
        var layout = hint.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        var fitter = hint.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        iconObject.transform.SetParent(hint.transform, false);
        Image image = iconObject.GetComponent<Image>();
        image.sprite = icon;
        image.color = Color.white;
        image.preserveAspect = true;
        image.raycastTarget = false;
        var iconLayout = iconObject.GetComponent<LayoutElement>();
        iconLayout.preferredHeight = IconHeight;
        iconLayout.preferredWidth = IconHeight * icon.rect.width / icon.rect.height;

        var label = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        label.transform.SetParent(hint.transform, false);
        Text text = label.GetComponent<Text>();
        text.text = Words;
        text.fontSize = FontSize;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleLeft;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        // The font the encyclopedia's names use, if there is one.
        var encyclopedia = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/UI/Encyclopedia.prefab");
        Text sample = encyclopedia != null ? encyclopedia.GetComponentsInChildren<Text>(true).FirstOrDefault(t => t.font != null) : null;
        text.font = sample != null ? sample.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        introSo.FindProperty("headphones").objectReferenceValue = group;
        introSo.FindProperty("titleVideo").objectReferenceValue = title.GetComponent<VideoPlayer>();
        introSo.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(intro.gameObject.scene);
        return "ok: Headphones under " + canvas.name + ", font " + text.font.name + ", video " + (title.GetComponent<VideoPlayer>() != null);
    }
}
