using UnityEngine;

// Small ring that briefly marks a clicked point, shrinking and fading out.
// Keep the object active; only its renderer is toggled. Fading needs a transparent material with _BaseColor (URP).
public class ClickMarker : MonoBehaviour
{
    [SerializeField] float duration = 0.4f;
    // Lifts the ring off the ground so it does not z-fight.
    [SerializeField] float lift = 0.02f;

    Renderer ring;
    MaterialPropertyBlock block;
    Vector3 baseScale;
    Color baseColor;
    float time;

    void Awake()
    {
        ring = GetComponentInChildren<Renderer>();
        block = new MaterialPropertyBlock();
        baseScale = transform.localScale;
        baseColor = ring.sharedMaterial.GetColor("_BaseColor");
        ring.enabled = false;
    }

    public void Show(Vector3 point)
    {
        transform.position = point + Vector3.up * lift;
        time = 0f;
        ring.enabled = true;
        Apply(0f);
    }

    void Update()
    {
        if (!ring.enabled)
            return;
        time += Time.deltaTime;
        if (time >= duration)
            ring.enabled = false;
        else
            Apply(time / duration);
    }

    void Apply(float t)
    {
        transform.localScale = baseScale * (1f - 0.5f * t);
        block.SetColor("_BaseColor", new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * (1f - t)));
        ring.SetPropertyBlock(block);
    }
}
