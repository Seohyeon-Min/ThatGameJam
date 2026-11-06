using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

// Player-controlled bird: left-click flies to a point or lands on a Perchable, holding the button keeps following the pointer over the
// ground (also when the press began on a branch),
// right-click cries, clicking the baby toggles sheltering her (following her pace) until clicked off again. Left idle
// in the air near a branch, it lands on it by itself (birdIdlePerchRadius) rather than
// hopping the camera to any branch on screen. Sent past where the way is still closed
// (the far end of the current stage's area, a little past its sunlit spot), it flies up to that line, then lands on the nearest branch and shakes its head (ADR 0011, ADR 0016).
// Flies inside the area BirdCamera gives and on screen, and perches only on screen (ADR 0015). Its height follows the wheel's zoom
// (SetZoom): the stage's altitude at zoom 1, higher zoomed out, and low zoomed all the way in, where it comes down and walks
// wherever it can stand (Landable). Its changes of height ease along GameTuning.motionCurve.
// The transform is where the bird's feet are, so it stands on a perch point.
public class BirdController : MonoBehaviour
{
    static readonly int PerchedParam = Animator.StringToHash("Perched");
    static readonly int MovingParam = Animator.StringToHash("Moving");
    static readonly int RefuseParam = Animator.StringToHash("Refuse");
    static readonly int DivingParam = Animator.StringToHash("Diving");
    static readonly int WalkingParam = Animator.StringToHash("Walking");
    static readonly int WalkRateParam = Animator.StringToHash("WalkRate");
    static readonly int HoppingParam = Animator.StringToHash("Hopping");
    static readonly int HoverRateParam = Animator.StringToHash("HoverRate");
    // How fast Bird_Hop goes forward without its feet sliding, in the model's units per second (the bird's export notes).
    const float HopClipSpeed = 0.44f;
    // How fast Bird_Walk steps forward without its feet sliding, in the model's units per second (the bird's export notes).
    const float WalkClipSpeed = 0.26f;
    // In the sky, going back on screen: how much of the camera's speed it may lose.
    const float SkyMostFallBehind = 0.7f;
    // Flying low, it comes down only where the ground this far ahead along its way is also landable, so it doesn't land at the
    // water's edge only to take off again.
    const float LandLookahead = 0.5f;
    // Flying low, how far ahead it looks for logs and banks to rise over in time.
    const float ClearLookahead = 3f;
    // How high above what is under it it must be to move along (Fly).
    const float SurfaceGap = 0.2f;

    [SerializeField] Animator animator;
    [SerializeField] ClickMarker clickMarker;
    // Rolled for the turn bank (Face), separate from this transform so the root's rotation stays pure yaw
    // for perch alignment and area clamping. Usually the Animator's GameObject (e.g. "Bird-Animated").
    [SerializeField] Transform visualRoot;
    [Tooltip("The head bone (Head). Rising or sinking in the air, it tips to look the way the bird goes, over the animation.")]
    [SerializeField] Transform headBone;
    [SerializeField] Texture2D perchCursor;
    [SerializeField] Vector2 perchCursorHotspot;
    // The bird and the branches it perches on stay this far inside the screen's edges, as a fraction of the screen (ADR 0015).
    [SerializeField, Range(0f, 0.3f)] float screenMargin = 0.08f;
    [SerializeField] UnityEvent onCry;
    // Sky-style call pulse (BirdCryPulse prefab: an expanding glow ring plus a burst of warm motes), played at the bird's
    // position each cry. Reference any ParticleSystem in the prefab instance; Play()'s default withChildren plays the rest.
    [SerializeField] ParticleSystem cryPulse;

    enum State { Flying, ToPerch, Landing, Perched, Walking }
    // Who moves the bird. Sky and Scripted are for the intro (IntroSequence): Sky is clicks on the flying height, with no perches or
    // area, and Scripted is MoveScripted only.
    enum Control { Game, Sky, Scripted }

    public static BirdController Instance { get; private set; }

    // While flying by clicks in the sky: how fast the camera is travelling, so the bird keeps its place on screen and is carried along.
    public Vector3 SkyDrift { get; set; }
    // The way the bird faces in the sky, flat. It doesn't turn from this.
    public Vector3 SkyForward { get; set; } = Vector3.forward;

    public bool IsPerched => state == State.Perched;
    // Sitting on a perch or on the way to one.
    public bool IsPerchedOrLanding => state is State.ToPerch or State.Landing or State.Perched;
    // Toggled on by a click on the walking baby, keeping the snow off her (SnowShelter) until clicked off again.
    public bool IsSheltering { get; private set; }
    // Sheltering and over her (not held back at the edge of its area or the screen): its wings keep the snow off.
    public bool IsCoveringBaby => IsSheltering && shelterReached
        && Vector2.Distance(new Vector2(transform.position.x, transform.position.z), new Vector2(shelterSpot.x, shelterSpot.z)) <= 0.3f;

