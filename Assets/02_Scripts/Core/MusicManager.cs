using System.Collections;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;

// The stage BGM. Starts once Start Cutscene ends, as one FMOD event instance holding Stage 0 to 2's tracks so they stay in
// sample-exact sync: Stage 0's track plays at once, Stage 1 and 2's sit silent behind the Layer1-2 parameters until Season raises
// its level (design doc Season_Change_Effect) past them, when that layer fades in over Fade Seconds - layering, not a crossfade.
// Reaching Season step 3 (Stage 3) retires that whole layered instance and switches, once, to Final Music Event playing alone;
// levels past that (the ending) do nothing further.
public class MusicManager : MonoBehaviour
{
    [Tooltip("event:/Music/backgroundmusic — one event whose Stage 0 track always plays and whose Stage 1-2 tracks are gated by the Layer1-2 parameters.")]
    [SerializeField] EventReference musicEvent;
    [Tooltip("event:/Music/BGM_Stage_Final — played alone once Season reaches Stage 3, replacing the layered event above.")]
    [SerializeField] EventReference finalMusicEvent;
    [Tooltip("Seconds a layer takes to fade from silent to full once its Season step is reached, and the Final track's own fade-in time.")]
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
    bool finalStarted;

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

    // Catches the instance up to wherever Season's level currently puts it (snapped, not faded: this is catching up, not a
    // Season_Change_Effect). Already at Stage 3 or past it: skip the layered event entirely and start on Final Music Event.
    void StartMusic()
    {
        int level = season != null ? season.Level : 0;
        if (level >= FinalLevel)
        {
            StartFinalMusic(snap: true);
            return;
        }

        if (music.isValid())
            return;
        music = RuntimeManager.CreateInstance(musicEvent);
        for (int i = 0; i < layerParameters.Length; i++)
            music.setParameterByName(layerParameters[i], level >= i + 1 ? 1f : 0f, true);
        music.start();
    }

    // Layer i (0-based) is on once level has passed Season step i+1, and stays on from then on: driven by the level itself, not a
    // count of calls, so Game Flow's Back One Stage dev tool can't double-fire or desync a layer. Reaching Final Level switches
    // over once and for good; Back One Stage before Stage 3 is not expected to undo that switch.
    void OnSeasonChanged(int level)
    {
        if (level >= FinalLevel)
        {
            StartFinalMusic(snap: false);
            return;
        }

        if (!music.isValid())
            return;
        for (int i = 0; i < layerParameters.Length; i++)
            FadeLayerTo(i, level >= i + 1 ? 1f : 0f);
    }

    // Retires the layered event and starts Final Music Event alone. snap: true catches up an instance that never played the
    // layers at all (StartMusic called straight into Stage 3); false is the normal in-game switch-over, which fades.
    void StartFinalMusic(bool snap)
    {
        if (finalStarted)
            return;
        finalStarted = true;

        if (music.isValid())
        {
            music.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            music.release();
        }

        finalMusic = RuntimeManager.CreateInstance(finalMusicEvent);
        if (snap)
        {
            finalMusic.start();
            return;
        }
        finalMusic.setVolume(0f);
        finalMusic.start();
        finalFade = StartCoroutine(FadeFinalVolume());
    }

    IEnumerator FadeFinalVolume()
    {
        for (float t = 0f; t < fadeSeconds; t += Time.deltaTime)
        {
            finalMusic.setVolume(t / fadeSeconds);
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

    // Debug only: as if Season had advanced one more step, to watch a layer fade in (or the Stage 3 switch-over happen) without
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
