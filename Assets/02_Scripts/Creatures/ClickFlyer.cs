using UnityEngine;
using UnityEngine.InputSystem;

// Model test scenes only: a left click sends the bird to the ground point under the mouse.
// Holding the button keeps moving the target with the mouse. The wheel zoom (ModelTestCamera) moves its height
// as in the game (BirdController.AltitudeForZoom), from its starting height at zoom 1 over the ground at y 0.
// Main's BirdController needs the game flow and the baby, so it is not used there.
public class ClickFlyer : MonoBehaviour
{
    static readonly int MovingParam = Animator.StringToHash("Moving");

    [SerializeField] Animator animator;
    [SerializeField] ModelTestCamera zoomCamera;
    [Tooltip("Metres per second (GameTuning.birdSpeed).")]
    [SerializeField, Min(0.1f)] float speed = 6f;
    [Tooltip("Degrees per second the heading turns (GameTuning.birdTurnSpeed).")]
    [SerializeField, Min(1f)] float turnSpeed = 240f;

    Vector3 target;
    float baseAltitude;

    void Start()
    {
        target = transform.position;
        baseAltitude = transform.position.y;
    }

    void Update()
    {
        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.isPressed && !Encyclopedia.IsOpen)
        {
            Ray ray = Camera.main.ScreenPointToRay(mouse.position.ReadValue());
            if (new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float distance))
                target = ray.GetPoint(distance);
        }

        Vector3 position = transform.position;
        position.y = BirdController.AltitudeForZoom(zoomCamera.Zoom, baseAltitude, 0f);
        Vector3 toTarget = new Vector3(target.x - position.x, 0f, target.z - position.z);
        bool moving = toTarget.sqrMagnitude > 0.0001f;
        animator.SetBool(MovingParam, moving);
        if (moving)
        {
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(toTarget), turnSpeed * Time.deltaTime);
            position = Vector3.MoveTowards(position, new Vector3(target.x, position.y, target.z), speed * Time.deltaTime);
        }
        transform.position = position;
    }
}
