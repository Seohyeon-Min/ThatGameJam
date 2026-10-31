using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

// Player-controlled bird: left-click flies to a point or lands on a Perchable, holding the button keeps following the pointer,
// right-click cries. Left idle in the air near a branch, it lands on it by itself (birdIdlePerchRadius) rather than
// hopping the camera to any branch on screen. Sent past where the way is still closed
// (the current stage's sunlit spot), it flies up to that line, then lands on the nearest branch and shakes its head (ADR 0011, ADR 0016).
// Flies at the current map's fixed altitude inside the area BirdCamera gives and on screen, and perches only on screen (ADR 0015).
// The transform is where the bird's feet are, so it stands on a perch point.
public class BirdController : MonoBehaviour
{
    static readonly int PerchedParam = Animator.StringToHash("Perched");
    static readonly int MovingParam = Animator.StringToHash("Moving");
    static readonly int RefuseParam = Animator.StringToHash("Refuse");

    [SerializeField] Animator animator;
    [SerializeField] ClickMarker clickMarker;
    // Rolled for the turn bank (Face), separate from this transform so the root's rotation stays pure yaw
    // for perch alignment and area clamping. Usually the Animator's GameObject (e.g. "Bird-Animated").
    [SerializeField] Transform visualRoot;
    [SerializeField] Texture2D perchCursor;
    [SerializeField] Vector2 perchCursorHotspot;
    // The bird and the branches it perches on stay this far inside the screen's edges, as a fraction of the screen (ADR 0015).
    [SerializeField, Range(0f, 0.3f)] float screenMargin = 0.08f;
    [SerializeField] UnityEvent onCry;

    enum State { Flying, ToPerch, Landing, Perched }

    public static BirdController Instance { get; private set; }

    public bool IsPerched => state == State.Perched;

    InputAction moveAction;
    InputAction cryAction;
    InputAction pointAction;
    int clickMask;
    int groundMask;
    int interactableLayer;
    State state;
    // Horizontal target while flying. Y is unused; the bird's height follows altitude.
    Vector3 target;
    float altitude;
    // Current horizontal speed, ramping toward GameTuning.birdSpeed (or down, braking into a landing).
    float currentSpeed;
    // True from the moment a perch is left until the Takeoff clip's FootRelease event fires: the body holds
    // still at the branch so it doesn't glide away while the feet are still visually gripping it.
    bool liftingOff;
    float liftingOffTimer;
    float bankAngle;
    float facingYaw;
    // visualRoot's authored local rotation (e.g. a corrective 180Y flip to face the model forward). Bank is
    // composed on top of this, never replacing it.
    Quaternion visualBaseRotation = Quaternion.identity;
    // Where it flies, in X and Z (GameFlow.FlyArea). Past its far Z end the way ahead is closed: the bird refuses there (ADR 0012, ADR 0016).
    Bounds bounds = new(Vector3.zero, Vector3.positiveInfinity);
    // Shakes its head once it has landed.
    bool refusePending;
    readonly List<Vector2> screenPatch = new();
    readonly List<Vector2> clipped = new();
    float idleTime;
    bool following;
    Perchable perch;
    float landTime;
    // The turn while landing, from the heading it arrived with.
    Quaternion landFrom, landTo;
    float nextCryTime;
    bool perchCursorShown;
    Vector3 baseScale;

    void Awake()
    {
        Instance = this;
        target = transform.position;
        altitude = transform.position.y;
        baseScale = transform.localScale;
        facingYaw = transform.eulerAngles.y;
        if (visualRoot != null)
            visualBaseRotation = visualRoot.localRotation;
        clickMask = LayerMask.GetMask("Ground", "Interactable", "ClickPlane");
        groundMask = LayerMask.GetMask("Ground", "ClickPlane");
        interactableLayer = LayerMask.NameToLayer("Interactable");

        moveAction = new InputAction("Move", InputActionType.Button, "<Mouse>/leftButton");
        cryAction = new InputAction("Cry", InputActionType.Button, "<Mouse>/rightButton");
        // Pass-through: the most recently moved mouse wins, instead of the one with the largest value.
        pointAction = new InputAction("Point", InputActionType.PassThrough, "<Mouse>/position");
    }

