using UnityEngine;

// A thick yellow ring that grows from the middle of something out to about its size and fades, like a sound wave (ADR 0028).
// Drawn by a LineRenderer facing the camera, so it reads as a circle in the quarter view. When the thing is off the screen, the ring
// shows at the screen's edge in its direction. Its sorting order puts it over the fog.
[RequireComponent(typeof(LineRenderer))]
public class CryRing : MonoBehaviour
{
    const int Points = 48;
    static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

    [SerializeField] float duration = 0.6f;
    [SerializeField] Color color = new(1f, 0.85f, 0.15f);
    // The ring starts at this share of its full radius.
    [SerializeField, Range(0f, 1f)] float startRadius = 0.35f;
    // Line width as a share of the full radius.
    [SerializeField] float thickness = 0.3f;
    // At the screen's edge the whole ring stays this far inside it, as a share of the screen's height.
    [SerializeField, Range(0f, 0.2f)] float edgeInset = 0.03f;
    // At the screen's edge the ring is at least this big, as a share of the screen's height: a small animal far off would hardly show.
    [SerializeField, Range(0f, 0.3f)] float edgeMinRadius = 0.06f;
    // At the screen's edge the ring is drawn at most this far from the camera, so the trees in between don't hide it.
    [SerializeField, Min(0.5f)] float edgeDepth = 5f;

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
        Camera view = Camera.main;
        Transform eye = view.transform;
        // Brought toward the camera by its radius, so the thing it sounds from doesn't hide half of it.
        Vector3 middle = OnScreen(view, center - eye.forward * radius, out float size);
        float r = size * Mathf.Lerp(startRadius, 1f, 1f - (1f - t) * (1f - t));
        line.widthMultiplier = size * thickness * (1f - 0.5f * t);
        Color faded = color;
        faded.a *= 1f - t * t;
        line.startColor = line.endColor = faded;
        for (int i = 0; i < Points; i++)
        {
            float angle = i * Mathf.PI * 2f / Points;
            line.SetPosition(i, middle + (eye.right * Mathf.Cos(angle) + eye.up * Mathf.Sin(angle)) * r);
        }
    }

    // The point, or if it is off the screen, a point on the screen's edge toward it, with the full ring inside.
    // The ring's full radius there: its own, and at the edge at least edgeMinRadius.
    Vector3 OnScreen(Camera view, Vector3 point, out float size)
    {
        size = radius;
        Vector3 viewport = view.WorldToViewportPoint(point);
        // Behind the camera the projection comes out mirrored. Nearer than the near plane it is not drawn either.
        bool hidden = viewport.z < view.nearClipPlane;
        if (viewport.z < 0f)
            viewport = new Vector3(1f - viewport.x, 1f - viewport.y, -viewport.z);
        viewport.z = Mathf.Max(viewport.z, view.nearClipPlane);
        Vector2 offset = new(viewport.x - 0.5f, viewport.y - 0.5f);
        if (!hidden && Mathf.Abs(offset.x) <= 0.5f && Mathf.Abs(offset.y) <= 0.5f)
            return point;
        if (offset.sqrMagnitude < 1e-6f)
            offset = Vector2.down;
        // Nearer the camera, smaller by as much, so it looks the same size.
        float depth = Mathf.Min(viewport.z, edgeDepth);
        float screenHeight = 2f * depth * Mathf.Tan(view.fieldOfView * 0.5f * Mathf.Deg2Rad);
        size = Mathf.Max(radius * depth / viewport.z, edgeMinRadius * screenHeight);
        float insetY = Mathf.Min(size * (1f + thickness) / screenHeight + edgeInset, 0.45f);
        float insetX = Mathf.Min(insetY / view.aspect, 0.45f);
        offset /= Mathf.Max(Mathf.Abs(offset.x) / (0.5f - insetX), Mathf.Abs(offset.y) / (0.5f - insetY));
        return view.ViewportToWorldPoint(new Vector3(0.5f + offset.x, 0.5f + offset.y, depth));
    }
}
