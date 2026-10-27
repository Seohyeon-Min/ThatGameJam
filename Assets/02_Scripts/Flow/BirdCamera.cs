using Unity.Cinemachine;
using UnityEngine;

// The camera follows the bird (ADR 0005). The point it follows stays inside the stage area, close enough to the baby
// that the baby stays on screen, and behind the first camera gate whose device is still closed.
// The bird in turn is kept within view of that point, so it never leaves the screen (ADR 0003 Q3).
public class BirdCamera : MonoBehaviour
{
    [SerializeField] CinemachineCamera followCamera;
    // How far the followed point may be from the baby, in X and Z, so the baby stays on screen.
    [SerializeField] Vector2 babyRange = new(5f, 4f);
    // How far the bird may be from the followed point, in X and Z, so the bird stays on screen.
    [SerializeField] Vector2 birdRange = new(6f, 5f);

    Stage stage;
    CameraGate[] gates;

    public void SetStage(Stage newStage)
    {
        stage = newStage;
        gates = stage.GetComponentsInChildren<CameraGate>();
        LateUpdate();
        followCamera.PreviousStateIsValid = false;
    }

    void LateUpdate()
    {
        Vector3 baby = Baby.Instance.transform.position;
        Vector3 point = BirdController.Instance.transform.position;
        point = ClampXZ(point, stage.area.min, stage.area.max);
        point = ClampXZ(point, baby - Flat(babyRange), baby + Flat(babyRange));
        foreach (CameraGate gate in gates)
        {
            if (!gate.IsOpen)
                point.z = Mathf.Min(point.z, gate.transform.position.z);
        }
        point.y = baby.y;
        transform.position = point;
        BirdController.Instance.SetBounds(new Bounds(point, new Vector3(birdRange.x * 2f, 100f, birdRange.y * 2f)));
    }

    static Vector3 Flat(Vector2 range) => new(range.x, 0f, range.y);

    static Vector3 ClampXZ(Vector3 point, Vector3 min, Vector3 max) =>
        new(Mathf.Clamp(point.x, min.x, max.x), point.y, Mathf.Clamp(point.z, min.z, max.z));
}
