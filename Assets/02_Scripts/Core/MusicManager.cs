using System.Collections;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;

// The stage BGM. Starts once Start Cutscene ends: the layered event (Stage 0's track always on, Stage 1-2 gated by the Layer1-2
// parameters) and BGM_Stage_Final both start together, Final silent, so Final's half-length intro Loop Region stays phase-locked
// to the layered event's loop from the first frame (Final is authored at twice the layered event's loop length, split at the
// midpoint, per design). Season reaching Stage 3 (design doc Season_Change_Effect) fades the layered event out, sets Final's
// Release parameter so FMOD's Transition Region carries it out of the half loop into the rest of the piece on the next bar, and
// fades Final in - the switch lands on the beat instead of Final restarting from 0. Levels past Stage 3 (the ending) do nothing
// further.
public class MusicManager : MonoBehaviour
{
    [Tooltip("event:/Music/backgroundmusic — one event whose Stage 0 track always plays and whose Stage 1-2 tracks are gated by the Layer1-2 parameters.")]
    [SerializeField] EventReference musicEvent;
    [Tooltip("event:/Music/BGM_Stage_Final — starts muted alongside Music Event (so its half-length intro Loop Region stays phase-locked to it), then takes over alone once Season reaches Stage 3.")]
    [SerializeField] EventReference finalMusicEvent;
    [Tooltip("Final's parameter that releases it from its half-length intro loop into the rest of the piece, via a Transition Region in FMOD Studio.")]
    [SerializeField] string finalReleaseParameter = "Release";
    [Tooltip("Seconds a layer takes to fade from silent to full once its Season step is reached, and Final's own fade-in/out time at the Stage 3 switch-over.")]
    [SerializeField, Min(0.01f)] float fadeSeconds = 3f;
    [Tooltip("The event's local parameters that gate Stage 1 and 2's tracks, in Season level order.")]
    [SerializeField] string[] layerParameters = { "Layer1", "Layer2" };
    [Tooltip("Its end starts the music, in sync across all tracks. Empty starts the music at once instead.")]
    [SerializeField] Cutscene startCutscene;
    [Tooltip("Empty finds the one in the scene.")]
    [SerializeField] Season season;

    // The Season level at which the layered event is retired in favour of Final Music Event.
    const int FinalLevel = 3;

    EventInstance music;
    EventInstance finalMusic;
    Coroutine[] fades;
    Coroutine finalFade;
    bool released;

    void Awake()
    {
        if (season == null)
            season = FindAnyObjectByType<Season>();
        fades = new Coroutine[layerParameters.Length];
    }

    void Start()
    {
        if (season != null)
            season.OnSeasonChanged.AddListener(OnSeasonChanged);

        if (startCutscene != null)
            startCutscene.Finished += StartMusic;
        else
            StartMusic();
    }

    void OnDestroy()
    {
        if (startCutscene != null)
            startCutscene.Finished -= StartMusic;
        if (season != null)
            season.OnSeasonChanged.RemoveListener(OnSeasonChanged);
        if (music.isValid())
        {
            music.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            music.release();
        }
        if (finalMusic.isValid())
        {
            finalMusic.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            finalMusic.release();
        }
    }

    // Starts the layered event and Final together so Final's half-length intro loop is phase-locked to the layered event's loop
    // from the first frame. Catches an instance up to wherever Season's level currently puts it (snapped, not faded): already at
    // Stage 3 or past it, Final starts released and audible and the layered event isn't started at all.
    void StartMusic()
    {
        int level = season != null ? season.Level : 0;
        bool startReleased = level >= FinalLevel;

        if (!startReleased && !music.isValid())
        {
            music = RuntimeManager.CreateInstance(musicEvent);
            for (int i = 0; i < layerParameters.Length; i++)
                music.setParameterByName(layerParameters[i], level >= i + 1 ? 1f : 0f, true);
            music.start();
        }

        if (!finalMusic.isValid())
        {
            finalMusic = RuntimeManager.CreateInstance(finalMusicEvent);
            finalMusic.setParameterByName(finalReleaseParameter, startReleased ? 1f : 0f, true);
            finalMusic.setVolume(startReleased ? 1f : 0f);
            finalMusic.start();
        }
        released = startReleased;
    }

    // Layer i (0-based) is on once level has passed Season step i+1, and stays on from then on: driven by the level itself, not a
    // count of calls, so Game Flow's Back One Stage dev tool can't double-fire or desync a layer. Reaching Final Level releases
    // Final once and for good; Back One Stage before Stage 3 is not expected to undo that.
    void OnSeasonChanged(int level)
    {
        if (level >= FinalLevel)
        {
            ReleaseFinal();
            return;
        }

        if (!music.isValid())
            return;
        for (int i = 0; i < layerParameters.Length; i++)
            FadeLayerTo(i, level >= i + 1 ? 1f : 0f);
    }

    // Sets Final's Release parameter (FMOD's Transition Region carries it out of its half-length intro loop on the next bar) and
    // crossfades: the layered event out, Final in. Final itself is already playing (muted, phase-locked) from StartMusic.
    void ReleaseFinal()
    {
        if (released)
            return;
        released = true;

        if (music.isValid())
        {
            music.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            music.release();
        }

        if (!finalMusic.isValid())
            return;
        finalMusic.setParameterByName(finalReleaseParameter, 1f, true);
        if (finalFade != null)
            StopCoroutine(finalFade);
        finalFade = StartCoroutine(FadeFinalVolume());
    }

    IEnumerator FadeFinalVolume()
    {
        finalMusic.getVolume(out float from);
        for (float t = 0f; t < fadeSeconds; t += Time.deltaTime)
        {
            finalMusic.setVolume(Mathf.Lerp(from, 1f, t / fadeSeconds));
            yield return null;
        }
        finalMusic.setVolume(1f);
    }

    void FadeLayerTo(int index, float target)
    {
        if (fades[index] != null)
            StopCoroutine(fades[index]);
        fades[index] = StartCoroutine(FadeParameter(layerParameters[index], target));
    }

    IEnumerator FadeParameter(string parameter, float target)
    {
        music.getParameterByName(parameter, out float from);
        for (float t = 0f; t < fadeSeconds; t += Time.deltaTime)
        {
            music.setParameterByName(parameter, Mathf.Lerp(from, target, t / fadeSeconds), true);
            yield return null;
        }
        music.setParameterByName(parameter, target, true);
    }

    // Debug only: as if Season had advanced one more step, to watch a layer fade in (or the Stage 3 release happen) without
    // playing through a stage.
    [ContextMenu("Debug/Advance Season (Season_Change_Effect)")]
    void DebugAdvanceSeason()
    {
        if (season != null)
            season.AdvanceSeason();
        else
            Debug.LogWarning("MusicManager: no Season assigned to advance.", this);
    }
}
