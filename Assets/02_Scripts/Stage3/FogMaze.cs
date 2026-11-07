using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// The fog stage (ADR 0028). The girl stops at the start of the long straight way into the fog, and this leads her from there
// through the forks. Each fork has an animal on its right way. When the bird cries, the first animal the bird has not passed
// (in Z) answers, and the girl runs along the ways to the point of the maze nearest the bird, behind her too (ADR 0033). There
// she waits a moment for another cry, then walks on by herself. At a fork she takes the right way if its animal has answered or
// she has been to its dead end's wall; otherwise she stands a moment, then picks one of the two at random. A dead end takes her to
// its wall and back, unless its animal has answered or she has been to its wall: then she turns back at once.
// The fog is light, so the animals and the ways show through it.
public class FogMaze : PathBlocker
{
    [SerializeField] FogField field;
    // Children in order, from where the girl stops at the start of the straight way to the first fork.
    [SerializeField] Transform approach;
    [SerializeField] FogFork[] forks;
    // The fog lies thinner over the ways, so both ways of a fork show through it: this wide, down to this share of the rest
    // (a quarter of 0.9 is about the light fog of ADR 0027).
    [SerializeField, Min(0.1f)] float wayFogRadius = 2.8f;
    [SerializeField, Range(0f, 1f)] float wayFogShare = 0.25f;
    // The bird has no horizontal avoidance for the trees lining the maze (only vertical clearance over logs/banks underfoot),
    // so BirdController keeps it within this far either side of the ways instead, flat, while this is the current stage and it is
    // not yet past the maze's exit.
    [Tooltip("How far either side of the path (ways) the bird may fly, flat, so it can't cross the trees beyond it.")]
    [SerializeField, Min(0.1f)] float birdCorridorWidth = 2.5f;

    // No natural resolution: without help she still gets through, by chance.
    public override NaturalTimer Timer => null;
    // The help is the run to the bird, not something to look up at.
    public override bool LookUpWhenOpenedByBird => false;

    // Only one in the game: BirdController checks this to tell whether the maze's corridor applies.
    public static FogMaze Instance { get; private set; }
    public Stage Stage => stage;

    // The ways, as points along the ground, each from the fork it leaves (the straight way from its start). Way 0 is the straight
    // way, way i (1 to n) the right way of fork i - 1, and way n + 1 + i the dead end of fork i. The straight way and each right way end at the next
    // fork, so way i (0 to n - 1) ends at fork i.
    Vector3[][] ways;
    float[][] distances;
    bool[] wallReached;
    Stage stage;
    Baby baby;
    Coroutine routine;
    int way;
    bool helped;
    // A cry after she woke but before she came to the start of the straight way: she runs to it when she gets there.
    Vector3? call;

    int ForkCount => forks.Length;
    bool Leading => routine != null;
    static int RightWay(int fork) => fork + 1;
    int DeadEnd(int fork) => ForkCount + 1 + fork;
    bool IsDeadEnd(int index) => index > ForkCount;
    int ForkOfDeadEnd(int index) => index - ForkCount - 1;
    float Length(int index) => distances[index][^1];

    void Awake()
    {
        Instance = this;
        stage = GetComponentInParent<Stage>();
        ways = new Vector3[2 * ForkCount + 1][];
        ways[0] = Points(approach, null);
        for (int i = 0; i < ForkCount; i++)
        {
            ways[RightWay(i)] = Points(forks[i].RightWay, forks[i].transform);
            ways[DeadEnd(i)] = Points(forks[i].DeadEnd, forks[i].transform);
        }
        distances = new float[ways.Length][];
        for (int i = 0; i < ways.Length; i++)
        {
            distances[i] = new float[ways[i].Length];
            for (int p = 1; p < ways[i].Length; p++)
                distances[i][p] = distances[i][p - 1] + Vector3.Distance(ways[i][p - 1], ways[i][p]);
        }
        wallReached = new bool[ForkCount];
    }

    // After FogField's Awake, which lays the fog this thins.
    void Start()
    {
        foreach (Vector3[] points in ways)
            for (int i = 1; i < points.Length; i++)
            {
                float length = Vector3.Distance(points[i - 1], points[i]);
                for (float along = 0f; along <= length; along += wayFogRadius * 0.25f)
                    field.Clear(Vector3.Lerp(points[i - 1], points[i], along / length), wayFogRadius, 1f - wayFogShare);
            }
    }

