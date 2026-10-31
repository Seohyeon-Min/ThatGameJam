using FMOD.Studio;
using FMODUnity;
using UnityEngine;
using UnityEngine.InputSystem;

// Plays the game's ambient and effect sounds. Lives as a persistent prefab in the scene.
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [SerializeField] EventReference windEvent;
    [SerializeField] EventReference birdEvent;
    [Tooltip("Test only: an event to play from the start of the game, such as music being tried out. Empty plays nothing.")]
    [SerializeField] EventReference testAudioEvent;

    EventInstance windInstance;
    EventInstance testAudioInstance;

    void Awake() => Instance = this;

    void Start()
    {
        windInstance = RuntimeManager.CreateInstance(windEvent);
        windInstance.start();
        if (!testAudioEvent.IsNull)
        {
            testAudioInstance = RuntimeManager.CreateInstance(testAudioEvent);
            testAudioInstance.start();
        }
    }

    void Update()
    {
        if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
            RuntimeManager.PlayOneShot(birdEvent);
    }

    void OnDestroy()
    {
        windInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        windInstance.release();
        if (testAudioInstance.isValid())
        {
            testAudioInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            testAudioInstance.release();
        }
    }
}
