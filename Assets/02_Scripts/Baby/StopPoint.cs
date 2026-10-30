using UnityEngine;

public enum WaitPose { Stand, Crouch, Sit }

// A route point where the baby stops until the blocker opens, and the pose it waits in.
public class StopPoint : MonoBehaviour
{
    [SerializeField] PathBlocker blocker;
    [SerializeField] WaitPose pose;
    [SerializeField] BabyRoute byBirdRoute;
    [SerializeField] Transform observeTarget;

    public PathBlocker Blocker => blocker;
    public WaitPose Pose => pose;
    public BabyRoute ByBirdRoute => byBirdRoute;
    public Transform ObserveTarget => observeTarget;
}
