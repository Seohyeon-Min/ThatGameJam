using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// The very end, once the sleep shot's camera has come to rest (EndingFlight): white birds come sweeping past the camera from the
// bottom left up to the top right, a few, then a rush of them, near and far, until they fill the screen; all white, so where they
// overlap no edge shows, and the screen goes white under them. On the white, the logo video plays in black, in the middle.
// The birds are WhiteFlockBird prefabs under Birds, off until their turn; the white and the logo are on the End Card canvas.
public class EndingFlock : MonoBehaviour
{
    [Tooltip("Their children are the birds (WhiteFlockBird), each off until it sets off.")]
    [SerializeField] Transform birds;
    [Tooltip("Full-screen white over everything, clear until the birds cover the screen.")]
    [SerializeField] Graphic white;
    [Tooltip("The logo video (black, in the middle), turned on once the screen is white; it plays as it turns on.")]
    [SerializeField] GameObject logo;

    [Header("Birds")]
    [Tooltip("Seconds over which the birds set off, one after another; more and more of them as it goes (Rush: 1 evenly, higher crowds them to the end).")]
    [SerializeField, Min(0.1f)] float setOffSeconds = 2.4f;
    [SerializeField, Min(1f)] float rush = 1.8f;
    [Tooltip("Seconds a bird takes to cross the screen, from-to (each picks one).")]
    [SerializeField] Vector2 crossSeconds = new Vector2(0.9f, 1.5f);
    [Tooltip("Metres in front of the camera it flies at, from-to; the near ones are big and cover the most. Then metres further it draws away as it crosses.")]
    [SerializeField] Vector2 depth = new Vector2(1.6f, 14f);
    [SerializeField, Min(0f)] float drawAway = 3f;
    [Tooltip("Degrees above level across the screen they fly (bottom left to top right), and degrees each may stray from it.")]
    [SerializeField, Range(0f, 80f)] float angle = 35f;
    [SerializeField, Range(0f, 40f)] float angleSpread = 12f;
    [Tooltip("Size from-to, and wingbeat speed from-to.")]
    [SerializeField] Vector2 size = new Vector2(0.8f, 1.3f);
    [SerializeField] Vector2 beatSpeed = new Vector2(0.9f, 1.4f);

    [Header("White and logo")]
    [Tooltip("Seconds after the first bird that the screen starts going white, and seconds it takes to be all white.")]
    [SerializeField, Min(0f)] float whiteFrom = 1.5f;
    [SerializeField, Min(0.05f)] float whiteSeconds = 1.2f;
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

    IEnumerator Run(float delay)
    {
        yield return new WaitForSeconds(delay);
        int count = birds != null ? birds.childCount : 0;
        for (int i = 0; i < count; i++)
        {
            // Crowding towards the end: few at first, then a rush.
            float at = setOffSeconds * Mathf.Pow((i + Random.value * 0.8f) / count, 1f / rush);
            StartCoroutine(Fly(birds.GetChild(i), at));
        }
        float start = Time.time;
        while (Time.time - start < whiteFrom + whiteSeconds)
        {
            SetWhite(Mathf.SmoothStep(0f, 1f, (Time.time - start - whiteFrom) / whiteSeconds));
            yield return null;
        }
        SetWhite(1f);
        yield return new WaitForSeconds(logoAfter);
        if (logo != null)
            logo.SetActive(true);
    }

    void SetWhite(float alpha)
    {
        if (white == null)
            return;
        Color colour = white.color;
        colour.a = alpha;
        white.color = colour;
    }

    // One bird, across the screen in front of the camera, along a slanted line, drawing away a little; on only while it crosses.
    IEnumerator Fly(Transform bird, float after)
    {
        yield return new WaitForSeconds(after);
        Camera view = Camera.main;
        if (view == null)
            yield break;
        // More of them near the camera, where each covers the most.
        float near = Mathf.Lerp(depth.x, depth.y, Mathf.Pow(Random.value, 1.6f));
        float slant = (angle + Random.Range(-angleSpread, angleSpread)) * Mathf.Deg2Rad;
        Vector2 way = new Vector2(Mathf.Cos(slant), Mathf.Sin(slant));
        Vector2 across = new Vector2(-way.y, way.x);
        // Where it crosses, as a share of the screen from its middle, across the way it flies.
        float side = Random.Range(-1.1f, 1.1f);
        float seconds = Random.Range(crossSeconds.x, crossSeconds.y);
        bird.localScale = Vector3.one * Random.Range(size.x, size.y);
        Animator animator = bird.GetComponent<Animator>();
        bird.gameObject.SetActive(true);
        if (animator != null)
        {
            animator.speed = Random.Range(beatSpeed.x, beatSpeed.y);
            animator.Play(0, 0, Random.value);
        }
        Vector3 before = Point(view, near, across * side - way * 1.6f);
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            float k = t / seconds;
            Vector3 at = Point(view, near + drawAway * k, across * side + way * Mathf.Lerp(-1.6f, 1.6f, k));
            Vector3 moved = at - before;
            if (moved.sqrMagnitude > 1e-8f)
                // The model's head is to its -Z.
                bird.SetPositionAndRotation(at, Quaternion.LookRotation(-moved, view.transform.up));
            else
                bird.position = at;
            before = at;
            yield return null;
        }
        bird.gameObject.SetActive(false);
    }

    // A point this far in front of the camera, at a place on the screen given from its middle (1 = the edge, up and across).
    static Vector3 Point(Camera view, float distance, Vector2 screen)
    {
        float up = distance * Mathf.Tan(view.fieldOfView * 0.5f * Mathf.Deg2Rad);
        Transform t = view.transform;
        return t.position + t.forward * distance + t.right * (screen.x * up * view.aspect) + t.up * (screen.y * up);
    }
}
