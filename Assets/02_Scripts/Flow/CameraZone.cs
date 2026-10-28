using UnityEngine;

// A device's camera zone (ADR 0007, ADR 0008). Once the bird crosses the trigger line, the camera eases into this zone's area
// and follows the bird only inside it, until the device opens. Opening releases it for good, even if the bird flies back over
// the trigger. The area is centred on this transform.
public class CameraZone : MonoBehaviour
{
    [SerializeField] PathBlocker blocker;
    // A line across the road. Stages run along +Z, so the line is this transform's Z.
    [SerializeField] Transform trigger;
    // X and Z size of the area the camera's followed point stays in while locked. Keep it on the road.
    [SerializeField] Vector2 areaSize = new(2f, 1f);

    bool reached;

    public bool IsLocked => reached && !blocker.IsOpen;

    public Vector3 AreaMin => transform.position - new Vector3(areaSize.x, 0f, areaSize.y) * 0.5f;
    public Vector3 AreaMax => transform.position + new Vector3(areaSize.x, 0f, areaSize.y) * 0.5f;

    // BirdCamera calls this for the current stage's zones only, so a zone of another stage never locks early.
    public void TrackBird(Vector3 bird)
    {
        if (bird.z >= trigger.position.z)
            reached = true;
    }

    void OnDrawGizmos()
    {
        if (trigger == null)
            return;
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireCube(transform.position, new Vector3(areaSize.x, 0.1f, areaSize.y));
        Gizmos.DrawLine(trigger.position + Vector3.left * 8f, trigger.position + Vector3.right * 8f);
    }
}
