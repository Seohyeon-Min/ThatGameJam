using System.Collections;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Timeline;

// A staged moment: plays this object's Timeline, with its own watercolor look if it has one, and tells the game when it's over.
// Assign one to a Stage's Wake Cutscene and the baby waits for it after waking, before it walks.
// The camera, animation and sound are on the Timeline. The look's Volume Profile switches with the look.
// While one with Lock Player plays, the player can't move the bird or cry, and the bird stops where it is.
// Animation tracks are for previewing in the Timeline window: in Play Mode they are unbound, and animators play what the game tells them.
[RequireComponent(typeof(PlayableDirector))]
public class Cutscene : MonoBehaviour
{
    // The baby's animations run at this rate, so a frame seen in its GetUp clip can be typed in as it is.
    public const float AnimationFramesPerSecond = 30f;

    static int playing;
    // Playing cutscenes that hold the player still (Lock Player).
    static int locking;

    [Tooltip("Frames to wait after Play before it really starts, at 30 per second like the baby's animations. Nothing but Lock Player is changed until then.")]
    [SerializeField, Min(0)] int startDelayFrames;
    [Tooltip("Applied at once when the cutscene starts, with its Volume Profile if it has one, and what was there before comes back at once when it ends. Empty keeps the current look.")]
    [SerializeField] WatercolorLook look;
    [Tooltip("The look stays when it ends, rather than what was there before coming back: for the ending, which holds on its last shot.")]
    [SerializeField] bool keepLook;
    [Tooltip("From Play (its start delay included) to its end: the player can't move the bird, zoom or cry, and the bird stops where it is.")]
    [SerializeField] bool lockPlayer = true;
    [Tooltip("Starts alongside the Timeline (after the start delay). Stops if the cutscene is cut short. Empty plays nothing.")]
    [SerializeField] EventReference cutsceneAudio;

    PlayableDirector director;
    EventInstance audioInstance;
    WatercolorManager manager;
    WatercolorManager.Settings before;
    Volume volume;
    VolumeProfile profileBefore;
    bool lookHeld;
    bool started;
    bool locked;

    public static bool AnyPlaying => playing > 0;
    // A cutscene with Lock Player is playing: the bird takes no clicks or cries and stays where it is.
    public static bool PlayerLocked => locking > 0;
    public bool IsPlaying { get; private set; }
    // Raised once this cutscene has ended, whether it ran to the end of its Timeline or was cut short by Stop.
    public event System.Action Finished;

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
        UnbindAnimationTracks();
    }

    // Only for this play: the binding saved in the scene is untouched.
    void UnbindAnimationTracks()
    {
        if (director.playableAsset is not TimelineAsset timeline)
            return;
        foreach (TrackAsset track in timeline.GetOutputTracks())
            if (track is AnimationTrack)
                director.SetGenericBinding(track, null);
    }

    void OnDestroy()
    {
        if (director != null)
            director.stopped -= OnDirectorStopped;
        if (started)
            playing--;
        if (locked)
            locking--;
    }

    public void Play()
    {
        if (IsPlaying)
            return;
        IsPlaying = true;
        // From Play on, its start delay too: the player is held still for the whole of it.
        if (lockPlayer)
        {
            locked = true;
            locking++;
        }
        StartCoroutine(StartAfterDelay());
    }

    IEnumerator StartAfterDelay()
    {
        if (startDelayFrames > 0)
            yield return new WaitForSeconds(startDelayFrames / AnimationFramesPerSecond);
        started = true;
        playing++;

        manager = FindAnyObjectByType<WatercolorManager>();
        if (look != null && manager != null && !lookHeld)
        {
            before = JsonUtility.FromJson<WatercolorManager.Settings>(JsonUtility.ToJson(manager.settings));
            lookHeld = true;
            manager.Apply(look.settings, 1f);
            SwitchVolume();
        }

        if (!cutsceneAudio.IsNull)
        {
            audioInstance = RuntimeManager.CreateInstance(cutsceneAudio);
            audioInstance.start();
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
        if (!IsPlaying)
            return;
        StopAllCoroutines();
        if (started)
            director.Stop();
        else
            Finish();
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
        if (started)
            playing--;
        started = false;
        if (locked)
            locking--;
        locked = false;
        if (audioInstance.isValid())
        {
            audioInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            audioInstance.release();
        }
        if (lookHeld && !keepLook)
        {
            lookHeld = false;
            manager.Apply(before, 1f);
            if (volume != null)
                volume.sharedProfile = profileBefore;
            volume = null;
        }
        Finished?.Invoke();
    }

    void SwitchVolume()
    {
        if (look.volumeProfile == null)
            return;
        // Not just any Volume: during a season's crossfade there is a second one, and it isn't the scene's.
        WatercolorSeasonLooks seasonLooks = FindAnyObjectByType<WatercolorSeasonLooks>();
        volume = seasonLooks != null && seasonLooks.SceneVolume != null ? seasonLooks.SceneVolume : FindAnyObjectByType<Volume>();
        if (volume == null)
            return;
        profileBefore = volume.sharedProfile;
        volume.sharedProfile = look.volumeProfile;
    }

    void Update()
    {
        // Debug only: Debug.isDebugBuild is also true in the Editor.
        if (IsPlaying && Debug.isDebugBuild && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            Stop();
    }
}
