using System;
using System.Collections;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;

// The stage BGM: one event (event:/Music/backgroundMusic) with a track per piece, all on one timeline so they stay on the beat,
// each track's volume set by its own parameter (0 silent, 1 full, in FMOD Studio). It starts once Start Cutscene ends and is never
// restarted; only the parameters are faded. Stage 0: bgm_st_0. Stage 1 adds 1_1 and 1_2, Stage 2 adds 2_1 and 2_2. Entering
// Stage 3, 0, 1_1 and 2_1 fade out and 3 fades in. Out of the fog at the cliff (CliffEnding), 3 fades out and 0, 1_1, 2_1 and 4
// fade in, so 0, 1_1, 1_2, 2_1, 2_2 and 4 play. As the bird heads out over the drop for the ending, it all fades out (FadeOut).
public class MusicManager : MonoBehaviour
{
    public enum CliffRule
    {
        [Tooltip("As its levels say.")] AsLevels,
        [Tooltip("Plays from the cliff on.")] On,
        [Tooltip("Silent from the cliff on.")] Off,
    }

    [Serializable]
    public class Layer
    {
        [Tooltip("The event's parameter for this track's volume.")]
        public string parameter;
        [Tooltip("Season level from which it plays, and the level from which it stops again (-1: never).")]
        public int fromLevel;
        public int untilLevel = -1;
        public CliffRule atCliff;

        public Layer(string parameter, int fromLevel, int untilLevel, CliffRule atCliff)
        {
            this.parameter = parameter;
            this.fromLevel = fromLevel;
            this.untilLevel = untilLevel;
            this.atCliff = atCliff;
        }
    }

    [Tooltip("event:/Music/backgroundMusic: a track per piece, each with its own volume parameter.")]
    [SerializeField] EventReference musicEvent;
    [SerializeField] Layer[] layers =
    {
        new Layer("bgm_st_0", 0, 3, CliffRule.On),
        new Layer("bgm_st_1_1", 1, 3, CliffRule.On),
        new Layer("bgm_st_1_2", 1, -1, CliffRule.On),
        new Layer("bgm_st_2_1", 2, 3, CliffRule.On),
        new Layer("bgm_st_2_2", 2, -1, CliffRule.On),
        new Layer("bgm_st_3", 3, -1, CliffRule.Off),
        new Layer("bgm_st_4", 99, -1, CliffRule.On),
    };
    [Tooltip("Seconds a track takes to fade in or out.")]
    [SerializeField, Min(0.01f)] float fadeSeconds = 3f;
    [Tooltip("Seconds it all takes to fade out as the bird heads out for the ending.")]
    [SerializeField, Min(0.01f)] float endFadeSeconds = 5f;
    [Tooltip("Its end starts the music. Empty starts the music at once instead.")]
    [SerializeField] Cutscene startCutscene;
    [Tooltip("Empty finds the one in the scene.")]
    [SerializeField] Season season;

    public static MusicManager Instance { get; private set; }

    EventInstance music;
    float[] values;
    Coroutine[] fades;
    // Out of the fog, at the cliff.
    bool atCliff;
    // Fading out for the ending, and silent from then on.
    bool ending;
    // Debug only (DevTools): paused instead of stopped, so it picks up where it left off once unmuted.
    bool muted;

    public bool Muted => muted;

    void Awake()
    {
        Instance = this;
        if (season == null)
            season = FindAnyObjectByType<Season>();
        values = new float[layers.Length];
        fades = new Coroutine[layers.Length];
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

    // Each track at once where Season's level (and the cliff) puts it, snapped.
    void StartMusic()
    {
        if (music.isValid())
            return;
        music = RuntimeManager.CreateInstance(musicEvent);
        int level = Level;
        for (int i = 0; i < layers.Length; i++)
        {
            values[i] = Wanted(layers[i], level);
            music.setParameterByName(layers[i].parameter, values[i], true);
        }
        music.start();
        music.setPaused(muted);
    }

    int Level => season != null ? season.Level : 0;

    float Wanted(Layer layer, int level)
    {
        if (ending)
            return 0f;
        if (atCliff && layer.atCliff == CliffRule.On)
            return 1f;
        if (atCliff && layer.atCliff == CliffRule.Off)
            return 0f;
        return level >= layer.fromLevel && (layer.untilLevel < 0 || level < layer.untilLevel) ? 1f : 0f;
    }

    void OnSeasonChanged(int level)
    {
        // Back before the last stage (a dev tool): not at the cliff any more.
        if (level < 3)
            atCliff = ending = false;
        FadeAll();
    }

    // Out of the fog, on to the cliff (CliffEnding).
    public void EnterCliff()
    {
        // Again after the ending (a dev tool took it back to the cliff): the music comes back.
        if (atCliff && !ending)
            return;
        atCliff = true;
        ending = false;
        // Started there without its start cutscene (a dev tool's stage start): the music starts here, as the cliff has it.
        if (!music.isValid())
            StartMusic();
        FadeAll();
    }

    // The bird heads out over the drop for the ending: every track fades out.
    public void FadeOut()
    {
        if (ending)
            return;
        ending = true;
        FadeAll(endFadeSeconds);
    }

    void FadeAll() => FadeAll(fadeSeconds);

    void FadeAll(float seconds)
    {
        if (!music.isValid())
            return;
        int level = Level;
        for (int i = 0; i < layers.Length; i++)
        {
            float target = Wanted(layers[i], level);
            if (Mathf.Approximately(target, values[i]) && fades[i] == null)
                continue;
            if (fades[i] != null)
                StopCoroutine(fades[i]);
            fades[i] = StartCoroutine(Fade(i, target, seconds));
        }
    }

    IEnumerator Fade(int index, float target, float seconds)
    {
        float from = values[index];
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            values[index] = Mathf.Lerp(from, target, t / seconds);
            music.setParameterByName(layers[index].parameter, values[index], true);
            yield return null;
        }
        values[index] = target;
        music.setParameterByName(layers[index].parameter, target, true);
        fades[index] = null;
    }

    // Debug only (DevTools): pauses it in place so testing can go on without the music, then picks up again on unmute.
    public void ToggleMute() => SetMuted(!muted);

    public void SetMuted(bool value)
    {
        muted = value;
        if (music.isValid())
            music.setPaused(muted);
    }

    // Debug only: as if Season had advanced one more step, to hear the tracks change without playing through a stage.
    [ContextMenu("Debug/Advance Season (Season_Change_Effect)")]
    void DebugAdvanceSeason()
    {
        if (season != null)
            season.AdvanceSeason();
        else
            Debug.LogWarning("MusicManager: no Season assigned to advance.", this);
    }

    [ContextMenu("Debug/Enter Cliff")]
    void DebugEnterCliff() => EnterCliff();
}
