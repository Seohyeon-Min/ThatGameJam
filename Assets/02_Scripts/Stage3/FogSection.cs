using System.Collections.Generic;
using UnityEngine;

// One fog section (design doc §9.2 - §9.5, plan 4.12). The baby stops at the fog edge and follows the bird's trail
// to the exit. When the timer runs out, the fog recedes along the authored correct path and the baby walks that instead (Q10).
public class FogSection : PathBlocker
{
    [SerializeField] FogField field;
    [SerializeField] NaturalTimer timer;
    // Its children, in order, are the correct path from the fog edge to the exit.
    [SerializeField] Transform correctPath;
    [SerializeField] Transform exit;
    // How wide the bird clears the fog (design TBD, §16).
    [SerializeField, Min(0.1f)] float clearRadius = 1.3f;
    // Also the step of loops along the path, so it must stay positive.
    [SerializeField, Min(0.1f)] float pointSpacing = 0.5f;
    // The bird passing this close to the baby starts a new trail from the baby (design TBD, §16).
    [SerializeField] float reconnectRadius = 1.5f;
    [SerializeField] float exitRadius = 1.2f;
    // How fast the natural clearing front moves along the correct path, in m/s.
    [SerializeField] float naturalClearSpeed = 2.5f;

    const float BirdMargin = 1f;
    const float BodyHeight = 0.5f;
    const float BodyRadius = 0.25f;
    const float ThinDensity = 0.85f;
    const float OutlineStrength = 0.35f;
    // How close to the correct path the baby must be to join it at natural resolution.
    const float RejoinDistance = 1f;
    const float GroundProbeSpacing = 0.25f;

    // Pending trail points the baby has not reached yet.
    readonly List<Vector3> trail = new();
    // Trail points the baby has walked, so it can retrace them at natural resolution instead of cutting across water.
    readonly List<Vector3> walked = new();
    readonly List<Vector3> retreat = new();
    // The correct path points and then the exit, with their distances along that polyline.
    readonly List<Vector3> path = new();
    readonly List<float> pathDistance = new();

    int groundMask;
    bool waiting;
    bool trailStarted;
    Vector3 trailEnd;
    // The trail point already checked for obstacles.
    Vector3 checkedPoint = Vector3.positiveInfinity;
    bool natural;
    float front;
    int pathIndex;
    int signalLevel;

    public override NaturalTimer Timer => timer;
    // The bird's help here is the trail the baby follows, not something to look up at (design doc §4.2).
    public override bool LookUpWhenOpenedByBird => false;

    void Awake()
    {
        groundMask = LayerMask.GetMask("Ground");
        foreach (Transform point in correctPath)
            AddPathPoint(point.position);
        AddPathPoint(exit.position);
        // Resolved(true) only comes from reaching the exit below, or from a dev tool shortcut.
        timer.Resolved += byBird =>
        {
            if (!byBird)
                StartNaturalResolution();
        };
    }

    void AddPathPoint(Vector3 point)
    {
        pathDistance.Add(path.Count == 0 ? 0f : pathDistance[^1] + Vector3.Distance(path[^1], point));
        path.Add(point);
    }

    public override void BeginWaiting()
    {
        // Where the baby stands at the fog edge, so retracing at natural resolution always ends next to the correct path.
        walked.Add(Baby.Instance.transform.position);
        waiting = true;
        timer.Begin();
    }

    void Update()
    {
        Vector3 bird = BirdController.Instance.transform.position;
        if (field.Contains(bird, BirdMargin))
            field.Clear(bird, clearRadius);

        if (!waiting || IsOpen)
            return;

        UpdateSignals();
        Baby baby = Baby.Instance;
        if (natural)
        {
            MoveFront();
            FollowCorrectPath(baby);
        }
        else
        {
            RecordTrail(bird, baby.transform.position);
            FollowTrail(baby);
        }

        if (FlatDistance(baby.transform.position, exit.position) <= exitRadius)
        {
            trail.Clear();
            timer.ResolveByBird();
            Open(!natural);
        }
    }

