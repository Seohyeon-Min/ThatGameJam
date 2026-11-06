using UnityEngine;

// Snow on the bird (easter_eggs.md): standing still on the ground or a branch while it snows, the bird slowly gathers a cap of snow.
// Flying off shakes it off. Lives under the bird's root, so it doesn't bank with the body.
public class SnowOnBird : MonoBehaviour
{
    [SerializeField] Discovery discovery;
    [Tooltip("The snow. Scaled from nothing to its authored scale as it piles up.")]
    [SerializeField] Transform snow;
    [Tooltip("The last season it snows in (SeasonSnow stops at 1, so 0: the intro).")]
    [SerializeField, Min(0)] int lastSnowySeason;
    [Tooltip("Seconds standing still before the first flake stays.")]
    [SerializeField, Min(0f)] float settleDelay = 1.5f;
    [SerializeField, Min(0.1f)] float pileSeconds = 8f;
    [SerializeField, Min(0.1f)] float shakeOffSeconds = 0.3f;

    Season season;
    Vector3 fullScale;
    Vector3 lastPosition;
    float stillTime;
    float amount;

    void Awake()
    {
        fullScale = snow.localScale;
        snow.localScale = Vector3.zero;
    }

    void Start() => season = FindAnyObjectByType<Season>();

    void Update()
    {
        BirdController bird = BirdController.Instance;
        bool still = bird.IsOnFoot && (bird.transform.position - lastPosition).sqrMagnitude < 1e-6f;
        lastPosition = bird.transform.position;
        stillTime = still ? stillTime + Time.deltaTime : 0f;
        bool snowing = season != null && season.Level <= lastSnowySeason;

        if (stillTime >= settleDelay && snowing)
        {
            bool wasFull = amount >= 1f;
            amount = Mathf.MoveTowards(amount, 1f, Time.deltaTime / pileSeconds);
            if (!wasFull && amount >= 1f)
                discovery.Reveal();
        }
        else if (!still)
            amount = Mathf.MoveTowards(amount, 0f, Time.deltaTime / shakeOffSeconds);
        snow.localScale = fullScale * amount;
    }
}
