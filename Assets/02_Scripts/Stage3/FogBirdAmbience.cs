using FMOD.Studio;
using FMODUnity;
using UnityEngine;

// Birds heard, not seen, while she's inside the fog (FogMaze's FogField): fades in while the baby is in its area, out once she
// leaves it, rather than switching abruptly.
[RequireComponent(typeof(FogField))]
public class FogBirdAmbience : MonoBehaviour
{
    [SerializeField] string eventPath = "event:/Ambience/birdsTweeting";
    [SerializeField, Min(0.01f)] float fadeSeconds = 1.5f;

    FogField field;
    EventInstance instance;
    float volume;

    void OnEnable()
    {
        field = GetComponent<FogField>();
        StartCoroutine(Begin());
    }

    // Once the sound banks are in (on the web they come down after the game starts).
    System.Collections.IEnumerator Begin()
    {
        while (!RuntimeManager.HaveMasterBanksLoaded)
            yield return null;
        instance = RuntimeManager.CreateInstance(eventPath);
        instance.setVolume(0f);
        instance.start();
    }

    void Update()
    {
        if (Baby.Instance == null || !instance.isValid())
            return;
        // The fog area spans the whole maze, far bigger than the event's attenuation range, so the sound has to follow her
        // through it rather than sit fixed at the field's own pivot.
        instance.set3DAttributes(RuntimeUtils.To3DAttributes(Baby.Instance.transform.position));
        bool inside = field.Contains(Baby.Instance.transform.position);
        volume = Mathf.MoveTowards(volume, inside ? 1f : 0f, Time.deltaTime / fadeSeconds);
        instance.setVolume(volume);
    }

    void OnDisable()
    {
        if (!instance.isValid())
            return;
        instance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        instance.release();
        instance.clearHandle();
    }
}
