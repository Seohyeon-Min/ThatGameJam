using UnityEngine;

// A device's camera zone (ADR 0007, ADR 0014). When the bird enters the trigger, a band across the road, the camera eases into
// this zone's area and follows the bird only inside it. Once the bird leaves the larger lock area the camera follows it again;
// entering the trigger again locks it again. Opening the device releases it for good. The areas are centred on this transform.
public class CameraZone : MonoBehaviour
{
    [SerializeField] PathBlocker blocker;
    // The centre of the trigger band. Keep the band inside the lock area.
    [SerializeField] Transform trigger;
    // X and Z size of the trigger band.
    [SerializeField] Vector2 triggerSize = new(10f, 1f);
    // X and Z size of the area the bird must leave to unlock. The bird stays on screen, so keep this inside the locked view.
    [SerializeField] Vector2 lockSize = new(10f, 8f);
    // X and Z size of the area the camera's followed point stays in while locked.
    [SerializeField] Vector2 areaSize = new(2f, 1f);

    bool locked;

    public bool IsLocked => locked && !blocker.IsOpen;

    public Vector3 AreaMin => transform.position - XZ(areaSize) * 0.5f;
    public Vector3 AreaMax => transform.position + XZ(areaSize) * 0.5f;

    // BirdCamera calls this for the current stage's zones only, so a zone of another stage never locks early.
    public void TrackBird(Vector3 bird)
    {
        if (!locked)
            locked = Inside(bird, trigger.position, triggerSize);
        else if (!Inside(bird, transform.position, lockSize))
            locked = false;
    }

    static bool Inside(Vector3 point, Vector3 center, Vector2 size) =>
        Mathf.Abs(point.x - center.x) <= size.x * 0.5f && Mathf.Abs(point.z - center.z) <= size.y * 0.5f;

    static Vector3 XZ(Vector2 size) => new(size.x, 0f, size.y);

    void OnDrawGizmos()
    {
        if (trigger == null)
            return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(trigger.position, XZ(triggerSize) + Vector3.up * 0.1f);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position, XZ(lockSize) + Vector3.up * 0.1f);
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireCube(transform.position, XZ(areaSize) + Vector3.up * 0.1f);
    }
}
