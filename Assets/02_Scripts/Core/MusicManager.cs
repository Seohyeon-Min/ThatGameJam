using System.Collections;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;

// The stage BGM: one FMOD event instance holding Stage 0 to 3's tracks, started once Start Cutscene ends so all four stay in sample-exact
// sync. Stage 0's track plays at once; Stage 1 to 3's tracks sit silent behind the Layer1-3 parameters until Season raises its level
// (design doc Season_Change_Effect) past them, when that layer fades in over Fade Seconds. Layers only ever fade in: this is layering,
// not a crossfade, and levels above Layer Parameters.Length (the ending) do nothing.
public class MusicManager : MonoBehaviour
{
    [Tooltip("event:/Music/backgroundmusic — one event whose Stage 0 track always plays and whose Stage 1-3 tracks are gated by the Layer1-3 parameters.")]
    [SerializeField] EventReference musicEvent;
    [Tooltip("Seconds a layer takes to fade from silent to full once its Season step is reached.")]
    [SerializeField, Min(0.01f)] float fadeSeconds = 3f;
    [Tooltip("The event's local parameters that gate Stage 1, 2 and 3's tracks, in Season level order.")]
    [SerializeField] string[] layerParameters = { "Layer1", "Layer2", "Layer3" };
    [Tooltip("Its end starts the music, in sync across all four tracks. Empty starts the music at once instead.")]
    [SerializeField] Cutscene startCutscene;
    [Tooltip("Empty finds the one in the scene.")]
    [SerializeField] Season season;

    EventInstance music;
    Coroutine[] fades;

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
    }

    // Starts every track together, with Stage 1-3 already set to wherever Season's level currently puts them (snapped, not faded:
    // this is catching the instance up, not a Season_Change_Effect).
    void StartMusic()
    {
        if (music.isValid())
            return;
        music = RuntimeManager.CreateInstance(musicEvent);
        int level = season != null ? season.Level : 0;
        for (int i = 0; i < layerParameters.Length; i++)
            music.setParameterByName(layerParameters[i], level >= i + 1 ? 1f : 0f, true);
        music.start();
    }

    // Layer i (0-based) is on once level has passed Season step i+1, and stays on from then on: driven by the level itself, not a
    // count of calls, so Game Flow's Back One Stage dev tool can't double-fire or desync a layer.
    void OnSeasonChanged(int level)
    {
        if (!music.isValid())
            return;
        for (int i = 0; i < layerParameters.Length; i++)
            FadeLayerTo(i, level >= i + 1 ? 1f : 0f);
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

    // Debug only: as if Season had advanced one more step, to watch a layer fade in without playing through a stage.
    [ContextMenu("Debug/Advance Season (Season_Change_Effect)")]
    void DebugAdvanceSeason()
    {
        if (season != null)
            season.AdvanceSeason();
        else
            Debug.LogWarning("MusicManager: no Season assigned to advance.", this);
    }
}
