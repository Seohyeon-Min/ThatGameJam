using UnityEngine;

// A line across the road beyond a device: until the device opens, the camera cannot move past this line (ADR 0005).
// Stages run along +Z, so the line is this transform's Z.
public class CameraGate : MonoBehaviour
{
    [SerializeField] PathBlocker blocker;

    public bool IsOpen => blocker.IsOpen;
}
