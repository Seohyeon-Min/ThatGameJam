using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

// The girl at the sunlit spots (BuildGirlPlay): with the bird far she leaves the light and plays, with the bird near she goes back
// and sleeps, and Zs rise off her head only while she lies fully down. Run in Play Mode right after entering it:
// unity command script run --file AgentScripts/VerifyGirlPlay.cs --entry VerifyGirlPlay.Run
public static class VerifyGirlPlay
{
    static async Task Until(Func<bool> condition, float timeout, string error, Action each = null)
    {
        float end = Time.realtimeSinceStartup + timeout;
        while (!condition() && Time.realtimeSinceStartup < end) { each?.Invoke(); await Task.Delay(50); }
        if (!condition()) throw new Exception(error);
    }

    static Transform End => GameFlow.Instance.CurrentStage.route[GameFlow.Instance.CurrentStage.route.Count - 1];
    static float Flat(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
    static void PutBird(Vector3 at) => BirdController.Instance.Teleport(new Vector3(at.x, GameFlow.Instance.CurrentStage.birdAltitude, at.z));
    static bool Zzz => ((ParticleSystem)typeof(Baby).GetField("sleepZzz", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Baby.Instance)).isEmitting;
    static string AnimState()
    {
        var animator = Baby.Instance.GetComponentInChildren<Animator>();
        var info = animator.GetCurrentAnimatorStateInfo(0);
        foreach (string n in new[] { "PlaySnow", "PlayTree", "Run", "Walk", "Idle", "Hum", "Stretch", "LieDown", "Sleep", "GetUp", "IntroGetUp", "IntroSleep" })
            if (info.shortNameHash == Animator.StringToHash(n)) return n;
        return "?";
    }

    public static async Task<string> Run()
    {
        var log = new System.Text.StringBuilder();
        float oldScale = Time.timeScale;
        try
        {
            var intro = UnityEngine.Object.FindAnyObjectByType<IntroSequence>();
            if (IntroSequence.IsPlaying)
            {
                typeof(IntroSequence).GetField("running", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(intro, false);
                intro.StopAllCoroutines();
                intro.StartCoroutine((System.Collections.IEnumerator)typeof(IntroSequence).GetMethod("StartGame", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(intro, null));
                await Until(() => !IntroSequence.IsPlaying, 30f, "Intro did not end");
            }
            Baby baby = Baby.Instance;
            if (GameFlow.Instance.CurrentStage.name != "Intro") throw new Exception("Not in Intro");
            await Until(() => Zzz, 5f, "No Zs while asleep in the intro");
            log.AppendLine("PASS Zs while asleep (" + baby.State + ", " + AnimState() + ")");
            Time.timeScale = 3f;

            // Far: the bird stays back at the stage's start, 20 m behind the light.
            Vector3 far = GameFlow.Instance.CurrentStage.babyStart.position + new Vector3(2f, 0f, 2f);
            PutBird(far);
            baby.WakeTimer.ResolveByBird();
            await Until(() => !Zzz, 15f, "Zs still on after getting up", () => PutBird(far));
            log.AppendLine("PASS Zs stop as she gets up (" + AnimState() + ")");
            await Until(() => baby.State == BabyState.Playing, 60f, "She never started playing (" + baby.State + ")", () => PutBird(far));
            log.AppendLine("PASS playing with the bird " + Flat(BirdController.Instance.transform.position, baby.transform.position).ToString("F1") + " m away");
            var seen = new HashSet<string>();
            float maxOff = 0f;
            float until = Time.realtimeSinceStartup + 30f;
            while (Time.realtimeSinceStartup < until)
            {
                PutBird(far);
                seen.Add(AnimState());
                maxOff = Mathf.Max(maxOff, Flat(baby.transform.position, End.position));
                if (baby.State != BabyState.Playing) throw new Exception("Stopped playing with the bird far: " + baby.State);
                await Task.Delay(100);
            }
            log.AppendLine($"PASS played 90 s game time, states {string.Join(",", seen)}, up to {maxOff:F1} m off the light");
            if (maxOff < 2f) throw new Exception("She never left the light");

            // Near: the bird comes to her.
            PutBird(baby.transform.position + new Vector3(1.5f, 0f, -1.5f));
            await Until(() => baby.State == BabyState.Resting, 30f, "She never lay down with the bird near (" + baby.State + ")");
            float off = Flat(baby.transform.position, End.position);
            if (off > 0.2f) throw new Exception("Lay down " + off + " m off the light");
            log.AppendLine("PASS back at the light and lying down");
            await Until(() => Zzz, 15f, "No Zs once lying");
            log.AppendLine("PASS Zs once lying (" + AnimState() + ")");
            await Until(() => GameFlow.Instance.CurrentStage.name == "Stage1" && baby.State == BabyState.Sleeping, 20f, "Next stage did not start");
            if (!Zzz) throw new Exception("No Zs asleep at the next stage's start");
            log.AppendLine("PASS Zs on into Stage1");

            // Near all the way: no play.
            baby.WakeTimer.ResolveByBird();
            bool played = false;
            await Until(() => baby.State == BabyState.Resting, 120f, "Stage1: never reached the light (" + baby.State + ")", () =>
            {
                PutBird(baby.transform.position + new Vector3(1.5f, 0f, -1.5f));
                played |= baby.State == BabyState.Playing;
            });
            if (played) throw new Exception("Played with the bird near");
            log.AppendLine("PASS Stage1 with the bird near: straight to sleep");
        }
        finally
        {
            Time.timeScale = oldScale;
        }
        return log.ToString();
    }
}
