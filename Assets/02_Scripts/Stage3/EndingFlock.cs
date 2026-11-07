using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// The very end, once the sleep shot's camera has come to rest (EndingFlight): a flock of white birds sweeps over the screen from
// the bottom left to the top right as one mass, fast, a wipe made of birds. Its front row is spaced out enough to see they are
// birds; the rows behind come closer and closer, packed up to it, and behind them a white sheet (Tail) carries the white on over
// the rest of the screen. All the same white, so where they overlap no edge shows. Once it has covered the screen, it is white
// (End Card's White), and the logo video plays on it in black, in the middle.
// The birds are WhiteFlockBird prefabs under Birds and the sheet is Tail, off until it starts; the white and the logo are on the
// End Card canvas.
public class EndingFlock : MonoBehaviour
{
    [Tooltip("Their children are the birds (WhiteFlockBird), off until it starts.")]
    [SerializeField] Transform birds;
    [Tooltip("A white sheet (the birds' material) that follows close behind the flock and covers what is behind it.")]
    [SerializeField] Transform tail;
    [Tooltip("Full-screen white over everything, clear until the birds cover the screen.")]
    [SerializeField] Graphic white;
    [Tooltip("The logo video (black, in the middle), turned on once the screen is white; it plays as it turns on.")]
    [SerializeField] GameObject logo;

    [Header("Birds")]
    [Tooltip("Rows of the flock, one behind another, from its front back (the birds are shared out over them, side by side in each). Then the gap behind the front row, as a share of the screen's height; each gap after is this much of the one before (closer and closer), down to the least.")]
    [SerializeField, Min(1)] int rows = 10;
    [SerializeField, Min(0.01f)] float rowGap = 0.35f;
    [SerializeField, Range(0.3f, 1f)] float closer = 0.72f;
    [SerializeField, Min(0.01f)] float leastRowGap = 0.05f;
    [Tooltip("The row (counted from the front, 0) the white sheet starts behind.")]
    [SerializeField, Min(0)] int tailFromRow = 4;
    [Tooltip("How far each bird may sit off its place in the flock, as a share of its gaps, so it doesn't read as a grid.")]
    [SerializeField, Range(0f, 1f)] float scatter = 0.45f;
    [Tooltip("Seconds the flock's front takes from off the bottom left until the sheet behind it covers the screen.")]
    [SerializeField, Min(0.1f)] float sweepSeconds = 1.2f;
    [Tooltip("Metres in front of the camera the flock flies at, from-to (each bird picks one). Then metres further it draws away as it crosses.")]
    [SerializeField] Vector2 depth = new Vector2(5f, 6.5f);
    [SerializeField, Min(0f)] float drawAway = 1f;
    [Tooltip("Degrees above level it flies across the screen (bottom left to top right).")]
    [SerializeField, Range(0f, 80f)] float angle = 35f;
    [Tooltip("Size from-to, and wingbeat speed from-to.")]
    [SerializeField] Vector2 size = new Vector2(0.5f, 0.6f);
    [SerializeField] Vector2 beatSpeed = new Vector2(1f, 1.15f);
    [Tooltip("The state in the birds' controller (WhiteFlock) they fly in, looping.")]
    [SerializeField] string flyState = "Fly";

    [Header("White and logo")]
    [Tooltip("Seconds the screen takes to go all white once the sheet covers it.")]
    [SerializeField, Min(0.01f)] float whiteSeconds = 0.2f;
    [Tooltip("Seconds all white before the logo.")]
    [SerializeField, Min(0f)] float logoAfter = 0.8f;

    public bool IsPlaying { get; private set; }

    // Starts the flock after this many seconds.
    public void Play(float delay)
    {
        if (IsPlaying)
            return;
        IsPlaying = true;
        StartCoroutine(Run(delay));
    }

    // A bird's place in the flock: behind its front and across it, in screen heights; how far off; its wobble.
    struct Place
    {
        public Transform bird;
        public float back, side, near, wobble;
        public Vector3 before;
    }