    void OnEnable()
    {
        moveAction.Enable();
        cryAction.Enable();
        pointAction.Enable();
    }

    void OnDisable()
    {
        moveAction.Disable();
        cryAction.Disable();
        pointAction.Disable();
        ShowPerchCursor(false);
    }

    void OnDestroy()
    {
        moveAction.Dispose();
        cryAction.Dispose();
        pointAction.Dispose();
    }

    public void Teleport(Vector3 position)
    {
        LeavePerch();
        following = false;
        idleTime = 0f;
        liftingOff = false;
        currentSpeed = 0f;
        transform.position = position;
        target = position;
    }

    // Hooked to BirdAnimationEvents.onFootRelease (Bird_Takeoff, feet leave the branch): the body was holding
    // still at the perch since LeavePerch and starts moving/rising from here, instead of jumping to speed the
    // instant the player clicks away while the Takeoff clip is still showing the feet gripping it.
    public void OnTakeoffFootRelease() => liftingOff = false;

    public void SetAltitude(float y, bool instant)
    {
        altitude = y;
        if (instant && state == State.Flying)
            transform.position = new Vector3(transform.position.x, y, transform.position.z);
    }

    // Where it flies, in X and Z. It also stays on screen, and refuses at the area's far Z end.
    public void SetArea(Bounds area)
    {
        bounds = area;
        if (state != State.Flying && !OnScreen(perch.PerchPoint.position))
            LeavePerch();
    }


    void Update()
    {
        Ray ray = Camera.main.ScreenPointToRay(pointAction.ReadValue<Vector2>());
        bool hasHit = Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, clickMask, QueryTriggerInteraction.Collide);
        Perchable hovered = hasHit ? PerchableAt(hit) : null;
        // Something the bird cannot perch on now (a branch off screen, a fallen log) does not catch the click: it goes to what lies behind.
        if (hasHit && hovered == null && hit.collider.gameObject.layer == interactableLayer)
            hasHit = Physics.Raycast(ray, out hit, Mathf.Infinity, groundMask, QueryTriggerInteraction.Ignore);
        ShowPerchCursor(hovered != null);

        if (Cutscene.AnyPlaying)
            following = false;
        else if (moveAction.WasPressedThisFrame() && hasHit && !DevTools.Covers(pointAction.ReadValue<Vector2>()))
            Click(hit, hovered);
        else if (!moveAction.IsPressed())
            following = false;
        else if (following && hasHit)
            target = hit.point;

        bool cried = cryAction.WasPressedThisFrame() && !Cutscene.AnyPlaying;
        if (cried && Time.time >= nextCryTime)
            Cry();

        Vector3 before = transform.position;
        switch (state)
        {
            case State.Flying: Fly(); break;
            case State.ToPerch: FlyToPerch(); break;
            case State.Landing: Land(); Bank(0f); break;
            case State.Perched: transform.position = perch.PerchPoint.position; Bank(0f); break;
        }
        bool moved = transform.position != before;
        // Glides while it moves and hovers in place otherwise.
        animator.SetBool(MovingParam, moved);
        animator.SetBool(PerchedParam, state == State.Landing || state == State.Perched);