    InputAction moveAction;
    InputAction cryAction;
    InputAction pointAction;
    int clickMask;
    int groundMask;
    int interactableLayer;
    State state;
    Control control;
    // Placeholder for a dive animation: tips visualRoot forward, on top of the bank.
    float bodyPitch;
    // Horizontal target while flying. Y is unused; the bird's height follows altitude.
    Vector3 target;
    // The height it flies at now: baseAltitude moved by the zoom (FlightAltitude).
    float altitude;
    // A change of height: from climbFrom to where it should be (which may move meanwhile), climbTime seconds in.
    float climbFrom;
    float climbTime = float.MaxValue;
    // The height it is heading for: its altitude, the ground to come down on, or above a log or bank (Clearance).
    enum Aim { Altitude, Ground, Clear }
    Aim aim;
    float clearHeight;
    // On the way to a branch: the start, and the point along the straight way there. The height eases along that way.
    Vector3 approachFrom, approach;
    // The stage's altitude (SetAltitude), the height at zoom 1.
    float baseAltitude;
    // BirdCamera's zoom, eased, and the zoom it is heading to. 1 = the authored distance.
    float zoom = 1f;
    float zoomHeading = 1f;
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
    // Pressed on this branch at pressedTime and still held: a click on it if let go within birdPerchClickSeconds.
    Perchable pressedPerch;
    float pressedTime;
    Perchable perch;
    bool clicksLocked;
    float landTime;
    // The turn while landing, from the heading it arrived with.
    Quaternion landFrom, landTo;
    float nextCryTime;
    bool perchCursorShown;
    Vector3 baseScale;
    // While sheltering: reached the spot over the baby, from then on moving at her pace.
    bool shelterReached;
    Vector3 lastBabyPosition;
    // Degrees the head tips over the animation, down positive (headBone).
    float headPitch;
    // Seconds of hovering left after the height last changed, and the rise smoothed over the wheel's ticks.
    float climbHold;
    float smoothRise;
    float hoverRate = 1f;
    // Where it would be over her, before the area and screen limits.
    Vector3 shelterSpot;

    // After the animator has posed the bird: the head tips down as it sinks and up as it rises.
    void LateUpdate()
    {
        if (headBone != null && Mathf.Abs(headPitch) > 0.01f)
            headBone.rotation = Quaternion.AngleAxis(headPitch, transform.right) * headBone.rotation;
    }

    void Awake()
    {
        Instance = this;
        target = transform.position;
        altitude = baseAltitude = transform.position.y;
        baseScale = transform.localScale;
        facingYaw = transform.eulerAngles.y;
        if (visualRoot != null)
        {
            visualBaseRotation = visualRoot.localRotation;
        }
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
        IsSheltering = false;
        idleTime = 0f;
        liftingOff = false;
        currentSpeed = 0f;
        transform.position = position;
        target = position;
        StartClimb();
    }

    // Hooked to BirdAnimationEvents.onFootRelease (Bird_Takeoff, feet leave the branch): the body was holding
    // still at the perch since LeavePerch and starts moving/rising from here, instead of jumping to speed the
    // instant the player clicks away while the Takeoff clip is still showing the feet gripping it.
    public void OnTakeoffFootRelease() => liftingOff = false;

    public void SetAltitude(float y, bool instant)
    {
        baseAltitude = y;
        altitude = control == Control.Game ? FlightAltitude() : y;
        if (!instant)
        {
            StartClimb();
            return;
        }
        climbTime = float.MaxValue;
        if (state == State.Flying)
            transform.position = new Vector3(transform.position.x, altitude, transform.position.z);
    }

    // Where it flies, in X and Z. It also stays on screen, and refuses at the area's far Z end.
    public void SetArea(Bounds area)
    {
        if (control != Control.Game)
            return;
        bounds = area;
        // Not during PerchOn: the camera may not have reached its new place yet.
        if (perch != null && !clicksLocked && !OnScreen(perch.PerchPoint.position))
            LeavePerch();
    }

    // Flies to the perch and lands, and the player's clicks don't count until it has landed.
    public void PerchOn(Perchable perchable)
    {
        StartPerch(perchable);
        clicksLocked = true;
    }

    // The eased zoom moves the flying height; the zoom it is heading to says whether to come down, so it starts down at once
    // instead of first settling at the low flying height.
    public void SetZoom(float eased, float heading)
    {
        zoom = eased;
        zoomHeading = heading;
    }

    // Zoomed all the way in: down on the ground.
    bool OnGroundZoom => zoomHeading <= GameTuning.Current.cameraZoomMin;

    // From the zoom only, over the stage's ground (the area's centre height), so it doesn't bump over logs and water.
    float FlightAltitude() => AltitudeForZoom(zoom, baseAltitude, bounds.center.y);

