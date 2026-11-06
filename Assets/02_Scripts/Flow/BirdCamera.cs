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
    // The farthest the wheel may zoom out in the current stage (its Max Altitude Share).
    float zoomMax = float.MaxValue;
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
    // From the moment the baby starts lying down at a sunlit spot until she wakes in the next stage, the camera eases onto her,
    // centred on her, and stays there, so the season's ring spreads around her over a still picture (the season wipe masks the new
    // season against a photo taken as it changes). Eased in and out like a zone. Before she lies down, the spot's semi-lock zone
    // (CameraZone without a device) keeps it in view.
    Vector3? restPoint;
    float restWeight;
    // While set (CliffEnding, once the girl sits by the stump), the camera eases onto this point and stays near it, drifting
    // after the bird by viewFollow only. Eased in and out like a zone; the point is kept while easing out.
    Vector3? viewPoint;
    bool viewHeld;
    float viewWeight;
    float viewFollow;

    public bool IsLocked => lockedPoint.HasValue;
    // Set by Freeze: the camera stays exactly where it is, height too.
    bool frozen;
    // The wheel has zoomed out as far as this stage lets it, and the zoom has got there.
    public bool IsZoomedAllTheWayOut => zoom >= Mathf.Min(zoomMax, GameTuning.Current.cameraZoomMax) - 0.001f;

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
        zoomMax = ZoomForAltitudeShare(stage);
        // Already zoomed out past it: eased back in to it.
        if (targetZoom > zoomMax)
        {
            targetZoom = zoomMax;
            zoomFrom = zoom;
            zoomTime = 0f;
        }
    }

    // The zoom at which the bird flies at the stage's Max Altitude Share of its highest height above the stage's ground. Height rises
    // with the zoom (BirdController.AltitudeForZoom), so halving the range finds it.
    static float ZoomForAltitudeShare(Stage stage)
    {
        GameTuning tuning = GameTuning.Current;
        if (stage.maxAltitudeShare >= 1f)
            return tuning.cameraZoomMax;
        float ground = stage.area.center.y;
        float Height(float zoom) => BirdController.AltitudeForZoom(zoom, stage.birdAltitude, ground) - ground;
        float goal = Height(tuning.cameraZoomMax) * stage.maxAltitudeShare;
        float low = tuning.cameraZoomMin, high = tuning.cameraZoomMax;
        for (int i = 0; i < 24; i++)
        {
            float middle = (low + high) * 0.5f;
            if (Height(middle) > goal)
                high = middle;
            else
                low = middle;
        }
        return low;
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

    void UpdateRestHold()
    {
        BabyState state = Baby.Instance.State;
        // She starts lying down: from here the camera settles on her.
        if (!restPoint.HasValue && state == BabyState.Resting)
        {
            restPoint = Baby.Instance.transform.position;
            restWeight = 0f;
        }
        if (!restPoint.HasValue)
            return;
        bool asleep = state == BabyState.Resting || state == BabyState.Sleeping;
        restWeight = Mathf.MoveTowards(restWeight, asleep ? 1f : 0f, Time.deltaTime / zoneEaseTime);
        if (!asleep && restWeight <= 0f)
            restPoint = null;
    }

    // Eases onto this point and stays near it, drifting after the bird by this much (0 = held still, 1 = free); null eases off it.
    public void HoldView(Vector3? point, float follow)
    {
        viewHeld = point.HasValue;
        if (point.HasValue)
        {
            if (!viewPoint.HasValue)
                viewWeight = 0f;
            viewPoint = point;
            viewFollow = follow;
        }
    }

    void UpdateViewHold()
    {
        if (!viewPoint.HasValue)
            return;
        viewWeight = Mathf.MoveTowards(viewWeight, viewHeld ? 1f : 0f, Time.deltaTime / zoneEaseTime);
        if (!viewHeld && viewWeight <= 0f)
            viewPoint = null;
    }

    // The ending: stays exactly where it is from now on, whatever the bird does (it flies out of the picture).
    public void Freeze() => frozen = true;

    // Puts the camera where it frames at once instead of sweeping there.
    public void Cut()
    {
        snap = true;
        LateUpdate();
        followCamera.PreviousStateIsValid = false;
    }

    void LateUpdate()
    {
        if (frozen)
            return;
        GameTuning tuning = GameTuning.Current;
        // A cutscene holding the player still (Lock Player) holds the zoom too: the wheel would move the bird's height.
        // So does the open encyclopedia, which takes the mouse.
        float scroll = Cutscene.PlayerLocked || Encyclopedia.IsOpen ? 0f : scrollAction.ReadValue<Vector2>().y;
        float next = Mathf.Clamp(targetZoom - Mathf.Sign(scroll) * tuning.cameraZoomStep, tuning.cameraZoomMin, Mathf.Min(zoomMax, tuning.cameraZoomMax));
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
        Bounds cameraArea = GameFlow.Instance.CameraArea;
        Vector3 point = ClampXZ(Framed(bird, tuning), cameraArea.min, cameraArea.max);
        for (int i = 0; i < zones.Length; i++)
        {
            CameraZone zone = zones[i];
            zone.TrackBird(bird);
            zoneWeights[i] = Mathf.MoveTowards(zoneWeights[i], zone.Hold, Time.deltaTime / zoneEaseTime);
            point = Vector3.Lerp(point, zone.SemiFollow(point), Mathf.SmoothStep(0f, 1f, zoneWeights[i]));
        }
        if (heldPoint.HasValue)
        {
            UpdateHold();
            point = heldPoint ?? point;
        }
        UpdateViewHold();
        if (viewPoint.HasValue)
        {
            Vector3 view = new(viewPoint.Value.x, point.y, viewPoint.Value.z);
            point = Vector3.Lerp(point, Vector3.Lerp(view, point, viewFollow), Mathf.SmoothStep(0f, 1f, viewWeight));
        }
        UpdateRestHold();
        if (restPoint.HasValue)
            point = Vector3.Lerp(point, new Vector3(restPoint.Value.x, point.y, restPoint.Value.z), Mathf.SmoothStep(0f, 1f, restWeight));
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
