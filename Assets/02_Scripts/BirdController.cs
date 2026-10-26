using UnityEngine;
using UnityEngine.InputSystem;

// Player-controlled bird: WASD moves relative to the camera, mouse wheel steps the flight height, E interacts.
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

    Rigidbody body;
    InputAction moveAction;
    InputAction heightAction;
    InputAction interactAction;
    float targetHeight;

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
        if (scroll != 0f)
            targetHeight = Mathf.Clamp(targetHeight + Mathf.Sign(scroll) * heightStep, minHeight, maxHeight);

        if (interactAction.WasPressedThisFrame())
            Interact();
    }

    void FixedUpdate()
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

    void Interact()
    {
        Obstacle nearest = null;
        float nearestDistance = float.MaxValue;
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

        if (nearest != null)
            nearest.Clear();
    }
}
