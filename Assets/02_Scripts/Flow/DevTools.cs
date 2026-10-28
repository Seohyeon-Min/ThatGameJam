using UnityEngine;
using UnityEngine.InputSystem;

// Development shortcuts, only in the Editor and development builds (plan 4.18).
// F1 fast time, F2 resolve the current wait naturally, F3 resolve it by bird, F4 next stage.
public class DevTools : MonoBehaviour
{
    [SerializeField] float fastTimeScale = 2f;

    void Awake()
    {
        // Debug.isDebugBuild is also true in the Editor.
        if (!Debug.isDebugBuild)
            enabled = false;
    }

    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (keyboard.f1Key.wasPressedThisFrame)
            Time.timeScale = Time.timeScale > 1f ? 1f : fastTimeScale;
        if (keyboard.f2Key.wasPressedThisFrame)
            CurrentTimer()?.ResolveNaturally();
        if (keyboard.f3Key.wasPressedThisFrame)
            CurrentTimer()?.ResolveByBird();
        if (keyboard.f4Key.wasPressedThisFrame)
            GameFlow.Instance.SkipToNextStage();
    }

    static NaturalTimer CurrentTimer()
    {
        Baby baby = Baby.Instance;
        if (baby.State == BabyState.Sleeping)
            return baby.WakeTimer;
        return baby.CurrentBlocker != null ? baby.CurrentBlocker.Timer : null;
    }

    void OnGUI()
    {
        NaturalTimer timer = CurrentTimer();
        string timerText = timer != null
            ? $"{timer.Elapsed:0.0}/{timer.Duration:0}s  signal {timer.SignalLevel}"
            : "-";
        GUI.Label(new Rect(10f, 10f, 500f, 22f),
            $"x{Time.timeScale:0.##}  {Baby.Instance.State}  {timerText}   [F1 speed  F2 natural  F3 bird  F4 skip]");
    }
}
