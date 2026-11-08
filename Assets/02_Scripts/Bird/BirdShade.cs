using UnityEngine;

// A soft dark spot behind the bird (a quad with SoftShade), so it shows against the bright pink of the cliff once out of the fog.
// It sits a little behind the bird, away from the camera, facing the camera, and follows it. It is never cut off by the ground
// (SoftShade draws over everything but the bird and the characters, which stay on top of it).
// Clear until Show(true) (CliffEnding, out of the fog), and fades away again on Show(false) (the ending, or a dev tool going back).
[RequireComponent(typeof(MeshRenderer))]
public class BirdShade : MonoBehaviour
{
    static readonly int FadeId = Shader.PropertyToID("_Fade");

    [Tooltip("Metres behind the bird, away from the camera.")]
    [SerializeField, Min(0f)] float behind = 0.6f;
    [Tooltip("Width of the spot in metres.")]
    [SerializeField, Min(0.1f)] float width = 2.6f;
    [Tooltip("Seconds it takes to fade in or out.")]
    [SerializeField, Min(0.01f)] float fadeSeconds = 1.5f;

    MeshRenderer spot;
    MaterialPropertyBlock block;
    float fade;
    float target;

    void Awake()
    {
        spot = GetComponent<MeshRenderer>();
        block = new MaterialPropertyBlock();
        Apply();
    }

    public void Show(bool show) => target = show ? 1f : 0f;

    void LateUpdate()
    {
        fade = Mathf.MoveTowards(fade, target, Time.deltaTime / fadeSeconds);
        Apply();
        if (fade <= 0f)
            return;
        BirdController bird = BirdController.Instance;
        Camera view = Camera.main;
        if (bird == null || view == null)
            return;
        Vector3 at = bird.transform.position;
        Vector3 away = (at - view.transform.position).normalized;
        transform.SetPositionAndRotation(at + away * behind, Quaternion.LookRotation(away, view.transform.up));
        transform.localScale = new Vector3(width, width, 1f);
    }

    void Apply()
    {
        spot.enabled = fade > 0f;
        block.SetFloat(FadeId, Mathf.SmoothStep(0f, 1f, fade));
        spot.SetPropertyBlock(block);
    }
}
