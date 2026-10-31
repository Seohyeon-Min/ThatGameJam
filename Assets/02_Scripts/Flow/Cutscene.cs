using UnityEngine;
using UnityEngine.Playables;

// A staged moment: plays this object's Timeline, with its own watercolor look if it has one, and tells the game when it's over.
// Assign one to a Stage's Wake Cutscene and the baby waits for it after waking, before it walks.
// The camera, animation and sound are on the Timeline. For a different post-processing, add a Volume under this object and switch
// it with an Activation track. While one plays, the player can't move the bird or cry.
[RequireComponent(typeof(PlayableDirector))]
public class Cutscene : MonoBehaviour
{
    static int playing;

    [Tooltip("Applied while the cutscene plays, then the look from before comes back. Empty keeps the current look.")]
    [SerializeField] WatercolorLook look;
    [Tooltip("Seconds to blend into the look at the start and back out of it at the end.")]
    [SerializeField, Min(0f)] float lookBlendSeconds = 1f;

    PlayableDirector director;
    WatercolorManager manager;
    WatercolorManager.Settings before;
    float blend;
    bool lookHeld;

    public static bool AnyPlaying => playing > 0;
    public bool IsPlaying { get; private set; }

    void Awake()
    {
        director = GetComponent<PlayableDirector>();
        director.playOnAwake = false;
        // Play On Awake may have started it before this ran.
        if (director.state == PlayState.Playing)
            director.Stop();
        // Otherwise a Timeline set to Hold never reports its end.
        director.extrapolationMode = DirectorWrapMode.None;
        director.stopped += OnDirectorStopped;
    }

    void OnDestroy()
    {
        if (director != null)
            director.stopped -= OnDirectorStopped;
        if (IsPlaying)
            playing--;
    }

    public void Play()
    {
        if (IsPlaying)
            return;
        IsPlaying = true;
        playing++;

        manager = FindAnyObjectByType<WatercolorManager>();
        if (look != null && manager != null && !lookHeld)
        {
            before = JsonUtility.FromJson<WatercolorManager.Settings>(JsonUtility.ToJson(manager.settings));
            lookHeld = true;
            blend = 0f;
        }

        if (director.playableAsset != null)
        {
            director.time = 0d;
            director.Play();
            Debug.Log($"Cutscene: {name} plays, {director.duration:0.##} s, director {director.state}.", this);
        }
        else
        {
            Debug.LogWarning("Cutscene: no Timeline assigned.", this);
            Finish();
        }
    }

    // Ends it now, as if the Timeline had reached its end.
    public void Stop()
    {
        if (IsPlaying)
            director.Stop();
    }

    void OnDirectorStopped(PlayableDirector stopped)
    {
        Debug.Log($"Cutscene: {name} ended.", this);
        Finish();
    }

    void Finish()
    {
        if (!IsPlaying)
            return;
        IsPlaying = false;
        playing--;
    }

    void Update()
    {
        if (!lookHeld)
            return;
        float step = lookBlendSeconds > 0f ? Time.deltaTime / lookBlendSeconds : 1f;
        blend = Mathf.Clamp01(blend + (IsPlaying ? step : -step));
        manager.Blend(before, look.settings, Mathf.SmoothStep(0f, 1f, blend));
        if (!IsPlaying && blend <= 0f)
            lookHeld = false;
    }
}
