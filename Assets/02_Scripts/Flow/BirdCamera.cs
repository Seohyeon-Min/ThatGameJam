using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

// The camera follows the bird (ADR 0005). The bird also locks the zones (ADR 0014). The bird flies over the area GameFlow opens to
// it, the road and the terrain beside it (ADR 0010, ADR 0013), and so does the point the camera follows. Alone, the bird sits in the
// middle of the screen. Nearer the baby, the point slides over to between the two by how close they are, so both are on screen,
// and holds still while that framing stays good; as the bird flies off, it slides back onto the bird. While a device's camera zone is locked, the point stays
// inside that zone's area. The bird itself never leaves the screen (ADR 0003 Q3); BirdController keeps it
// there (ADR 0015). The wheel zooms, and the bird flies lower as the camera comes closer (BirdController.SetZoom).
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
    // magnitude isn't portable across browsers), then eased there from zoomFrom along GameTuning.motionCurve.
    float targetZoom = 1f;
    float zoom = 1f;
    float zoomFrom = 1f;
    float zoomTime = float.MaxValue;
    // Dev tool: while set, the camera stays on this point instead of following the bird.
    Vector3? lockedPoint;
    // While set, the camera stays on this point while the bird lands and sits, and follows again once the bird flies off.
    Vector3? heldPoint;
    bool heldBirdPerching;
    // Framing the bird with the baby (GameTuning camera "together" values): where the framing stays while its subject moves within
    // the dead zone.
    Vector3 held;
    // Set by Cut: the next frame frames at once instead of easing.
    bool snap = true;

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

    // Dev tool: stays on this point, or follows the bird again with null. Cuts there at once.
    public void LockOn(Vector3? point)
    {
        lockedPoint = point;
        Cut();
    }

    // Tools menu, outside Play Mode: puts what the camera follows on this point, cutting there.
    public void JumpInEditor(Vector3 point)
    {
        transform.position = point;
        followCamera.PreviousStateIsValid = false;
    }

    // Dev tool: if locked, moves the lock to this point.
    public void MoveLock(Vector3 point)
    {
        if (lockedPoint.HasValue)
            lockedPoint = point;
    }

    // The opening shot: the bird lands on a tree in the frame around this point instead of the camera centring on it.
    public void HoldWhileBirdPerches(Vector3 point)
    {
        heldPoint = point;
        heldBirdPerching = false;
    }

    void UpdateHold()
    {
        if (BirdController.Instance.IsPerchedOrLanding)
            heldBirdPerching = true;
        else if (heldBirdPerching)
            heldPoint = null;
    }

    // Puts the camera where it frames at once instead of sweeping there.
    public void Cut()
    {
        snap = true;
        LateUpdate();
        followCamera.PreviousStateIsValid = false;
    }

    void LateUpdate()
    {
        GameTuning tuning = GameTuning.Current;
        // A cutscene holding the player still (Lock Player) holds the zoom too: the wheel would move the bird's height.
        // So does the open encyclopedia, which takes the mouse.
        float scroll = Cutscene.PlayerLocked || Encyclopedia.IsOpen ? 0f : scrollAction.ReadValue<Vector2>().y;
        float next = Mathf.Clamp(targetZoom - Mathf.Sign(scroll) * tuning.cameraZoomStep, tuning.cameraZoomMin, tuning.cameraZoomMax);
        if (scroll != 0f && next != targetZoom)
        {
            targetZoom = next;
            zoomFrom = zoom;
            zoomTime = 0f;
        }
        zoomTime += Time.deltaTime;
        zoom = zoomTime >= tuning.cameraZoomDuration
            ? targetZoom
            : Mathf.LerpUnclamped(zoomFrom, targetZoom, tuning.motionCurve.Evaluate(zoomTime / tuning.cameraZoomDuration));
        follow.FollowOffset = baseFollowOffset * zoom;
        BirdController.Instance.SetZoom(zoom, targetZoom);

        Vector3 bird = BirdController.Instance.transform.position;
        Bounds area = GameFlow.Instance.FlyArea;
        Vector3 point = ClampXZ(Framed(bird, tuning), area.min, area.max);
        for (int i = 0; i < zones.Length; i++)
        {
            CameraZone zone = zones[i];
            zone.TrackBird(bird);
            zoneWeights[i] = Mathf.MoveTowards(zoneWeights[i], zone.IsLocked ? 1f : 0f, Time.deltaTime / zoneEaseTime);
            point = Vector3.Lerp(point, ClampXZ(point, zone.AreaMin, zone.AreaMax), Mathf.SmoothStep(0f, 1f, zoneWeights[i]));
        }
        if (heldPoint.HasValue)
        {
            UpdateHold();
            point = heldPoint ?? point;
        }
        if (lockedPoint.HasValue)
            point = new Vector3(lockedPoint.Value.x, bird.y, lockedPoint.Value.z);
        transform.position = point;
        BirdController.Instance.SetArea(area);
    }

    // From the bird alone to between the bird and the baby, by how far apart they are only, so the camera never moves on after the
    // bird stops and every change of pace is gradual. Distances scale with the zoom, as the view does. Within the dead zone the
    // framing holds still; past it, it follows by an amount that grows with how far past it is, so it never sets off with a jolt.
    // The point never strays further than cameraTogetherMaxBirdScreen of the view from the bird, whatever the window's shape, so the
    // bird can't get stuck at the screen's edge (BirdController.Limit) while the camera holds near the baby.
    Vector3 Framed(Vector3 bird, GameTuning tuning)
    {
        Vector3 baby = Baby.Instance.transform.position;
        float gap = Vector2.Distance(new Vector2(bird.x, bird.z), new Vector2(baby.x, baby.z)) / zoom;
        // 1 framing both up to the near distance, easing down to 0, the bird alone, at the far distance.
        float weight = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(tuning.cameraTogetherNearDistance, tuning.cameraTogetherFarDistance, gap));
        Vector3 subject = Vector3.Lerp(bird, baby, tuning.cameraTogetherBabyShare * weight);
        if (snap)
            held = subject;
        snap = false;

        // The dead zone and the follow time shrink with the weight, so the bird alone is held exactly in the middle.
        Vector3 toSubject = subject - held;
        float past = toSubject.magnitude - tuning.cameraTogetherDeadZone * zoom * weight;
        if (past > 0f)
        {
            float pull = weight > 0f ? 1f - Mathf.Exp(-Time.deltaTime / (tuning.cameraTogetherFollowTime * weight)) : 1f;
            held += toSubject.normalized * (past * pull);
        }

        // The camera looks at the point from distance d. A bird r from the point, in any direction (nearer the camera shows larger),
        // is at most r / sqrt(d² - r²) off the line of sight in tangent terms. Keeping that within the fraction k of the shorter
        // half view gives r <= d k / sqrt(1 + k²).
        LensSettings lens = followCamera.Lens;
        float k = tuning.cameraTogetherMaxBirdScreen * Mathf.Tan(lens.FieldOfView * 0.5f * Mathf.Deg2Rad) * Mathf.Min(1f, lens.Aspect);
        float maxOffset = follow.FollowOffset.magnitude * k / Mathf.Sqrt(1f + k * k);
        held = bird + Vector3.ClampMagnitude(held - bird, maxOffset);
        return held;
    }

    static Vector3 ClampXZ(Vector3 point, Vector3 min, Vector3 max) =>
        new(Mathf.Clamp(point.x, min.x, max.x), point.y, Mathf.Clamp(point.z, min.z, max.z));
}
