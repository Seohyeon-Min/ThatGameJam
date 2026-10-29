using UnityEngine;
using UnityEngine.InputSystem;

// Plays the game's ambient and effect sounds. Lives as a persistent prefab in the scene.
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [SerializeField] AudioSource windSource;
    [SerializeField] AudioSource sfxSource;
    [SerializeField] AudioClip birdClip;

    void Awake() => Instance = this;

    void Start() => windSource.Play();

    void Update()
    {
        if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
            sfxSource.PlayOneShot(birdClip);
    }
}
