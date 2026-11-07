using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

// The ending's last moment (EndingFlock): white birds sweeping over the screen, then the logo in black on white.
// - WhiteFlockBird.mat (white, unlit, so where they overlap no edge shows) and WhiteFlock.controller (only the Flap loop),
//   made once.
// - The WhiteFlockBird prefab: the bird model with those, no shadows, and WhiteFlockBird; remade each run.
// - Under 04_CUTSCENE/Ending: Ending Flock (EndingFlock) with Birds (Count prefab instances, off) and End Card (a canvas over
//   everything: White, clear, and Logo, off: the intro title's video, black, in the middle). Rerunning replaces these.
// unity command script run --file AgentScripts/BuildEndingFlock.cs --entry BuildEndingFlock.Run
public static class BuildEndingFlock
{
    const string ModelPath = "Assets/01_Arts/Models/Bird/Bird-Animated.fbx";
    const string MaterialPath = "Assets/01_Arts/Materials/WhiteFlockBird.mat";
    const string ControllerPath = "Assets/01_Arts/Models/Bird/WhiteFlock.controller";
    const string PrefabPath = "Assets/03_Prefabs/Stage3/WhiteFlockBird.prefab";
    const string LogoTexturePath = "Assets/01_Arts/Video/TitleLogo.renderTexture";
    const int Count = 60;
    const int CardOrder = 200;

    public static string Run()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.SetColor("_BaseColor", Color.white);
            AssetDatabase.CreateAsset(material, MaterialPath);
        }

        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            AnimationClip flap = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>().First(c => c.name == "Bird_Flap");
            AnimatorState state = controller.layers[0].stateMachine.AddState("Flap");
            state.motion = flap;
            controller.layers[0].stateMachine.defaultState = state;
        }

        // The prefab.
        var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath));
        model.name = "WhiteFlockBird";
        Animator animator = model.GetComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.applyRootMotion = false;
        model.AddComponent<WhiteFlockBird>();
        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (renderer is SkinnedMeshRenderer skinned)
                skinned.updateWhenOffscreen = true;
        }
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(model, PrefabPath);
        Object.DestroyImmediate(model);

        // The scene.
        EndingFlight ending = Object.FindAnyObjectByType<EndingFlight>(FindObjectsInactive.Include);
        Transform root = ending.transform;
        foreach (string old in new[] { "Ending Flock" })
        {
            Transform found = root.Find(old);
            if (found != null)
                Object.DestroyImmediate(found.gameObject);
        }
        var flockObject = new GameObject("Ending Flock");
        flockObject.transform.SetParent(root, false);
        EndingFlock flock = flockObject.AddComponent<EndingFlock>();

        var birds = new GameObject("Birds").transform;
        birds.SetParent(flockObject.transform, false);
        for (int i = 0; i < Count; i++)
        {
            var bird = (GameObject)PrefabUtility.InstantiatePrefab(prefab, birds);
            bird.name = "WhiteFlockBird " + (i + 1);
            bird.SetActive(false);
        }

        // End Card: as the intro's canvas is set up, but over everything.
        Transform title = Object.FindObjectsByType<RawImage>(FindObjectsInactive.Include)
            .First(r => r.name == "Title" && r.GetComponent<VideoPlayer>() != null).transform;
        Canvas introCanvas = title.GetComponentInParent<Canvas>(true);
        var card = new GameObject("End Card", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        card.transform.SetParent(flockObject.transform, false);
        Canvas canvas = card.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = CardOrder;
        EditorUtility.CopySerialized(introCanvas.GetComponent<CanvasScaler>(), card.GetComponent<CanvasScaler>());

        var white = new GameObject("White", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        white.transform.SetParent(card.transform, false);
        Stretch((RectTransform)white.transform);
        Image whiteImage = white.GetComponent<Image>();
        whiteImage.color = new Color(1f, 1f, 1f, 0f);
        whiteImage.raycastTarget = false;

        var logo = new GameObject("Logo", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage), typeof(VideoPlayer), typeof(StreamingVideo));
        logo.transform.SetParent(card.transform, false);
        var titleRect = (RectTransform)title;
        var logoRect = (RectTransform)logo.transform;
        logoRect.anchorMin = logoRect.anchorMax = new Vector2(0.5f, 0.5f);
        logoRect.pivot = new Vector2(0.5f, 0.5f);
        logoRect.sizeDelta = titleRect.rect.size;
        logoRect.anchoredPosition = Vector2.zero;
        RawImage titleImage = title.GetComponent<RawImage>();
        RawImage logoImage = logo.GetComponent<RawImage>();
        logoImage.texture = AssetDatabase.LoadAssetAtPath<RenderTexture>(LogoTexturePath);
        logoImage.uvRect = titleImage.uvRect;
        logoImage.color = Color.black;
        logoImage.raycastTarget = false;
        EditorUtility.CopySerialized(title.GetComponent<VideoPlayer>(), logo.GetComponent<VideoPlayer>());
        EditorUtility.CopySerialized(title.GetComponent<StreamingVideo>(), logo.GetComponent<StreamingVideo>());
        logo.SetActive(false);

        var so = new SerializedObject(flock);
        so.FindProperty("birds").objectReferenceValue = birds;
        so.FindProperty("white").objectReferenceValue = whiteImage;
        so.FindProperty("logo").objectReferenceValue = logo;
        so.ApplyModifiedPropertiesWithoutUndo();
        var endingSo = new SerializedObject(ending);
        endingSo.FindProperty("flock").objectReferenceValue = flock;
        endingSo.ApplyModifiedPropertiesWithoutUndo();

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(ending.gameObject.scene);
        return "ok: " + PrefabPath + ", " + Count + " birds, End Card order " + CardOrder + ", logo " + logoRect.sizeDelta;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
