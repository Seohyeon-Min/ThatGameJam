using UnityEngine;

// AI child: walks straight toward the goal and waits while an Obstacle blocks the way.
public class ChildWalker : MonoBehaviour
{
    [SerializeField] Transform goal;
    [SerializeField] float walkSpeed = 1.5f;
    [SerializeField] float probeRadius = 0.3f;
    [SerializeField] float probeDistance = 0.6f;

    void Update()
    {
        Vector3 toGoal = goal.position - transform.position;
        toGoal.y = 0f;
        float remaining = toGoal.magnitude;
        if (remaining < 0.01f)
            return;

        Vector3 direction = toGoal / remaining;
        transform.rotation = Quaternion.LookRotation(direction);
        if (IsBlocked(direction))
            return;

        transform.position += direction * Mathf.Min(walkSpeed * Time.deltaTime, remaining);
    }

    bool IsBlocked(Vector3 direction)
    {
        foreach (RaycastHit hit in Physics.SphereCastAll(transform.position, probeRadius, direction, probeDistance))
        {
            if (hit.collider.TryGetComponent(out Obstacle _))
                return true;
        }
        return false;
    }
}
