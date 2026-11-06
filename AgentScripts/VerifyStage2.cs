using System;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

// Stage 2's rabbit burrow (BuildStage2). Start Play Mode at Stage 2 first (Prepare, then enter Play Mode), then run one scenario:
// unity command script run --file AgentScripts/VerifyStage2.cs --entry VerifyStage2.Prepare
// unity command script run --file AgentScripts/VerifyStage2.cs --entry VerifyStage2.Natural   (or Cry, Clear, Both, Partial, Early)
public static class VerifyStage2
{
    public static string Prepare()
    {
        UnityEditor.SessionState.SetInt(GameFlow.StartStageKey, 2);
        return "Next Play Mode starts at stage 2";
    }

    static RabbitBurrow Burrow => UnityEngine.Object.FindAnyObjectByType<RabbitBurrow>();
    static T Field<T>(string name) => (T)typeof(RabbitBurrow).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Burrow);
    static Vector3 Hole => Field<Transform>("hole").position;
    static float Dug => Field<float>("dug");
    static void Cry() => CryListener.Broadcast(Hole + new Vector3(2f, 3f, -2f));
    static void Away() => BirdController.Instance.Teleport(new Vector3(2f, 3f, Baby.Instance.transform.position.z - 4f));

    static async Task Until(Func<bool> condition, float timeout, string error)
    {
        float end = Time.realtimeSinceStartup + timeout;
        while (!condition() && Time.realtimeSinceStartup < end) await Task.Delay(20);
        if (!condition()) throw new Exception(error);
    }

    static async Task<string> Scenario(string name, bool early, Func<Task> help, bool expectByBird, float expectDig)
    {
        var log = new System.Text.StringBuilder(name + ": ");
        float oldScale = Time.timeScale;
        try
        {
            Time.timeScale = 2f;
            await Until(() => GameFlow.Instance.CurrentStage.name == "Stage2" && Baby.Instance.State != BabyState.Sleeping, 20f, "Not walking in Stage2");
            Away();
            RabbitBurrow burrow = Burrow;
            if (!early)
                await Until(() => Baby.Instance.CurrentBlocker == burrow, 40f, "Baby never waited at the burrow (" + Baby.Instance.State + ")");
            await help();
            float start = Time.time;
            await Until(() => burrow.Timer.IsResolved, 60f, "Never dug through (dug " + Dug.ToString("F2") + ")");
            float dig = Time.time - start;
            log.Append($"through after {dig:F1} s (expected ~{expectDig}), ");
            if (Mathf.Abs(dig - expectDig) > 1f) throw new Exception(log + "wrong time");
            await Until(() => burrow.IsOpen, 20f, "Never opened");
            log.Append($"open {Time.time - start - dig:F1} s later, byBird {burrow.OpenedByBird}, ");
            if (burrow.OpenedByBird != expectByBird) throw new Exception(log + "wrong byBird");
            bool waited = false, lookedUp = false;
            float pass = Field<Transform>("hole").position.z + 2f;
            float end = Time.realtimeSinceStartup + 40f;
            while (Baby.Instance.transform.position.z < pass && Time.realtimeSinceStartup < end)
            {
                waited |= Baby.Instance.State == BabyState.Waiting;
                lookedUp |= Baby.Instance.State == BabyState.Reacting;
                await Task.Delay(20);
            }
            if (Baby.Instance.transform.position.z < pass) throw new Exception(log + "baby did not walk on (" + Baby.Instance.State + ")");
            foreach (BurrowRabbit r in Field<BurrowRabbit[]>("roadRabbits"))
                if (r.gameObject.activeSelf) throw new Exception(log + r.name + " still on the road");
            foreach (BurrowRabbit r in Field<BurrowRabbit[]>("burrowRabbits"))
                if (r.gameObject.activeSelf) throw new Exception(log + r.name + " still out");
            if (Field<Perchable>("snowPerch").CanPerch) throw new Exception(log + "snow still perchable");
            log.Append($"baby walked on (reacted {lookedUp}), rabbits all in. PASS");
        }
        finally { Time.timeScale = oldScale; }
        return log.ToString();
    }

    static Task Perch() { BirdController.Instance.PerchOn(Field<Perchable>("snowPerch")); return Task.CompletedTask; }
    static float Landing => GameTuning.Current.birdPerchDuration;

    public static Task<string> Natural() => Scenario("Natural", false, () => Task.CompletedTask, false, GameTuning.Current.rabbitBurrowTime);
    public static Task<string> CryOnly() => Scenario("Cry", false, () => { Cry(); return Task.CompletedTask; }, true, 8f);
    public static Task<string> Both() => Scenario("Both", false, async () =>
    {
        await Perch();
        await Until(() => BirdController.Instance.IsPerched, 20f, "Bird never landed");
        Cry();
    }, true, 4f);
    public static Task<string> Clear() => Scenario("Clear", false, async () =>
    {
        await Perch();
        await Until(() => BirdController.Instance.IsPerched, 20f, "Bird never landed");
    }, true, 8f);
    // Half cleared by the bird, which then leaves: what it dug stays. A cry then digs the other half.
    public static Task<string> Partial() => Scenario("Partial", false, async () =>
    {
        await Perch();
        await Until(() => BirdController.Instance.IsPerched, 20f, "Bird never landed");
        await Until(() => Dug >= 0.5f, 20f, "Not half dug");
        Away();
        float kept = Dug;
        await Task.Delay(1500);
        if (Mathf.Abs(Dug - kept) > 0.01f) throw new Exception("Dug changed without anyone digging: " + kept + " -> " + Dug);
        Cry();
    }, true, 4f);
    // The bird calls the rabbits before the baby comes: she goes by without waiting.
    public static Task<string> Early() => Scenario("Early", true, () => { Cry(); return Task.CompletedTask; }, true, 8f);
}