    // Fog edge thins -> hidden animals sound -> the path outline shows (design doc §3.2).
    void UpdateSignals()
    {
        while (signalLevel < timer.SignalLevel)
        {
            signalLevel++;
            switch (signalLevel)
            {
                case 1:
                    field.Density = ThinDensity;
                    break;
                case 2:
                    foreach (FogAnimal animal in GetComponentsInChildren<FogAnimal>())
                        animal.Answer();
                    break;
                case 3:
                    for (float distance = 0f; distance <= pathDistance[^1]; distance += pointSpacing)
                        field.Clear(PointOnPath(distance), clearRadius, OutlineStrength);
                    break;
            }
        }
    }

    #region Trail (design doc §9.2)

    void RecordTrail(Vector3 bird, Vector3 baby)
    {
        // Only once the baby has run out of trail: a bird hovering just ahead of a walking baby must not cut the trail.
        if (trail.Count == 0 && FlatDistance(bird, baby) <= reconnectRadius)
        {
            trailStarted = true;
            trailEnd = baby;
            // A bird hovering close by, nearer the exit, still leads the baby the last step to it (for example out of the fog
            // when the bird cannot fly further). Never backwards, which could walk the baby back into the previous camera zone.
            if (FlatDistance(bird, baby) >= pointSpacing && FlatDistance(bird, exit.position) < FlatDistance(baby, exit.position))
                AddTrailPoint(bird, baby);
            return;
        }
        if (!trailStarted || FlatDistance(bird, trailEnd) < pointSpacing)
            return;
        AddTrailPoint(bird, baby);
    }

    void AddTrailPoint(Vector3 bird, Vector3 baby)
    {
        float y = Physics.Raycast(bird, Vector3.down, out RaycastHit hit, Mathf.Infinity, groundMask, QueryTriggerInteraction.Ignore)
            ? hit.point.y
            : baby.y;
        Vector3 point = new(bird.x, y, bird.z);
        RemoveLoop(baby, point);
        trail.Add(point);
        trailEnd = point;
    }

    // The pending polyline is the baby, then the trail. If the new segment from trailEnd crosses an older segment of it,
    // the loop between them is cut and the two are joined at the crossing, so the trail stays on where the bird flew.
    // The last segment ends at trailEnd, so it only touches the new one.
    void RemoveLoop(Vector3 baby, Vector3 point)
    {
        for (int i = 0; i < trail.Count - 1; i++)
        {
            Vector3 start = i == 0 ? baby : trail[i - 1];
            if (SegmentsCross(start, trail[i], trailEnd, point, out float t))
            {
                Vector3 crossing = Vector3.Lerp(start, trail[i], t);
                trail.RemoveRange(i, trail.Count - i);
                trail.Add(crossing);
                return;
            }
        }
    }

    void FollowTrail(Baby baby)
    {
        if (trail.Count == 0)
        {
            baby.StandStill();
            return;
        }

        Vector3 target = trail[0];
        if (target != checkedPoint)
        {
            if (!Walkable(baby.transform.position, target))
            {
                // The trail beyond an obstacle is void; the bird has to come back to the baby and draw again.
                trail.Clear();
                trailStarted = false;
                baby.StandStill();
                return;
            }
            checkedPoint = target;
        }
        if (baby.StepToward(target))
        {
            walked.Add(target);
            trail.RemoveAt(0);
        }
    }