    // The flying height for a zoom: the altitude at zoom 1, higher zoomed out and lower zoomed in (GameTuning).
    public static float AltitudeForZoom(float zoom, float baseAltitude, float ground)
    {
        GameTuning tuning = GameTuning.Current;
        if (zoom >= 1f)
            return baseAltitude + (baseAltitude - ground) * (tuning.birdZoomedOutHeightScale - 1f) * Mathf.InverseLerp(1f, tuning.cameraZoomMax, zoom);
        return Mathf.Lerp(ground + tuning.birdLowFlightHeight, baseAltitude, Mathf.InverseLerp(tuning.cameraZoomMin, 1f, zoom));
    }

    // The terrain where it can stand, and its height: not the water (ClickPlane) or a log or bank on the terrain, and no steeper
    // than birdLandMaxSlope, unlike the banks down to the water.
    bool Landable(Vector3 point, out float ground)
    {
        bool hit = SurfaceAt(point, out RaycastHit h);
        ground = hit ? h.point.y : point.y;
        return hit && h.collider is TerrainCollider && Vector3.Angle(h.normal, Vector3.up) <= GameTuning.Current.birdLandMaxSlope;
    }

    float SurfaceHeight(Vector3 point) => SurfaceAt(point, out RaycastHit h) ? h.point.y : float.MinValue;

    // The top of what a click there would hit below: the terrain, a log or bank on it, or the water (ClickPlane).
    bool SurfaceAt(Vector3 point, out RaycastHit hit) =>
        Physics.Raycast(new Vector3(point.x, 1000f, point.z), Vector3.down, out hit, Mathf.Infinity, groundMask, QueryTriggerInteraction.Ignore);

    static float Ease(float t) => GameTuning.Current.motionCurve.Evaluate(Mathf.Clamp01(t));

    void StartClimb()
    {
        climbFrom = transform.position.y;
        climbTime = 0f;
    }

    bool Climbed => climbTime >= GameTuning.Current.birdClimbDuration;

    // The height on the way from climbFrom to goal.
    float Climb(float goal)
    {
        climbTime += Time.deltaTime;
        return Climbed ? goal : Mathf.LerpUnclamped(climbFrom, goal, Ease(climbTime / GameTuning.Current.birdClimbDuration));
    }

    // Intro: nothing moves the bird but MoveScripted.
    public void BeginScripted() => control = Control.Scripted;

    // Intro: the player flies the bird by clicking, on the screen of a camera that may be travelling (SkyDrift).
    public void BeginSkyFlight()
    {
        control = Control.Sky;
        bounds = new Bounds(Vector3.zero, Vector3.positiveInfinity);
        following = false;
        currentSpeed = 0f;
        target = transform.position;
    }

    // Back to the player's normal control. BirdCamera gives the area again.
    public void EndIntro()
    {
        control = Control.Game;
        SkyDrift = Vector3.zero;
        bodyPitch = 0f;
        SetDiving(false);
    }

    // Intro: Bird_Dive then Bird_DiveLoop while on, back to gliding when off.
    public void SetDiving(bool diving) => animator.SetBool(DivingParam, diving);

    public void MoveScripted(Vector3 position, float pitch = 0f)
    {
        bodyPitch = pitch;
        Face(position - transform.position);
        transform.position = position;
        animator.SetBool(MovingParam, true);
        animator.SetBool(PerchedParam, false);
    }

    // Hovers level where it is, until the script moves it again or EndIntro.
    public void HoldStill()
    {
        bodyPitch = 0f;
        bankAngle = 0f;
        if (visualRoot != null)
            visualRoot.localRotation = visualBaseRotation;
        animator.SetBool(MovingParam, false);
        animator.SetBool(PerchedParam, false);
        SetDiving(false);
    }

    // MoveScripted, but facing ahead as in FlyAhead.
    public void MoveScriptedAhead(Vector3 position)
    {
        bodyPitch = 0f;
        FaceAhead(position - transform.position);
        transform.position = position;
        animator.SetBool(MovingParam, true);
        animator.SetBool(PerchedParam, false);
    }

    public void SnapFacing(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
            return;
        facingYaw = Quaternion.LookRotation(direction).eulerAngles.y;
        transform.rotation = Quaternion.Euler(0f, facingYaw, 0f);
    }

