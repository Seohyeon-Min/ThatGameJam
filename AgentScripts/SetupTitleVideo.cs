using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.UI;
using UnityEngine.Video;

// Swaps the intro's grey title box in Main for the game's logo video (StreamingAssets/primavis_anim.webm): a VideoPlayer draws it into
// a render texture, which a RawImage shows over the whole screen at the video's 16:9. IntroSequence fades and hides it as before.
// Rerunning puts it in place again.
// unity command script run --file AgentScripts/SetupTitleVideo.cs --entry SetupTitleVideo.Run
public static class SetupTitleVideo
{
    const string TexturePath = "Assets/01_Arts/Video/TitleLogo.renderTexture";
    const int Width = 1920, Height = 1080;

    public static string Run()
    {
        var intro = Object.FindAnyObjectByType<IntroSequence>(FindObjectsInactive.Include);
        if (intro == null)
            return "No IntroSequence in the open scene.";
        var serialized = new SerializedObject(intro);
        SerializedProperty titleProperty = serialized.FindProperty("title");
        var oldTitle = (Graphic)titleProperty.objectReferenceValue;
        GameObject title = oldTitle.gameObject;

        var texture = AssetDatabase.LoadAssetAtPath<RenderTexture>(TexturePath);
        if (texture == null)
        {
            AssetDatabase.CreateFolder("Assets/01_Arts", "Video");
            texture = new RenderTexture(Width, Height, GraphicsFormat.R8G8B8A8_SRGB, GraphicsFormat.None) { name = "TitleLogo" };
            AssetDatabase.CreateAsset(texture, TexturePath);
        }

        if (oldTitle is not RawImage)
            Undo.DestroyObjectImmediate(oldTitle);
        if (!title.TryGetComponent(out RawImage image))
            image = Undo.AddComponent<RawImage>(title);
        Undo.RecordObject(image, "Setup Title Video");
        image.texture = texture;
        image.color = Color.white;
        image.raycastTarget = false;

        var rect = (RectTransform)title.transform;
        Undo.RecordObject(rect, "Setup Title Video");
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        if (!title.TryGetComponent(out AspectRatioFitter fitter))
            fitter = Undo.AddComponent<AspectRatioFitter>(title);
        Undo.RecordObject(fitter, "Setup Title Video");
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = (float)Width / Height;

        if (!title.TryGetComponent(out VideoPlayer player))
            player = Undo.AddComponent<VideoPlayer>(title);
        Undo.RecordObject(player, "Setup Title Video");
        player.playOnAwake = false;
        player.source = VideoSource.Url;
        player.renderMode = VideoRenderMode.RenderTexture;
        player.targetTexture = texture;
        player.audioOutputMode = VideoAudioOutputMode.None;
        player.isLooping = false;
        player.waitForFirstFrame = true;
        if (title.GetComponent<StreamingVideo>() == null)
            Undo.AddComponent<StreamingVideo>(title);

        titleProperty.objectReferenceValue = image;
        serialized.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(title.scene);
        EditorSceneManager.SaveScene(title.scene);
        AssetDatabase.SaveAssets();
        return $"Title {title.name}: RawImage on {TexturePath}, VideoPlayer, StreamingVideo. Scene saved.";
    }
}