    // Ground all along the way (water and cliffs have none) and no hidden obstacle on it.
    bool Walkable(Vector3 from, Vector3 to)
    {
        Vector3 move = to - from;
        int steps = Mathf.CeilToInt(move.magnitude / GroundProbeSpacing);
        for (int i = 1; i <= steps; i++)
        {
            if (!Physics.Raycast(from + move * ((float)i / steps) + Vector3.up, Vector3.down, 2f, groundMask, QueryTriggerInteraction.Ignore))
                return false;
        }
        RaycastHit[] hits = Physics.SphereCastAll(from + Vector3.up * BodyHeight, BodyRadius, move.normalized, move.magnitude,
            Physics.AllLayers, QueryTriggerInteraction.Collide);
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.GetComponentInParent<HiddenObstacle>() != null)
                return false;
        }
        return true;
    }

    #endregion

    #region Natural resolution (Q10)

    // The baby drops the trail, retraces its own steps until it is back near the correct path,
    // and then walks the correct path from the nearest point, never ahead of the clearing front.
    void StartNaturalResolution()
    {
        natural = true;
        trail.Clear();
        Vector3 rejoin = Baby.Instance.transform.position;
        for (int i = walked.Count - 1; i >= 0 && DistanceToPath(rejoin) > RejoinDistance; i--)
        {
            rejoin = walked[i];
            retreat.Add(rejoin);
        }
        // Head for the end of the nearest segment, so the baby never steps back along the path.
        pathIndex = NearestSegmentEnd(rejoin, out _);
    }

    float DistanceToPath(Vector3 point)
    {
        NearestSegmentEnd(point, out float distance);
        return distance;
    }

    int NearestSegmentEnd(Vector3 point, out float distance)
    {
        int nearestEnd = 1;
        distance = float.MaxValue;
        Vector2 p = new(point.x, point.z);
        for (int i = 1; i < path.Count; i++)
        {
            Vector2 a = new(path[i - 1].x, path[i - 1].z);
            Vector2 ab = new Vector2(path[i].x, path[i].z) - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            float d = Vector2.Distance(p, a + ab * t);
            if (d < distance)
            {
                distance = d;
                nearestEnd = i;
            }
        }
        return nearestEnd;
    }

    void MoveFront()
    {
        if (front >= pathDistance[^1])
            return;
        float from = front;
        front = Mathf.Min(front + naturalClearSpeed * Time.deltaTime, pathDistance[^1]);
        // In steps smaller than the radius, so a long frame leaves no fog between the circles.
        for (float distance = from + clearRadius * 0.5f; distance < front; distance += clearRadius * 0.5f)
            field.Clear(PointOnPath(distance), clearRadius);
        field.Clear(PointOnPath(front), clearRadius);
    }

    void FollowCorrectPath(Baby baby)
    {
        if (retreat.Count > 0)
        {
            if (baby.StepToward(retreat[0]))
                retreat.RemoveAt(0);
        }
        else if (pathIndex >= path.Count || pathDistance[pathIndex] > front)
            baby.StandStill();
        else if (baby.StepToward(path[pathIndex]))
            pathIndex++;
    }

    Vector3 PointOnPath(float distance)
    {
        for (int i = 1; i < path.Count; i++)
        {
            if (distance <= pathDistance[i])
                return Vector3.Lerp(path[i - 1], path[i], Mathf.InverseLerp(pathDistance[i - 1], pathDistance[i], distance));
        }
        return path[^1];
    }

    #endregion

    static float FlatDistance(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

    // Proper crossing of two XZ segments; touching at an end point does not count. t is where it crosses along a -> b.
    static bool SegmentsCross(Vector3 a, Vector3 b, Vector3 c, Vector3 d, out float t)
    {
        float d1 = Cross(c, d, a);
        float d2 = Cross(c, d, b);
        float d3 = Cross(a, b, c);
        float d4 = Cross(a, b, d);
        t = d1 / (d1 - d2);
        return d1 * d2 < 0f && d3 * d4 < 0f;
    }

    static float Cross(Vector3 origin, Vector3 a, Vector3 b) =>
        (a.x - origin.x) * (b.z - origin.z) - (a.z - origin.z) * (b.x - origin.x);

    void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Vector3 previous = correctPath.childCount > 0 ? correctPath.GetChild(0).position : exit.position;
        foreach (Transform point in correctPath)
        {
            Gizmos.DrawSphere(point.position, 0.12f);
            Gizmos.DrawLine(previous, point.position);
            previous = point.position;
        }
        Gizmos.DrawLine(previous, exit.position);

        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(exit.position, exitRadius);

        if (!Application.isPlaying || trail.Count == 0)
            return;
        Gizmos.color = Color.cyan;
        previous = Baby.Instance.transform.position;
        foreach (Vector3 point in trail)
        {
            Gizmos.DrawSphere(point, 0.08f);
            Gizmos.DrawLine(previous, point);
            previous = point;
        }
    }
}
