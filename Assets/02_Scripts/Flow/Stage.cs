using UnityEngine;

// One stage's setup: where the baby and bird start, the baby's route, the bird's altitude, and the road area (design doc §10, §11).
// The stages lie one after another along +Z on one long road; each starts where the last one's route ended (ADR 0010).
public class Stage : MonoBehaviour
{
    public Transform babyStart;
    public Transform birdStart;
    public BabyRoute route;
    public float birdAltitude = 3f;
    // World-space area of the stage's road, in X and Z. The bird flies over this and the earlier stages' areas (ADR 0007, ADR 0010).
    public Bounds area = new(Vector3.zero, new Vector3(6f, 1f, 30f));

    // How far along +Z the bird may fly: the first blocker on the route that is still closed, else the end of the stage (ADR 0010).
    public float BirdForwardLimit
    {
        get
        {
            for (int i = 0; i < route.Count; i++)
            {
                if (route[i].TryGetComponent(out StopPoint stop) && !stop.Blocker.IsOpen && stop.Blocker.BirdLimit != null)
                    return stop.Blocker.BirdLimit.position.z;
            }
            return area.max.z;
        }
    }

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
