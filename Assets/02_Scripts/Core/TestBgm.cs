using FMODUnity;
using UnityEngine;

// Test only: loops StreamingAssets/BGM_test1.mp3 from the start of the game, through FMOD's core API, so no bank has to be rebuilt.
// Unity's own audio is off in this project, so an AudioSource would be silent. Delete this file and the mp3 when the real music comes.
// Doesn't work in a web build, which can't read StreamingAssets as files.
public static class TestBgm
{
    const string FileName = "BGM_test1.mp3";

    static FMOD.Sound sound;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Play()
    {
        string path = System.IO.Path.Combine(Application.streamingAssetsPath, FileName);
        FMOD.RESULT result = RuntimeManager.CoreSystem.createStream(path, FMOD.MODE.LOOP_NORMAL | FMOD.MODE._2D, out sound);
        if (result != FMOD.RESULT.OK)
        {
            Debug.LogWarning($"TestBgm: couldn't open {path}: {result}");
            return;
        }
        RuntimeManager.CoreSystem.getMasterChannelGroup(out FMOD.ChannelGroup master);
        result = RuntimeManager.CoreSystem.playSound(sound, master, false, out FMOD.Channel _);
        if (result != FMOD.RESULT.OK)
            Debug.LogWarning($"TestBgm: couldn't play {path}: {result}");
    }
}
