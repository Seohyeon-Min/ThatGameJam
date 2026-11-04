using UnityEngine;

// A thick white ring that grows from the middle of something out to about its size and fades, like a sound wave (ADR 0028).
// Drawn by a LineRenderer facing the camera, so it reads as a circle in the quarter view.
[RequireComponent(typeof(LineRenderer))]
public class CryRing : MonoBehaviour
{
    const int Points = 48;
    static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

    [SerializeField] float duration = 0.6f;
    // The ring starts at this share of its full radius.
    [SerializeField, Range(0f, 1f)] float startRadius = 0.35f;
    // Line width as a share of the full radius.
    [SerializeField] float thickness = 0.3f;

    LineRenderer line;
    Vector3 center;
    float radius;
    float time;

    void Awake()
    {
        line = GetComponent<LineRenderer>();
        line.positionCount = Points;
        line.loop = true;
        line.useWorldSpace = true;
        // The material is URP's particle material, whose own texture is a soft dot: a white texture keeps the line solid.
        var block = new MaterialPropertyBlock();
        block.SetTexture(BaseMapId, Texture2D.whiteTexture);
        line.SetPropertyBlock(block);
        line.enabled = false;
    }

    public void Play(Vector3 center, float radius)
    {
        this.center = center;
        this.radius = radius;
        time = 0f;
        line.enabled = true;
    }

    void LateUpdate()
    {
        if (!line.enabled)
            return;
        time += Time.deltaTime;
        if (time >= duration)
        {
            line.enabled = false;
            return;
        }
        float t = time / duration;
        float r = radius * Mathf.Lerp(startRadius, 1f, 1f - (1f - t) * (1f - t));
        line.widthMultiplier = radius * thickness * (1f - 0.5f * t);
        Color color = Color.white;
        color.a = 1f - t * t;
        line.startColor = line.endColor = color;
        Transform view = Camera.main.transform;
        // Brought toward the camera by its radius, so the thing it sounds from doesn't hide half of it.
        Vector3 middle = center - view.forward * radius;
        for (int i = 0; i < Points; i++)
        {
            float angle = i * Mathf.PI * 2f / Points;
            line.SetPosition(i, middle + (view.right * Mathf.Cos(angle) + view.up * Mathf.Sin(angle)) * r);
        }
    }
}
