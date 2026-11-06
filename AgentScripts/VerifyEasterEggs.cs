using System;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Play Mode check of the easter egg clearing in Main (BuildEasterEggs): skips the intro, then sets off each egg with a virtual
// mouse and reports whether its encyclopedia entry was found. Input devices and the game speed are restored in finally.
// unity command script run --file AgentScripts/VerifyEasterEggs.cs --entry VerifyEasterEggs.Run
public static class VerifyEasterEggs
{
    static Mouse mouse;
    static readonly StringBuilder log = new();

    static async Task<bool> Until(Func<bool> condition, float seconds)
    {
        float end = Time.realtimeSinceStartup + seconds;
        while (!condition() && Time.realtimeSinceStartup < end)
            await Task.Delay(40);
        return condition();
    }

    static bool Found(string file) => Encyclopedia.Instance.Entries.Where(e => e.name == file).Any(e => Encyclopedia.Instance.IsFound(e));

    static async Task FlyOver(Vector3 point)
    {
        BirdController.Instance.Teleport(new Vector3(point.x + 1.5f, BirdController.Instance.transform.position.y, point.z - 1.5f));
        UnityEngine.Object.FindAnyObjectByType<BirdCamera>().Cut();
        await Task.Delay(600);
    }

    static async Task Press(Vector3 world, MouseButton button)
    {
        Vector3 screen = Camera.main.WorldToScreenPoint(world);
        InputSystem.QueueStateEvent(mouse, new MouseState { position = screen });
        await Task.Delay(60);
        InputSystem.QueueStateEvent(mouse, new MouseState { position = screen }.WithButton(button));
        await Task.Delay(60);
        InputSystem.QueueStateEvent(mouse, new MouseState { position = screen });
        await Task.Delay(60);
    }

    // Wheel ticks: up (+1) zooms in, down (-1) out.
    static async Task Scroll(float direction, int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { scroll = new Vector2(0f, 120f * direction) });
            await Task.Delay(80);
            InputSystem.QueueStateEvent(mouse, new MouseState());
            await Task.Delay(80);
        }
    }

    static async Task Check(string name, string entry, Func<Task> act, float seconds)
    {
        if (Found(entry))
        {
            log.AppendLine($"{name}: already found before the check, run it in a fresh Play Mode session");
            return;
        }
        try
        {
            await act();
            bool ok = await Until(() => Found(entry), seconds);
            log.AppendLine($"{name}: {(ok ? "found" : "NOT found")} bird={BirdController.Instance.transform.position} perched={BirdController.Instance.IsPerched}");
        }
        catch (Exception e)
        {
            log.AppendLine($"{name}: ERROR {e.Message}");
        }
    }

    static T Egg<T>() where T : Component => UnityEngine.Object.FindAnyObjectByType<T>();

    static Vector3 PerchClick(Component egg) => egg.GetComponentInChildren<Perchable>().GetComponent<Collider>().bounds.center;

    public static async Task<string> Run()
    {
        log.Clear();
        float oldScale = Time.timeScale;
        var oldFocus = InputSystem.settings.backgroundBehavior;
        var oldEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
        // The Editor is not focused while this runs: the virtual mouse must reach the game anyway.
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        mouse = InputSystem.AddDevice<Mouse>();
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        try
        {
            IntroSequence intro = Egg<IntroSequence>();
            if (IntroSequence.IsPlaying)
            {
                typeof(IntroSequence).GetField("running", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(intro, false);
                intro.StopAllCoroutines();
                intro.StartCoroutine((System.Collections.IEnumerator)typeof(IntroSequence).GetMethod("StartGame", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(intro, null));
            }
            if (!await Until(() => !IntroSequence.IsPlaying, 20f))
                return "intro did not end";
            await Task.Delay(1500);
            Time.timeScale = 2f;

            // Held zoomed all the way in, the bird stands on the ground. The baby is kept asleep so the intro (and its snow) lasts.
            Baby.Instance.HoldAsleep();
            await Check("SnowOnBird", "01_SnowOnBird", async () =>
            {
                await FlyOver(Egg<SnowWhirl>().transform.position + new Vector3(3f, 0f, 3f));
                await Scroll(1f, 6);
            }, 20f);
            await Scroll(-1f, 6);

            await Check("StumpLadybug", "02_StumpLadybug", async () =>
            {
                var egg = Egg<StumpLadybug>();
                await FlyOver(egg.transform.position);
                await Press(PerchClick(egg), MouseButton.Left);
            }, 15f);

            await Check("SnowWhirl", "03_SnowWhirl", async () =>
            {
                var egg = Egg<SnowWhirl>();
                await FlyOver(egg.transform.position);
                await Press(PerchClick(egg), MouseButton.Left);
            }, 15f);
            bool backInControl = await Until(() => !BirdController.Instance.IsPerchedOrLanding && typeof(BirdController)
                .GetField("control", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(BirdController.Instance).ToString() == "Game", 10f);
            log.AppendLine("SnowWhirl: bird back in game control = " + backInControl);

            await Check("SnowFlower", "04_SnowFlower", async () =>
            {
                var egg = Egg<SnowFlower>();
                await FlyOver(egg.transform.position);
                await Press(PerchClick(egg), MouseButton.Left);
            }, 15f);

            await Check("IcicleXylophone", "05_IcicleXylophone", async () =>
            {
                var egg = Egg<IcicleXylophone>();
                await FlyOver(egg.transform.position);
                Collider icicle = egg.GetComponentsInChildren<BoxCollider>().First(c => c.name.StartsWith("Cylinder"));
                await Press(icicle.bounds.center, MouseButton.Left);
            }, 10f);

            await Check("HollowSquirrel", "06_HollowSquirrel", async () =>
            {
                var egg = Egg<HollowSquirrel>();
                await FlyOver(egg.transform.position);
                await Press(egg.transform.position, MouseButton.Right);
            }, 10f);

            await Check("CornelianCherry", "07_CornelianCherry", async () =>
            {
                var egg = Egg<CornelianCherry>();
                await FlyOver(egg.transform.position);
                await Press(PerchClick(egg), MouseButton.Left);
            }, 15f);

            await Check("CliffEcho", "08_CliffEcho", async () =>
            {
                var egg = Egg<CliffEcho>();
                await FlyOver(egg.transform.position);
                await Task.Delay(1200);
                await Press(egg.transform.position, MouseButton.Right);
            }, 10f);

            await Check("MigratingBirds", "10_MigratingBirds", async () =>
            {
                await Scroll(-1f, 8);
            }, 25f);
        }
        finally
        {
            Time.timeScale = oldScale;
            InputSystem.settings.backgroundBehavior = oldFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = oldEditorInput;
            InputSystem.RemoveDevice(mouse);
        }
        return log.ToString();
    }
}
