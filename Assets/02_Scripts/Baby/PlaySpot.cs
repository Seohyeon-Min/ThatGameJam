using UnityEngine;

public enum PlayKind { SnowPlay, LookAtTree, RunAround }

// A place off the road by a sunlit spot where the girl plays while the bird is away from her (the director, 2026-11-06).
// A child of the route's last point. RunAround runs through its own children in order, round and round.
public class PlaySpot : MonoBehaviour
{
    public PlayKind kind;
    [Tooltip("LookAtTree: the point on the tree she looks up at.")]
    public Transform lookAt;

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.6f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, 0.3f);
        if (lookAt != null)
            Gizmos.DrawLine(transform.position, lookAt.position);
        for (int i = 0; i < transform.childCount; i++)
            Gizmos.DrawLine(transform.GetChild(i).position, transform.GetChild((i + 1) % transform.childCount).position);
    }
}
