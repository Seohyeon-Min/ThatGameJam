using FMODUnity;
using UnityEngine;
using UnityEngine.Video;

// The notice in the bottom-right corner when an entry is found: "New Discovery", the quill-pen clip replaying
// beside it, and "Press TAB to see" above that, with event:/SFX/eastereggPop played alongside. Several finds in a
// row show one after another (the clip restarts and the sound replays each time). Runs on unscaled time, so it
// is not held up by a paused game.
public class DiscoveryPopup : MonoBehaviour
{
    [SerializeField] RectTransform box;
    [SerializeField] CanvasGroup group;
    [SerializeField] VideoPlayer quillVideo;
    [SerializeField, Min(0.01f)] float inSeconds = 0.3f;
    [SerializeField, Min(0f)] float holdSeconds = 3f;
    [SerializeField, Min(0.01f)] float outSeconds = 0.4f;
    [Tooltip("How far below its place the box starts, in canvas units.")]
    [SerializeField] float riseDistance = 60f;

    int pending;
    Vector2 restPosition;
    float elapsed = -1f;

    void Awake()
    {
        restPosition = box.anchoredPosition;
        group.alpha = 0f;
    }

    public void Show() => pending++;

    void Update()
    {
        if (elapsed < 0f)
        {
            if (pending == 0)
                return;
            pending--;
            quillVideo.Stop();
            quillVideo.Play();
            RuntimeManager.PlayOneShot("event:/SFX/eastereggPop");
            elapsed = 0f;
        }

        elapsed += Time.unscaledDeltaTime;
        float shown;
        if (elapsed < inSeconds)
            shown = Mathf.SmoothStep(0f, 1f, elapsed / inSeconds);
        else if (elapsed < inSeconds + holdSeconds)
            shown = 1f;
        else
            shown = 1f - Mathf.Clamp01((elapsed - inSeconds - holdSeconds) / outSeconds);

        group.alpha = shown;
        box.anchoredPosition = restPosition + Vector2.down * (riseDistance * (1f - shown));
        if (elapsed >= inSeconds + holdSeconds + outSeconds)
            elapsed = -1f;
    }
}
