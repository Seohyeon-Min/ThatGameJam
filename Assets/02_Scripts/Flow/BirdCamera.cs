using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

// The camera follows the bird (ADR 0005), not the baby: the two are separate beings (ADR 0007). The bird also locks the zones (ADR 0014).
// The bird flies over the area GameFlow opens to it, the road and the terrain beside it (ADR 0010, ADR 0013), and so does the point
// the camera follows. While a device's camera zone is locked, the point stays inside that zone's area. The bird itself never leaves
// the screen (ADR 0003 Q3); BirdController keeps it there (ADR 0015).
public class BirdCamera : MonoBehaviour
{
    [SerializeField] CinemachineCamera followCamera;
    // Seconds the camera takes to ease into or out of a zone's area.
    [SerializeField, Min(0.01f)] float zoneEaseTime = 1f;

    CameraZone[] zones;
    // Per zone, 0 free to 1 locked, eased so locking and unlocking glide, also when one zone hands over to the next.
    float[] zoneWeights;

    CinemachineFollow follow;
    // The offset authored in the Inspector (quarter-view angle and distance). Zoom scales its length only, so the angle never changes.
    Vector3 baseFollowOffset;
    InputAction scrollAction;
    // 1 = the authored distance. Raised/lowered one cameraZoomStep per wheel tick, direction only (AGENTS.md: wheel
    // magnitude isn't portable across browsers), then eased towards smoothly.
    float targetZoom = 1f;
    float zoom = 1f;
    // Dev tool: while set, the camera stays on this point instead of following the bird.
    Vector3? lockedPoint;

    public bool IsLocked => lockedPoint.HasValue;

    void Awake()
    {
        follow = followCamera.GetComponent<CinemachineFollow>();
        baseFollowOffset = follow.FollowOffset;
        // Value, not PassThrough: holding the wheel still at rest should read as (0, 0), not the last tick's delta.
        scrollAction = new InputAction("Scroll", InputActionType.Value, "<Mouse>/scroll");
    }

    void OnEnable() => scrollAction.Enable();
    void OnDisable() => scrollAction.Disable();
    void OnDestroy() => scrollAction.Dispose();

    // Only the current stage's zones lock.
    public void SetStage(Stage stage)
    {
        zones = stage.GetComponentsInChildren<CameraZone>();
        zoneWeights = new float[zones.Length];
    }

    // Dev tool: locks the camera on the point, or unlocks it. Cuts there at once.
    public void ToggleLock(Vector3 point)
    {
        lockedPoint = lockedPoint.HasValue ? null : point;
        Cut();
    }

    // Dev tool: if locked, moves the lock to this point.
    public void MoveLock(Vector3 point)
    {
        if (lockedPoint.HasValue)
            lockedPoint = point;
    }

    // Puts the camera on the bird at once instead of sweeping there.
    public void Cut()
    {
        LateUpdate();
        followCamera.PreviousStateIsValid = false;
    }

    void LateUpdate()
    {
        float scroll = scrollAction.ReadValue<Vector2>().y;
        if (scroll != 0f)
        {
            GameTuning tuning = GameTuning.Current;
            targetZoom = Mathf.Clamp(targetZoom - Mathf.Sign(scroll) * tuning.cameraZoomStep, tuning.cameraZoomMin, tuning.cameraZoomMax);
        }
        zoom = Mathf.MoveTowards(zoom, targetZoom, GameTuning.Current.cameraZoomSpeed * Time.deltaTime);
        follow.FollowOffset = baseFollowOffset * zoom;

        Vector3 bird = BirdController.Instance.transform.position;
        Bounds area = GameFlow.Instance.FlyArea;
        Vector3 point = ClampXZ(bird, area.min, area.max);
        for (int i = 0; i < zones.Length; i++)
        {
            CameraZone zone = zones[i];
            zone.TrackBird(bird);
            zoneWeights[i] = Mathf.MoveTowards(zoneWeights[i], zone.IsLocked ? 1f : 0f, Time.deltaTime / zoneEaseTime);
            point = Vector3.Lerp(point, ClampXZ(point, zone.AreaMin, zone.AreaMax), Mathf.SmoothStep(0f, 1f, zoneWeights[i]));
        }
        if (lockedPoint.HasValue)
            point = lockedPoint.Value;
        point.y = area.center.y;
        transform.position = point;
        BirdController.Instance.SetArea(area);
    }

    static Vector3 ClampXZ(Vector3 point, Vector3 min, Vector3 max) =>
        new(Mathf.Clamp(point.x, min.x, max.x), point.y, Mathf.Clamp(point.z, min.z, max.z));
}
