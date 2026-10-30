using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
public static class VerifyStage1Logs
{
    public static async Task<string> Play()
    {
        var mouse = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();
        var oldFocus = UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior;
        UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
        float oldScale = Time.timeScale;
        try
        {
            Time.timeScale = 3f;
            GameFlow.Instance.SkipToNextStage();
            var stage = GameFlow.Instance.CurrentStage;
            var log = stage.GetComponentInChildren<LeaningLog>();
            var top = log.GetComponentInChildren<Perchable>();
            BirdController.Instance.Teleport(new Vector3(0f, 2.5f, 16f));
            var cam = UnityEngine.Object.FindAnyObjectByType<BirdCamera>();
            cam.Cut();
            int frame = Time.frameCount;
            await Task.Delay(600);
            if (Time.frameCount <= frame) throw new Exception("Play Mode is not advancing");
            var animator = log.GetComponentInChildren<Animator>();
            if (!animator.GetCurrentAnimatorStateInfo(0).IsName("Idle")) throw new Exception("Idle is not playing");
            Vector3 hitPoint = top.PerchPoint.position - Vector3.up * 0.3f;
            Vector3 screen = Camera.main.WorldToScreenPoint(hitPoint);
            Physics.SyncTransforms();
            if (!Physics.Raycast(Camera.main.ScreenPointToRay(screen), out RaycastHit hit, 100f, LayerMask.GetMask("Ground", "Interactable", "ClickPlane")))
                throw new Exception("Click ray missed");
            if (hit.collider.GetComponentInParent<Perchable>() != top) throw new Exception("Click blocked by " + hit.collider.name);
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState { position = screen });
            await Task.Delay(100);
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState { position = screen }.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left));
            await Task.Delay(100);
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState { position = screen });
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!log.Timer.IsResolved && Time.realtimeSinceStartup < deadline) await Task.Delay(50);
            if (!log.Timer.ResolvedByBird) throw new Exception("Bird click did not resolve log");
            if (log.IsOpen) throw new Exception("Opened before the break animation");
            await Task.Delay(200);
            if (!animator.GetCurrentAnimatorStateInfo(0).IsName("Break")) throw new Exception("Break is not playing");
            if (BirdController.Instance.IsPerched || top.CanPerch) throw new Exception("Bird not released");
            deadline = Time.realtimeSinceStartup + 4f;
            while (!log.IsOpen && Time.realtimeSinceStartup < deadline) await Task.Delay(50);
            if (!log.IsOpen) throw new Exception("Break never opened bridge");
            string result = "PASS advancing frames; virtual mouse click; Idle->Break; bird release; opens only after break.\n";
            result += "Fallen trunk " + log.transform.Find("broken tree/Tube").GetComponent<Renderer>().bounds;
            // Walk the real authored stage route (wake timer naturally resolves), until stage 2 starts.
            deadline = Time.realtimeSinceStartup + 25f;
            while (GameFlow.Instance.CurrentStage == stage && Time.realtimeSinceStartup < deadline) await Task.Delay(100);
            if (GameFlow.Instance.CurrentStage == stage) throw new Exception("Baby failed to cross and reach stage 2: " + Baby.Instance.State + " at " + Baby.Instance.transform.position);
            result += "\nPASS baby crosses both models and stage advances.";
            System.IO.File.WriteAllText("Temp/log-bird-test.txt", result);
            return result;
        }
        finally
        {
            Time.timeScale = oldScale;
            UnityEngine.InputSystem.InputSystem.RemoveDevice(mouse);
            UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior = oldFocus;
        }
    }

    public static async Task<string> Natural()
    {
        float oldScale = Time.timeScale;
        try
        {
            Time.timeScale = 3f;
            GameFlow.Instance.SkipToNextStage();
            var stage = GameFlow.Instance.CurrentStage;
            var log = stage.GetComponentInChildren<LeaningLog>();
            var animator = log.GetComponentInChildren<Animator>();
            BirdController.Instance.Teleport(new Vector3(1f, 2.5f, 16f));
            UnityEngine.Object.FindAnyObjectByType<BirdCamera>().Cut();
            float deadline = Time.realtimeSinceStartup + 25f;
            while (Baby.Instance.CurrentBlocker != log && Time.realtimeSinceStartup < deadline) await Task.Delay(100);
            if (Baby.Instance.CurrentBlocker != log) throw new Exception("Baby never waited at the log");
            if (log.Timer.Elapsed > 1f || !log.Timer.IsRunning) throw new Exception("Timer did not start at baby arrival");
            deadline = Time.realtimeSinceStartup + 14f;
            while (!log.Timer.IsResolved && Time.realtimeSinceStartup < deadline) await Task.Delay(50);
            if (!log.Timer.IsResolved || log.Timer.ResolvedByBird) throw new Exception("Natural timer failed");
            if (log.IsOpen) throw new Exception("Natural solution opened during break");
            await Task.Delay(150);
            if (!animator.GetCurrentAnimatorStateInfo(0).IsName("Break")) throw new Exception("Natural break is not playing");
            deadline = Time.realtimeSinceStartup + 4f;
            while (!log.IsOpen && Time.realtimeSinceStartup < deadline) await Task.Delay(50);
            if (!log.IsOpen || log.OpenedByBird) throw new Exception("Natural solution did not open");
            var final = log.transform.Find("broken tree/Tube").localToWorldMatrix;
            await Task.Delay(1500);
            var held = log.transform.Find("broken tree/Tube").localToWorldMatrix;
            if (final != held) throw new Exception("Final pose did not hold");
            deadline = Time.realtimeSinceStartup + 12f;
            while (GameFlow.Instance.CurrentStage == stage && Time.realtimeSinceStartup < deadline) await Task.Delay(100);
            if (GameFlow.Instance.CurrentStage == stage) throw new Exception("Baby stuck after natural solution");
            string result = "PASS baby waits; timer starts on arrival; natural 30s solution; Break; delayed Open; final pose holds; baby crosses to stage 2.";
            System.IO.File.WriteAllText("Temp/log-natural-test.txt", result);
            return result;
        }
        finally { Time.timeScale = oldScale; }
    }
}
