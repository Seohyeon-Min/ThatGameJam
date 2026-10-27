using Unity.Cinemachine;
using UnityEngine;

// The camera follows the bird (ADR 0005), not the baby: the two are separate beings (ADR 0007).
// The bird stays on the stage's road and behind the wall of every device not yet open, and so does the point the camera
// follows. While a device's camera zone is locked,
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
        Vector3 point = ClampXZ(BirdController.Instance.transform.position, stage.area.min, stage.area.max);
        for (int i = 0; i < zones.Length; i++)
        {
            CameraZone zone = zones[i];
            zoneWeights[i] = Mathf.MoveTowards(zoneWeights[i], zone.IsLocked ? 1f : 0f, Time.deltaTime / zoneEaseTime);
            point = Vector3.Lerp(point, ClampXZ(point, zone.AreaMin, zone.AreaMax), Mathf.SmoothStep(0f, 1f, zoneWeights[i]));
        }
        point.y = stage.area.center.y;
        transform.position = point;

        Vector3 range = new(birdRange.x, 0f, birdRange.y);
        Vector3 min = Vector3.Max(stage.area.min, point - range);
        Vector3 max = Vector3.Min(stage.area.max, point + range);
        foreach (CameraZone zone in zones)
        {
            if (zone.IsWallUp)
                max.z = Mathf.Min(max.z, zone.WallZ);
        }
        var bounds = new Bounds();
        bounds.SetMinMax(new Vector3(min.x, point.y - 50f, min.z), new Vector3(max.x, point.y + 50f, max.z));
        BirdController.Instance.SetBounds(bounds);
    }

    static Vector3 ClampXZ(Vector3 point, Vector3 min, Vector3 max) =>
        new(Mathf.Clamp(point.x, min.x, max.x), point.y, Mathf.Clamp(point.z, min.z, max.z));
}
