using FMODUnity;
using UnityEngine;

// Receives the animation events baked into NPC-Girl-Animated.fbx.
// Put this on the same GameObject as the Animator.
public class BabyAnimationEvents : MonoBehaviour
{
    // The ground is snow for the whole game so far; swap this event once a melted-ground variant exists
    // (design doc §8.4/§10, the snow melting away stage by stage). Crossing a balance beam, her feet are on
    // wood instead.
    public void Footstep() => RuntimeManager.PlayOneShot(
        Baby.Instance.OnBalanceBeam ? "event:/SFX/footstepTreeWalk" : "event:/SFX/footstepSnow", transform.position);
}
