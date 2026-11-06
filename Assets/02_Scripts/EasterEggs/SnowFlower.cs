using System.Collections;
using UnityEngine;

// The flower in the snow (easter_eggs.md): the bird lands on the glowing snow and pecks a hole in it, and a flower shows there.
public class SnowFlower : MonoBehaviour
{
    [SerializeField] Discovery discovery;
    [SerializeField] Perchable glowingSnow;
    [Tooltip("Shrinks away as the bird digs.")]
    [SerializeField] Transform snow;
    [SerializeField] ParticleSystem glow;
    [Tooltip("Hidden until it is dug out, then grows to its authored scale.")]
    [SerializeField] Transform flower;
    [SerializeField, Min(0)] int pecks = 3;
    [SerializeField, Min(0.01f)] float showSeconds = 0.8f;

    void OnEnable() => glowingSnow.Perched += OnPerched;
    void OnDisable() => glowingSnow.Perched -= OnPerched;

    void OnPerched()
    {
        glowingSnow.CanPerch = false;
        StartCoroutine(Dig());
    }

    IEnumerator Dig()
    {
        yield return new WaitForSeconds(BirdController.Instance.Peck(pecks));
        glow.Stop();
        glowingSnow.Release();
        Vector3 snowScale = snow.localScale, flowerScale = flower.localScale;
        flower.localScale = Vector3.zero;
        flower.gameObject.SetActive(true);
        for (float t = 0f; t < showSeconds; t += Time.deltaTime)
        {
            float shown = Mathf.SmoothStep(0f, 1f, t / showSeconds);
            snow.localScale = snowScale * (1f - shown);
            flower.localScale = flowerScale * shown;
            yield return null;
        }
        snow.gameObject.SetActive(false);
        flower.localScale = flowerScale;
        discovery.Reveal();
    }
}
