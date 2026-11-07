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
        instance = RuntimeManager.CreateInstance(eventPath);
        instance.set3DAttributes(RuntimeUtils.To3DAttributes(transform.position));
        instance.setVolume(0f);
        instance.start();
    }

    void Update()
    {
        if (Baby.Instance == null)
            return;
        bool inside = field.Contains(Baby.Instance.transform.position);
        volume = Mathf.MoveTowards(volume, inside ? 1f : 0f, Time.deltaTime / fadeSeconds);
        instance.setVolume(volume);
    }

    void OnDisable()
    {
        instance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        instance.release();
    }
}
