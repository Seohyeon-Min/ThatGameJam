using System.Collections.Generic;
using UnityEngine;
using TMPro;

// The notice in the bottom-right corner when an entry is found. Several finds in a row show one after another.
// Runs on unscaled time, so it is not held up by a paused game.
public class DiscoveryPopup : MonoBehaviour
{
    [SerializeField] RectTransform box;
    [SerializeField] CanvasGroup group;
    [SerializeField] TMP_Text title;
    [SerializeField, Min(0.01f)] float inSeconds = 0.3f;
    [SerializeField, Min(0f)] float holdSeconds = 3f;
    [SerializeField, Min(0.01f)] float outSeconds = 0.4f;
    [Tooltip("How far below its place the box starts, in canvas units.")]
    [SerializeField] float riseDistance = 60f;

    readonly Queue<string> waiting = new Queue<string>();
    Vector2 restPosition;
    float elapsed = -1f;

    void Awake()
    {
        restPosition = box.anchoredPosition;
        group.alpha = 0f;
    }

    public void Show(int entryNumber, EncyclopediaEntry entry) => waiting.Enqueue($"No. {entryNumber:00}  {entry.title}");

    void Update()
    {
        if (elapsed < 0f)
        {
            if (waiting.Count == 0)
                return;
            title.text = waiting.Dequeue();
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
