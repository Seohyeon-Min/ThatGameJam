using FMOD.Studio;
using FMODUnity;
using UnityEngine;

// A looping FMOD event tied to this object's position for as long as it's enabled (e.g. a stream's flowing water).
// Its volume fades with distance from the bird (the player's "ears" - see Main.unity's StudioListener) in code, rather than
// leaning only on the event's own Spatialiser: that keeps it reliably silent far away (the intro included) without depending
// on exactly how the FMOD Studio project's distance curve is tuned. The Spatialiser still drives left/right panning.
public class LoopingAmbience : MonoBehaviour
{
    [SerializeField] string eventPath = "event:/Ambience/water";
    [Tooltip("Full volume at or within this distance from the bird.")]
    [SerializeField] float nearDistance = 4f;
    [Tooltip("Silent at or beyond this distance from the bird.")]
    [SerializeField] float farDistance = 15f;

    EventInstance instance;

    void OnEnable()
    {
        instance = RuntimeManager.CreateInstance(eventPath);
        instance.set3DAttributes(RuntimeUtils.To3DAttributes(transform.position));
        instance.setVolume(0f);
        instance.start();
    }

    void Update()
    {
        if (BirdController.Instance == null)
            return;
        float distance = Vector3.Distance(transform.position, BirdController.Instance.transform.position);
        instance.setVolume(1f - Mathf.InverseLerp(nearDistance, farDistance, distance));
    }

    void OnDisable()
    {
        instance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        instance.release();
    }
}
