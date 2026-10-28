using Unity.Cinemachine;
using UnityEngine;

// The camera follows the bird (ADR 0005), not the baby: the two are separate beings (ADR 0007). The bird also locks the zones (ADR 0008).
// The bird flies on the road GameFlow opens to it (ADR 0010), and so does the point the camera follows; it perches anywhere on screen. While a device's camera zone is locked,
// the point stays inside that zone's area, and the bird is also kept within view of the point so it never leaves the screen
// (ADR 0003 Q3).
public class BirdCamera : MonoBehaviour
{
    [SerializeField] CinemachineCamera followCamera;
    // How far the bird may be from the followed point, in X and Z, so the bird stays on screen.
    [SerializeField] Vector2 birdRange = new(6f, 5f);
    // Seconds the camera takes to ease into or out of a zone's area.
    [SerializeField, Min(0.01f)] float zoneEaseTime = 1f;

    CameraZone[] zones;
    // Per zone, 0 free to 1 locked, eased so locking and unlocking glide, also when one zone hands over to the next.
    float[] zoneWeights;

    // Only the current stage's zones lock.
    public void SetStage(Stage stage)
    {
        zones = stage.GetComponentsInChildren<CameraZone>();
        zoneWeights = new float[zones.Length];
    }

    // Puts the camera on the bird at once instead of sweeping there.
    public void Cut()
    {
        LateUpdate();
        followCamera.PreviousStateIsValid = false;
    }

    void LateUpdate()
    {
        Vector3 bird = BirdController.Instance.transform.position;
        Bounds area = GameFlow.Instance.FlyArea;
        Vector3 point = ClampXZ(bird, area.min, area.max);
        for (int i = 0; i < zones.Length; i++)
        {
            CameraZone zone = zones[i];
            zone.TrackBird(bird);
            zoneWeights[i] = Mathf.MoveTowards(zoneWeights[i], zone.IsLocked ? 1f : 0f, Time.deltaTime / zoneEaseTime);
            point = Vector3.Lerp(point, ClampXZ(point, zone.AreaMin, zone.AreaMax), Mathf.SmoothStep(0f, 1f, zoneWeights[i]));
        }
        point.y = area.center.y;
        transform.position = point;

        // The bird flies on the road and on screen. It may perch anywhere on screen across the road (roadside trees stand beyond it),
        // but not past the area's ends, so no branch takes it beyond the current stage's sunlit spot (ADR 0012).
        Vector3 range = new(birdRange.x, 0f, birdRange.y);
        Vector3 perchMin = new(point.x - range.x, 0f, Mathf.Max(area.min.z, point.z - range.z));
        Vector3 perchMax = new(point.x + range.x, 0f, Mathf.Min(area.max.z, point.z + range.z));
        BirdController.Instance.SetBounds(Box(Vector3.Max(area.min, point - range), Vector3.Min(area.max, point + range), point.y),
            Box(perchMin, perchMax, point.y), area.max.z);
    }

    static Bounds Box(Vector3 min, Vector3 max, float y)
    {
        var bounds = new Bounds();
        bounds.SetMinMax(new Vector3(min.x, y - 50f, min.z), new Vector3(max.x, y + 50f, max.z));
        return bounds;
    }

    static Vector3 ClampXZ(Vector3 point, Vector3 min, Vector3 max) =>
        new(Mathf.Clamp(point.x, min.x, max.x), point.y, Mathf.Clamp(point.z, min.z, max.z));
}
