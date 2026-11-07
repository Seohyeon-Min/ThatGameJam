using Unity.Cinemachine;
using UnityEngine;

// When the bird refuses to go on (the closed way, BirdController's Refuse), the screen gives a quick "no" shake, as for a wrong
// click: the camera jolts side to side, fast, a few times, dying away at once. On the camera that follows the bird; it moves the
// camera a little across the screen (after its aim and noise), not where it looks.
public class RefuseShake : CinemachineExtension
{
    static RefuseShake current;

    [Tooltip("Seconds after the bird starts refusing before the shake.")]
    [SerializeField, Min(0f)] float delay = 0f;
    [Tooltip("Seconds the shake lasts, how many times it goes side to side and back, and metres it moves to each side at the first.")]
    [SerializeField, Min(0.05f)] float seconds = 0.35f;
    [SerializeField, Min(0.5f)] float shakes = 4f;
    [SerializeField, Min(0f)] float metres = 0.18f;
    [Tooltip("Share of that it also jolts up and down, a little out of step, so it reads as a shake and not a slide.")]
    [SerializeField, Range(0f, 1f)] float upDown = 0.25f;

    float startedAt = -1f;

    // The bird has started refusing: the screen shakes, if the camera following it has this.
    public static void Shake()
    {
        if (current != null)
            current.startedAt = Time.time + current.delay;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        current = this;
    }

    protected override void OnDestroy()
    {
        if (current == this)
            current = null;
        base.OnDestroy();
    }

    protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage,
        ref CameraState state, float deltaTime)
    {
        if (stage != CinemachineCore.Stage.Noise || startedAt < 0f)
            return;
        float t = (Time.time - startedAt) / seconds;
        if (t < 0f || t >= 1f)
            return;
        // Full at once, dying away fast.
        float strength = (1f - t) * (1f - t);
        float phase = t * shakes * 2f * Mathf.PI;
        Quaternion facing = state.GetFinalOrientation();
        Vector3 offset = facing * new Vector3(Mathf.Sin(phase), Mathf.Sin(phase * 1.7f + 0.6f) * upDown, 0f);
        state.PositionCorrection += offset * (metres * strength);
    }
}
