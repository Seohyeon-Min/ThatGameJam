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
    // The stream is often a long, thin plane (a river running far past the bird on one side): distance to its
    // single transform point read as arbitrary - right beside one end could compute as far, and vice versa.
    // The nearest point on its actual bounds reads as distance to the water itself instead.
    Renderer body;

    void Awake() => body = GetComponent<Renderer>();

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
        Vector3 birdPosition = BirdController.Instance.transform.position;
        Vector3 nearestPoint = body != null ? body.bounds.ClosestPoint(birdPosition) : transform.position;
        float distance = Vector3.Distance(nearestPoint, birdPosition);
        instance.setVolume(1f - Mathf.InverseLerp(nearDistance, farDistance, distance));
    }

    void OnDisable()
    {
        instance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        instance.release();
    }
}
