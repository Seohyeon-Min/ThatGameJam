using System.Collections;
using UnityEngine;

// The end of the fog stage (3-3, rough first version by the director's order of 2026-11-04). Past the maze there is no sunlit
// spot: the girl walks on through the thinning forest to the big stone before the cliff and rests there. She no longer comes
// to the bird's cry. When the bird has flown out over the cliff nearly to the edge of the screen, the ending plays.
// It never opens, so the girl never lies down here and the season stays.
public class CliffEnding : PathBlocker
{
    // Children in order, from the end of the maze to where she rests by the stone.
    [SerializeField] Transform path;
    // She faces this while she rests: out over the cliff.
    [SerializeField] Transform view;
    // The cliff's edge; its forward points out over the drop.
    [SerializeField] Transform edge;
    // The ending plays when the bird, past the edge, is this close to a side of the screen (a share of the screen).
    // BirdController keeps the bird screenMargin (0.08) inside, so keep this above that.
    [SerializeField, Range(0f, 0.5f)] float screenEdge = 0.1f;

    // Set while she is here: walking to the stone, then resting by it.
    Baby baby;
    Coroutine walking;
    bool resting;
    bool ended;

    public override NaturalTimer Timer => null;

    public override void BeginWaiting()
    {
        baby = Baby.Instance;
        // Again after a dev tool took her back a stage, maybe after the ending.
        if (walking != null)
            StopCoroutine(walking);
        resting = false;
        ended = false;
        walking = StartCoroutine(WalkToStone());
    }

    IEnumerator WalkToStone()
    {
        foreach (Transform point in path)
            while (!baby.StepToward(point.position))
                yield return null;
        walking = null;
        resting = true;
    }

    void Update()
    {
        if (baby == null || ended)
            return;
        // A dev tool moved her elsewhere (next stage, back, lie down).
        if (baby.CurrentBlocker != this)
        {
            if (walking != null)
                StopCoroutine(walking);
            walking = null;
            resting = false;
            baby = null;
            return;
        }
        if (!resting)
            return;
        baby.SitFacing(view.position);
        if (BirdNearScreenEdgePastCliff())
        {
            ended = true;
            baby.PlayEnding();
        }
    }

    bool BirdNearScreenEdgePastCliff()
    {
        Vector3 bird = BirdController.Instance.transform.position;
        if (Vector3.Dot(bird - edge.position, edge.forward) <= 0f)
            return false;
        Vector3 viewport = Camera.main.WorldToViewportPoint(bird);
        // Off the screen only when a dev tool holds the camera elsewhere.
        if (viewport.z <= 0f || viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f)
            return false;
        return viewport.x <= screenEdge || viewport.x >= 1f - screenEdge || viewport.y <= screenEdge || viewport.y >= 1f - screenEdge;
    }

    void OnDrawGizmos()
    {
        if (path == null || edge == null)
            return;
        Gizmos.color = Color.green;
        Vector3? previous = null;
        foreach (Transform point in path)
        {
            Gizmos.DrawSphere(point.position, 0.12f);
            if (previous is Vector3 from)
                Gizmos.DrawLine(from, point.position);
            previous = point.position;
        }
        Gizmos.color = Color.red;
        Vector3 across = Vector3.Cross(Vector3.up, edge.forward) * 30f;
        Gizmos.DrawLine(edge.position - across, edge.position + across);
    }
}
