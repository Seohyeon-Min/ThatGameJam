using FMOD.Studio;
using FMODUnity;

// The intro's music: four separate loop events (Music/Intro_0..3 in FMOD Studio). Intro_0 plays from the title
// screen; Next() advances one track at each of the intro's three real camera changes (the sky flight starting,
// the dive camera coming on, and the game camera taking over at StartGame), so Intro_3 is already playing once
// the game itself begins.
public static class IntroMusic
{
    static readonly string[] Tracks =
    {
        "event:/Music/Intro_0", "event:/Music/Intro_1", "event:/Music/Intro_2", "event:/Music/Intro_3",
    };

    static EventInstance instance;
    static int index = -1;

    public static void Play() => PlayTrack(0);

    // No-op once already on the last track.
    public static void Next()
    {
        if (index + 1 < Tracks.Length)
            PlayTrack(index + 1);
    }

    static void PlayTrack(int next)
    {
        if (instance.isValid())
        {
            instance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            instance.release();
        }
        index = next;
        instance = RuntimeManager.CreateInstance(Tracks[index]);
        instance.start();
    }
}
