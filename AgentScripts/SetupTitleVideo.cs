using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.UI;
using UnityEngine.Video;

// Swaps the intro's grey title box in Main for the game's logo video (StreamingAssets/primavis_anim.webm): a VideoPlayer draws it into
// a render texture, which a RawImage shows where the grey box was, at its size. IntroSequence fades and hides it as before.
// Rerunning puts it in place again.
// unity command script run --file AgentScripts/SetupTitleVideo.cs --entry SetupTitleVideo.Run
public static class SetupTitleVideo
{
    const string TexturePath = "Assets/01_Arts/Video/TitleLogo.renderTexture";
    const int Width = 1920, Height = 1080;
    static readonly Vector2 BoxSize = new(640f, 200f);
    // Bottom of the band shown, as a fraction of the video's height from its bottom. The logo lies between about 0.21 and 0.65.
    const float BandBottom = 0.15f;

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

        // Where the grey box was, at its size. The video is wider than the box's 640:200, so the box shows the video's full width
        // and the band of its height the logo is in, unstretched.
        image.uvRect = new Rect(0f, BandBottom, 1f, BoxSize.y / BoxSize.x * Width / Height);
        if (title.TryGetComponent(out AspectRatioFitter fitter))
            Undo.DestroyObjectImmediate(fitter);
        var rect = (RectTransform)title.transform;
        Undo.RecordObject(rect, "Setup Title Video");
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.7f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = BoxSize;
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
