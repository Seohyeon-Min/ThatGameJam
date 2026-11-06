using UnityEngine;
using UnityEngine.Video;

// Plays a video from Assets/StreamingAssets as soon as this turns on. The Web build can only play a video from a URL, not from a
// VideoClip asset, and the URL of StreamingAssets is only known at run time.
[RequireComponent(typeof(VideoPlayer))]
public class StreamingVideo : MonoBehaviour
{
    [Tooltip("File name in Assets/StreamingAssets.")]
    [SerializeField] string fileName = "primavis_anim.webm";

    void OnEnable()
    {
        VideoPlayer player = GetComponent<VideoPlayer>();
        // A render texture keeps its last picture, which would show until the first frame comes in.
        if (player.targetTexture != null)
        {
            RenderTexture active = RenderTexture.active;
            RenderTexture.active = player.targetTexture;
            GL.Clear(false, true, Color.clear);
            RenderTexture.active = active;
        }
        player.url = Application.streamingAssetsPath + "/" + fileName;
        player.Play();
    }
}
