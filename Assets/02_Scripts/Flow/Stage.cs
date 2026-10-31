using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

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

#if UNITY_EDITOR
    // Moves this stage's whole content (everything parented under it: route, start points, tree generators)
    // and its flight area along +Z by deltaZ, without touching the shared terrain or any other stage - the
    // road widens or narrows at this one join, and every other stage's own internal spacing is untouched
    // (StageEditor's inspector button). Positive deltaZ pushes the stage further from whatever comes before it.
    // cascadeToLaterStages also applies the same shift to every stage after this one (by GameFlow's authored
    // order, not Transform order), so a stage further down the road keeps its own existing gaps; without it,
    // only the gap right before this stage changes, same as moving a single stage on its own would.
    public void AdjustSpacing(float deltaZ, bool cascadeToLaterStages)
    {
        if (Mathf.Approximately(deltaZ, 0f))
            return;
        Undo.RecordObject(transform, "Adjust Stage Spacing");
        transform.position += new Vector3(0f, 0f, deltaZ);
        Undo.RecordObject(this, "Adjust Stage Spacing");
        Bounds shifted = area;
        shifted.center += new Vector3(0f, 0f, deltaZ);
        area = shifted;
        EditorUtility.SetDirty(this);

        if (!cascadeToLaterStages)
            return;
        GameFlow flow = FindAnyObjectByType<GameFlow>();
        if (flow == null)
        {
            Debug.LogWarning("Stage.AdjustSpacing: no GameFlow in the scene to find later stages through; only this stage moved.", this);
            return;
        }
        int index = -1;
        for (int i = 0; i < flow.Stages.Count; i++)
            if (flow.Stages[i] == this)
                index = i;
        for (int i = index + 1; i < flow.Stages.Count; i++)
            flow.Stages[i].AdjustSpacing(deltaZ, false);
    }
#endif
}
