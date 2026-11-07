using System.Collections;
using UnityEngine;

// The end of the fog stage (3-3, rough first version by the director's order of 2026-11-04). Past the maze there is no sunlit
// spot: the girl walks on through the thinning forest to the stump before the cliff and sits there. She no longer comes to the
// bird's cry; a small bird on the stump (GuideBird) answers it instead, and once it has, she hurries toward it, running the first
// part of the way left and walking the rest. When she comes close it flies away, and she sits by the stump in its place.
// Until she sits, the way over the cliff is closed: the bird flies no further than the edge (GameFlow.FlyArea), refusing there
// like at any closed way (it lands on a tree nearby and shakes its head), and the camera's aim comes no further than a little
// before it. Once she sits, the way opens, the camera eases over to show more of the drop, a light breeze blows past her out
// over it, and when the bird has flown a little
// way out past the edge the ending plays. It never opens as a blocker, so the girl never lies down here and the season stays.
public class CliffEnding : PathBlocker
{
    // Children in order, from the end of the maze to where she sits by the stump.
    [SerializeField] Transform path;
    // She faces this while she sits: out over the cliff.
    [SerializeField] Transform view;
    // The cliff's edge; its forward points out over the drop, along +Z (the fly area's far end is a Z).
    [SerializeField] Transform edge;
    // The ending plays once the bird itself has flown this far out past the edge (metres along its forward), not as soon as it
    // crosses. The fly area ends 40 m past the edge and the ClickPlane below takes the clicks out there, so it can get this far.
    [SerializeField, Min(0f)] float pastEdge = 3f;

    [Header("Guide")]
    [Tooltip("The small bird on the stump. Empty: she just walks to the stump.")]
    [SerializeField] GuideBird guide;
    [Tooltip("Once the guide has answered, she runs this share of the way she has left, then walks.")]
    [SerializeField, Range(0f, 1f)] float runShare = 0.5f;
    [Tooltip("The guide sees her coming and flies away when she is this close to it, in metres (seen from above).")]
    [SerializeField, Min(0f)] float guideFliesAt = 6f;

    [Header("Camera")]
    [Tooltip("Until she sits, the camera's aim comes no further than this past the edge, in metres (below 0: before it).")]
    [SerializeField] float cameraReach = -1f;
    [Tooltip("Once she sits, the camera eases onto this point, so more of the drop shows.")]
    [SerializeField] Transform sittingView;
    [Tooltip("While she sits, how much the camera still drifts after the bird, off the Sitting View (0 = held still, 1 = free).")]
    [SerializeField, Range(0f, 1f)] float sittingFollow = 0.3f;

    [Header("Breeze")]
    [Tooltip("A light breeze blowing past her out over the drop (CliffBreeze prefab), while she sits, until the ending's first cut. Empty: none.")]
    [SerializeField] ParticleSystem breeze;

    [Header("Ending")]
    [Tooltip("The ending (the bird flies out and up, seen from the air, then from far off). Empty: only the girl's ending animation plays.")]
    [SerializeField] EndingFlight ending;

    // Set while she is here: walking to the stump, then sitting by it.
    Baby baby;
    Coroutine walking;
    bool resting;
    bool ended;

    public override NaturalTimer Timer => null;

    // The way over the cliff opens when she sits and stays open through the ending.
    bool WayOpen => resting || ended;
    // How far the bird may fly along +Z (GameFlow.FlyArea).
    public float BirdFarEnd => WayOpen ? float.PositiveInfinity : edge.position.z;
    // How far the camera's aim may come along +Z (GameFlow.CameraArea).
    public float CameraFarEnd => WayOpen ? float.PositiveInfinity : edge.position.z + cameraReach;

    void OnEnable()
    {
        if (ending != null)
            ending.FirstShotOver += StopBreeze;
    }

    void OnDisable()
    {
        if (ending != null)
            ending.FirstShotOver -= StopBreeze;
    }

