using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

// Player-controlled bird: left-click flies to a point or lands on a Perchable, holding the button keeps following the pointer,
// right-click cries. Flies at the current map's fixed altitude and stays inside the current camera section's bounds.
public class BirdController : MonoBehaviour
{
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
    int interactableLayer;
    State state;
    // Horizontal target while flying. Y is unused; the bird's height follows altitude.
    Vector3 target;
    float altitude;
    Bounds bounds = new(Vector3.zero, Vector3.positiveInfinity);
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
        transform.position = position;
        target = position;
    }

    public void SetAltitude(float y, bool instant)
    {
        altitude = y;
        if (instant && state == State.Flying)
            transform.position = new Vector3(transform.position.x, y, transform.position.z);
    }

    public void SetBounds(Bounds area)
    {
        bounds = area;
        if (state != State.Flying && !InBounds(perch.PerchPoint.position))
            LeavePerch();
        target = Clamp(target);
    }

    void Update()
    {
        bool hasHit = Physics.Raycast(Camera.main.ScreenPointToRay(pointAction.ReadValue<Vector2>()), out RaycastHit hit,
            Mathf.Infinity, clickMask, QueryTriggerInteraction.Collide);
        Perchable hovered = hasHit ? PerchableAt(hit) : null;
        ShowPerchCursor(hovered != null);

        if (moveAction.WasPressedThisFrame() && hasHit)
            Click(hit, hovered);
        else if (!moveAction.IsPressed())
            following = false;
        else if (following && hasHit)
            target = Clamp(hit.point);

        if (cryAction.WasPressedThisFrame() && Time.time >= nextCryTime)
            Cry();

        switch (state)
        {
            case State.Flying: Fly(); break;
            case State.ToPerch: FlyToPerch(); break;
            case State.Landing: Land(); break;
            case State.Perched: transform.position = perch.PerchPoint.position; break;
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

        LeavePerch();
        target = Clamp(hit.point);
        clickMarker.Show(new Vector3(target.x, hit.point.y, target.z));
    }

    // Null unless the hit is a perchable Interactable whose perch point is inside the bounds.
    Perchable PerchableAt(RaycastHit hit)
    {
        if (hit.collider.gameObject.layer != interactableLayer)
            return null;
        Perchable perchable = hit.collider.GetComponentInParent<Perchable>();
        return perchable != null && perchable.CanPerch && InBounds(perchable.PerchPoint.position) ? perchable : null;
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
        if (state != State.Flying)
            target = Clamp(transform.position);
        state = State.Flying;
    }

    void Fly()
    {
        Vector3 position = transform.position;
        Vector3 flat = Vector3.MoveTowards(new Vector3(position.x, 0f, position.z), new Vector3(target.x, 0f, target.z),
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

    bool InBounds(Vector3 point) =>
        point.x >= bounds.min.x && point.x <= bounds.max.x && point.z >= bounds.min.z && point.z <= bounds.max.z;

    Vector3 Clamp(Vector3 point) => new(
        Mathf.Clamp(point.x, bounds.min.x, bounds.max.x), point.y, Mathf.Clamp(point.z, bounds.min.z, bounds.max.z));
}
