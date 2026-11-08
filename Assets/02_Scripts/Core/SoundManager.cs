using FMOD.Studio;
using FMODUnity;
using UnityEngine;

// Plays the game's ambient and effect sounds. Lives as a persistent prefab in the scene.
// The bird's cry sound is BirdController's own job (Cry(), cooldown and all), not this: it has no business
// deciding right-click behaviour a second time.
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [SerializeField] EventReference windEvent;
    [Tooltip("Test only: an event to play from the start of the game, such as music being tried out. Empty plays nothing.")]
    [SerializeField] EventReference testAudioEvent;

    EventInstance windInstance;
    EventInstance testAudioInstance;

    void Awake() => Instance = this;

    // Once the sound banks are in (on the web they come down after the game starts).
    System.Collections.IEnumerator Start()
    {
        while (!RuntimeManager.HaveMasterBanksLoaded)
            yield return null;
        windInstance = RuntimeManager.CreateInstance(windEvent);
        windInstance.start();
        if (!testAudioEvent.IsNull)
        {
            testAudioInstance = RuntimeManager.CreateInstance(testAudioEvent);
            testAudioInstance.start();
        }
    }

    void OnDestroy()
    {
        if (windInstance.isValid())
        {
            windInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            windInstance.release();
        }
        if (testAudioInstance.isValid())
        {
            testAudioInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            testAudioInstance.release();
        }
    }
}
