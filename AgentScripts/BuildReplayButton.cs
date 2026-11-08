using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// The ending's replay button, bottom left on the End Card: the white Replay icon (from the team's drive) over a soft dark spot
// (SoftShadeUI) so it shows on the white card. Clear and off until EndingFlock fades it in after the credits; a click on it plays
// the game again from the start (ReplayButton). Wires it to EndingFlock. Rerunning keeps the material and replaces the button.
// unity command script run --file AgentScripts/BuildReplayButton.cs --entry BuildReplayButton.Run
public static class BuildReplayButton
{
    const string IconPath = "Assets/01_Arts/Textures/UI/Replay.png";
    const string ShaderName = "ThatGameJam/SoftShadeUI";
    const string MaterialPath = "Assets/01_Arts/Materials/ReplayShade.mat";
    // Canvas units (1920 x 1080): the button's middle in from the bottom left corner, its click area, the dark spot and the icon.
    const float Inset = 110f;
    const float HitSize = 120f;
    const float ShadeSize = 190f;
    const float IconSize = 72f;
    static readonly Color ShadeColour = new Color(0.12f, 0.1f, 0.18f, 0.45f);

    public static string Run()
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(IconPath);
        if (importer == null)
            return "no icon at " + IconPath;
        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 256;
            importer.SaveAndReimport();
        }
        Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);

        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
                return "no shader " + ShaderName;
            material = new Material(shader) { name = "ReplayShade" };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }

        EndingFlock flock = Object.FindAnyObjectByType<EndingFlock>(FindObjectsInactive.Include);
        var flockSo = new SerializedObject(flock);
        var logo = (GameObject)flockSo.FindProperty("logo").objectReferenceValue;
        Transform card = logo.transform.parent;

        Transform old = card.Find("Replay");
        if (old != null)
            Object.DestroyImmediate(old.gameObject);

        var button = new GameObject("Replay", typeof(RectTransform), typeof(CanvasGroup), typeof(ReplayButton));
        button.transform.SetParent(card, false);
        button.transform.SetAsLastSibling();
        var rect = (RectTransform)button.transform;
        rect.anchorMin = rect.anchorMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(Inset, Inset);
        rect.sizeDelta = new Vector2(HitSize, HitSize);
        var group = button.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        Image shade = AddImage(button.transform, "Shade", ShadeSize);
        shade.material = material;
        shade.color = ShadeColour;
        Image image = AddImage(button.transform, "Icon", IconSize);
        image.sprite = icon;
        image.preserveAspect = true;
        image.color = Color.white;

        button.SetActive(false);
        flockSo.FindProperty("replay").objectReferenceValue = group;
        flockSo.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(flock.gameObject.scene);
        return "ok: Replay under " + card.name + " at " + rect.anchoredPosition;
    }

    static Image AddImage(Transform parent, string name, float size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(size, size);
        var image = go.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }
}
