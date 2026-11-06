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
        Thin,
    }

    [Tooltip("Trees: trees grow here. Clear: none here, even inside a Trees zone (the road). Pines: some of the trees here are pines, without adding any. Thin: the trees here thin out along the box's Z, from all of them at its -Z face to Far Share at its +Z face.")]
    public Kind kind = Kind.Trees;
    [Tooltip("Thin: the share of the trees kept at the box's +Z face.")]
    [Range(0f, 1f)] public float farShare = 0.05f;

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

    // Thin: the share of the trees kept at this point, eased from 1 at the box's -Z face to farShare at its +Z face. 1 outside the box.
    public float KeptShare(Vector3 world)
    {
        if (!Contains(world))
            return 1f;
        BoxCollider box = Box;
        float along = (transform.InverseTransformPoint(world).z - box.center.z) / box.size.z + 0.5f;
        return Mathf.Lerp(1f, farShare, Mathf.SmoothStep(0f, 1f, along));
    }

#if UNITY_EDITOR
    // A stage gap opened at joinZ (WorldGap): past it the zone moves, across it the zone grows by the gap at its far end.
    public void ShiftBeyond(float joinZ, float size)
    {
        float near = float.MaxValue, far = float.MinValue;
        foreach (Vector3 corner in Corners())
        {
            near = Mathf.Min(near, corner.z);
            far = Mathf.Max(far, corner.z);
        }
        if (far <= joinZ)
            return;
        if (near >= joinZ)
        {
            UnityEditor.Undo.RecordObject(transform, "Open Stage Gap");
            transform.position += Vector3.forward * size;
            return;
        }
        // Across the join: the far side moves, so the box grows along world Z.
        BoxCollider box = Box;
        Vector3 localGrow = transform.InverseTransformVector(Vector3.forward * size);
        UnityEditor.Undo.RecordObject(box, "Open Stage Gap");
        box.size += new Vector3(Mathf.Abs(localGrow.x), 0f, Mathf.Abs(localGrow.z)) * Mathf.Sign(size);
        box.center += localGrow * 0.5f;
    }
#endif

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
            Kind.Thin => new Color(1f, 0.75f, 0.3f),
            _ => new Color(0.2f, 0.55f, 0.35f),
        };
        Vector3[] corners = Corners();
        for (int i = 0; i < 4; i++)
            Gizmos.DrawLine(corners[i], corners[(i + 1) % 4]);
    }
}
