using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// The ending's credits, on the End Card under the logo: one CreditLine (a prefab: the name large, the role small under it) per
// person, one under another, in black Cinzel like the logo. Clear and off until EndingFlock fades them in, once the logo has played
// and moved up. Makes the prefab, puts the credits on the End Card and wires them to EndingFlock. Rerunning replaces them.
// unity command script run --file AgentScripts/BuildEndingCredits.cs --entry BuildEndingCredits.Run
public static class BuildEndingCredits
{
    const string LinePrefab = "Assets/03_Prefabs/UI/CreditLine.prefab";
    const string FontPath = "Assets/01_Arts/Fonts/Cinzel-VariableFont_wght SDF.asset";
    const float NameSize = 36f;
    const float RoleSize = 18f;
    static readonly Color NameColour = new Color(0.08f, 0.08f, 0.1f, 1f);
    static readonly Color RoleColour = new Color(0.08f, 0.08f, 0.1f, 0.6f);
    // The credits' top edge, from the canvas's middle (canvas units, 1920 x 1080): under where the logo ends up once it has moved up.
    const float Top = 30f;
    const float Spacing = 22f;

    static readonly string[,] People =
    {
        { "Seohyeon Min", "Art Direction, Programming & Production" },
        { "Tommy Ji", "3D Art & Modeling" },
        { "Seungju Song", "Game Design & Programming" },
        { "Sam Jeon", "Programming" },
        { "Port Nugent", "Sound Design" },
    };

    public static string Run()
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null)
            return "no font at " + FontPath;
        GameObject prefab = MakeLinePrefab(font);

        EndingFlock flock = Object.FindAnyObjectByType<EndingFlock>(FindObjectsInactive.Include);
        var flockSo = new SerializedObject(flock);
        var logo = (GameObject)flockSo.FindProperty("logo").objectReferenceValue;
        Transform card = logo.transform.parent;

        Transform old = card.Find("Credits");
        if (old != null)
            Object.DestroyImmediate(old.gameObject);

        var credits = new GameObject("Credits", typeof(RectTransform), typeof(CanvasGroup), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        credits.transform.SetParent(card, false);
        credits.transform.SetSiblingIndex(logo.transform.GetSiblingIndex() + 1);
        var rect = (RectTransform)credits.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, Top);
        rect.sizeDelta = new Vector2(1200f, 0f);
        var group = credits.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
        var layout = credits.GetComponent<VerticalLayoutGroup>();
        layout.spacing = Spacing;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        credits.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        for (int i = 0; i < People.GetLength(0); i++)
        {
            var line = (GameObject)PrefabUtility.InstantiatePrefab(prefab, credits.transform);
            line.name = "Credit " + People[i, 0];
            line.transform.Find("Name").GetComponent<TMP_Text>().text = People[i, 0];
            line.transform.Find("Role").GetComponent<TMP_Text>().text = People[i, 1];
        }
        credits.SetActive(false);

        flockSo.FindProperty("credits").objectReferenceValue = group;
        flockSo.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(flock.gameObject.scene);
        return "ok: " + People.GetLength(0) + " credits under " + card.name + ", prefab " + LinePrefab;
    }

    // A name, large, and the role under it, small; centred, sized to their words.
    static GameObject MakeLinePrefab(TMP_FontAsset font)
    {
        var root = new GameObject("CreditLine", typeof(RectTransform), typeof(VerticalLayoutGroup));
        var layout = root.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 2f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        AddText(root.transform, "Name", "Name", font, NameSize, NameColour);
        AddText(root.transform, "Role", "Role", font, RoleSize, RoleColour);
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, LinePrefab);
        Object.DestroyImmediate(root);
        return prefab;
    }

    static void AddText(Transform parent, string name, string words, TMP_FontAsset font, float size, Color colour)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<TextMeshProUGUI>();
        text.text = words;
        text.font = font;
        text.fontSize = size;
        text.color = colour;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
    }
}
