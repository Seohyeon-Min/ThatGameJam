using UnityEngine;

// Scatters flower prefabs (ScatterGenerator), keeping paths such as the stages' routes clear: fewer flowers the nearer they are.
public class FlowerGenerator : ScatterGenerator
{
    [Tooltip("Paths kept clear of flowers, such as the stages' routes: fewer flowers the nearer they are, fading in away from them")]
    [SerializeField] BabyRoute[] paths = { };
    [Tooltip("How strongly the paths are kept clear. 0 = paths ignored, 1 = no flowers at all on them")]
    [SerializeField, Range(0f, 1f)] float pathClearing = 1f;
    [Tooltip("Distance from a path's line that is cleared fully, in metres")]
    [SerializeField, Min(0f)] float pathWidth = 1.5f;
    [Tooltip("Distance beyond Path Width over which the flowers come back to full, in metres")]
    [SerializeField, Min(0.01f)] float pathFade = 3f;

    [System.Serializable]
    public struct ClearArea
    {
        [Tooltip("Corner (X, Z) in this object's local space")]
        public Vector2 min;
        [Tooltip("Opposite corner (X, Z) in this object's local space")]
        public Vector2 max;
    }

    [Tooltip("Boxes where no flower grows at all, such as the cliff at the end of the fog stage (the red boxes)")]
    [SerializeField] ClearArea[] clearAreas = { };
    [Tooltip("Distance inside a Clear Area over which the flowers thin out to none, in metres, so its border doesn't show as a line")]
    [SerializeField, Min(0f)] float clearAreaFade = 1.5f;

    protected override Color GizmoColor => new(1f, 0.55f, 0.75f);

    protected override float Keep(Vector3 point) => KeepByPaths(point) * KeepByClearAreas(point);

    // 1 outside every clear area, 0 deep inside one, eased over clearAreaFade from its border.
    float KeepByClearAreas(Vector3 point)
    {
        Vector3 local = transform.InverseTransformPoint(point);
        float keep = 1f;
        foreach (ClearArea area in clearAreas)
        {
            Vector2 min = Vector2.Min(area.min, area.max), max = Vector2.Max(area.min, area.max);
            // How far inside the box, to its nearest side.
            float inside = Mathf.Min(Mathf.Min(local.x - min.x, max.x - local.x), Mathf.Min(local.z - min.y, max.y - local.z));
            if (inside <= 0f)
                continue;
            keep *= clearAreaFade > 0f ? 1f - Mathf.SmoothStep(0f, 1f, inside / clearAreaFade) : 0f;
        }
        return keep;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(1f, 0.2f, 0.2f);
        foreach (ClearArea area in clearAreas)
        {
            Vector2 center = (area.min + area.max) / 2f;
            Vector2 size = area.max - area.min;
            Gizmos.DrawWireCube(new Vector3(center.x, 0f, center.y), new Vector3(Mathf.Abs(size.x), 0f, Mathf.Abs(size.y)));
        }
    }

    // 1 away from every path, falling to 1 - pathClearing on one, smoothly in between.
    float KeepByPaths(Vector3 point)
    {
        if (pathClearing <= 0f || paths.Length == 0)
            return 1f;
        float nearest = float.MaxValue;
        var flat = new Vector2(point.x, point.z);
        foreach (BabyRoute path in paths)
        {
            if (path == null || path.Count == 0)
                continue;
            for (int i = 0; i < path.Count; i++)
            {
                Vector3 a = path[i].position, b = path[Mathf.Min(i + 1, path.Count - 1)].position;
                nearest = Mathf.Min(nearest, DistanceToSegment(flat, new Vector2(a.x, a.z), new Vector2(b.x, b.z)));
            }
        }
        float away = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(pathWidth, pathWidth + pathFade, nearest));
        return Mathf.Lerp(1f, away, pathClearing);
    }

    static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float along = ab.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector2.Dot(point - a, ab) / ab.sqrMagnitude) : 0f;
        return Vector2.Distance(point, a + ab * along);
    }
}
