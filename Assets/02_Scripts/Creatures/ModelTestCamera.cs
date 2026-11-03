using UnityEngine;
using UnityEngine.InputSystem;

// Model test scenes only: the wheel zooms the fixed camera toward and away from the ground point it looks at,
// with the game's zoom steps, limits and easing (GameTuning, as BirdCamera). Direction only, per AGENTS.md.
public class ModelTestCamera : MonoBehaviour
{
    InputAction scrollAction;
    Vector3 pivot;
    Vector3 baseOffset;
    float targetZoom = 1f;
    float zoomFrom = 1f;
    float zoomTime = float.MaxValue;

    // 1 = the distance set in the scene.
    public float Zoom { get; private set; } = 1f;

    void Awake()
    {
        scrollAction = new InputAction("Scroll", InputActionType.Value, "<Mouse>/scroll");
        new Plane(Vector3.up, Vector3.zero).Raycast(new Ray(transform.position, transform.forward), out float distance);
        pivot = transform.position + transform.forward * distance;
        baseOffset = transform.position - pivot;
    }

    void OnEnable() => scrollAction.Enable();
    void OnDisable() => scrollAction.Disable();
    void OnDestroy() => scrollAction.Dispose();

    void LateUpdate()
    {
        GameTuning tuning = GameTuning.Current;
        float scroll = Encyclopedia.IsOpen ? 0f : scrollAction.ReadValue<Vector2>().y;
        float next = Mathf.Clamp(targetZoom - Mathf.Sign(scroll) * tuning.cameraZoomStep, tuning.cameraZoomMin, tuning.cameraZoomMax);
        if (scroll != 0f && next != targetZoom)
        {
            targetZoom = next;
            zoomFrom = Zoom;
            zoomTime = 0f;
        }
        zoomTime += Time.deltaTime;
        Zoom = zoomTime >= tuning.cameraZoomDuration
            ? targetZoom
            : Mathf.LerpUnclamped(zoomFrom, targetZoom, tuning.motionCurve.Evaluate(zoomTime / tuning.cameraZoomDuration));
        transform.position = pivot + baseOffset * Zoom;
    }
}