    void Update()
    {
        if (control == Control.Scripted)
        {
            // The intro's scripted moments (circling, the dive run-up) move the bird on their own, but crying
            // is still the player's: it only sets off a cooldown/effect, nothing here moves it.
            TryCry();
            return;
        }
        // The encyclopedia is open: the game is paused and the mouse belongs to the page.
        if (Encyclopedia.IsOpen)
        {
            ShowPerchCursor(false);
            pressedPerch = null;
            return;
        }
        if (control == Control.Sky)
        {
            FlyInSky();
            // The intro (above the clouds, before the game proper) still lets the player cry: the only
            // thing the sky flight doesn't offer is perching/the stage area, not the cry itself.
            TryCry();
            return;
        }
        altitude = FlightAltitude();

        Ray ray = Camera.main.ScreenPointToRay(pointAction.ReadValue<Vector2>());
        Perchable hovered = Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, clickMask, QueryTriggerInteraction.Collide)
            ? PerchableAt(hit) : null;
        // Where it flies to: the ground or water under the pointer, behind any branch there.
        bool onGround = Physics.Raycast(ray, out RaycastHit ground, Mathf.Infinity, groundMask, QueryTriggerInteraction.Ignore);
        bool held = moveAction.IsPressed();
        // Holding the button the bird follows the pointer and no branch reacts, unless the press began on one and is still a click.
        ShowPerchCursor(hovered != null && (!held || pressedPerch != null));

        bool pressed = moveAction.WasPressedThisFrame() && !clicksLocked && !DevTools.Covers(pointAction.ReadValue<Vector2>());
        if (Cutscene.PlayerLocked)
        {
            // Held still: no clicks count, and it stops where it is instead of going on to its last target.
            following = false;
            pressedPerch = null;
            target = transform.position;
        }
        else if (pressed && CanShelter() && Baby.Instance.GetComponent<Collider>().Raycast(ray, out _, Mathf.Infinity))
            ToggleShelter();
        else if (pressed && hovered != null)
        {
            // A press on a branch is a click on it only if let go soon: held on, it follows the pointer like any hold.
            pressedPerch = hovered;
            pressedTime = Time.time;
        }
        else if (pressed && onGround)
            Click(ground);
        else if (pressedPerch != null)
        {
            if (!held)
            {
                if (pressedPerch.CanPerch && OnScreen(pressedPerch.PerchPoint.position))
                    ClickPerch(pressedPerch);
                pressedPerch = null;
            }
            else if (Time.time - pressedTime >= GameTuning.Current.birdPerchClickSeconds)
            {
                pressedPerch = null;
                if (onGround)
                    Click(ground);
            }
        }
        else if (!held)
            following = false;
        else if (following && onGround)
            target = ground.point;
        if (IsSheltering && !CanShelter())
        {
            // The baby has reached the sunlit spot, or stopped walking: hovers where it is.
            IsSheltering = false;
            target = transform.position;
            StartClimb();
        }

        bool cried = TryCry();

        Vector3 before = transform.position;
        switch (state)
        {
            case State.Flying: Fly(); break;
            case State.ToPerch: FlyToPerch(); break;
            case State.Landing: Land(); Bank(0f); break;
            case State.Perched: transform.position = perch.PerchPoint.position; Bank(0f); break;
            case State.Walking: Walk(); break;
        }
        bool moved = transform.position != before;
        // Rising or sinking in the air (the wheel's zoom changing its height): it hovers in place and looks the way it goes.
        GameTuning tuning = GameTuning.Current;
        float rise = Time.deltaTime > 0f ? (transform.position.y - before.y) / Time.deltaTime : 0f;
        climbHold = state == State.Flying && Mathf.Abs(rise) > tuning.birdClimbHoverSpeed ? tuning.birdClimbHoverHold
            : state == State.Flying ? climbHold - Time.deltaTime : 0f;
        bool climbing = climbHold > 0f;
        // The head follows the rise smoothed over the wheel's ticks, so it holds its tilt between them instead of nodding.
        smoothRise = Mathf.Lerp(smoothRise, climbing ? rise : 0f, 1f - Mathf.Exp(-3f * Time.deltaTime));
        float headGoal = climbing ? Mathf.Clamp(-smoothRise * tuning.birdClimbHeadPerSpeed, -tuning.birdClimbHeadMost, tuning.birdClimbHeadMost) : 0f;
        headPitch = Mathf.Lerp(headPitch, headGoal, 1f - Mathf.Exp(-tuning.birdClimbHeadEase * Time.deltaTime));
        // Glides while it moves and hovers in place otherwise; rising or sinking, the wings beat faster, eased in and out.
        animator.SetBool(MovingParam, moved && !climbing);
        hoverRate = Mathf.Lerp(hoverRate, climbing ? tuning.birdClimbHoverRate : 1f, 1f - Mathf.Exp(-6f * Time.deltaTime));
        animator.SetFloat(HoverRateParam, hoverRate);
        animator.SetBool(PerchedParam, state is State.Landing or State.Perched or State.Walking);
        animator.SetBool(WalkingParam, state == State.Walking && moved);
        animator.SetBool(HoppingParam, state == State.Walking && moved && hopping);
        animator.SetFloat(WalkRateParam, GameTuning.Current.birdWalkClipRate);

