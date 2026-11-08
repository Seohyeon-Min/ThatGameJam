using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Under the intro's headphones line: "Click to start", in the same lettering as that line (its Label: font, size, colour, style),
// clear (IntroSequence fades it in once the logo has played and clicks count). Wires it to IntroSequence. Rerunning replaces it.
// unity command script run --file AgentScripts/BuildClickToStart.cs --entry BuildClickToStart.Run
public static class BuildClickToStart
{
    const string Words = "Click to start";
    // Below the headphones line's middle, in the canvas's units (1920 x 1080).
    const float BelowHeadphones = 48f;

    public static string Run()
    {
        IntroSequence intro = Object.FindAnyObjectByType<IntroSequence>(FindObjectsInactive.Include);
        var introSo = new SerializedObject(intro);
        var headphones = (CanvasGroup)introSo.FindProperty("headphones").objectReferenceValue;
        if (headphones == null)
            return "no headphones line to sit under";
        TMP_Text sample = headphones.GetComponentInChildren<TMP_Text>(true);
        if (sample == null)
            return "the headphones line has no TextMeshPro label";
        Transform canvas = headphones.transform.parent;

        Transform old = canvas.Find("ClickToStart");
        if (old != null)
            Object.DestroyImmediate(old.gameObject);

        var line = new GameObject("ClickToStart", typeof(RectTransform), typeof(CanvasGroup));
        line.transform.SetParent(canvas, false);
        line.transform.SetSiblingIndex(headphones.transform.GetSiblingIndex() + 1);
        var from = (RectTransform)headphones.transform;
        var rect = (RectTransform)line.transform;
        rect.anchorMin = rect.anchorMax = from.anchorMin;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localScale = from.localScale;
        rect.sizeDelta = new Vector2(600f, 40f);
        rect.anchoredPosition = from.anchoredPosition + Vector2.down * BelowHeadphones * from.localScale.y;
        var group = line.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        label.transform.SetParent(line.transform, false);
        var labelRect = (RectTransform)label.transform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
        var text = label.GetComponent<TextMeshProUGUI>();
        text.text = Words;
        text.font = sample.font;
        text.fontSharedMaterial = sample.fontSharedMaterial;
        text.fontSize = sample.fontSize;
        text.fontStyle = sample.fontStyle;
        text.characterSpacing = sample.characterSpacing;
        text.color = sample.color;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;

        introSo.FindProperty("clickToStart").objectReferenceValue = group;
        introSo.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(intro.gameObject.scene);
        return "ok: ClickToStart under " + canvas.name + " at " + rect.anchoredPosition + ", font " + text.font.name + " " + text.fontSize;
    }
}
