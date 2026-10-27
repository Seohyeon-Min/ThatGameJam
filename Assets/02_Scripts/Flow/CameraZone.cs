using UnityEngine;

// A device's camera zone (ADR 0007). Once the baby reaches the trigger, the camera eases into this zone's area and follows
// the bird only inside it, until the device opens. Opening releases it for good, even if the baby walks back over the trigger.
// The area is centred on this transform. Until the device opens, an invisible wall also keeps the bird from flying past it,
// whether or not the baby has arrived.
public class CameraZone : MonoBehaviour
{
    [SerializeField] PathBlocker blocker;
    [SerializeField] Transform trigger;
    [SerializeField] float triggerRadius = 1f;
    // X and Z size of the area the camera's followed point stays in while locked. Keep it on the road, and its far edge
    // within BirdCamera's birdRange of the wall, so the bird's range never turns inside out.
    [SerializeField] Vector2 areaSize = new(4f, 5f);
    // The bird cannot fly past this line until the device opens. Stages run along +Z, so the line is this transform's Z.
    [SerializeField] Transform wall;

    bool reached;

    public bool IsLocked => reached && !blocker.IsOpen;
    public bool IsWallUp => !blocker.IsOpen;
    public float WallZ => wall.position.z;

    public Vector3 AreaMin => transform.position - new Vector3(areaSize.x, 0f, areaSize.y) * 0.5f;
    public Vector3 AreaMax => transform.position + new Vector3(areaSize.x, 0f, areaSize.y) * 0.5f;

    void Update()
    {
        Vector3 offset = Baby.Instance.transform.position - trigger.position;
        offset.y = 0f;
        if (offset.magnitude <= triggerRadius)
            reached = true;
    }

    void OnDrawGizmos()
    {
        if (trigger == null || wall == null)
            return;
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireCube(transform.position, new Vector3(areaSize.x, 0.1f, areaSize.y));
        Gizmos.DrawWireSphere(trigger.position, triggerRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawLine(wall.position + Vector3.left * 8f, wall.position + Vector3.right * 8f);
    }
}
