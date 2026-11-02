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
    // Plays after the baby wakes in this stage, before it walks. Empty: it just walks.
    public Cutscene wakeCutscene;
    // Holding the click on the walking baby makes the bird fly over her and keep the snow off (SnowShelter, the intro only).
    public bool snowShelter;
    // World-space area of the stage's road and the terrain beside it, in X and Z. The bird flies over this and the earlier stages' areas
    // (ADR 0010, ADR 0013), ahead as far as this one's far end: make it reach a little past the sunlit spot, where the bird waits.
    public Bounds area = new(Vector3.zero, new Vector3(6f, 1f, 30f));

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
    // Where this stage begins along the road: where its baby starts, which is where the stage before it ended.
    public float JoinZ => babyStart != null ? babyStart.position.z : area.min.z;

    // Opens a gap of deltaZ right before this stage (a negative deltaZ closes one), as one undo step (StageEditor's inspector button).
    // This stage's whole content (everything parented under it) and its flight area move along +Z, and the world opens with it at the
    // join (WorldGap): the terrain grows there, and the streams, trees and flowers past the join move with the ground.
    // cascadeToLaterStages also moves every stage after this one (by GameFlow's authored order, not Transform order), so each keeps
    // its own gaps. Without it the later stages stay, and the world closes the gap again where the next stage begins.
    public void AdjustSpacing(float deltaZ, bool cascadeToLaterStages)
    {
        if (Mathf.Approximately(deltaZ, 0f))
            return;
        int group = Undo.GetCurrentGroup();
        GameFlow flow = FindAnyObjectByType<GameFlow>();
        var later = new System.Collections.Generic.List<Stage>();
        if (flow != null)
        {
            bool past = false;
            foreach (Stage stage in flow.Stages)
            {
                if (past)
                    later.Add(stage);
                past |= stage == this;
            }
        }
        else
            Debug.LogWarning("Stage.AdjustSpacing: no GameFlow in the scene to find later stages through; only this stage moved.", this);

        // The world first, at the joins as they are before anything moves. The far join first, so the near one doesn't move it.
        if (!cascadeToLaterStages && later.Count > 0)
            WorldGap.Open(later[0].JoinZ, -deltaZ);
        WorldGap.Open(JoinZ, deltaZ);

        Move(deltaZ);
        if (cascadeToLaterStages)
            foreach (Stage stage in later)
                stage.Move(deltaZ);
        Undo.SetCurrentGroupName("Adjust Stage Spacing");
        Undo.CollapseUndoOperations(group);
    }

    void Move(float deltaZ)
    {
        Undo.RecordObject(transform, "Adjust Stage Spacing");
        transform.position += new Vector3(0f, 0f, deltaZ);
        Undo.RecordObject(this, "Adjust Stage Spacing");
        Bounds shifted = area;
        shifted.center += new Vector3(0f, 0f, deltaZ);
        area = shifted;
        EditorUtility.SetDirty(this);
    }
#endif
}
