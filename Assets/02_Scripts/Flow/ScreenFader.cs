using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Covers and uncovers the screen with a full-screen image during stage transitions.
public class ScreenFader : MonoBehaviour
{
    [SerializeField] Image image;

    void Awake()
    {
        image.raycastTarget = false;
        SetAlpha(0f);
    }

    public IEnumerator FadeOut() => Fade(0f, 1f);

    public IEnumerator FadeIn() => Fade(1f, 0f);

    IEnumerator Fade(float from, float to)
    {
        float duration = GameTuning.Current.fadeDuration;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            SetAlpha(Mathf.Lerp(from, to, t / duration));
            yield return null;
        }
        SetAlpha(to);
    }

    void SetAlpha(float alpha)
    {
        Color color = image.color;
        color.a = alpha;
        image.color = color;
    }
}