        idleTime = state == State.Flying && !moved && !moveAction.IsPressed() && !cried ? idleTime + Time.deltaTime : 0f;
        if (idleTime >= GameTuning.Current.birdIdlePerchDelay)
            PerchNearby(GameTuning.Current.birdIdlePerchRadius);
    }

    // False if no branch is on screen (within maxDistance world units, when given).
    bool PerchNearby(float maxDistance = float.MaxValue)
    {
        idleTime = 0f;
        Perchable nearest = null;
        float nearestDistance = float.MaxValue;
        foreach (Perchable perchable in Perchable.All)
        {
            float distance = Vector3.Distance(transform.position, perchable.PerchPoint.position);
            if (perchable.IsIdleSpot && perchable.CanPerch && distance <= maxDistance && OnScreen(perchable.PerchPoint.position) && distance < nearestDistance)
                (nearest, nearestDistance) = (perchable, distance);
        }
        if (nearest == null)
            return false;
        StartPerch(nearest);
        return true;
    }

    // Reached the closed way: lands on the nearest branch on screen and shakes its head there. With no branch on screen it stays.
    // Unlike idling, this isn't distance-limited: being sent back, the bird commits to acknowledging the block wherever the nearest branch is.
    void Refuse()
    {
        following = false;
        if (PerchNearby())
            refusePending = true;
        else
            target = transform.position;
    }

    void Click(RaycastHit hit, Perchable hovered)
    {
        following = hovered == null;
        if (hovered != null)
        {
            if (hovered != perch || state != State.Perched)
                StartPerch(hovered);
            return;
        }

        LeavePerch();
        // The target itself is not clamped: the bird presses against the edge of its area,
        // and moves on as the area moves (the camera follows, or a gate opens).
        target = hit.point;
        clickMarker.Show(hit.point);
    }

    // Null unless the hit is a perchable Interactable whose perch point is on screen.
    Perchable PerchableAt(RaycastHit hit)
    {
        if (hit.collider.gameObject.layer != interactableLayer)
            return null;
        Perchable perchable = hit.collider.GetComponentInParent<Perchable>();
        return perchable != null && perchable.CanPerch && OnScreen(perchable.PerchPoint.position) ? perchable : null;
    }

    void StartPerch(Perchable perchable)
    {
        LeavePerch();
        perch = perchable;
        perch.Released += LeavePerch;
        state = State.ToPerch;
    }

    // Back to flying, hovering above the current spot and returning to the altitude.
    void LeavePerch()
    {
        if (perch != null)
        {
            perch.Released -= LeavePerch;
            // Released only fires when the perch itself ejects the bird (e.g. the log falling); leaving by
            // choice (a new click, Refuse, ...) doesn't raise it, so the camera-fade exemption is cleared here too.
            perch.SetCameraFadeExempt(false);
        }
        bool takingOff = state == State.Perched || state == State.Landing;
        perch = null;
        refusePending = false;
        animator.ResetTrigger(RefuseParam);
        if (state != State.Flying)
            target = transform.position;
        state = State.Flying;
        if (takingOff)
        {
            liftingOff = true;
            liftingOffTimer = GameTuning.Current.birdTakeoffHoldTimeout;
            currentSpeed = 0f;
        }
    }

    void Fly()
    {
        // Held at the branch (LeavePerch) until OnTakeoffFootRelease fires, or this safety timeout runs out.
        if (liftingOff)
        {
            liftingOffTimer -= Time.deltaTime;
            if (liftingOffTimer > 0f)
                return;
            liftingOff = false;
        }

        // Towards the clamped target, so a bird taking off from a branch off the road glides back onto it instead of jumping.
        Vector3 position = transform.position;
        currentSpeed = Mathf.MoveTowards(currentSpeed, GameTuning.Current.birdSpeed, GameTuning.Current.birdAcceleration * Time.deltaTime);
        Vector3 flat = Vector3.MoveTowards(new Vector3(position.x, 0f, position.z), Limit(new Vector3(target.x, 0f, target.z)),
            currentSpeed * Time.deltaTime);
        float y = Mathf.MoveTowards(position.y, altitude, GameTuning.Current.birdAltitudeSpeed * Time.deltaTime);
        Face(flat - position);
        transform.position = new Vector3(flat.x, y, flat.z);
        // Heading past the far end of its area, it has reached the line where the way is closed (ADR 0016).
        if (target.z > bounds.max.z && flat.z >= bounds.max.z - 0.01f)
            Refuse();
    }

    void FlyToPerch()
    {
        Vector3 point = perch.PerchPoint.position;
        Vector3 position = transform.position;
        float distance = Vector3.Distance(position, point);
        if (distance <= 0.02f)
        {
            transform.position = point;
            currentSpeed = 0f;
            state = State.Landing;
            landTime = 0f;
            landFrom = transform.rotation;
            landTo = perch.AlignsBird ? AlongPerch() : landFrom;
            return;
        }
        // Brakes into the branch instead of cruising straight into it: speed falls away as the remaining
        // distance does, so it always has time to slow to a stop right at the perch point.
        float stoppingSpeed = Mathf.Sqrt(2f * GameTuning.Current.birdLandingDeceleration * distance);
        currentSpeed = Mathf.MoveTowards(currentSpeed, Mathf.Min(GameTuning.Current.birdSpeed, stoppingSpeed),
            GameTuning.Current.birdAcceleration * Time.deltaTime);
        Face(point - position);
        transform.position = Vector3.MoveTowards(position, point, currentSpeed * Time.deltaTime);
    }

    void Land()
    {
        transform.position = perch.PerchPoint.position;
        landTime += Time.deltaTime;
        transform.rotation = Quaternion.Slerp(landFrom, landTo, landTime / GameTuning.Current.birdPerchDuration);
        if (landTime < GameTuning.Current.birdPerchDuration)
            return;
        state = State.Perched;
        perch.NotifyPerched();
        if (refusePending)
        {
            refusePending = false;
            animator.SetTrigger(RefuseParam);
        }
    }

    // Facing along the perch point's forward axis, the way nearer the current heading.
    Quaternion AlongPerch()
    {
        Vector3 axis = perch.PerchPoint.forward;
        axis.y = 0f;
        if (Vector3.Dot(axis, transform.forward) < 0f)
            axis = -axis;
        return axis.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(axis) : transform.rotation;
    }

    void Face(Vector3 direction)
    {
        direction.y = 0f;
        // Turns at a limited rate instead of snapping to point straight at wherever the target just moved to;
        // the nose lagging slightly behind a sudden direction change is what reads as a turn rather than a snap.
        float desiredYaw = direction.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(direction).eulerAngles.y : facingYaw;
        float previousYaw = facingYaw;
        facingYaw = Mathf.MoveTowardsAngle(facingYaw, desiredYaw, GameTuning.Current.birdTurnSpeed * Time.deltaTime);
        float turnRate = Mathf.DeltaAngle(previousYaw, facingYaw) / Mathf.Max(Time.deltaTime, 0.0001f);
        transform.rotation = Quaternion.Euler(0f, facingYaw, 0f);
        // Positive bankAngle banks right wing down on this rig (verified in Play Mode), and positive turnRate
        // is a rightward turn (increasing yaw), so the sign here must match, not oppose, turnRate.
        float bankTarget = Mathf.Clamp(turnRate * GameTuning.Current.birdBankSensitivity,
            -GameTuning.Current.birdMaxBankAngle, GameTuning.Current.birdMaxBankAngle);
        Bank(bankTarget);
    }

    // Rolls visualRoot only; the root's own rotation stays pure yaw (OnScreen/Limit don't care about rotation,
    // but keeping it yaw-only avoids surprises for anything added later that reads the bird's facing).
    void Bank(float targetAngle)
    {
        bankAngle = Mathf.MoveTowards(bankAngle, targetAngle, GameTuning.Current.birdBankSpeed * Time.deltaTime);
        if (visualRoot != null)
            visualRoot.localRotation = visualBaseRotation * Quaternion.Euler(0f, 0f, bankAngle);
    }

    void Cry()
    {
        nextCryTime = Time.time + GameTuning.Current.birdCryCooldown;
        CryListener.Broadcast(transform.position);
        onCry.Invoke();
        StartCoroutine(CryPulse());
    }

    // Placeholder until the cry animation exists.
    IEnumerator CryPulse()
    {
        const float duration = 0.2f;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            transform.localScale = baseScale * (1f + 0.3f * Mathf.Sin(Mathf.PI * t / duration));
            yield return null;
        }
        transform.localScale = baseScale;
    }

    void ShowPerchCursor(bool show)
    {
        if (show == perchCursorShown || perchCursor == null)
            return;
        perchCursorShown = show;
        Cursor.SetCursor(show ? perchCursor : null, show ? perchCursorHotspot : Vector2.zero, CursorMode.Auto);
    }

    // On screen, inside the margin, and not past the area's Z ends: no branch takes the bird beyond the current stage's sunlit spot
    // (ADR 0012). Across the area it may perch anywhere on screen (trees stand beyond it).
    bool OnScreen(Vector3 point)
    {
        Vector3 viewport = Camera.main.WorldToViewportPoint(point);
        return viewport.z > 0f && InMargin(viewport.x) && InMargin(viewport.y) && point.z >= bounds.min.z && point.z <= bounds.max.z;
    }

    bool InMargin(float viewport) => viewport >= screenMargin && viewport <= 1f - screenMargin;

    // A flat point (Y unused) moved into where the area and what the camera shows at the flying height overlap (ADR 0015).
    // The screen shows a slanted four-sided patch of that height (quarter view), wider far from the camera. Clipped by the area it stays
    // convex. Done in the world: in screen coordinates a point behind the camera's near side would come out mirrored.
    Vector3 Limit(Vector3 point)
    {
        Camera view = Camera.main;
        var height = new Plane(Vector3.up, new Vector3(0f, altitude, 0f));
        float near = screenMargin, far = 1f - screenMargin;
        Vector2[] corners = { new(near, near), new(far, near), new(far, far), new(near, far) };
        screenPatch.Clear();
        foreach (Vector2 corner in corners)
        {
            Ray ray = view.ViewportPointToRay(corner);
            height.Raycast(ray, out float distance);
            Vector3 onHeight = ray.GetPoint(distance);
            screenPatch.Add(new Vector2(onHeight.x, onHeight.z));
        }
        ClipToArea(screenPatch);
        var flat = new Vector2(point.x, point.z);
        // The camera's point is inside the area, so the two always overlap; this guards a degenerate frame only.
        if (screenPatch.Count < 3)
            return ClampToArea(point);
        if (InsideConvex(screenPatch, flat))
            return point;
        Vector2 nearest = flat;
        float nearestDistance = float.MaxValue;
        for (int i = 0; i < screenPatch.Count; i++)
        {
            Vector2 onEdge = ClosestOnSegment(screenPatch[i], screenPatch[(i + 1) % screenPatch.Count], flat);
            float distance = (onEdge - flat).sqrMagnitude;
            if (distance < nearestDistance)
                (nearest, nearestDistance) = (onEdge, distance);
        }
        return new Vector3(nearest.x, point.y, nearest.y);
    }

    Vector3 ClampToArea(Vector3 point) => new(
        Mathf.Clamp(point.x, bounds.min.x, bounds.max.x), point.y, Mathf.Clamp(point.z, bounds.min.z, bounds.max.z));

    // Cuts the polygon down to the area, one side of the area at a time (Sutherland-Hodgman).
    void ClipToArea(List<Vector2> polygon)
    {
        ClipSide(polygon, Vector2.right, bounds.min.x);
        ClipSide(polygon, Vector2.left, -bounds.max.x);
        ClipSide(polygon, Vector2.up, bounds.min.z);
        ClipSide(polygon, Vector2.down, -bounds.max.z);
    }

    // Keeps the part where Dot(p, inward) >= offset.
    void ClipSide(List<Vector2> polygon, Vector2 inward, float offset)
    {
        clipped.Clear();
        for (int i = 0; i < polygon.Count; i++)
        {
            Vector2 a = polygon[i], b = polygon[(i + 1) % polygon.Count];
            float da = Vector2.Dot(a, inward) - offset, db = Vector2.Dot(b, inward) - offset;
            if (da >= 0f)
                clipped.Add(a);
            if (da >= 0f != db >= 0f)
                clipped.Add(Vector2.Lerp(a, b, da / (da - db)));
        }
        polygon.Clear();
        polygon.AddRange(clipped);
    }

    static bool InsideConvex(List<Vector2> polygon, Vector2 point)
    {
        float sign = 0f;
        for (int i = 0; i < polygon.Count; i++)
        {
            Vector2 a = polygon[i], b = polygon[(i + 1) % polygon.Count];
            float cross = (b.x - a.x) * (point.y - a.y) - (b.y - a.y) * (point.x - a.x);
            if (cross * sign < 0f)
                return false;
            if (cross != 0f)
                sign = cross;
        }
        return true;
    }

    static Vector2 ClosestOnSegment(Vector2 a, Vector2 b, Vector2 point)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / ab.sqrMagnitude);
        return a + ab * t;
    }
}
