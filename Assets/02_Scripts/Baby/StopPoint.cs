using UnityEngine;

public enum WaitPose { Stand, Crouch, Sit }

// A route point where the baby stops until the blocker opens, and the pose it waits in.
public class StopPoint : MonoBehaviour
{
    [SerializeField] PathBlocker blocker;
    [SerializeField] WaitPose pose;
    [SerializeField] BabyRoute byBirdRoute;
    [SerializeField] Transform observeTarget;
    [Tooltip("While she waits, her head follows the observe target too, tilted to one side: caught by something small and uneasy, not just held up.")]
    [SerializeField] bool watchWithHead;
    [Tooltip("Degrees her head tilts to the side while she watches it.")]
    [SerializeField] float headTilt = 14f;

    public PathBlocker Blocker => blocker;
    public WaitPose Pose => pose;
    public BabyRoute ByBirdRoute => byBirdRoute;
    public Transform ObserveTarget => observeTarget;
    public bool WatchWithHead => watchWithHead;
    public float HeadTilt => headTilt;
}
