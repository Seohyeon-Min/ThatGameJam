using Unity.Cinemachine;
using UnityEngine;

// A camera section of a stage. When the baby enters its trigger, its fixed camera takes over
// and the bird is kept inside this section's area (design doc §2.5, plan Q3).
public class CameraZone : MonoBehaviour
{
    const int ActivePriority = 10;

    static CameraZone active;

    [SerializeField] CinemachineCamera sectionCamera;
    // World space. The bird cannot leave this area while the zone is active.
    [SerializeField] Bounds birdArea = new(Vector3.zero, new Vector3(10f, 4f, 10f));

    public void Activate()
    {
        if (active != null)
            active.sectionCamera.Priority = 0;
        active = this;
        sectionCamera.Priority = ActivePriority;
        BirdController.Instance.SetBounds(birdArea);
    }

    void OnTriggerEnter(Collider other)
    {
        if (active != this && other.GetComponentInParent<Baby>() != null)
            Activate();
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireCube(birdArea.center, birdArea.size);
    }
}
