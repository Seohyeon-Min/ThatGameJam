using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

// Player-controlled bird: left-click flies to a point or lands on a Perchable, holding the button keeps following the pointer,
// right-click cries. Left idle in the air, it lands on the nearest roadside branch by itself. Sent past where the way is still closed
// (beyond the current stage's sunlit spot), it lands on the nearest branch instead and shakes its head (ADR 0011, ADR 0012).
// Flies at the current map's fixed altitude inside the area BirdCamera gives, and perches only on screen.
// The transform is where the bird's feet are, so it stands on a perch point.
public class BirdController : MonoBehaviour
{
    static readonly int PerchedParam = Animator.StringToHash("Perched");
    static readonly int MovingParam = Animator.StringToHash("Moving");
    static readonly int RefuseParam = Animator.StringToHash("Refuse");

    [SerializeField] Animator animator;
    [SerializeField] ClickMarker clickMarker;
    [SerializeField] Texture2D perchCursor;
    [SerializeField] Vector2 perchCursorHotspot;
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
    Bounds bounds = new(Vector3.zero, Vector3.positiveInfinity);
    Bounds view = new(Vector3.zero, Vector3.positiveInfinity);
    // The bird refuses to go past this Z: the way ahead is closed there (ADR 0012).
    float forwardLimit = float.PositiveInfinity;
    // Shakes its head once it has landed.
    bool refusePending;
    float idleTime;
    bool following;
    Perchable perch;
    float landTime;
    float nextCryTime;
    bool perchCursorShown;
    Vector3 baseScale;

    void Awake()
    {
        Instance = this;
        target = transform.position;
        altitude = transform.position.y;
        baseScale = transform.localScale;
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
        transform.position = position;
        target = position;
    }

    public void SetAltitude(float y, bool instant)
    {
        altitude = y;
        if (instant && state == State.Flying)
            transform.position = new Vector3(transform.position.x, y, transform.position.z);
    }

    // area: where it flies. onScreen: where it may perch. forward: the Z it refuses to go past.
    public void SetBounds(Bounds area, Bounds onScreen, float forward)
    {
        bounds = area;
        view = onScreen;
        forwardLimit = forward;
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

        if (moveAction.WasPressedThisFrame() && hasHit && !DevTools.Covers(pointAction.ReadValue<Vector2>()))
            Click(hit, hovered);
        else if (!moveAction.IsPressed())
            following = false;
        else if (following && hasHit)
        {
            if (hit.point.z > forwardLimit)
                Refuse();
            else
                target = hit.point;
        }

        bool cried = cryAction.WasPressedThisFrame();
        if (cried && Time.time >= nextCryTime)
            Cry();

        Vector3 before = transform.position;
        switch (state)
        {
            case State.Flying: Fly(); break;
            case State.ToPerch: FlyToPerch(); break;
            case State.Landing: Land(); break;
            case State.Perched: transform.position = perch.PerchPoint.position; break;
        }
        bool moved = transform.position != before;
        // Glides while it moves and hovers in place otherwise.
        animator.SetBool(MovingParam, moved);
        animator.SetBool(PerchedParam, state == State.Landing || state == State.Perched);

        idleTime = state == State.Flying && !moved && !moveAction.IsPressed() && !cried ? idleTime + Time.deltaTime : 0f;
        if (idleTime >= GameTuning.Current.birdIdlePerchDelay)
            PerchNearby();
    }

    // False if no branch is on screen.
    bool PerchNearby()
    {
        idleTime = 0f;
        Perchable nearest = null;
        float nearestDistance = float.MaxValue;
        foreach (Perchable perchable in Perchable.All)
        {
            float distance = Vector3.Distance(transform.position, perchable.PerchPoint.position);
            if (perchable.IsIdleSpot && perchable.CanPerch && OnScreen(perchable.PerchPoint.position) && distance < nearestDistance)
                (nearest, nearestDistance) = (perchable, distance);
        }
        if (nearest == null)
            return false;
        StartPerch(nearest);
        return true;
    }

    // Sent where the way is still closed: lands on the nearest branch and shakes its head there, or at once if already perched,
    // or once landed if it is landing on a branch. A device it was heading for or landing on is given up, so refusing never opens one.
    // With no branch on screen it stays where it is.
    void Refuse()
    {
        following = false;
        if (state == State.Perched)
            animator.SetTrigger(RefuseParam);
        else if ((state == State.Landing && perch.IsIdleSpot) || PerchNearby())
            refusePending = true;
        else
        {
            LeavePerch();
            target = transform.position;
        }
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

        if (hit.point.z > forwardLimit)
        {
            Refuse();
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
            perch.Released -= LeavePerch;
        perch = null;
        refusePending = false;
        animator.ResetTrigger(RefuseParam);
        if (state != State.Flying)
            target = transform.position;
        state = State.Flying;
    }

    void Fly()
    {
        // Towards the clamped target, so a bird taking off from a branch off the road glides back onto it instead of jumping.
        Vector3 position = transform.position;
        Vector3 flat = Vector3.MoveTowards(new Vector3(position.x, 0f, position.z), Clamp(new Vector3(target.x, 0f, target.z)),
            GameTuning.Current.birdSpeed * Time.deltaTime);
        float y = Mathf.MoveTowards(position.y, altitude, GameTuning.Current.birdAltitudeSpeed * Time.deltaTime);
        Face(flat - position);
        transform.position = new Vector3(flat.x, y, flat.z);
    }

    void FlyToPerch()
    {
        Vector3 point = perch.PerchPoint.position;
        Face(point - transform.position);
        transform.position = Vector3.MoveTowards(transform.position, point, GameTuning.Current.birdSpeed * Time.deltaTime);
        if (transform.position == point)
        {
            state = State.Landing;
            landTime = 0f;
        }
    }

    void Land()
    {
        transform.position = perch.PerchPoint.position;
        landTime += Time.deltaTime;
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

    void Face(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(direction);
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

    bool OnScreen(Vector3 point) =>
        point.x >= view.min.x && point.x <= view.max.x && point.z >= view.min.z && point.z <= view.max.z;

    Vector3 Clamp(Vector3 point) => new(
        Mathf.Clamp(point.x, bounds.min.x, bounds.max.x), point.y, Mathf.Clamp(point.z, bounds.min.z, bounds.max.z));
}
