
using System;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Play Mode check in Main: the bird pecks every icicle under a branch from that branch's one perch, and goes to the other branch
// for its icicles. Input devices and the game speed are restored in finally.
// unity command script run --file AgentScripts/VerifyIcicleBranches.cs --entry VerifyIcicleBranches.Run
public static class VerifyIcicleBranches
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static Mouse mouse;
    static readonly StringBuilder log = new();

    static async Task<bool> Until(Func<bool> condition, float seconds)
    {
        float end = Time.realtimeSinceStartup + seconds;
        while (!condition() && Time.realtimeSinceStartup < end)
            await Task.Delay(40);
        return condition();
    }

    static async Task Press(Vector3 world)
    {
        Vector3 screen = Camera.main.WorldToScreenPoint(world);
        InputSystem.QueueStateEvent(mouse, new MouseState { position = screen });
        await Task.Delay(60);
        InputSystem.QueueStateEvent(mouse, new MouseState { position = screen }.WithButton(MouseButton.Left));
        await Task.Delay(60);
        InputSystem.QueueStateEvent(mouse, new MouseState { position = screen });
        await Task.Delay(60);
    }

    static async Task Tap(IcicleXylophone egg, string icicle, string branch)
    {
        BirdController bird = BirdController.Instance;
        var swings = (Coroutine[])typeof(IcicleXylophone).GetField("swings", Private).GetValue(egg);
        var body = egg.GetComponentsInChildren<BoxCollider>().First(c => c.name == icicle);
        var icicles = (Array)typeof(IcicleXylophone).GetField("icicles", Private).GetValue(egg);
        int index = Enumerable.Range(0, icicles.Length).First(i =>
            (Collider)icicles.GetValue(i).GetType().GetField("body").GetValue(icicles.GetValue(i)) == body);
        await Until(() => !(bool)typeof(IcicleXylophone).GetField("pecking", Private).GetValue(egg) && swings[index] == null, 5f);
        bool wasThere = bird.IsPerched && bird.Perch != null && bird.Perch.name == branch;
        Vector3 before = bird.transform.position;
        await Press(body.bounds.center);
        bool perched = await Until(() => bird.IsPerched && bird.Perch != null && bird.Perch.name == branch, 10f);
        bool rang = await Until(() => swings[index] != null, 4f);
        Vector3 toIcicle = Vector3.ProjectOnPlane(body.bounds.center - bird.transform.position, Vector3.up);
        float off = Vector3.Angle(Vector3.ProjectOnPlane(bird.transform.forward, Vector3.up), toIcicle);
        log.AppendLine($"{icicle}: perched on {branch}={perched} rang={rang} was already there={wasThere} moved={(bird.transform.position - before).magnitude:0.00} m facing off by {off:0}°");
    }

    public static async Task<string> Run()
    {
        log.Clear();
        float oldScale = Time.timeScale;
        var oldFocus = InputSystem.settings.backgroundBehavior;
        var oldEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        mouse = InputSystem.AddDevice<Mouse>();
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        try
        {
            IntroSequence intro = UnityEngine.Object.FindAnyObjectByType<IntroSequence>();
            if (IntroSequence.IsPlaying)
            {
                typeof(IntroSequence).GetField("running", Private).SetValue(intro, false);
                intro.StopAllCoroutines();
                intro.StartCoroutine((System.Collections.IEnumerator)typeof(IntroSequence).GetMethod("StartGame", Private).Invoke(intro, null));
            }
            if (!await Until(() => !IntroSequence.IsPlaying, 20f))
                return "intro did not end";
            await Task.Delay(1500);
            Time.timeScale = 2f;
            Baby.Instance.HoldAsleep();
            // The xylophone is in stage 2.
            GameFlow.Instance.SkipToNextStage();
            await Task.Delay(4000);

            var egg = UnityEngine.Object.FindAnyObjectByType<IcicleXylophone>();
            Vector3 p = egg.transform.position;
            BirdController.Instance.Teleport(new Vector3(p.x + 1.5f, BirdController.Instance.transform.position.y, p.z - 1.5f));
            UnityEngine.Object.FindAnyObjectByType<BirdCamera>().Cut();
            await Task.Delay(600);

            foreach (string icicle in new[] { "Cylinder 3", "Cylinder 2", "Cylinder 1", "Cylinder" })
                await Tap(egg, icicle, "Branch_Right");
            foreach (string icicle in new[] { "Cylinder 4", "Cylinder 5", "Cylinder 7", "Cylinder 6" })
                await Tap(egg, icicle, "Branch_Left");
        }
        catch (Exception e)
        {
            log.AppendLine($"ERROR {e}");
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