        idleTime = state == State.Flying && !moved && !moveAction.IsPressed() && !cried ? idleTime + Time.deltaTime : 0f;
        if (idleTime >= GameTuning.Current.birdIdlePerchDelay)
            PerchNearby(GameTuning.Current.birdIdlePerchRadius);
    }

    // Click and hold as in the game, but on the flying height: nothing is down here to hit, and the ground is far below the bird.
    // Each new click plays the intro's Swoosh stinger.
    void FlyInSky()
    {
        Vector2 pointer = pointAction.ReadValue<Vector2>();
        bool pressed = moveAction.WasPressedThisFrame() && !DevTools.Covers(pointer);
        if (!moveAction.IsPressed())
            following = false;
        var height = new Plane(Vector3.up, new Vector3(0f, altitude, 0f));
        Ray ray = Camera.main.ScreenPointToRay(pointer);
        if ((pressed || following) && height.Raycast(ray, out float distance))
        {
            target = ray.GetPoint(distance);
            if (pressed)
            {
                following = true;
                clickMarker.Show(target);
                IntroSfx.PlaySwoosh();
            }
        }
        else
            target += SkyDrift * Time.deltaTime;

        transform.position += SkyDrift * Time.deltaTime;
        FlyAhead();
        animator.SetBool(MovingParam, true);
        animator.SetBool(PerchedParam, false);
    }

    // Moves toward the target on screen while always facing ahead: sideways by rolling, and backward only by falling behind the camera.
    void FlyAhead()
    {
        Vector3 position = transform.position;
        var flat = new Vector3(position.x, 0f, position.z);
        Vector3 flatTarget = Limit(new Vector3(target.x, 0f, target.z));
        float stoppingSpeed = Mathf.Sqrt(2f * GameTuning.Current.birdLandingDeceleration * Vector3.Distance(flat, flatTarget));
        currentSpeed = Mathf.MoveTowards(currentSpeed, Mathf.Min(GameTuning.Current.birdSpeed, stoppingSpeed),
            GameTuning.Current.birdAcceleration * Time.deltaTime);
        Vector3 step = Vector3.MoveTowards(flat, flatTarget, currentSpeed * Time.deltaTime) - flat;

        // It only ever slows down, and keeps some of the camera's speed: it never flies backward through the air.
        float slowest = -SkyDrift.magnitude * SkyMostFallBehind * Time.deltaTime;
        float along = Vector3.Dot(step, SkyForward);
        if (along < slowest)
            step += SkyForward * (slowest - along);

        float y = Mathf.MoveTowards(position.y, altitude, GameTuning.Current.birdAltitudeSpeed * Time.deltaTime);
        transform.position = new Vector3(position.x + step.x, y, position.z + step.z);
        FaceAhead(step);
    }

    // Holds the heading ahead, with the wing on the side it is sliding toward held low.
    void FaceAhead(Vector3 step)
    {
        step.y = 0f;
        facingYaw = Mathf.MoveTowardsAngle(facingYaw, Quaternion.LookRotation(SkyForward).eulerAngles.y,
            GameTuning.Current.birdTurnSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Euler(0f, facingYaw, 0f);
        float sideSpeed = Vector3.Dot(step, Vector3.Cross(Vector3.up, SkyForward)) / Mathf.Max(Time.deltaTime, 0.0001f);
        Bank(Mathf.Clamp(sideSpeed / (GameTuning.Current.birdSpeed * 0.5f), -1f, 1f) * GameTuning.Current.birdMaxBankAngle);
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

    // Only in a stage that has it, and only while the baby is up and on her way (not asleep, waking or lying at the sunlit spot).
    static bool CanShelter() => GameFlow.Instance.CurrentStage.snowShelter
        && Baby.Instance.State is BabyState.Walking or BabyState.Reacting or BabyState.Waiting;

    // A click on the baby toggles sheltering on; a second click there (or anywhere else, via Click) turns it off again.
    void ToggleShelter()
    {
        if (IsSheltering)
        {
            IsSheltering = false;
            target = transform.position;
        }
        else
            StartShelter();
    }

    void StartShelter()
    {
        LeavePerch();
        following = false;
        IsSheltering = true;
        shelterReached = false;
        lastBabyPosition = Baby.Instance.transform.position;
    }

    // An explicit click on a branch or the ground always breaks off sheltering.
    void ClickPerch(Perchable perchable)
    {
        IsSheltering = false;
        following = false;
        if (perchable != perch || state != State.Perched)
            StartPerch(perchable);
    }

    void Click(RaycastHit hit)
    {
        IsSheltering = false;
        following = true;

        // Sitting (or about to) and sent past where the way is still closed again: it doesn't set off just to refuse again at the line,
        // it stays and shakes its head once more, however often the player clicks there.
        if (hit.point.z > bounds.max.z && state is State.Perched or State.Landing or State.ToPerch)
        {
            following = false;
            ShakeHead();
            return;
        }

        if (state != State.Walking)
            LeavePerch();
        // The target itself is not clamped: the bird presses against the edge of its area,
        // and moves on as the area moves (the camera follows, or a gate opens).
        target = hit.point;
        clickMarker.Show(hit.point);
        // A new click on the ground hops again, however often it comes (HopAgain).
        if (state == State.Walking)
            HopAgain();
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
        approachFrom = approach = transform.position;
        perch = perchable;
        perch.Released += LeavePerch;
        // From the moment it heads there, not only once it stands there: the tree it is landing in stays solid.
        perch.SetCameraFadeExempt(true);
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
        bool takingOff = state is State.Perched or State.Landing or State.Walking;
        perch = null;
        clicksLocked = false;
        refusePending = false;
        animator.ResetTrigger(RefuseParam);
        // Taking off from the ground, it keeps going where it was walking.
        if (state is not (State.Flying or State.Walking))
            target = transform.position;
        if (state != State.Flying)
        {
            aim = Aim.Altitude;
            StartClimb();
        }
        state = State.Flying;
        if (takingOff)
        {
            liftingOff = true;
            liftingOffTimer = GameTuning.Current.birdTakeoffHoldTimeout;
            currentSpeed = 0f;
        }
    }

    // Held where it took off (LeavePerch) until OnTakeoffFootRelease fires, or this safety timeout runs out.
    bool HoldingForTakeoff()
    {
        if (!liftingOff)
            return false;
        liftingOffTimer -= Time.deltaTime;
        if (liftingOffTimer > 0f)
            return true;
        liftingOff = false;
        return false;
    }

    void Fly()
    {
        if (HoldingForTakeoff())
            return;
        if (IsSheltering)
        {
            Shelter();
            return;
        }

        // Towards the clamped target, so a bird taking off from a branch off the road glides back onto it instead of jumping.
        Vector3 position = transform.position;
        var from = new Vector3(position.x, 0f, position.z);
        Vector3 to = Limit(new Vector3(target.x, 0f, target.z));
        currentSpeed = Mathf.MoveTowards(currentSpeed, GameTuning.Current.birdSpeed, GameTuning.Current.birdAcceleration * Time.deltaTime);
        Vector3 flat = Vector3.MoveTowards(from, to, currentSpeed * Time.deltaTime);
        // Zoomed all the way in, it comes down where it can stand. Flying low, it keeps above logs, banks and the water.
        Vector3 heading = to - from;
        float ground = 0f;
        float clear = Clearance(flat, heading);
        Aim next = OnGroundZoom && CanComeDown(flat, heading, out ground) ? Aim.Ground : clear > altitude + 0.05f ? Aim.Clear : Aim.Altitude;
        // Over a log or bank the height to keep only rises, each time eased from where it is, until it is past.
        bool higher = next == Aim.Clear && aim == Aim.Clear && clear > clearHeight + 0.01f;
        if (next != aim || higher)
        {
            aim = next;
            clearHeight = clear;
            StartClimb();
        }
        float y = Climb(aim switch { Aim.Ground => ground, Aim.Clear => clearHeight, _ => altitude });
        // Coming down onto ground that rises, it never sinks into it.
        if (aim == Aim.Ground)
            y = Mathf.Max(y, ground);
        // It doesn't fly into a log or bank while still below its top, nor skim the ground taking off: it rises off it first.
        // Coming down, the ground there is where it lands.
        if (aim != Aim.Ground && SurfaceHeight(flat) > y - SurfaceGap)
        {
            flat = from;
            currentSpeed = 0f;
        }
        Face(flat - position);
        transform.position = new Vector3(flat.x, y, flat.z);
        RefuseAtClosedWay(flat);
        if (state == State.Flying && aim == Aim.Ground && Climbed)
        {
            state = State.Walking;
            currentSpeed = 0f;
            wasWalking = false;
            hopping = false;
        }
    }

    bool CanComeDown(Vector3 flat, Vector3 heading, out float ground)
    {
        if (!Landable(flat, out ground))
            return false;
        if (heading.sqrMagnitude > 0.000001f)
            for (float distance = 0.1f; distance <= LandLookahead + 0.001f; distance += 0.1f)
                if (!Landable(flat + heading.normalized * distance, out _))
                    return false;
        return true;
    }

    // Flying low, it keeps birdLowFlightHeight above the highest of what is under it and up to ClearLookahead ahead.
    float Clearance(Vector3 flat, Vector3 step)
    {
        float highest = SurfaceHeight(flat);
        if (step.sqrMagnitude > 0.000001f)
            for (float distance = 0.25f; distance <= ClearLookahead + 0.001f; distance += 0.25f)
                highest = Mathf.Max(highest, SurfaceHeight(flat + step.normalized * distance));
        return highest + GameTuning.Current.birdLowFlightHeight;
    }

    // On the ground, it walks to the target. Zoomed back out, or reaching what it can't stand on, it takes off (and Fly brings it
    // down again past it).
    // Hops for a while from now (birdHopSeconds). Already hopping, it only goes on longer, never shorter, so clicks coming fast keep
    // one hop going and the animation never cuts back and forth between hopping and walking.
    void HopAgain()
    {
        Vector2 seconds = GameTuning.Current.birdHopSeconds;
        float until = Time.time + Random.Range(Mathf.Min(seconds.x, seconds.y), Mathf.Max(seconds.x, seconds.y));
        hopUntil = Mathf.Max(hopUntil, until);
    }

    // The speed its steps carry the bird at, hopping or walking, as the clip plays (birdWalkClipRate) at the model's size, so its feet
    // don't slide.
    float WalkSpeed => (hopping ? HopClipSpeed : WalkClipSpeed) * animator.transform.lossyScale.x * GameTuning.Current.birdWalkClipRate
        * GameTuning.Current.birdWalkSpeedScale;
    // On the ground: hopping for now (it set off a moment ago), and whether it was going anywhere last frame.
    bool hopping;
    float hopUntil;
    bool wasWalking;

    void Walk()
    {
        Vector3 position = transform.position;
        Vector3 goal = Limit(new Vector3(target.x, 0f, target.z));
        // Setting off from standing, it hops for a moment before it settles into walking.
        bool going = (goal - new Vector3(position.x, 0f, position.z)).sqrMagnitude > 1e-6f;
        if (going && !wasWalking)
            HopAgain();
        wasWalking = going;
        hopping = going && Time.time < hopUntil;
        Vector3 flat = Vector3.MoveTowards(new Vector3(position.x, 0f, position.z), goal, WalkSpeed * Time.deltaTime);
        if (!OnGroundZoom || !Landable(flat, out float ground))
        {
            LeavePerch();
            return;
        }
        // Toward where it is walking to, not this frame's step: walking slowly, a step is too short for Face to count it, so it never turned.
        Face(goal - position, false, GameTuning.Current.birdWalkTurnSpeed);
        transform.position = new Vector3(flat.x, ground, flat.z);
        RefuseAtClosedWay(flat);
    }

    // Heading past the far end of its area, it has reached the line where the way is closed (ADR 0016).
    // Shakes its head where it sits: now if it has landed, or as soon as it does. Clicks during a shake queue one more, not many.
    void ShakeHead()
    {
        if (state == State.Perched)
            animator.SetTrigger(RefuseParam);
        else
            refusePending = true;
    }

    void RefuseAtClosedWay(Vector3 flat)
    {
        if (target.z > bounds.max.z && flat.z >= bounds.max.z - 0.01f)
            Refuse();
    }

    // Flies down to a spot a little ahead of and above the baby, then keeps over it at her pace, facing the way she walks.
    // Released, Fly climbs back to the stage's altitude.
    void Shelter()
    {
        Transform baby = Baby.Instance.transform;
        Vector3 forward = Vector3.ProjectOnPlane(baby.forward, Vector3.up).normalized;
        shelterSpot = baby.position + forward * GameTuning.Current.birdShelterAhead;
        Vector3 spot = Limit(shelterSpot);
        Vector3 position = transform.position;
        var flat = new Vector3(position.x, 0f, position.z);
        spot.y = 0f;
        float babyStep = Vector3.ProjectOnPlane(baby.position - lastBabyPosition, Vector3.up).magnitude;
        lastBabyPosition = baby.position;
        float step;
        if (shelterReached)
            step = babyStep + GameTuning.Current.birdShelterCatchUp * Time.deltaTime;
        else
        {
            currentSpeed = Mathf.MoveTowards(currentSpeed, GameTuning.Current.birdSpeed, GameTuning.Current.birdAcceleration * Time.deltaTime);
            step = currentSpeed * Time.deltaTime;
        }
        Vector3 next = Vector3.MoveTowards(flat, spot, step);
        Face(shelterReached ? forward : next - flat);
        float height = baby.position.y + GameTuning.Current.birdShelterHeight;
        float y = Mathf.MoveTowards(position.y, height, GameTuning.Current.birdAltitudeSpeed * Time.deltaTime);
        transform.position = new Vector3(next.x, y, next.z);
        // Over her once at the spot and down at sheltering height.
        if (next == spot && Mathf.Approximately(y, height))
            shelterReached = true;
    }

    void FlyToPerch()
    {
        if (HoldingForTakeoff())
            return;
        Vector3 point = perch.PerchPoint.position;
        Vector3 position = transform.position;
        float distance = Vector3.Distance(approach, point);
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
        // Along the straight way there, with the height easing in and out along the way.
        approach = Vector3.MoveTowards(approach, point, currentSpeed * Time.deltaTime);
        float way = Vector3.Distance(approachFrom, point);
        float progress = way > 0f ? 1f - Vector3.Distance(approach, point) / way : 1f;
        Vector3 next = new(approach.x, Mathf.LerpUnclamped(approachFrom.y, point.y, Ease(progress)), approach.z);
        Face(next - position);
        transform.position = next;
    }

    void Land()
    {
        transform.position = perch.PerchPoint.position;
        landTime += Time.deltaTime;
        transform.rotation = Quaternion.Slerp(landFrom, landTo, landTime / GameTuning.Current.birdPerchDuration);
        if (landTime < GameTuning.Current.birdPerchDuration)
            return;
        state = State.Perched;
        clicksLocked = false;
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

    // Banked into the turn in the air; level on the ground.
    void Face(Vector3 direction, bool bank = true, float turnSpeed = -1f)
    {
        if (turnSpeed < 0f)
            turnSpeed = GameTuning.Current.birdTurnSpeed;
        direction.y = 0f;
        // Turns at a limited rate instead of snapping to point straight at wherever the target just moved to;
        // the nose lagging slightly behind a sudden direction change is what reads as a turn rather than a snap.
        float desiredYaw = direction.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(direction).eulerAngles.y : facingYaw;
        float previousYaw = facingYaw;
        facingYaw = Mathf.MoveTowardsAngle(facingYaw, desiredYaw, turnSpeed * Time.deltaTime);
        float turnRate = Mathf.DeltaAngle(previousYaw, facingYaw) / Mathf.Max(Time.deltaTime, 0.0001f);
        transform.rotation = Quaternion.Euler(0f, facingYaw, 0f);
        // Positive bankAngle banks right wing down on this rig (verified in Play Mode), and positive turnRate
        // is a rightward turn (increasing yaw), so the sign here must match, not oppose, turnRate.
        float bankTarget = Mathf.Clamp(turnRate * GameTuning.Current.birdBankSensitivity,
            -GameTuning.Current.birdMaxBankAngle, GameTuning.Current.birdMaxBankAngle);
        Bank(bank ? bankTarget : 0f);
    }

    // Rolls visualRoot only; the root's own rotation stays pure yaw (OnScreen/Limit don't care about rotation,
    // but keeping it yaw-only avoids surprises for anything added later that reads the bird's facing).
    void Bank(float targetAngle)
    {
        bankAngle = Mathf.MoveTowards(bankAngle, targetAngle, GameTuning.Current.birdBankSpeed * Time.deltaTime);
        if (visualRoot != null)
            visualRoot.localRotation = visualBaseRotation * Quaternion.Euler(bodyPitch, 0f, bankAngle);
    }

    // Right-click, no cutscene playing: shared by Game and Sky control (the intro cries too). Returns whether
    // the player attempted a cry this frame (counts as activity for the idle-perch timer) even if it was on
    // cooldown and so didn't actually Cry().
    bool TryCry()
    {
        // A cutscene holding the player still (Lock Player) holds the cry too.
        bool cried = cryAction.WasPressedThisFrame() && !Cutscene.PlayerLocked;
        if (cried && Time.time >= nextCryTime)
            Cry();
        return cried;
    }

    void Cry()
    {
        nextCryTime = Time.time + GameTuning.Current.birdCryCooldown;
        CryListener.Broadcast(transform.position);
        onCry.Invoke();
        StartCoroutine(CryPulse());
        if (cryPulse != null)
        {
            if (cryPulse.TryGetComponent(out CryTail tail))
                tail.Aim();
            cryPulse.Play();
        }
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

    // On screen, inside the margin, and not past the area's Z ends: no branch takes the bird beyond the current stage's area.
    // Across the area it may perch anywhere on screen (trees stand beyond it).
    bool OnScreen(Vector3 point)
    {
        Vector3 viewport = Camera.main.WorldToViewportPoint(point);
        return viewport.z > 0f && InMargin(viewport.x) && InMargin(viewport.y) && point.z >= bounds.min.z && point.z <= bounds.max.z;
    }

    bool InMargin(float viewport) => viewport >= screenMargin && viewport <= 1f - screenMargin;

    // A flat point (Y unused) moved into where the area and what the camera shows at the bird's height overlap (ADR 0015).
    // The screen shows a slanted four-sided patch of that height (quarter view), wider far from the camera. Clipped by the area it stays
    // convex. Done in the world: in screen coordinates a point behind the camera's near side would come out mirrored.
    Vector3 Limit(Vector3 point)
    {
        Camera view = Camera.main;
        var height = new Plane(Vector3.up, transform.position);
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
