using UnityEngine;

// One stage's setup: where the baby and bird start, the baby's route, the bird's altitude, and the area the bird flies over (design doc §10, §11).
// The stages lie one after another along +Z on one long road; each starts where the last one's route ended (ADR 0010).
public class Stage : MonoBehaviour
{
    public Transform babyStart;
    public Transform birdStart;
    public BabyRoute route;
    public float birdAltitude = 3f;
    // World-space area of the stage's road and the terrain beside it, in X and Z. The bird flies over this and the earlier stages' areas
    // (ADR 0010, ADR 0013).
    public Bounds area = new(Vector3.zero, new Vector3(6f, 1f, 30f));

    // How far along +Z the bird may fly while this is the current stage: its sunlit spot, the last point of the route (ADR 0012).
    public float BirdForwardLimit => route[route.Count - 1].position.z;

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