    // Only the ending's first shot (the game's camera) shows the breeze; from the cut on it is gone at once.
    void StopBreeze()
    {
        if (breeze != null)
            breeze.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    public override void BeginWaiting()
    {
        baby = Baby.Instance;
        // Again after a dev tool took her back a stage, maybe after the ending.
        if (walking != null)
            StopCoroutine(walking);
        resting = false;
        ended = false;
        HoldSittingView(false);
        if (guide != null && guide.HasFlown)
            guide.ResetToStump();
        walking = StartCoroutine(WalkToStump());
    }

    IEnumerator WalkToStump()
    {
        // Metres still to run, once the guide has answered.
        float runLeft = 0f;
        bool hurried = false;
        for (int i = 0; i < path.childCount; i++)
        {
            Vector3 point = path.GetChild(i).position;
            while (true)
            {
                if (!hurried && guide != null && guide.HasAnswered)
                {
                    hurried = true;
                    runLeft = runShare * DistanceLeft(i);
                }
                Vector3 before = baby.transform.position;
                bool arrived = runLeft > 0f ? baby.RunToward(point) : baby.StepToward(point);
                runLeft -= Vector3.Distance(before, baby.transform.position);
                LetGuideFly();
                if (arrived)
                    break;
                yield return null;
            }
        }
        walking = null;
        resting = true;
        HoldSittingView(true);
    }

#if UNITY_EDITOR
    // Dev tool (the toolbar's Ending start): she is already sitting by the stump with the guide gone, the camera on the sitting
    // view, and the bird already out over the drop, halfway through its flight out of the picture, so the ending plays from there
    // (the first frames of Play Mode, which stutter, fall on that flight, not on the first shot).
    public void PlayEndingNow()
    {
        baby = Baby.Instance;
        if (walking != null)
            StopCoroutine(walking);
        walking = null;
        Vector3 seat = path.GetChild(path.childCount - 1).position;
        Vector3 facing = Vector3.ProjectOnPlane(view.position - seat, Vector3.up);
        baby.transform.SetPositionAndRotation(seat, facing.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(facing) : baby.transform.rotation);
        if (guide != null)
            guide.Gone();
        resting = true;
        ended = true;
        baby.PlayEnding();
        // The bird just past where it would set the ending off, at its height, with the camera cut to the sitting view.
        BirdController bird = BirdController.Instance;
        Vector3 outThere = edge.position + edge.forward * (pastEdge + 0.5f);
        bird.Teleport(new Vector3(outThere.x, bird.transform.position.y, outThere.z));
        HoldSittingView(true);
        if (GameFlow.Instance != null && GameFlow.Instance.BirdCamera != null)
            GameFlow.Instance.BirdCamera.Cut();
        if (ending != null)
            ending.Play(edge.forward);
    }
#endif

    // Along the path from where she is, through the points from this one on.
    float DistanceLeft(int next)
    {
        float distance = 0f;
        Vector3 from = baby.transform.position;
        for (int i = next; i < path.childCount; i++)
        {
            Vector3 to = path.GetChild(i).position;
            distance += Vector3.Distance(from, to);
            from = to;
        }
        return distance;
    }

    void LetGuideFly()
    {
        if (guide == null || guide.HasFlown)
            return;
        Vector3 apart = guide.transform.position - baby.transform.position;
        apart.y = 0f;
        if (apart.sqrMagnitude <= guideFliesAt * guideFliesAt)
            guide.FlyAway();
    }

    // The camera eases over the drop and the breeze rises once she sits; both go when she no longer does.
    void HoldSittingView(bool hold)
    {
        if (breeze != null)
        {
            if (hold && !breeze.isEmitting)
                breeze.Play(true);
            else if (!hold)
                breeze.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
        BirdCamera birdCamera = GameFlow.Instance != null ? GameFlow.Instance.BirdCamera : null;
        if (birdCamera != null)
            birdCamera.HoldView(hold && sittingView != null ? sittingView.position : null, sittingFollow);
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
            HoldSittingView(false);
            baby = null;
            return;
        }
        if (!resting)
            return;
        baby.SitFacing(view.position);
        if (BirdOutPastCliff())
        {
            ended = true;
            baby.PlayEnding();
            if (ending != null)
                ending.Play(edge.forward);
        }
    }

    bool BirdOutPastCliff()
    {
        Vector3 bird = BirdController.Instance.transform.position;
        return Vector3.Dot(bird - edge.position, edge.forward) >= pastEdge;
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
        Vector3 across = Vector3.Cross(Vector3.up, edge.forward) * 30f;
        // Red: the edge, the bird's far end until she sits. Cyan: the camera's far end until then. Yellow: where the bird sets the ending off.
        Gizmos.color = Color.red;
        Gizmos.DrawLine(edge.position - across, edge.position + across);
        Gizmos.color = Color.cyan;
        Vector3 reach = edge.position + edge.forward * cameraReach;
        Gizmos.DrawLine(reach - across, reach + across);
        Gizmos.color = Color.yellow;
        Vector3 trigger = edge.position + edge.forward * pastEdge;
        Gizmos.DrawLine(trigger - across, trigger + across);
        if (sittingView != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(sittingView.position, 0.5f);
        }
    }
}
