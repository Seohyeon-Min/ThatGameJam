using UnityEngine;
using UnityEngine.Events;

// One stage's setup: where the baby and bird start, the baby's route, the bird's altitude, the camera area,
// and the season effect played after the baby falls asleep in the rest area (design doc §10, §11).
public class Stage : MonoBehaviour
{
    public Transform babyStart;
    public Transform birdStart;
    public BabyRoute route;
    public float birdAltitude = 3f;
    // World-space area the camera's followed point stays in (ADR 0005).
    public Bounds area = new(Vector3.zero, new Vector3(6f, 1f, 30f));
    public UnityEvent seasonEffect;
    // Seconds to wait after the season effect starts before the transition.
    public float seasonEffectDuration = 2f;

    void OnDrawGizmos()
    {
        // Unset while a stage is being authored by hand.
        if (babyStart == null || birdStart == null)
            return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(babyStart.position, 0.4f);
        Gizmos.color = Color.yellow;
        Vector3 bird = birdStart.position;
        bird.y = birdAltitude;
        Gizmos.DrawWireSphere(bird, 0.3f);
        Gizmos.DrawLine(birdStart.position, bird);
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireCube(area.center, area.size);
    }
}
