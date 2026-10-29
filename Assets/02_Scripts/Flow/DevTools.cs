using UnityEngine;
using UnityEngine.InputSystem;

// Development controls, only in the Editor and development builds (plan 4.18): a row of buttons along the top of the screen,
// each describing itself while the pointer is on it. The keys F1 to F4 do the same as the buttons.
public class DevTools : MonoBehaviour
{
    const int MaxTimeScale = 3;
    const float ButtonWidth = 100f;
    const float ButtonHeight = 28f;
    const float Gap = 6f;
    const float Top = 8f;
    const float TooltipWidth = 320f;

    static readonly Key[] Keys = { Key.F1, Key.F2, Key.F3, Key.F4 };
    static DevTools active;

    (System.Func<string> label, string tooltip, System.Action action)[] buttons;
    GUIStyle tooltipStyle;

    // True when a screen point (pixels, origin bottom-left) is on the button row, so the click is not also a game click.
    public static bool Covers(Vector2 screenPoint) =>
        active != null && BarRect().Contains(new Vector2(screenPoint.x, Screen.height - screenPoint.y));

    void Awake()
    {
        // Debug.isDebugBuild is also true in the Editor.
        if (!Debug.isDebugBuild)
            enabled = false;

        // In the order of Keys. English only: the built-in font has no Korean in a web build.
        buttons = new (System.Func<string>, string, System.Action)[]
        {
            (() => $"x{Time.timeScale:0.##}", "Game speed. Each click goes x1, x2, x3, then back to x1. (F1)", CycleSpeed),
            (() => "Natural",
                "Resolves what the baby waits for now (waking up, or the device ahead) as if its time ran out. The baby does not look up. (F2)",
                () => CurrentTimer()?.ResolveNaturally()),
            (() => "By bird", "Resolves what the baby waits for now as if the bird did it. The baby looks up at the bird. (F3)",
                () => CurrentTimer()?.ResolveByBird()),
            (() => "Next stage", "Puts the baby asleep at this stage's sunlit spot, as if it had walked the stage: the season advances and the next stage starts there. The bird comes along. (F4)",
                () => GameFlow.Instance.SkipToNextStage()),
        };
    }

    void OnEnable() => active = this;

    void OnDisable()
    {
        if (active == this)
            active = null;
    }

    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        for (int i = 0; i < Keys.Length; i++)
            if (keyboard[Keys[i]].wasPressedThisFrame)
                buttons[i].action();
    }

    static void CycleSpeed() => Time.timeScale = Mathf.Round(Time.timeScale) % MaxTimeScale + 1f;

    static NaturalTimer CurrentTimer()
    {
        Baby baby = Baby.Instance;
        if (baby.State == BabyState.Sleeping)
            return baby.WakeTimer;
        return baby.CurrentBlocker != null ? baby.CurrentBlocker.Timer : null;
    }

    // The row is centred at the top of the screen. GUI coordinates: origin top-left.
    static Rect BarRect()
    {
        float width = Keys.Length * ButtonWidth + (Keys.Length - 1) * Gap;
        return new Rect((Screen.width - width) / 2f, Top, width, ButtonHeight);
    }

    static Rect ButtonRect(int index) => new(BarRect().x + index * (ButtonWidth + Gap), Top, ButtonWidth, ButtonHeight);

    void OnGUI()
    {
        for (int i = 0; i < buttons.Length; i++)
            if (GUI.Button(ButtonRect(i), new GUIContent(buttons[i].label(), buttons[i].tooltip)))
                buttons[i].action();

        NaturalTimer timer = CurrentTimer();
        string timerText = timer != null ? $"{timer.Elapsed:0.0}/{timer.Duration:0}s  signal {timer.SignalLevel}" : "-";
        Rect bar = BarRect();
        float below = bar.yMax + 4f;
        GUI.Label(new Rect(bar.x, below, bar.width, 22f), $"{Baby.Instance.State}  {timerText}");

        if (string.IsNullOrEmpty(GUI.tooltip))
            return;
        tooltipStyle ??= new GUIStyle(GUI.skin.box) { wordWrap = true, alignment = TextAnchor.UpperLeft };
        var content = new GUIContent(GUI.tooltip);
        float height = tooltipStyle.CalcHeight(content, TooltipWidth);
        float x = Mathf.Clamp(Event.current.mousePosition.x - TooltipWidth / 2f, 0f, Screen.width - TooltipWidth);
        GUI.Box(new Rect(x, below + 22f, TooltipWidth, height), content, tooltipStyle);
    }
}
