using UnityEngine;

// A device's camera zone (ADR 0007, ADR 0014). When the bird first enters the trigger, a band across the road, the zone wakes up:
// the camera eases into this zone's area and, a semi-lock, only drifts a little after the bird: a share of the way, easing off
// smoothly toward the area's edge rather than stopping at it. It lets go gradually as the bird nears the edge of
// the larger lock area, over the last Release Softness of it, and is fully free at the edge. In that band it only ever lets go
// further, never takes hold again, so the camera moving after the bird can't make it grab and let go over and over; it holds again
// once the bird is back inside, clear of the band. Opening the device releases it for good. The areas are centred on this transform, the lock area unless it is
// offset. Without a device (a sunlit spot's semi-lock) it works the same until its stage ends, when the baby has fallen asleep
// there and the season changes: that is its opening, and it never holds again (GameFlow calls Release).
public class CameraZone : MonoBehaviour
{
    [Tooltip("The device that releases the zone for good once it opens. Empty: none, the zone locks until its stage ends.")]
    [SerializeField] PathBlocker blocker;
    // The centre of the trigger band. Keep the band inside the lock area.
    [SerializeField] Transform trigger;
    // X and Z size of the trigger band.
    [SerializeField] Vector2 triggerSize = new(10f, 1f);
    // X and Z size of the area the bird must leave to unlock. The bird stays on screen, so keep this inside the locked view.
    [SerializeField] Vector2 lockSize = new(10f, 8f);
    // X and Z offset of the lock area's centre from this transform, so it can reach further one way (past the cliff in the ending).
    [SerializeField] Vector2 lockOffset;
    // X and Z size of the area the camera's followed point stays in while locked.
    [SerializeField] Vector2 areaSize = new(2f, 1f);
    [Tooltip("Share of the bird's moves the camera follows while held (0 = none, 1 = all), easing off toward the area's edge")]
    [SerializeField, Range(0f, 1f)] float semiFollow = 0.3f;
    [Tooltip("Metres inside the lock area's edge over which the camera lets go as the bird flies out, so it eases off instead of letting go all at once at the edge, and the bird doesn't have to go as far")]
    [SerializeField, Min(0f)] float releaseSoftness = 4f;

    // The bird has crossed the trigger once: from then on the zone holds whenever the bird is in the lock area.
    bool woken;
    // Let go for good: a zone without a device, once its stage has ended.
    bool released;
    float hold;

    // 1 holding the camera in the area, down to 0 free, by how far into the lock area the bird is.
    public float Hold => released || blocker != null && blocker.IsOpen ? 0f : hold;

    public bool HasDevice => blocker != null;

    // Its stage has ended: never holds again, like a device that has opened.
    public void Release() => released = true;
    public bool IsLocked => Hold > 0f;

    // Where the camera's followed point goes while held, for the point it would take on its own: Semi Follow of the way from the
    // area's centre, and never past the area, which it nears smoothly (tanh) instead of stopping at with a jolt.
    public Vector3 SemiFollow(Vector3 point)
    {
        Vector3 center = transform.position;
        return new Vector3(center.x + Soft(point.x - center.x, areaSize.x * 0.5f), point.y, center.z + Soft(point.z - center.z, areaSize.y * 0.5f));
    }

    float Soft(float offset, float half) => half <= 0f ? 0f : half * Tanh(offset * semiFollow / half);

    static float Tanh(float x)
    {
        float e = Mathf.Exp(2f * Mathf.Clamp(x, -10f, 10f));
        return (e - 1f) / (e + 1f);
    }

    // BirdCamera calls this for the current stage's zones only, so a zone of another stage never locks early.
    public void TrackBird(Vector3 bird)
    {
        if (!woken)
            woken = Inside(bird, trigger.position, triggerSize);
        if (!woken)
        {
            hold = 0f;
            return;
        }
        // How far outside the lock area the bird is (negative inside), on whichever side it is nearest the edge.
        Vector3 fromCenter = bird - LockCenter;
        float outside = Mathf.Max(Mathf.Abs(fromCenter.x) - lockSize.x * 0.5f, Mathf.Abs(fromCenter.z) - lockSize.y * 0.5f);
        float byPlace = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-Mathf.Max(releaseSoftness, 1e-3f), 0f, outside));
        // Clear of the band: holds fully. In the band: only lets go further. BirdCamera eases the camera to either.
        hold = byPlace >= 1f ? 1f : Mathf.Min(hold, byPlace);
    }

    Vector3 LockCenter => transform.position + XZ(lockOffset);

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
        Gizmos.DrawWireCube(LockCenter, XZ(lockSize) + Vector3.up * 0.1f);
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireCube(transform.position, XZ(areaSize) + Vector3.up * 0.1f);
    }
}
