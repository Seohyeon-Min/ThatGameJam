using Unity.Cinemachine;
using UnityEngine;

// The camera follows the bird (ADR 0005), not the baby: the two are separate beings (ADR 0007). The bird also locks the zones (ADR 0008).
// The bird flies on the stage's road, and so does the point the camera follows; it perches anywhere on screen. While a device's camera zone is locked,
// the point stays inside that zone's area, and the bird is also kept within view of the point so it never leaves the screen
// (ADR 0003 Q3).
public class BirdCamera : MonoBehaviour
{
    [SerializeField] CinemachineCamera followCamera;
    // How far the bird may be from the followed point, in X and Z, so the bird stays on screen.
    [SerializeField] Vector2 birdRange = new(6f, 5f);
    // Seconds the camera takes to ease into or out of a zone's area.
    [SerializeField, Min(0.01f)] float zoneEaseTime = 1f;

    Stage stage;
    CameraZone[] zones;
    // Per zone, 0 free to 1 locked, eased so locking and unlocking glide, also when one zone hands over to the next.
    float[] zoneWeights;

    public void SetStage(Stage newStage)
    {
        stage = newStage;
        zones = stage.GetComponentsInChildren<CameraZone>();
        zoneWeights = new float[zones.Length];
        LateUpdate();
        followCamera.PreviousStateIsValid = false;
    }

    void LateUpdate()
    {
        Vector3 bird = BirdController.Instance.transform.position;
        Vector3 point = ClampXZ(bird, stage.area.min, stage.area.max);
        for (int i = 0; i < zones.Length; i++)
        {
            CameraZone zone = zones[i];
            zone.TrackBird(bird);
            zoneWeights[i] = Mathf.MoveTowards(zoneWeights[i], zone.IsLocked ? 1f : 0f, Time.deltaTime / zoneEaseTime);
            point = Vector3.Lerp(point, ClampXZ(point, zone.AreaMin, zone.AreaMax), Mathf.SmoothStep(0f, 1f, zoneWeights[i]));
        }
        point.y = stage.area.center.y;
        transform.position = point;

        // The bird flies on the road and on screen; it may perch anywhere on screen (roadside trees stand beyond the road).
        Vector3 range = new(birdRange.x, 0f, birdRange.y);
        BirdController.Instance.SetBounds(Box(Vector3.Max(stage.area.min, point - range), Vector3.Min(stage.area.max, point + range), point.y),
            Box(point - range, point + range, point.y));
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
