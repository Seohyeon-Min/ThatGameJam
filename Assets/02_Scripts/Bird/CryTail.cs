using UnityEngine;

// Turns the bird's cry ring into a speech bubble whose point shows the way to the girl, when she is off the screen after the intro
// stage. Decided at each cry; while the ring lasts, the point keeps facing her on the screen. Sits on the ring's particle system,
// whose material is GlowRing.
[RequireComponent(typeof(ParticleSystem))]
public class CryTail : MonoBehaviour
{
    static readonly int TailId = Shader.PropertyToID("_Tail");
    static readonly int TailTipId = Shader.PropertyToID("_TailTip");

    [Tooltip("The point on the girl the tail shows, this high above her feet (metres).")]
    [SerializeField, Min(0f)] float girlHeight = 0.6f;

    ParticleSystem ring;
    ParticleSystemRenderer ringRenderer;
    MaterialPropertyBlock block;
    float baseSize;
    bool pointing;

    void Awake()
    {
        ring = GetComponent<ParticleSystem>();
        ringRenderer = GetComponent<ParticleSystemRenderer>();
        block = new MaterialPropertyBlock();
        baseSize = ring.main.startSizeMultiplier;
    }

    // Before the ring plays: sets whether this cry points, and scales the particle so the ring keeps its size beside the tail.
    public void Aim()
    {
        pointing = GameFlow.Instance.CurrentStage != GameFlow.Instance.Stages[0] && !OnScreen(GirlPoint);
        var main = ring.main;
        main.startSizeMultiplier = pointing ? baseSize * ringRenderer.sharedMaterial.GetFloat(TailTipId) : baseSize;
        SetTail(pointing ? TowardGirl() : Vector2.zero);
    }

    void LateUpdate()
    {
        if (pointing && ring.IsAlive(false))
            SetTail(TowardGirl());
    }

    Vector3 GirlPoint => Baby.Instance.transform.position + Vector3.up * girlHeight;

    static bool OnScreen(Vector3 point)
    {
        Vector3 viewport = Camera.main.WorldToViewportPoint(point);
        return viewport.z > 0f && viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f;
    }

    // From the ring to the girl across the screen, in the particle's own axes (it faces the camera unrotated).
    Vector2 TowardGirl()
    {
        Camera view = Camera.main;
        Vector3 from = view.WorldToViewportPoint(transform.position);
        Vector3 to = view.WorldToViewportPoint(GirlPoint);
        // Behind the camera the projection comes out mirrored.
        if (to.z < 0f)
            to = new Vector3(1f - to.x, 1f - to.y, -to.z);
        return new Vector2((to.x - from.x) * view.aspect, to.y - from.y);
    }

    void SetTail(Vector2 toward)
    {
        ringRenderer.GetPropertyBlock(block);
        block.SetVector(TailId, new Vector4(toward.x, toward.y, toward == Vector2.zero ? 0f : 1f, 0f));
        ringRenderer.SetPropertyBlock(block);
    }
}
