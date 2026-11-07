using UnityEngine;
using UnityEngine.UI;

// Tuning helper for the discovery popup: drag these sliders (in Edit or Play mode) instead of guessing
// pixel values in code. Each slider is an anchored position on the popup's Box; the font fields cover
// both texts' typeface and size. Font left empty keeps whatever each Text already has.
[ExecuteAlways]
public class DiscoveryPopupLayout : MonoBehaviour
{
    [SerializeField] RectTransform label;
    [SerializeField] RectTransform video;
    [SerializeField] RectTransform escLabel;

    [Header("\"New Discovery\"")]
    [SerializeField, Range(0f, 550f)] float labelX = 28f;
    [SerializeField, Range(-40f, 40f)] float labelY;
    [SerializeField, Range(6f, 60f)] float labelFontSize = 22f;

    [Header("Quill video")]
    [SerializeField, Range(0f, 550f)] float videoX = 268f;
    [SerializeField, Range(-40f, 40f)] float videoY = -3f;
    [Tooltip("The VideoPlayer only ever renders a frame in Play mode, so the RawImage would otherwise show " +
        "nothing while tuning position/size here. Shown instead whenever not playing; swapped back to the " +
        "VideoPlayer's render texture automatically once Play starts.")]
    [SerializeField] Texture videoPreviewTexture;
    [SerializeField] RenderTexture videoLiveTexture;

    [Header("\"Press ESC to see\"")]
    [SerializeField, Range(0f, 550f)] float escX = 258f;
    [SerializeField, Range(-40f, 40f)] float escY = -2f;
    [SerializeField, Range(6f, 60f)] float escFontSize = 13f;

    [Header("Font (both texts; empty keeps each one's current font)")]
    [SerializeField] Font font;

    void OnValidate() => Apply();

    void Update()
    {
        if (!Application.isPlaying)
            Apply();
        else
            ApplyVideoPreview();
    }

    void Apply()
    {
        if (label != null)
        {
            label.anchoredPosition = new Vector2(labelX, labelY);
            ApplyFont(label, labelFontSize);
        }
        if (video != null)
            video.anchoredPosition = new Vector2(videoX, videoY);
        ApplyVideoPreview();
        if (escLabel != null)
        {
            escLabel.anchoredPosition = new Vector2(escX, escY);
            ApplyFont(escLabel, escFontSize);
        }
    }

    void ApplyVideoPreview()
    {
        if (video == null || !video.TryGetComponent(out RawImage rawImage))
            return;
        rawImage.texture = Application.isPlaying ? (Texture)videoLiveTexture : videoPreviewTexture;
    }

    void ApplyFont(RectTransform target, float size)
    {
        if (!target.TryGetComponent(out Text text))
            return;
        if (font != null)
            text.font = font;
        text.fontSize = Mathf.RoundToInt(size);
        // Both texts sit in a fixed-height box; Truncate (the Text default) renders nothing at all once a
        // single line no longer fits that height, which read as the text "disappearing" past some font size.
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
    }
}
