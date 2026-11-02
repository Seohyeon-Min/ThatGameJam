using UnityEngine;

// The path the baby walks: its child transforms, in order. Points carry height (for example along the fallen log).
// The last point is the sunlit rest area: the stage's SunlitSpot sits under it, so moving that point moves the light, where the baby
// lies down and how far the bird may fly, together.
public class BabyRoute : MonoBehaviour
{
    public int Count => transform.childCount;
    public Transform this[int index] => transform.GetChild(index);

    void OnDrawGizmos()
    {
        for (int i = 0; i < Count; i++)
        {
            Transform point = this[i];
            Gizmos.color = point.TryGetComponent(out StopPoint _) ? Color.red : Color.yellow;
            Gizmos.DrawSphere(point.position, 0.15f);
            Gizmos.color = Color.yellow;
            if (i > 0)
                Gizmos.DrawLine(this[i - 1].position, point.position);
        }
    }
}