    static Vector3[] Points(Transform path, Transform from)
    {
        var points = new List<Vector3>();
        if (from != null)
            points.Add(from.position);
        foreach (Transform point in path)
            points.Add(point.position);
        return points.ToArray();
    }

    void OnEnable() => CryListener.Cried += OnCry;

    void OnDisable()
    {
        CryListener.Cried -= OnCry;
        if (Leading)
        {
            StopCoroutine(routine);
            routine = null;
        }
    }

    public override void BeginWaiting()
    {
        baby = Baby.Instance;
        way = 0;
        routine = StartCoroutine(call is Vector3 bird ? RunToBird(bird) : WalkOn());
        call = null;
    }

    void Update()
    {
        // A dev tool moved the baby elsewhere (next stage, back, lie down): stop leading her.
        if (Leading && baby.CurrentBlocker != this)
        {
            StopCoroutine(routine);
            routine = null;
        }
    }

    void OnCry(Vector3 bird)
    {
        // Past the maze nothing answers, and she no longer comes to the bird (CliffEnding).
        if (IsOpen)
            return;
        Bounds area = stage.area;
        if (bird.x < area.min.x || bird.x > area.max.x || bird.z < area.min.z || bird.z > area.max.z)
            return;
        AnswerAhead(bird);
        if (!Leading)
        {
            if (stage == GameFlow.Instance.CurrentStage && Baby.Instance.State is BabyState.Waking or BabyState.Walking)
                call = bird;
            return;
        }
        StopCoroutine(routine);
        routine = StartCoroutine(RunToBird(bird));
    }

    // The first animal the bird has not passed answers. Every way runs forward (+Z), so passing an animal is getting ahead of it in Z,
    // wherever the bird is: above a dead end too (ADR 0032).
    void AnswerAhead(Vector3 bird)
    {
        foreach (FogFork fork in forks)
        {
            if (fork.Animal.transform.position.z > bird.z)
            {
                fork.Animal.Answer();
                return;
            }
        }
    }

    // The way a way leaves from: a right way from the way before it, a dead end from the way ending at its fork. -1 for the straight way.
    int Parent(int index) => IsDeadEnd(index) ? ForkOfDeadEnd(index) : index - 1;

    IEnumerator RunToBird(Vector3 bird)
    {
        helped = true;
        // Anywhere in the maze, behind her too (ADR 0033).
        int target = Nearest(bird, Enumerable.Range(0, ways.Length), out float along);
        yield return Travel(target, along, true);
        for (float time = 0f; time < GameTuning.Current.fogRunIdleTime; time += Time.deltaTime)
        {
            baby.StandFacing(BirdController.Instance.transform.position);
            yield return null;
        }
        yield return WalkOn();
    }

    // On by herself from where she is, to the goal.
    IEnumerator WalkOn()
    {
        GameTuning tuning = GameTuning.Current;
        if (IsDeadEnd(way))
        {
            int fork = ForkOfDeadEnd(way);
            if (!forks[fork].Animal.HasAnswered && !wallReached[fork])
                yield return ToWall(fork);
            yield return Along(way, 0f, false);
            way = RightWay(fork);
        }
        while (way < ForkCount)
        {
            yield return Along(way, Length(way), false);
            int fork = way;
            baby.StandStill();
            // Where she has been to the dead end's wall already (called back past it), she knows the way.
            for (float time = 0f; time < tuning.fogForkWaitTime && !forks[fork].Animal.HasAnswered && !wallReached[fork]; time += Time.deltaTime)
                yield return null;
            helped |= forks[fork].Animal.HasAnswered;
            if (!forks[fork].Animal.HasAnswered && !wallReached[fork] && Random.value < 0.5f)
            {
                way = DeadEnd(fork);
                yield return ToWall(fork);
                yield return Along(way, 0f, false);
            }
            way = RightWay(fork);
        }
        yield return Along(way, Length(way), false);
        routine = null;
        Open(helped);
    }

    IEnumerator ToWall(int fork)
    {
        yield return Along(DeadEnd(fork), Length(DeadEnd(fork)), false);
        wallReached[fork] = true;
        baby.StandStill();
        yield return new WaitForSeconds(GameTuning.Current.fogWallPauseTime);
    }

