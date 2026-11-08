using UnityEngine;
using UnityEngine.UI;

// The discovery popup's quill clip, seen while editing: the VideoPlayer only ever renders a frame in Play mode, so outside it
// the clip's RawImage shows a still (Video Preview Texture) and in Play mode the VideoPlayer's render texture (Video Live
// Texture). The popup's texts and clip are placed by hand (their RectTransforms); nothing here moves or resizes them.
[ExecuteAlways]
public class DiscoveryPopupLayout : MonoBehaviour
{
    [SerializeField] RectTransform video;
    [Tooltip("Shown on the clip whenever not playing; swapped for the VideoPlayer's render texture once Play starts.")]
    [SerializeField] Texture videoPreviewTexture;
    [SerializeField] RenderTexture videoLiveTexture;

    void OnValidate() => ApplyVideoPreview();

    void Update() => ApplyVideoPreview();

    void ApplyVideoPreview()
    {
        if (video == null || !video.TryGetComponent(out RawImage rawImage))
            return;
        Texture wanted = Application.isPlaying ? videoLiveTexture : videoPreviewTexture;
        if (rawImage.texture != wanted)
            rawImage.texture = wanted;
    }
}
