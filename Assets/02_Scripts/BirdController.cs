using UnityEngine;
using UnityEngine.InputSystem;

// Player-controlled bird: WASD moves relative to the camera, mouse wheel steps the flight height,
// E clears the nearest obstacle or perches on the nearest branch. Any move or height input takes off again.
[RequireComponent(typeof(Rigidbody))]
public class BirdController : MonoBehaviour
{
    [SerializeField] Transform cameraTransform;
    [SerializeField] float moveSpeed = 6f;
    [SerializeField] float verticalSpeed = 6f;
    [SerializeField] float heightStep = 0.5f;
    [SerializeField] float minHeight = 0.5f;
    [SerializeField] float maxHeight = 8f;
    [SerializeField] float interactRadius = 1.5f;
    [SerializeField] float perchRadius = 1f;
    [SerializeField, Min(0f)] float landDuration = 0.2f;

    enum State { Flying, Landing, Perched }

    Rigidbody body;
    InputAction moveAction;
    InputAction heightAction;
    InputAction interactAction;
    float targetHeight;
    State state;
    PerchPoint perch;
    PerchPoint highlighted;
    Vector3 landStart;
    float landTime;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        targetHeight = transform.position.y;

        moveAction = new InputAction("Move", InputActionType.Value);
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");
        heightAction = new InputAction("Height", InputActionType.Value, "<Mouse>/scroll/y");
        interactAction = new InputAction("Interact", InputActionType.Button, "<Keyboard>/e");
    }

    void OnEnable()
    {
        moveAction.Enable();
        heightAction.Enable();
        interactAction.Enable();
    }

    void OnDisable()
    {
        moveAction.Disable();
        heightAction.Disable();
        interactAction.Disable();
    }

    void OnDestroy()
    {
        moveAction.Dispose();
        heightAction.Dispose();
        interactAction.Dispose();
    }

    void Update()
    {
        // Scroll delta magnitudes differ between platforms and browsers, so only the sign is used.
        float scroll = heightAction.ReadValue<float>();
        if (state != State.Flying && (scroll != 0f || moveAction.ReadValue<Vector2>() != Vector2.zero))
            TakeOff();

        if (state == State.Flying)
        {
            if (scroll != 0f)
                targetHeight = Mathf.Clamp(targetHeight + Mathf.Sign(scroll) * heightStep, minHeight, maxHeight);
            if (interactAction.WasPressedThisFrame())
                Interact();
        }

        UpdateHighlight();
    }

    void FixedUpdate()
    {
        if (state == State.Flying)
            Fly();
        else if (state == State.Landing)
            Land();
    }

    void Fly()
    {
        Vector2 input = moveAction.ReadValue<Vector2>();
        Vector3 forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
        Vector3 horizontal = (forward * input.y + right * input.x) * moveSpeed;
        float vertical = Mathf.Clamp((targetHeight - body.position.y) / Time.fixedDeltaTime, -verticalSpeed, verticalSpeed);

        body.linearVelocity = new Vector3(horizontal.x, vertical, horizontal.z);
        if (horizontal.sqrMagnitude > 0.01f)
            body.MoveRotation(Quaternion.LookRotation(horizontal));
    }

    void Land()
    {
        landTime += Time.fixedDeltaTime;
        float t = Mathf.Clamp01(landTime / landDuration);
        body.MovePosition(Vector3.Lerp(landStart, perch.transform.position, t));
        if (t >= 1f)
        {
            body.MoveRotation(perch.transform.rotation);
            state = State.Perched;
        }
    }

    void Interact()
    {
        Obstacle obstacle = NearestObstacle(out float obstacleDistance);
        PerchPoint perchPoint = NearestPerch(out float perchDistance);
        if (perchPoint != null && perchDistance < obstacleDistance)
            PerchOn(perchPoint);
        else if (obstacle != null)
            obstacle.Clear();
    }

    void PerchOn(PerchPoint point)
    {
        perch = point;
        landStart = body.position;
        landTime = 0f;
        body.linearVelocity = Vector3.zero;
        body.isKinematic = true;
        state = State.Landing;
    }

    void TakeOff()
    {
        perch = null;
        body.isKinematic = false;
        targetHeight = Mathf.Clamp(body.position.y, minHeight, maxHeight);
        state = State.Flying;
    }

    void UpdateHighlight()
    {
        PerchPoint next = state == State.Flying ? NearestPerch(out _) : null;
        if (next == highlighted)
            return;
        if (highlighted != null)
            highlighted.SetHighlighted(false);
        if (next != null)
            next.SetHighlighted(true);
        highlighted = next;
    }

    // Distances are squared.
    Obstacle NearestObstacle(out float nearestDistance)
    {
        Obstacle nearest = null;
        nearestDistance = float.MaxValue;
        foreach (Collider hit in Physics.OverlapSphere(body.position, interactRadius))
        {
            if (!hit.TryGetComponent(out Obstacle obstacle))
                continue;
            float distance = (hit.ClosestPoint(body.position) - body.position).sqrMagnitude;
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = obstacle;
            }
        }
        return nearest;
    }

    PerchPoint NearestPerch(out float nearestDistance)
    {
        PerchPoint nearest = null;
        nearestDistance = float.MaxValue;
        foreach (PerchPoint point in PerchPoint.All)
        {
            float distance = (point.transform.position - body.position).sqrMagnitude;
            if (distance <= perchRadius * perchRadius && distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = point;
            }
        }
        return nearest;
    }
}
