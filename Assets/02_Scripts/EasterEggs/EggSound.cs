using FMODUnity;
using UnityEngine;

// One shot of an FMOD event at a place, with its own pitch and volume (the icicles' notes, the cliff's echo).
public static class EggSound
{
    public static void Play(EventReference sound, Vector3 position, float pitch = 1f, float volume = 1f)
    {
        if (sound.IsNull)
            return;
        FMOD.Studio.EventInstance instance = RuntimeManager.CreateInstance(sound);
        instance.set3DAttributes(RuntimeUtils.To3DAttributes(position));
        instance.setPitch(pitch);
        instance.setVolume(volume);
        instance.start();
        instance.release();
    }
}
