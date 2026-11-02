using System.Collections;
using UnityEngine;

// The fog stage (ADR 0027). The girl stops at the fog edge and this leads her through the forks. At each fork she takes the
// way the rabbit showed. If the bird has not cried there, she stands a moment, then picks one of the two at random; a dead end
// takes her to its wall and back. The fog is light, so the rabbits and the paths show through it.
public class FogMaze : PathBlocker
{
    [SerializeField] FogField field;
    // Children in order, from the fog edge to the first fork.
    [SerializeField] Transform approach;
    [SerializeField] FogFork[] forks;
    // Not settled by design yet: the director tries both (ADR 0027).
    [SerializeField] bool birdClearsFog;
    [SerializeField] bool girlClearsFog = true;
    [SerializeField, Min(0.1f)] float birdClearRadius = 1.3f;
    [SerializeField, Min(0.1f)] float girlClearRadius = 1.5f;

    // No natural resolution: without help she still gets through, by chance.
    public override NaturalTimer Timer => null;
    // The help is the rabbit showing the way, not something to look up at.
    public override bool LookUpWhenOpenedByBird => false;

    Coroutine lead;

    public override void BeginWaiting() => lead = StartCoroutine(Lead(Baby.Instance));

    void Update()
    {
        // A dev tool moved the baby elsewhere (next stage, back, lie down): stop leading her.
        if (lead != null && Baby.Instance.CurrentBlocker != this)
        {
            StopCoroutine(lead);
            lead = null;
        }
        if (birdClearsFog)
            ClearAround(BirdController.Instance.transform.position, birdClearRadius);
        if (girlClearsFog)
            ClearAround(Baby.Instance.transform.position, girlClearRadius);
    }

    void ClearAround(Vector3 point, float radius)
    {
        if (field.Contains(point, radius))
            field.Clear(point, radius);
    }

    IEnumerator Lead(Baby baby)
    {
        GameTuning tuning = GameTuning.Current;
        bool helped = false;
        yield return Walk(baby, approach);
        foreach (FogFork fork in forks)
        {
            baby.StandStill();
            for (float time = 0f; time < tuning.fogForkWaitTime && !fork.Shown; time += Time.deltaTime)
                yield return null;
            helped |= fork.Shown;
            if (!fork.Shown && Random.value < 0.5f)
            {
                yield return Walk(baby, fork.DeadEnd);
                baby.StandStill();
                yield return new WaitForSeconds(tuning.fogWallPauseTime);
                for (int i = fork.DeadEnd.childCount - 2; i >= 0; i--)
                    yield return WalkTo(baby, fork.DeadEnd.GetChild(i).position);
                yield return WalkTo(baby, fork.transform.position);
            }
            yield return Walk(baby, fork.RightWay);
        }
        lead = null;
        Open(helped);
    }

    static IEnumerator Walk(Baby baby, Transform path)
    {
        foreach (Transform point in path)
            yield return WalkTo(baby, point.position);
    }

    static IEnumerator WalkTo(Baby baby, Vector3 target)
    {
        while (!baby.StepToward(target))
            yield return null;
    }

    void OnDrawGizmos()
    {
        if (approach == null)
            return;
        Gizmos.color = Color.green;
        Vector3 previous = transform.position;
        foreach (Transform point in approach)
        {
            Gizmos.DrawSphere(point.position, 0.12f);
            Gizmos.DrawLine(previous, point.position);
            previous = point.position;
        }
    }
}