    IEnumerator Run(float delay)
    {
        yield return new WaitForSeconds(delay);
        Camera view = Camera.main;
        int count = birds != null ? birds.childCount : 0;
        if (view == null || count == 0)
        {
            SetWhite(1f);
            yield break;
        }

        float slant = angle * Mathf.Deg2Rad;
        Vector2 way = new Vector2(Mathf.Cos(slant), Mathf.Sin(slant));
        Vector2 across = new Vector2(-way.y, way.x);
        // The screen, in screen heights from its middle, measured along the way and across it.
        float aspect = view.aspect;
        float halfAlong = aspect * Mathf.Abs(way.x) + Mathf.Abs(way.y);
        float halfAcross = aspect * Mathf.Abs(across.x) + Mathf.Abs(across.y);
        int perRow = Mathf.CeilToInt(count / (float)rows);
        // Side by side across the whole screen and a little past it.
        float width = 2f * halfAcross + 0.4f;
        float sideGap = width / Mathf.Max(1, perRow - 1);
        // How far behind the front each row is: the gaps closing up.
        var rowBack = new float[rows + 1];
        for (int r = 1; r <= rows; r++)
            rowBack[r] = rowBack[r - 1] + Mathf.Max(leastRowGap, rowGap * Mathf.Pow(closer, r - 1));
        float tailBack = rowBack[Mathf.Min(tailFromRow, rows)];

        var places = new Place[count];
        for (int i = 0; i < count; i++)
        {
            int row = i / perRow, column = i % perRow;
            // Every other row half a gap over, as bricks are laid, so there is no straight gap through.
            float side = -width * 0.5f + (column + (row % 2) * 0.5f) * sideGap;
            float gap = rowBack[row + 1] - rowBack[row];
            places[i] = new Place
            {
                bird = birds.GetChild(i),
                back = rowBack[row] + Random.Range(-scatter, scatter) * gap * 0.5f,
                side = side + Random.Range(-scatter, scatter) * sideGap * 0.5f,
                near = Random.Range(depth.x, depth.y),
                wobble = Random.value * 10f,
            };
            Transform bird = places[i].bird;
            bird.localScale = Vector3.one * Random.Range(size.x, size.y);
            bird.gameObject.SetActive(true);
            Animator animator = bird.GetComponent<Animator>();
            if (animator != null)
            {
                animator.speed = Random.Range(beatSpeed.x, beatSpeed.y);
                animator.Play(flyState, 0, Random.value);
            }
        }
        float tailNear = depth.y + 0.5f;
        if (tail != null)
            tail.gameObject.SetActive(true);

        // The front comes from just off the screen at the bottom left, and goes on until the sheet behind covers all of it.
        float from = -halfAlong - 0.4f, to = halfAlong + tailBack + 0.2f;
        float whiteAt = -1f;
        for (float t = 0f; ; t += Time.deltaTime)
        {
            float k = t / sweepSeconds;
            float front = Mathf.LerpUnclamped(from, to, k);
            for (int i = 0; i < count; i++)
                places[i].before = Move(view, places[i], front, k, t, way, across);
            if (tail != null)
                PlaceTail(view, tailNear + drawAway * Mathf.Clamp01(k), front - tailBack, way, across, halfAcross);
            if (whiteAt < 0f && k >= 1f)
                whiteAt = t;
            if (whiteAt >= 0f)
            {
                float w = (t - whiteAt) / whiteSeconds;
                SetWhite(Mathf.SmoothStep(0f, 1f, w));
                if (w >= 1f)
                    break;
            }
            yield return null;
        }
        foreach (Place place in places)
            place.bird.gameObject.SetActive(false);
        if (tail != null)
            tail.gameObject.SetActive(false);
        yield return new WaitForSeconds(logoAfter);
        if (logo != null)
            logo.SetActive(true);
    }

    // One bird at its place in the flock, its front this far along, turned the way it has moved; where it is now.
    Vector3 Move(Camera view, Place place, float front, float k, float t, Vector2 way, Vector2 across)
    {
        float bob = Mathf.Sin(t * 3f + place.wobble) * 0.04f;
        Vector2 screen = way * (front - place.back) + across * (place.side + bob);
        Vector3 at = Point(view, place.near + drawAway * Mathf.Clamp01(k), screen);
        Vector3 moved = at - place.before;
        if (t > 0f && moved.sqrMagnitude > 1e-8f)
            // The model's head is to its -Z.
            place.bird.SetPositionAndRotation(at, Quaternion.LookRotation(-moved, view.transform.up));
        else
            place.bird.position = at;
        return at;
    }

    // The sheet (a quad, facing the camera) this far off, its front edge this far along the way, reaching far back behind that
    // and across the whole screen.
    void PlaceTail(Camera view, float distance, float frontEdge, Vector2 way, Vector2 across, float halfAcross)
    {
        const float Length = 12f;
        float unit = distance * Mathf.Tan(view.fieldOfView * 0.5f * Mathf.Deg2Rad);
        Transform t = view.transform;
        Vector3 acrossWorld = t.right * across.x + t.up * across.y;
        tail.SetPositionAndRotation(Point(view, distance, way * (frontEdge - Length * 0.5f)), Quaternion.LookRotation(t.forward, acrossWorld));
        tail.localScale = new Vector3(Length * unit, (2f * halfAcross + 1f) * unit, 1f);
    }

    void SetWhite(float alpha)
    {
        if (white == null)
            return;
        Color colour = white.color;
        colour.a = alpha;
        white.color = colour;
    }

    // A point this far in front of the camera, at a place on the screen given from its middle in screen heights (x across, y up).
    static Vector3 Point(Camera view, float distance, Vector2 screen)
    {
        float up = distance * Mathf.Tan(view.fieldOfView * 0.5f * Mathf.Deg2Rad);
        Transform t = view.transform;
        return t.position + t.forward * distance + t.right * (screen.x * up) + t.up * (screen.y * up);
    }
}
