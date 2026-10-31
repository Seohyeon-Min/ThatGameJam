using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Snow shelter in Main's intro: hold a virtual mouse on the walking baby, check the bird, shadow, snow and gaze, let go,
// then hold until she reaches the sunlit spot. Run in Play Mode right after entering it:
// unity command script run --file AgentScripts/VerifySnowShelter.cs --entry VerifySnowShelter.Run
public static class VerifySnowShelter
{
    static Mouse mouse;

    static async Task Until(Func<bool> condition, float timeout, string error)
    {
        float end = Time.realtimeSinceStartup + timeout;
        while (!condition() && Time.realtimeSinceStartup < end) await Task.Delay(30);
        if (!condition()) throw new Exception(error);
    }

    static Vector2 OnBaby() => Camera.main.WorldToScreenPoint(Baby.Instance.GetComponent<Collider>().bounds.center);

    static async Task Press(bool down)
    {
        var state = new MouseState { position = OnBaby() };
        if (down) state = state.WithButton(MouseButton.Left);
        InputSystem.QueueStateEvent(mouse, state);
        await Task.Delay(120);
    }

    // Keeps the pointer on the baby while held, as a player tracking her would.
    static async Task Hold(float seconds, Func<bool> stop = null)
    {
        float end = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < end && (stop == null || !stop()))
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = OnBaby() }.WithButton(MouseButton.Left));
            await Task.Delay(50);
        }
    }

    // Snowflakes over 1 s in a column around her, against 4 columns of the same size 2.5 m away, while still held.
    static async Task<(int, int)> SnowAround(ParticleSystem snow, Transform baby)
    {
        var particles = new ParticleSystem.Particle[snow.main.maxParticles];
        Vector2[] offsets = { new(2.5f, 0f), new(-2.5f, 0f), new(0f, 2.5f), new(0f, -2.5f) };
        int inside = 0, beside = 0;
        for (int frame = 0; frame < 20; frame++)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = OnBaby() }.WithButton(MouseButton.Left));
            await Task.Delay(50);
            int count = snow.GetParticles(particles);
            for (int i = 0; i < count; i++)
            {
                Vector3 d = particles[i].position - baby.position;
                if (d.y < 0f || d.y > 3.5f) continue;
                var flat = new Vector2(d.x, d.z);
                if (flat.magnitude < 0.6f) inside++;
                beside += offsets.Count(o => (flat - o).magnitude < 0.6f);
            }
        }
        return (inside, beside);
    }

    public static async Task<string> Run()
    {
        mouse = InputSystem.AddDevice<Mouse>();
        var oldBackground = InputSystem.settings.backgroundBehavior;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        float oldScale = Time.timeScale;
        var log = new System.Text.StringBuilder();
        try
        {
            var bird = BirdController.Instance;
            var baby = Baby.Instance;
            if (GameFlow.Instance.CurrentStage.name != "Intro") throw new Exception("Not in Intro");
            var shelter = UnityEngine.Object.FindAnyObjectByType<SnowShelter>();
            var so = new UnityEditor.SerializedObject(shelter);
            var shadow = (Transform)so.FindProperty("shadowCaster").objectReferenceValue;
            var block = (Transform)so.FindProperty("snowBlock").objectReferenceValue;
            var snow = GameObject.Find("World/Snowfall").GetComponent<ParticleSystem>();
            await Task.Delay(500);
            if (shadow.gameObject.activeSelf || block.gameObject.activeSelf) throw new Exception("Shelter visible before any click");

            // Asleep: a click on her is an ordinary click.
            await Press(true); await Press(false);
            if (bird.IsSheltering) throw new Exception("Sheltered a sleeping baby");
            log.Append("PASS asleep click is not sheltering. ");

            baby.WakeTimer.ResolveByBird();
            await Until(() => baby.State == BabyState.Walking, 25f, "Baby never walks");
            await Task.Delay(1500);
            bird.Teleport(baby.transform.position + new Vector3(6f, 4f, -4f));
            await Press(true);
            if (!bird.IsSheltering) throw new Exception("Holding on the walking baby did not shelter");
            if (shadow.gameObject.activeSelf || block.gameObject.activeSelf) throw new Exception("Shelter on before the bird reached her");
            await Hold(4f);
            Vector3 forward = Vector3.ProjectOnPlane(baby.transform.forward, Vector3.up).normalized;
            Vector3 spot = baby.transform.position + forward * GameTuning.Current.birdShelterAhead;
            float gap = Vector2.Distance(new Vector2(bird.transform.position.x, bird.transform.position.z), new Vector2(spot.x, spot.z));
            if (gap > 0.35f) throw new Exception("Bird not over the spot ahead of the baby, gap " + gap);
            Vector3 b0 = bird.transform.position, g0 = baby.transform.position;
            await Hold(1f);
            float birdMoved = Vector3.ProjectOnPlane(bird.transform.position - b0, Vector3.up).magnitude;
            float babyMoved = Vector3.ProjectOnPlane(baby.transform.position - g0, Vector3.up).magnitude;
            if (babyMoved > 0.3f && Mathf.Abs(birdMoved - babyMoved) > 0.35f * babyMoved) throw new Exception($"Pace differs: bird {birdMoved:0.00} baby {babyMoved:0.00}");
            if (!shadow.gameObject.activeSelf || shadow.localScale.x < 0.5f) throw new Exception("No shadow while sheltering");
            if (!block.gameObject.activeSelf) throw new Exception("No snow block while sheltering");
            var (inside, beside) = await SnowAround(snow, baby.transform);
            if (beside == 0 || inside * 10 > beside) throw new Exception($"Snow still over her: {inside} near her vs {beside} in 4 columns beside");
            Transform head = baby.GetComponentInChildren<Animator>().transform.Find("NPC-Girl-Rig/ROOT/Hips/Spine/Chest/Neck/Head");
            float headToBird = Vector3.Angle(head.forward, bird.transform.position - head.position);
            if (headToBird > 30f) throw new Exception("Not looking at the bird: " + headToBird + " deg");
            log.Append($"PASS sheltering: gap {gap:0.00}, pace bird {birdMoved:0.00}/baby {babyMoved:0.00}, snow inside {inside} vs beside {beside}, head-to-bird {headToBird:0}deg. ");

            await Press(false);
            if (bird.IsSheltering) throw new Exception("Still sheltering after release");
            await Task.Delay(600);
            if (shadow.gameObject.activeSelf || block.gameObject.activeSelf) throw new Exception("Shadow or block left after release");
            log.Append("PASS release clears. ");

            Time.timeScale = 3f;
            await Press(true);
            await Hold(60f, () => baby.State == BabyState.Resting);
            if (baby.State != BabyState.Resting) throw new Exception("Baby never reached the sunlit spot");
            await Hold(0.5f);
            if (bird.IsSheltering) throw new Exception("Sheltering past the sunlit spot");
            await Press(false);
            await Task.Delay(600);
            if (shadow.gameObject.activeSelf || block.gameObject.activeSelf) throw new Exception("Shadow or block left at the sunlit spot");
            log.Append("PASS ends at the sunlit spot.");
            return log.ToString();
        }
        finally
        {
            Time.timeScale = oldScale;
            InputSystem.RemoveDevice(mouse);
            InputSystem.settings.backgroundBehavior = oldBackground;
        }
    }
}
