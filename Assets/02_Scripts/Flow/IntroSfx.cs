using FMODUnity;

// The intro's one-shot stingers: HighPitch plays when the bird first appears flying up into view, Swoosh plays on
// each click while steering it through the sky, and Dive plays when it dives down to the baby. Sound design is
// still auditioning variants of HighPitch/Swoosh, so DevTools' Cycle buttons (F10/F11) step through them and play
// the pick immediately, with no need to replay the intro to hear it.
public static class IntroSfx
{
    const string Dive = "event:/SFX/dive";

    static readonly string[] HighPitch =
    {
        "event:/SFX/highpitch_0", "event:/SFX/highpitch_1", "event:/SFX/highpitch_2", "event:/SFX/highpitch_3",
    };
    static readonly string[] Swoosh = { "event:/SFX/swoosh_0", "event:/SFX/swoosh_1", "event:/SFX/swoosh_2" };

    static int highPitchIndex;
    static int swooshIndex;

    public static string HighPitchLabel => $"HiPitch {highPitchIndex}";
    public static string SwooshLabel => $"Swoosh {swooshIndex}";

    public static void PlayHighPitch() => RuntimeManager.PlayOneShot(HighPitch[highPitchIndex]);
    public static void PlaySwoosh() => RuntimeManager.PlayOneShot(Swoosh[swooshIndex]);
    public static void PlayDive() => RuntimeManager.PlayOneShot(Dive);

    // Dev tool: moves to the next variant and plays it right away.
    public static void CycleHighPitch()
    {
        highPitchIndex = (highPitchIndex + 1) % HighPitch.Length;
        PlayHighPitch();
    }

    public static void CycleSwoosh()
    {
        swooshIndex = (swooshIndex + 1) % Swoosh.Length;
        PlaySwoosh();
    }
}
