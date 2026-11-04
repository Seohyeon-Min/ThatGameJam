using UnityEngine;

// One fork of the fog maze (ADR 0028): the right way on to the next fork or the goal, a dead end closed by a wall, and the
// animal on the right way that answers the bird's cry.
public class FogFork : MonoBehaviour
{
    [SerializeField] FogForkAnimal animal;
    // Children in order, from the fork to the next fork or the goal.
    [SerializeField] Transform rightWay;
    // Children in order, from the fork to where the girl stops in front of the wall.
    [SerializeField] Transform deadEnd;

    public FogForkAnimal Animal => animal;
    public Transform RightWay => rightWay;
    public Transform DeadEnd => deadEnd;

    void OnDrawGizmos()
    {
        DrawPath(rightWay, Color.green);
        DrawPath(deadEnd, Color.red);
    }

    void DrawPath(Transform path, Color color)
    {
        if (path == null)
            return;
        Gizmos.color = color;
        Vector3 previous = transform.position;
        foreach (Transform point in path)
        {
            Gizmos.DrawSphere(point.position, 0.12f);
            Gizmos.DrawLine(previous, point.position);
            previous = point.position;
        }
    }
}
