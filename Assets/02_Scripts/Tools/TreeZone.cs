using UnityEngine;

// One area of a TreeGenerator, as a child of it: a box seen from above, sized with the BoxCollider's Edit Collider handles.
// The collider stays off; it is only a shape, so it never catches a click or blocks anything.
[RequireComponent(typeof(BoxCollider))]
public class TreeZone : MonoBehaviour
{
    public enum Kind
    {
        Trees,
        Clear,
        Pines,
    }

    [Tooltip("Trees: trees grow here. Clear: none here, even inside a Trees zone (the road). Pines: some of the trees here are pines, without adding any.")]
    public Kind kind = Kind.Trees;

    BoxCollider Box => GetComponent<BoxCollider>();

    void Reset() => KeepOff();
    void OnValidate() => KeepOff();

    void KeepOff()
    {
        BoxCollider box = Box;
        box.enabled = false;
        box.isTrigger = true;
    }

    // Height doesn't count.
    public bool Contains(Vector3 world)
    {
        BoxCollider box = Box;
        Vector3 local = transform.InverseTransformPoint(world) - box.center;
        return Mathf.Abs(local.x) <= box.size.x * 0.5f && Mathf.Abs(local.z) <= box.size.z * 0.5f;
    }

    // The box's four corners from above, in world space.
    public Vector3[] Corners()
    {
        BoxCollider box = Box;
        Vector3 c = box.center, h = box.size * 0.5f;
        return new[]
        {
            transform.TransformPoint(c + new Vector3(-h.x, 0f, -h.z)),
            transform.TransformPoint(c + new Vector3(-h.x, 0f, h.z)),
            transform.TransformPoint(c + new Vector3(h.x, 0f, h.z)),
            transform.TransformPoint(c + new Vector3(h.x, 0f, -h.z)),
        };
    }

    void OnDrawGizmos()
    {
        Gizmos.color = kind switch
        {
            Kind.Trees => new Color(0.3f, 0.9f, 0.4f),
            Kind.Clear => new Color(1f, 0.35f, 0.3f),
            _ => new Color(0.2f, 0.55f, 0.35f),
        };
        Vector3[] corners = Corners();
        for (int i = 0; i < 4; i++)
            Gizmos.DrawLine(corners[i], corners[(i + 1) % 4]);
    }
}
