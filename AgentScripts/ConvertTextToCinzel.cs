using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Every UI Text (the old built-in kind) becomes a TextMeshPro text in Cinzel (Cinzel-VariableFont_wght SDF), keeping its words,
// size, colour, style and alignment: the encyclopedia's (its prefab) and the ones made in the open scene. The encyclopedia's
// scripts take the new texts (EncyclopediaCard's Label, DiscoveryPopup's Title). TextMeshPro texts already there take the font
// too. Rerunning finds no old texts left and only sets the font again.

// unity command script run --file AgentScripts/ConvertTextToCinzel.cs --entry ConvertTextToCinzel.Run
public static class ConvertTextToCinzel
{
    const string FontPath = "Assets/01_Arts/Fonts/Cinzel-VariableFont_wght SDF.asset";
    const string EncyclopediaPath = "Assets/03_Prefabs/UI/Encyclopedia.prefab";

    public static string Run()
    {
        var report = new StringBuilder();
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null)
            return "No font at " + FontPath;

        // The encyclopedia's prefab.
        GameObject root = PrefabUtility.LoadPrefabContents(EncyclopediaPath);
        try
        {
            int converted = ConvertAll(root.GetComponentsInChildren<Text>(true), font);
            foreach (var card in root.GetComponentsInChildren<EncyclopediaCard>(true))
                Wire(card, "label", card.transform.Find("Label"));
            foreach (var popup in root.GetComponentsInChildren<DiscoveryPopup>(true))
                Wire(popup, "title", FindDeep(popup.transform, "Title"));
            int restyled = SetFont(root.GetComponentsInChildren<TMP_Text>(true), font);
            PrefabUtility.SaveAsPrefabAsset(root, EncyclopediaPath);
            report.AppendLine($"Encyclopedia prefab: {converted} converted, {restyled} in Cinzel.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        // The open scene's own texts (not parts of a prefab, which their prefab carries).
        var scene = EditorSceneManager.GetActiveScene();
        var own = new System.Collections.Generic.List<Text>();
        foreach (var text in Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (!PrefabUtility.IsPartOfPrefabInstance(text))
                own.Add(text);
            else
                report.AppendLine($"  left in a prefab instance: {Path(text.transform)}");
        int sceneConverted = ConvertAll(own.ToArray(), font);
        int sceneRestyled = SetFont(Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None), font);
        // The scene's encyclopedia, should its references be overridden there.
        foreach (var card in Object.FindObjectsByType<EncyclopediaCard>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Wire(card, "label", card.transform.Find("Label"));
        foreach (var popup in Object.FindObjectsByType<DiscoveryPopup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Wire(popup, "title", FindDeep(popup.transform, "Title"));
        EditorSceneManager.MarkSceneDirty(scene);
        report.AppendLine($"Scene {scene.name}: {sceneConverted} converted, {sceneRestyled} in Cinzel.");
        AssetDatabase.SaveAssets();
        return report.ToString();
    }

    static int ConvertAll(Text[] texts, TMP_FontAsset font)
    {
        foreach (Text text in texts)
            Convert(text, font);
        return texts.Length;
    }

    static void Convert(Text text, TMP_FontAsset font)
    {
        GameObject go = text.gameObject;
        string words = text.text;
        int size = text.fontSize;
        Color colour = text.color;
        FontStyle style = text.fontStyle;
        TextAnchor anchor = text.alignment;
        bool wrap = text.horizontalOverflow == HorizontalWrapMode.Wrap;
        bool raycast = text.raycastTarget;
        bool best = text.resizeTextForBestFit;
        int bestMin = text.resizeTextMinSize, bestMax = text.resizeTextMaxSize;
        float spacing = text.lineSpacing;
        bool enabled = text.enabled;
        // Outline / Shadow effects work on the old text's mesh only.
        foreach (var effect in go.GetComponents<BaseMeshEffect>())
            Object.DestroyImmediate(effect, true);
        Object.DestroyImmediate(text, true);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.text = words;
        tmp.fontSize = size;
        tmp.color = colour;
        tmp.fontStyle = style switch
        {
            FontStyle.Bold => FontStyles.Bold,
            FontStyle.Italic => FontStyles.Italic,
            FontStyle.BoldAndItalic => FontStyles.Bold | FontStyles.Italic,
            _ => FontStyles.Normal,
        };
        tmp.alignment = anchor switch
        {
            TextAnchor.UpperLeft => TextAlignmentOptions.TopLeft,
            TextAnchor.UpperCenter => TextAlignmentOptions.Top,
            TextAnchor.UpperRight => TextAlignmentOptions.TopRight,
            TextAnchor.MiddleLeft => TextAlignmentOptions.Left,
            TextAnchor.MiddleCenter => TextAlignmentOptions.Center,
            TextAnchor.MiddleRight => TextAlignmentOptions.Right,
            TextAnchor.LowerLeft => TextAlignmentOptions.BottomLeft,
            TextAnchor.LowerCenter => TextAlignmentOptions.Bottom,
            _ => TextAlignmentOptions.BottomRight,
        };
        tmp.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.raycastTarget = raycast;
        tmp.enableAutoSizing = best;
        if (best)
        {
            tmp.fontSizeMin = bestMin;
            tmp.fontSizeMax = bestMax;
        }
        tmp.lineSpacing = (spacing - 1f) * 100f;
        tmp.enabled = enabled;
    }

    static int SetFont(TMP_Text[] texts, TMP_FontAsset font)
    {
        foreach (TMP_Text text in texts)
        {
            text.font = font;
            text.fontSharedMaterial = font.material;
            EditorUtility.SetDirty(text);
        }
        return texts.Length;
    }

    static void Wire(Object owner, string field, Transform target)
    {
        var so = new SerializedObject(owner);
        so.FindProperty(field).objectReferenceValue = target != null ? target.GetComponent<TMP_Text>() : null;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static Transform FindDeep(Transform parent, string name)
    {
        foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
            if (child.name == name)
                return child;
        return null;
    }

    static string Path(Transform t)
    {
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }
}