    // To a point on any way, along the ways: back through the forks behind her as far as the way the target's ways leave from, then
    // out along those ways, each to its end, and along the target way to the point.
    IEnumerator Travel(int target, float along, bool run)
    {
        var behind = new List<int>();
        for (int index = way; index >= 0; index = Parent(index))
            behind.Add(index);
        var ahead = new List<int>();
        int common = target;
        while (!behind.Contains(common))
        {
            ahead.Add(common);
            common = Parent(common);
        }
        // Each way back to its start, which is on the way it leaves from.
        while (way != common)
        {
            yield return Along(way, 0f, run);
            way = Parent(way);
        }
        // Each way it passes through to its end, where the next leaves from.
        for (int i = ahead.Count - 1; i >= 0; i--)
        {
            yield return Along(way, Length(way), run);
            way = ahead[i];
        }
        yield return Along(way, along, run);
    }

    // Along the way she is on, through its corners, to the point this far along it.
    IEnumerator Along(int index, float along, bool run)
    {
        Vector3[] points = ways[index];
        float[] at = distances[index];
        float from = Project(index, baby.transform.position);
        if (along > from)
        {
            for (int i = 1; i < points.Length - 1; i++)
                if (at[i] > from && at[i] < along)
                    yield return Step(points[i], run);
        }
        else
        {
            for (int i = points.Length - 2; i >= 1; i--)
                if (at[i] < from && at[i] > along)
                    yield return Step(points[i], run);
        }
        yield return Step(PointAt(index, along), run);
    }

    IEnumerator Step(Vector3 target, bool run)
    {
        while (run ? !baby.RunToward(target) : !baby.StepToward(target))
            yield return null;
    }

    Vector3 PointAt(int index, float along)
    {
        Vector3[] points = ways[index];
        float[] at = distances[index];
        for (int i = 1; i < points.Length; i++)
            if (along <= at[i])
                return Vector3.Lerp(points[i - 1], points[i], Mathf.InverseLerp(at[i - 1], at[i], along));
        return points[^1];
    }

    // How far along the way its point nearest to this one is, seen from above.
    float Project(int index, Vector3 point)
    {
        DistanceTo(index, point, out float along);
        return along;
    }

    // The way among these with the point nearest to this one, seen from above, and how far along it that point is.
    int Nearest(Vector3 point, IEnumerable<int> among, out float along)
    {
        int best = -1;
        float bestDistance = float.MaxValue;
        along = 0f;
        foreach (int index in among)
        {
            float distance = DistanceTo(index, point, out float at);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = index;
                along = at;
            }
        }
        return best;
    }

    // Past the end of the last right way (the maze's way out), along +Z like the whole road: the open ground before the cliff.
    public bool IsPastExit(Vector3 point) => point.z >= ways[RightWay(ForkCount - 1)][^1].z;

    // The point, flat, moved in to stay within birdCorridorWidth of the nearest way if it was further out (not snapped onto
    // the way itself, only pulled back to the corridor's edge) - see birdCorridorWidth above.
    public Vector3 ClampToCorridor(Vector3 point)
    {
        int nearestWay = Nearest(point, Enumerable.Range(0, ways.Length), out float along);
        Vector3 nearest = PointAt(nearestWay, along);
        Vector2 flat = new(point.x, point.z), nearestFlat = new(nearest.x, nearest.z);
        float distance = Vector2.Distance(flat, nearestFlat);
        if (distance <= birdCorridorWidth)
            return point;
        Vector2 clamped = Vector2.Lerp(nearestFlat, flat, birdCorridorWidth / distance);
        return new Vector3(clamped.x, point.y, clamped.y);
    }

    // The flat distance from the point to the way, and how far along the way the nearest point is.
    float DistanceTo(int index, Vector3 point, out float along)
    {
        Vector3[] points = ways[index];
        Vector2 p = new(point.x, point.z);
        float best = float.MaxValue;
        along = 0f;
        for (int i = 1; i < points.Length; i++)
        {
            Vector2 a = new(points[i - 1].x, points[i - 1].z), b = new(points[i].x, points[i].z);
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            float distance = Vector2.Distance(p, a + ab * t);
            if (distance < best)
            {
                best = distance;
                along = distances[index][i - 1] + t * (distances[index][i] - distances[index][i - 1]);
            }
        }
        return best;
    }

    void OnDrawGizmos()
    {
        if (approach == null)
            return;
        Gizmos.color = Color.green;
        Transform previous = null;
        foreach (Transform point in approach)
        {
            Gizmos.DrawSphere(point.position, 0.12f);
            if (previous != null)
                Gizmos.DrawLine(previous.position, point.position);
            previous = point;
        }
    }
}
