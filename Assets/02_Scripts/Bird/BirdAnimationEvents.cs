using FMODUnity;
using UnityEngine;
using UnityEngine.Events;

// Receives the animation events baked into Bird-Animated.fbx.
// Put this on the same GameObject as the Animator and hook up the UnityEvents in the Inspector.
public class BirdAnimationEvents : MonoBehaviour
{
    [Tooltip("Bird_Landing: feet touch the branch (frame 29, 0.933 s). Start foot IK / snap to the perch here.")]
    public UnityEvent onFootContact;
    [Tooltip("Bird_Landing: toes finished gripping (frame 37, 1.2 s).")]
    public UnityEvent onGripComplete;
    [Tooltip("Bird_Takeoff: feet leave the branch (frame 12, 0.367 s). Detach from the perch and start moving here.")]
    public UnityEvent onFootRelease;

    public void FootContact()
    {
        onFootContact?.Invoke();
        RuntimeManager.PlayOneShot("event:/SFX/branch", transform.position);
    }
    public void GripComplete() => onGripComplete?.Invoke();
    public void FootRelease() => onFootRelease?.Invoke();

    // Bird_Flap: wing at the bottom of its downstroke (frame 4, 0.13 s) — estimated from the rotation curve,
    // not eyeballed in the Animation window, so nudge the event there if the sync feels off once heard.
    // Plays directly (no UnityEvent) since nothing else needs this one.
    public void Flap() => RuntimeManager.PlayOneShot("event:/SFX/flap", transform.position);
}
