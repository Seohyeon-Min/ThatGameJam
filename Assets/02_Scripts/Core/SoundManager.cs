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

    EventInstance windInstance;

    void Awake() => Instance = this;

    void Start()
    {
        windInstance = RuntimeManager.CreateInstance(windEvent);
        windInstance.start();
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
    }
}
