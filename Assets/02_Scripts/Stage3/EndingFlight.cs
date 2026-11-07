using System.Collections;
using System.Collections.Generic;
using FMODUnity;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;

// The ending (3-3), started by CliffEnding once the bird has flown out past the cliff. Its shots cut on what the bird does, not on
// a clock, so it is a script rather than a Timeline; the cameras and the paths are objects under it, to move about in the scene.
// 1. The player lets go: the bird flies on by itself, out over the drop and up, beating its wings, until it has left the screen;
//    a moment after (Out Wait, very short), the next shot.
// 2. Rise Shot: up in the air, looking back down at the cliff and the girl. The bird comes up along Rise Path, a rising curve, beating
//    its wings hard, past the camera and on above it. A moment after it has passed, the next shot.
// 3. Far Shot: from far off to the side of the cliff, the bird, small, climbing slowly away along Far Path for Far Seconds, only
//    just reaching the top of the picture as it ends.
// 4. Top Shot: just over the top of the clouds, looking across them. A moment of clouds only, then the bird bursts up through
//    them (Rise Curve: slow in the cloud, a rush out of it, easing over the top), hovers there a moment, its wings slowing as its strength goes, and lets itself fall straight back in where it came out (the dive's falling loop).
//    The camera tilts after it a beat late, a little, up and down only, never down below the clouds; it dips as the bird dives and springs back. Where
//    the bird goes in, the clouds swell in a ring that spreads out (Wave Speed), and the camera rides up and down on it as it passes under.
// 5. Low Shot: down by the girl's feet, looking up past her at the clouds from below. Over them a gap opens from nothing, the
//    clouds there drawing back, and thin shafts of light come down through it one after another, each brightening slowly.
// 6. Sleep Shot: back by her, from in front. She turns her head to the light that has come down beside her (Sleep Shaft), blinks
//    twice, walks into it and lies down there to sleep. All the while the camera draws away from her, back and a little up, still
//    looking at her and turning a little from left to right: slowly at first, then, as she lies down, faster and climbing more,
//    to look down over the ground round her (where the flowers come up), on until it eases to a stop high off. It ends there.
public class EndingFlight : MonoBehaviour
{
    [Header("Flying out")]
    [Tooltip("Metres per second, out over the drop and up, until it is off the screen.")]
    [SerializeField, Min(0.1f)] float outSpeed = 5f;
    [Tooltip("How steeply it climbs as it flies out, in degrees above level; its body tilts the same way.")]
    [SerializeField, Range(0f, 80f)] float outClimb = 35f;
    [Tooltip("Seconds after it has left the screen before the next shot.")]
    [SerializeField, Min(0f)] float outWait = 0.08f;

    [Tooltip("The watercolor look from the first shot on (Look_0), with its Volume Profile. Empty: the stage's own stays.")]
    [SerializeField] WatercolorLook look;

    [Header("Rise Shot")]
    [SerializeField] CinemachineCamera riseShot;
    [Tooltip("Children in order: where the bird starts below the camera, round and up, to past and above it. Curved through them.")]
    [SerializeField] Transform risePath;
    [SerializeField, Min(0.1f)] float riseSpeed = 3.6f;
    [Tooltip("Seconds after it has flown past the camera before the next shot.")]
    [SerializeField, Min(0f)] float passWait = 0.5f;

    [Header("Wingbeats")]
    [Tooltip("Seconds one wingbeat takes (the flap clip, Bird_Flap).")]
    [SerializeField, Min(0.1f)] float beatSeconds = 0.8f;
    [Tooltip("Wingbeats in a row, picked between these, before a glide.")]
    [SerializeField] Vector2Int beatsInRow = new(1, 3);
    [Tooltip("Seconds of gliding between rows of wingbeats, picked between these.")]
    [SerializeField] Vector2 glideSeconds = new(0.25f, 0.7f);
    [Tooltip("Share of the body's upward tilt taken back off the wings, so its strokes push down more than back as it climbs.")]
    [SerializeField, Range(0f, 1f)] float strokeDown = 0.5f;

    [Header("Far Shot")]
    [SerializeField] CinemachineCamera farShot;
    [Tooltip("Children in order: the bird's way up, seen from far off.")]
    [SerializeField] Transform farPath;
    [Tooltip("Slow, so it only just reaches the top of the picture as the shot ends.")]
    [SerializeField, Min(0.1f)] float farSpeed = 2.7f;
    [Tooltip("The bird's size in this shot, a share of its own: small and lonely against the sky.")]
    [SerializeField, Range(0.1f, 1f)] float farBirdScale = 0.5f;
    [Tooltip("Clouds just over the top of the picture, the bird climbing up into them. Off until this shot.")]
    [SerializeField] GameObject farClouds;
    [SerializeField, Min(0f)] float farSeconds = 3f;

    [Header("Top Shot")]
    [SerializeField] CinemachineCamera topShot;
    [Tooltip("The cloud sea it looks across. Off until this shot; the far shot's clouds go off as it comes on.")]
    [SerializeField] GameObject topClouds;
    [Tooltip("On the clouds' top, where the bird bursts out and dives back in.")]
    [SerializeField] Transform cloudSurface;
    [Tooltip("Seconds of clouds only before the bird comes up.")]
    [SerializeField, Min(0f)] float topLead = 0.8f;
    [Tooltip("Metres below the top it starts (inside the clouds), and above it the climb ends.")]
    [SerializeField, Min(0f)] float riseFrom = 6f;
    [SerializeField, Min(0f)] float riseHeight = 3f;
    [SerializeField, Min(0.1f)] float riseSeconds = 1.3f;
    [Tooltip("How far up (0 to 1) it is over the climb's time: slow in the cloud, a rush out, easing (and a touch over) at the top.")]
    [SerializeField] AnimationCurve riseCurve = new(
        new Keyframe(0f, 0f, 0f, 0.3f), new Keyframe(0.3f, 0.18f, 1.6f, 1.6f), new Keyframe(0.6f, 0.92f, 1.1f, 1.1f),
        new Keyframe(0.82f, 1.03f, 0f, 0f), new Keyframe(1f, 1f, 0f, 0f));
    [Tooltip("Seconds sooner the top shot cuts away than when the wave would have died down.")]
    [SerializeField, Min(0f)] float topEndSooner = 0.5f;
    [Tooltip("Seconds it hovers at the top where it climbed to, its wings beating on the spot slower and slower (rate from, to), sinking a little (metres), before it lets itself fall; and the seconds it takes to go over into the falling loop.")]
    [SerializeField, Min(0f)] float apexPause = 1.4f;
    [SerializeField, Min(0f)] float hoverRateFrom = 1.3f;
    [SerializeField, Min(0f)] float hoverRateTo = 0.35f;
    [SerializeField, Min(0f)] float hoverSink = 0.35f;
    [SerializeField, Min(0f)] float fallBlend = 0.35f;
    [SerializeField, Min(0.1f)] float diveSeconds = 0.75f;
    [Tooltip("Metres below the top the dive takes it, into the clouds.")]
    [SerializeField, Min(0f)] float diveDepth = 4f;
    [Tooltip("Degrees its body tilts down as it dives, over what the dive clip already has (the clip is already nose down).")]
    [SerializeField, Range(0f, 60f)] float divePitch = 30f;
    [Tooltip("How far down (0 to 1) it is over the dive's time: slow off the top, faster and faster.")]
    [SerializeField] AnimationCurve diveCurve = new(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(1f, 1f, 2.6f, 0f));
    [Tooltip("Seconds the camera is behind the bird, following it.")]
    [SerializeField, Min(0.01f)] float followLag = 0.35f;
    [Tooltip("How much the camera tilts up or down toward the bird (0 = not at all, 1 = right onto it); it never turns to the sides.")]
    [SerializeField, Range(0f, 1f)] float followShare = 0.55f;
    [Tooltip("How much the camera rises with the bird above the clouds, a share of its height; it never goes lower than it starts.")]
    [SerializeField, Range(0f, 1f)] float followRise = 0.2f;
    [Tooltip("Metres the camera drops as the bird dives, the seconds of that first drop, and the seconds over which it goes on bouncing, less and less (each bounce is that much smaller after so long, by about a third).")]
    [SerializeField, Min(0f)] float dipDepth = 0.45f;
    [SerializeField, Min(0.05f)] float dipSeconds = 0.6f;
    [SerializeField, Min(0.05f)] float dipFade = 0.9f;
    [Tooltip("Metres per second the wave spreads out across the clouds from where the bird went in.")]
    [SerializeField, Min(0.1f)] float waveSpeed = 10f;
    [Tooltip("The wave, like a crowd's wave going round: each puff near where the bird went in rises as it passes and goes on swelling up and down, less and less, as the rings spread on. How high (metres), the seconds of one rise, the seconds over which the swell dies down (a third as high after so long), and how far out from there it goes.")]
    [SerializeField, Min(0f)] float rippleHeight = 0.9f;
    [SerializeField, Min(0.05f)] float rippleSeconds = 0.9f;
    [SerializeField, Min(0.05f)] float rippleFade = 1.4f;
    [SerializeField, Min(1f)] float rippleReach = 22f;
    [Tooltip("The camera riding the ring as it passes under it, with the clouds: metres up and down, and degrees it tips with the slope (no shaking).")]
    [SerializeField, Min(0f)] float cameraRide = 0.35f;
    [SerializeField, Min(0f)] float cameraTip = 1.5f;

    [Header("Low Shot")]
    [SerializeField] CinemachineCamera lowShot;
    [Tooltip("In the clouds (the top shot's sea, seen from below): where the gap opens.")]
    [SerializeField] Transform gapCentre;
    [Tooltip("The gap's size once open (metres), and the seconds it takes to open from nothing.")]
    [SerializeField, Min(0f)] float gapRadius = 8f;
    [SerializeField, Min(0.1f)] float openSeconds = 4f;
    [Tooltip("How far open (0 to 1) the gap is over its time: moving from the first moment, slowly, and still moving at the end (it goes on, below).")]
    [SerializeField] AnimationCurve openCurve = new(new Keyframe(0f, 0f, 0.8f, 0.8f), new Keyframe(0.5f, 0.45f, 1.1f, 1.1f), new Keyframe(1f, 1f, 0.5f, 0.5f));
    [Tooltip("Seconds over which, once its time is up, the gap goes on widening more and more slowly to the end of the shot, from as fast as the curve's end has it (so the clouds never stop).")]
    [SerializeField, Min(0.1f)] float openOnSeconds = 2.5f;
    [Tooltip("Degrees the low shot's field of view widens over its seconds, slowly, as if the camera drew back a little.")]
    [SerializeField, Min(0f)] float lowZoomOut = 3f;
    [SerializeField, Min(0.1f)] float lowZoomSeconds = 10f;
    [Tooltip("The shafts of light through the gap (LightBeams), in the order they come; off until the gap opens.")]
    [SerializeField] LightBeam[] lightShafts = { };
    [Tooltip("How far open (0 to 1) the gap is when the first shaft starts to come, the seconds between one shaft and the next, and the seconds each takes to brighten fully.")]
    [SerializeField, Range(0f, 1f)] float firstShaftAt = 0.8f;
    [SerializeField, Min(0f)] float shaftStagger = 0.7f;
    [SerializeField, Min(0.05f)] float shaftFade = 1.5f;
    [Tooltip("Seconds of cloud only before the gap starts to open.")]
    [SerializeField, Min(0f)] float lowLead = 0f;

    [Header("Sleep Shot")]
    [SerializeField] CinemachineCamera sleepShot;
    [Tooltip("The light she walks into and sleeps in: where it meets the ground. One of the Light Shafts, so it has come with them.")]
    [SerializeField] LightBeam sleepShaft;
    [Tooltip("Seconds she looks at the light before blinking, and after blinking before she walks to it.")]
    [SerializeField, Min(0f)] float lookBeforeBlink = 1f;
    [SerializeField, Min(0f)] float lookAfterBlink = 1.2f;
    [Tooltip("How many times she blinks, one right after another, looking at it.")]
    [SerializeField, Min(0)] int blinks = 2;
    [Tooltip("Seconds she turns, there, to face out over the cliff before lying down; then seconds for lying down before the last shot.")]
    [SerializeField, Min(0f)] float turnSeconds = 0.8f;
    [SerializeField, Min(0f)] float lieSeconds = 3f;
    [Tooltip("The camera drawing away from her through the shot: metres a second at first, and once she lies down; the seconds it takes to go from the one to the other, the seconds it keeps on at the faster, and the seconds it then takes to ease to a stop.")]
    [SerializeField, Min(0f)] float pullSlow = 0.12f;
    [SerializeField, Min(0f)] float pullFast = 1.1f;
    [SerializeField, Min(0.1f)] float pullRamp = 3f;
    [SerializeField, Min(0f)] float pullFastSeconds = 12f;
    [SerializeField, Min(0.1f)] float pullStop = 5f;
    [Tooltip("How much it rises for each metre it draws back, at first and once she lies down (going faster), and degrees it turns round her from left to right for each metre (she stays in the middle).")]
    [SerializeField, Min(0f)] float pullRise = 0.2f;
    [SerializeField, Min(0f)] float pullRiseFast = 1.1f;
    [SerializeField, Min(0f)] float pullPan = 0.3f;
    [Tooltip("Seconds after she lies down that the season turns to the last (the ending's), as the camera starts to climb: the flowers round her bloom with it (RingBloom). The ring itself is cued separately below, by the ending audio.")]
    [SerializeField, Min(0f)] float seasonAfterLying = 0f;

    [Header("Ending audio & ring")]
    [Tooltip("Starts alongside the flight (Play's call), non-diegetic. Empty plays nothing.")]
    [SerializeField] EventReference endingAudio;
    [Tooltip("The sunlit spot where she lies down to sleep (BloomSpot: a SunlitSpot with no shafts). Its Reacts To Season should be off, since this plays its ring directly instead.")]
    [SerializeField] SunlitSpot bloomSpot;
    [Tooltip("Seconds into the ending audio that the sunlit spot's ring plays.")]
    [SerializeField, Min(0f)] float ringAtSeconds = 26f;

    static readonly int ShaftIntensityId = Shader.PropertyToID("_Intensity");
    static readonly int PoolIntensityId = Shader.PropertyToID("_PoolIntensity");
    static readonly int ShapeFromId = Shader.PropertyToID("_ShapeFrom");

    const int ShotPriority = 100;
    const int SamplesPerSegment = 16;

    float nextBeatAt;
    int beatsLeft;

    CinemachineBrain brain;
    CinemachineBlendDefinition blendBefore;

    public bool IsPlaying { get; private set; }
    // Out over the cliff, flat.
    Vector3 outwardWay = Vector3.forward;

    // From the bird's flight out over the cliff, or (fromFirstShot, a dev tool) straight from the rise shot.
    public void Play(Vector3 outward, bool fromFirstShot = false)
    {
        if (IsPlaying)
            return;
        IsPlaying = true;
        outwardWay = Vector3.ProjectOnPlane(outward, Vector3.up).normalized;
        StartCoroutine(Run(outwardWay, fromFirstShot));
    }

    IEnumerator Run(Vector3 outward, bool fromFirstShot)
    {
        BirdController bird = BirdController.Instance;
        Camera view = Camera.main;
        bird.BeginScripted();
        // The game's camera stays exactly where it is, so the bird flies out of the picture.
        GameFlow.Instance.BirdCamera.Freeze();

        if (!endingAudio.IsNull)
            RuntimeManager.PlayOneShot(endingAudio);
        if (bloomSpot != null)
            StartCoroutine(PlayRingAt(ringAtSeconds));
        nextBeatAt = Time.time;

        Vector3 way = (outward * Mathf.Cos(outClimb * Mathf.Deg2Rad) + Vector3.up * Mathf.Sin(outClimb * Mathf.Deg2Rad)).normalized;
        float pitch = 0f;
        float leftAt = -1f;
        while (!fromFirstShot && (leftAt < 0f || Time.time - leftAt < outWait))
        {
            pitch = Mathf.Lerp(pitch, outClimb, 1f - Mathf.Exp(-4f * Time.deltaTime));
            bird.FlyScripted(bird.transform.position + way * outSpeed * Time.deltaTime, way, pitch, Beat(), strokeDown);
            if (leftAt < 0f && !OnScreen(view, bird.transform.position))
                leftAt = Time.time;
            yield return null;
        }

        // The shots cut from one to the next, not blend.
        brain = view.GetComponent<CinemachineBrain>();
        if (brain != null)
        {
            blendBefore = brain.DefaultBlend;
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
        }

        Show(riseShot);
        UseLook();
        List<Vector3> rise = Curve(risePath);
        float passedAt = -1f;
        yield return Along(rise, riseSpeed, () =>
        {
            if (passedAt < 0f && PastCamera(view, bird.transform.position))
                passedAt = Time.time;
            return passedAt >= 0f && Time.time - passedAt >= passWait;
        });

        Show(farShot);
        bird.ScaleBy(farBirdScale);
        if (farClouds != null)
        {
            farClouds.SetActive(true);
            DrawFrom(farClouds, farShot.transform.position);
        }
        List<Vector3> far = Curve(farPath);
        float farFrom = Time.time;
        yield return Along(far, farSpeed, () => Time.time - farFrom >= farSeconds);

        if (topShot != null && cloudSurface != null)
        {
            bird.ScaleBy(1f / farBirdScale);
            yield return TopShot(bird);
        }
        if (lowShot != null && gapCentre != null)
            yield return LowShot();
        if (sleepShot != null && sleepShaft != null)
            yield return SleepShot();
        // Holds on the last shot.
    }

    // As a cutscene's look does (Cutscene): the manager's settings at once, and the scene's Volume on the look's profile.
    void UseLook()
    {
        if (look == null)
            return;
        var manager = FindAnyObjectByType<WatercolorManager>();
        if (manager != null)
            manager.Apply(look.settings, 1f);
        if (look.volumeProfile == null)
            return;
        WatercolorSeasonLooks seasonLooks = FindAnyObjectByType<WatercolorSeasonLooks>();
        Volume volume = seasonLooks != null && seasonLooks.SceneVolume != null ? seasonLooks.SceneVolume : FindAnyObjectByType<Volume>();
        if (volume != null)
            volume.sharedProfile = look.volumeProfile;
    }

    void Show(CinemachineCamera shot)
    {
        if (shot == null)
            return;
        foreach (CinemachineCamera other in new[] { riseShot, farShot, topShot, lowShot, sleepShot })
            if (other != null && other != shot)
                other.gameObject.SetActive(false);
        shot.Priority.Value = ShotPriority;
        shot.gameObject.SetActive(true);
    }

    // Flies the bird along the points at this speed until done says so; past the last point it keeps on the way it was going.
    IEnumerator Along(List<Vector3> points, float speed, System.Func<bool> done)
    {
        BirdController bird = BirdController.Instance;
        if (points.Count == 0)
            yield break;
        // Put there facing the way on, without turning from where it was.
        bird.transform.position = points[0];
        if (points.Count > 1)
            bird.SnapFacing(points[1] - points[0]);
        Vector3 at = points[0];
        Vector3 heading = points.Count > 1 ? (points[1] - points[0]).normalized : Vector3.up;
        int next = 1;
        float pitch = 0f;
        while (!done())
        {
            float step = speed * Time.deltaTime;
            while (step > 0f && next < points.Count)
            {
                Vector3 to = points[next];
                float left = Vector3.Distance(at, to);
                if (left > step)
                {
                    heading = (to - at).normalized;
                    at += heading * step;
                    step = 0f;
                }
                else
                {
                    at = to;
                    step -= left;
                    next++;
                }
            }
            if (step > 0f)
                at += heading * step;
            float across = new Vector2(heading.x, heading.z).magnitude;
            // The whole body tilts the way it goes: nose up as it climbs (positive pitch is nose up on the bird's rig).
            float along = Mathf.Clamp(Mathf.Atan2(heading.y, across) * Mathf.Rad2Deg, -75f, 75f);
            pitch = Mathf.Lerp(pitch, along, 1f - Mathf.Exp(-6f * Time.deltaTime));
            bird.FlyScripted(at, heading, pitch, Beat(), strokeDown);
            yield return null;
        }
    }

    IEnumerator TopShot(BirdController bird)
    {
        if (farClouds != null)
            farClouds.SetActive(false);
        if (topClouds != null)
        {
            topClouds.SetActive(true);
            DrawFrom(topClouds, topShot.transform.position);
        }
        Show(topShot);
        Transform shot = topShot.transform;
        Vector3 basePosition = shot.position;
        Quaternion baseRotation = shot.rotation;
        Vector3 surface = cloudSurface.position;
        // Side on to the camera as it goes up and down, so its wings show, facing left on the screen: the way it curls over.
        Vector3 side = -Vector3.ProjectOnPlane(shot.right, Vector3.up).normalized;
        Vector3 bottom = surface - Vector3.up * riseFrom;
        Vector3 top = surface + Vector3.up * riseHeight;
        bird.transform.position = bottom;
        bird.SnapFacing(side);

        // Where the camera looks, eased after the bird; at first, where it was looking at the clouds' top.
        Vector3 look = basePosition + baseRotation * Vector3.forward * Vector3.Distance(basePosition, surface);
        Vector3 lookVelocity = Vector3.zero;
        float lift = 0f, liftVelocity = 0f;
        float dipFrom = -1f, impactAt = -1f;
        List<(Transform puff, Vector3 rest)> puffs = Movable(topClouds);

        void Frame()
        {
            Vector3 birdAt = bird.transform.position;
            if (birdAt.y > surface.y)
                look = Vector3.SmoothDamp(look, birdAt, ref lookVelocity, followLag);
            lift = Mathf.SmoothDamp(lift, Mathf.Max(0f, birdAt.y - surface.y) * followRise, ref liftVelocity, followLag);
            Vector3 at = basePosition + Vector3.up * lift;
            // Down as the bird dives, then bouncing on up and down, less and less.
            if (dipFrom >= 0f)
                at += Vector3.down * dipDepth * Swell(Time.time - dipFrom, dipSeconds, dipFade);
            // Only tilting up and down after the bird, never turning to the sides.
            Vector3 toLook = look - at;
            float lookPitch = -Mathf.Atan2(toLook.y, new Vector2(toLook.x, toLook.z).magnitude) * Mathf.Rad2Deg;
            Vector3 baseAngles = baseRotation.eulerAngles;
            float pitch = baseAngles.x + Mathf.DeltaAngle(baseAngles.x, lookPitch) * followShare;
            Quaternion rotation = Quaternion.Euler(pitch, baseAngles.y, baseAngles.z);
            if (impactAt >= 0f)
            {
                // The ring reaches the camera after it has crossed the clouds to it.
                // The rings reach the camera after crossing the clouds to it, and it rides them as the clouds do: up and down,
                // less and less, tipping back as each comes under it and forward as it goes on past (with the swell's slope).
                float reached = Time.time - impactAt - FlatDistance(surface, at) / waveSpeed;
                if (reached > 0f)
                {
                    at += Vector3.up * cameraRide * Swell(reached, rippleSeconds, rippleFade);
                    const float step = 0.02f;
                    float slope = (Swell(reached + step, rippleSeconds, rippleFade) - Swell(reached - step, rippleSeconds, rippleFade)) / (2f * step);
                    rotation *= Quaternion.Euler(-cameraTip * slope * rippleSeconds / Mathf.PI, 0f, 0f);
                }
                Ripple(puffs, surface, Time.time - impactAt);
            }
            shot.SetPositionAndRotation(at, rotation);
        }

        for (float t = 0f; t < topLead; t += Time.deltaTime)
        {
            Frame();
            yield return null;
        }

        // Up through the clouds.
        float pitch = 70f;
        for (float t = 0f; t < riseSeconds; t += Time.deltaTime)
        {
            Vector3 at = Vector3.LerpUnclamped(bottom, top, riseCurve.Evaluate(t / riseSeconds));
            bird.FlyScripted(at, side * 0.1f + Vector3.up, pitch, Beat(), strokeDown);
            Frame();
            yield return null;
        }
        // It hovers there, beating its wings on the spot, slower and slower as its strength goes, sinking a little.
        float risePitch = pitch;
        Vector3 hoverAt = top;
        for (float t = 0f; t < apexPause; t += Time.deltaTime)
        {
            float share = t / apexPause;
            pitch = Mathf.Lerp(risePitch, 0f, Mathf.SmoothStep(0f, 1f, t / 0.4f));
            hoverAt = top + Vector3.down * hoverSink * share * share;
            bird.HoverScripted(hoverAt, side, pitch, Mathf.Lerp(hoverRateFrom, hoverRateTo, Mathf.SmoothStep(0f, 1f, share)));
            Frame();
            yield return null;
        }

        // And it lets itself fall, in the dive's falling loop, straight back down into them where it came out.
        bird.StartFalling(fallBlend);
        dipFrom = Time.time;
        Vector3 noseDown = (side * 0.1f + Vector3.down).normalized;
        Vector3 under = top + Vector3.down * (riseHeight + diveDepth);
        for (float t = 0f; t < diveSeconds; t += Time.deltaTime)
        {
            pitch = Mathf.Lerp(0f, -divePitch, Mathf.SmoothStep(0f, 1f, t / fallBlend));
            float down = diveCurve.Evaluate(t / diveSeconds);
            Vector3 at = Vector3.LerpUnclamped(hoverAt, under, down);
            bird.FlyScripted(at, noseDown, pitch, false, 0f);
            if (impactAt < 0f && at.y <= surface.y)
                impactAt = Time.time;
            Frame();
            yield return null;
        }
        if (impactAt < 0f)
            impactAt = Time.time;
        // The wave goes on spreading and the camera rides it as it reaches it; then it holds.
        float until = Time.time + Mathf.Max(rippleReach / waveSpeed, FlatDistance(surface, basePosition) / waveSpeed) + rippleSeconds + 2f * rippleFade
            - topEndSooner;
        while (Time.time < until)
        {
            Frame();
            yield return null;
        }
        foreach ((Transform puff, Vector3 rest) in puffs)
            puff.position = rest;
    }

    IEnumerator LowShot()
    {
        Show(lowShot);
        StartCoroutine(ZoomOut(lowShot, lowZoomOut, lowZoomSeconds));
        if (topClouds != null)
            DrawFrom(topClouds, lowShot.transform.position);
        List<(Transform puff, Vector3 rest)> puffs = Movable(topClouds);
        // Each shaft brightens from nothing to its material's own brightness.
        var shafts = new List<(Renderer renderer, float shaft, float pool)>();
        var props = new MaterialPropertyBlock();
        foreach (LightBeam beam in lightShafts)
        {
            if (beam == null)
                continue;
            var renderer = beam.GetComponent<Renderer>();
            Material material = renderer.sharedMaterial;
            shafts.Add((renderer, material.GetFloat(ShaftIntensityId), material.GetFloat(PoolIntensityId)));
            SetShaft(renderer, props, 0f, 0f);
            beam.gameObject.SetActive(true);
        }

        Vector3 centre = gapCentre.position;
        Baby baby = Baby.Instance;
        // She stands as she does, looking ahead (no longer after the bird); as the clouds part, from the start, she looks up.
        if (baby != null)
            baby.LookUpAtSky(centre, 0f, true);
        for (float t = 0f; t < lowLead; t += Time.deltaTime)
            yield return null;
        float lightFrom = -1f;
        float lightUntil = shaftStagger * Mathf.Max(0, shafts.Count - 1) + shaftFade;
        for (float t = 0f; t <= openSeconds || lightFrom < 0f || t - lightFrom <= lightUntil; t += Time.deltaTime)
        {
            float open = openCurve.Evaluate(Mathf.Clamp01(t / openSeconds));
            // On past its time, at the curve's own last speed, slowing.
            if (t > openSeconds)
            {
                Keyframe last = openCurve[openCurve.length - 1];
                float speed = last.inTangent / openSeconds;
                open += speed * openOnSeconds * (1f - Mathf.Exp(-(t - openSeconds) / openOnSeconds));
            }
            Part(puffs, centre, gapRadius * open);
            if (baby != null)
                baby.LookUpAtSky(centre, open);
            // The light comes once there is a gap to come through: one shaft, then the next, each slowly.
            if (lightFrom < 0f && open >= firstShaftAt)
                lightFrom = t;
            for (int i = 0; i < shafts.Count; i++)
            {
                float light = lightFrom < 0f ? 0f : Mathf.SmoothStep(0f, 1f, (t - lightFrom - i * shaftStagger) / shaftFade);
                SetShaft(shafts[i].renderer, props, shafts[i].shaft * light, shafts[i].pool * light);
            }
            yield return null;
        }
    }

    IEnumerator SleepShot()
    {
        Show(sleepShot);
        Baby baby = Baby.Instance;
        if (baby == null)
            yield break;
        lieDownAt = -1f;
        StartCoroutine(PullAway());
        Vector3 spot = sleepShaft.transform.position;
        baby.LookAtLight(spot + Vector3.up * 0.6f);
        yield return new WaitForSeconds(lookBeforeBlink);
        baby.BlinkQuickly(blinks);
        yield return new WaitForSeconds(lookAfterBlink);
        while (!baby.WalkIntoLight(spot))
            yield return null;
        // Facing out over the cliff, the way she came to see.
        Vector3 ahead = spot + Vector3.ProjectOnPlane(outwardWay, Vector3.up).normalized * 5f;
        for (float t = 0f; t < turnSeconds; t += Time.deltaTime)
        {
            baby.StandFacing(ahead);
            yield return null;
        }
        baby.LieDownInLight();
        lieDownAt = Time.time;
        StartCoroutine(LastSeason());
        yield return new WaitForSeconds(lieSeconds);
    }

    // The season turns to the last a moment after she lies down; the flowers follow the season on their own (the ring plays
    // separately, cued by the ending audio - see PlayRingAt).
    IEnumerator LastSeason()
    {
        yield return new WaitForSeconds(seasonAfterLying);
        Season season = FindAnyObjectByType<Season>();
        if (season != null)
            season.AdvanceSeason();
    }

    // The sunlit spot's ring, timed to a beat in the ending audio rather than to anything she does.
    IEnumerator PlayRingAt(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        bloomSpot.PlayRing();
    }

    // When she lay down in the sleep shot (below 0 until then).
    float lieDownAt = -1f;

    // The sleep shot's camera drawing away from her, back and up, its look coming round to where she will sleep as she goes there,
    // and turning from left to right: slowly, then from when she lies down eased up to faster, on at that a while, and eased to a stop. Then it holds.
    IEnumerator PullAway()
    {
        Transform shot = sleepShot.transform;
        Vector3 from = shot.position;
        Vector3 forward = shot.forward;
        Vector3 spot = sleepShaft.transform.position;
        Vector3 firstAim = from + forward * Mathf.Max(1f, Vector3.Dot(spot - from, forward));
        // Her body's middle as it is posed (lying, she is off to one side of where she stood), followed smoothly.
        var body = new List<Transform>();
        if (Baby.Instance != null)
            foreach (Transform bone in Baby.Instance.GetComponentsInChildren<Transform>())
                if (bone.name == "Hips" || bone.name == "Head")
                    body.Add(bone);
        Vector3 sleepAim = BodyMiddle(body, spot);
        float startedAt = Time.time;
        Vector3 back = -Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
        Vector3 at = from;
        for (float pulled = 0f; ; )
        {
            float speed = pullSlow, rise = pullRise;
            if (lieDownAt >= 0f)
            {
                float since = Time.time - lieDownAt;
                float faster = Mathf.SmoothStep(0f, 1f, since / pullRamp);
                speed = Mathf.Lerp(pullSlow, pullFast, faster);
                rise = Mathf.Lerp(pullRise, pullRiseFast, faster);
                float stopping = since - pullRamp - pullFastSeconds;
                if (stopping > 0f)
                {
                    if (stopping >= pullStop)
                        yield break;
                    speed *= 1f - Mathf.SmoothStep(0f, 1f, stopping / pullStop);
                }
            }
            pulled += speed * Time.deltaTime;
            at += (back + Vector3.up * rise).normalized * speed * Time.deltaTime;
            sleepAim = Vector3.Lerp(sleepAim, BodyMiddle(body, spot), 1f - Mathf.Exp(-2f * Time.deltaTime));
            Vector3 aim =Vector3.Lerp(firstAim, sleepAim, Mathf.SmoothStep(0f, 1f, (Time.time - startedAt) / 6f));
            // Turning round her rather than away from her, so she stays in the middle of the picture.
            Vector3 round = aim + Quaternion.Euler(0f, pullPan * pulled, 0f) * (at - aim);
            shot.SetPositionAndRotation(round, Quaternion.LookRotation(aim - round));
            yield return null;
        }
    }

    // Midway between her hips and her head as they are posed, or the spot (a little up) without them.
    static Vector3 BodyMiddle(List<Transform> body, Vector3 spot)
    {
        if (body.Count == 0)
            return spot + Vector3.up * 0.2f;
        Vector3 sum = Vector3.zero;
        foreach (Transform bone in body)
            sum += bone.position;
        return sum / body.Count;
    }

    // The field of view widening slowly, eased at both ends.
    static IEnumerator ZoomOut(CinemachineCamera shot, float degrees, float seconds)
    {
        float from = shot.Lens.FieldOfView;
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            LensSettings lens = shot.Lens;
            lens.FieldOfView = from + degrees * Mathf.SmoothStep(0f, 1f, t / seconds);
            shot.Lens = lens;
            yield return null;
        }
    }

    // The clouds' puffs are ordered for seeing them from above (CloudGenerator's Draw Order View), the intro's view. Seen from
    // the side or from below, that draws far puffs over near ones, and as they move the overlaps flicker. So for each shot they
    // are ordered again from its camera, farthest first, within the clouds' own range of orders.
    static void DrawFrom(GameObject clouds, Vector3 camera)
    {
        var puffs = new List<Renderer>(clouds.GetComponentsInChildren<Renderer>());
        puffs.Sort((a, b) => (b.transform.position - camera).sqrMagnitude.CompareTo((a.transform.position - camera).sqrMagnitude));
        for (int i = 0; i < puffs.Count; i++)
            puffs[i].sortingOrder = CloudGenerator.MiddleOrder + i;
    }

    // The puffs of these clouds, each with where it rests. Each keeps the shape it has there while it moves (CloudPuff picks a
    // puff's shape from its position, so otherwise it would become another puff every frame and the clouds would shiver).
    static List<(Transform puff, Vector3 rest)> Movable(GameObject clouds)
    {
        var puffs = new List<(Transform puff, Vector3 rest)>();
        if (clouds == null)
            return puffs;
        var props = new MaterialPropertyBlock();
        foreach (Renderer puff in clouds.GetComponentsInChildren<Renderer>())
        {
            Vector3 rest = puff.transform.position;
            puff.GetPropertyBlock(props);
            props.SetVector(ShapeFromId, new Vector4(rest.x, rest.y, rest.z, 1f));
            puff.SetPropertyBlock(props);
            puffs.Add((puff.transform, rest));
        }
        return puffs;
    }

    static void SetShaft(Renderer renderer, MaterialPropertyBlock props, float shaft, float pool)
    {
        renderer.GetPropertyBlock(props);
        props.SetFloat(ShaftIntensityId, shaft);
        props.SetFloat(PoolIntensityId, pool);
        renderer.SetPropertyBlock(props);
    }

    // The clouds draw back from the gap like cloth parting: each puff moves straight out from its centre, the nearest by the
    // radius and less and less out to twice the radius, beyond which nothing moves. None ever overtakes another (the push never
    // falls off faster than the distance grows), so only the clouds around the hole shift. Across only (X and Z).
    static void Part(List<(Transform puff, Vector3 rest)> puffs, Vector3 centre, float radius)
    {
        for (int i = 0; i < puffs.Count; i++)
        {
            (Transform puff, Vector3 rest) = puffs[i];
            Vector3 away = rest - centre;
            away.y = 0f;
            float distance = away.magnitude;
            Vector3 outward = distance > 0.01f ? away / distance : Quaternion.Euler(0f, i * 137.5f, 0f) * Vector3.forward;
            float reach = 2f * radius;
            float left = reach > 0f ? Mathf.Max(0f, 1f - distance / reach) : 0f;
            float pushed = radius * left * left;
            puff.position = rest + outward * pushed;
        }
    }

    // A wave goes out from where the bird went in, like a crowd's wave going round: each puff near there rises once and settles
    // as it passes, later and lower the farther out it is. Only its height moves.
    void Ripple(List<(Transform puff, Vector3 rest)> puffs, Vector3 centre, float since)
    {
        foreach ((Transform puff, Vector3 rest) in puffs)
        {
            float distance = FlatDistance(centre, rest);
            float local = since - distance / waveSpeed;
            float height = 0f;
            if (distance < rippleReach)
                height = rippleHeight * Swell(local, rippleSeconds, rippleFade) * (1f - distance / rippleReach);
            puff.position = rest + Vector3.up * height;
        }
    }

    // A swell that starts at nothing: up for the first rise seconds, then down, then up again, less and less, about a third as high
    // after fade seconds. Eased in, so neither it nor its slope jumps as it starts.
    static float Swell(float t, float rise, float fade)
    {
        if (t <= 0f)
            return 0f;
        float envelope = Mathf.SmoothStep(0f, 1f, t / (0.5f * rise)) * Mathf.Exp(-t / fade);
        return Mathf.Sin(t / rise * Mathf.PI) * envelope;
    }

    static float FlatDistance(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

    // Whether a wingbeat starts now: a few in a row, then a glide, in uneven rows and pauses, like a bird working its way up.
    bool Beat()
    {
        if (Time.time < nextBeatAt)
            return false;
        if (beatsLeft <= 0)
            beatsLeft = Random.Range(beatsInRow.x, beatsInRow.y + 1);
        beatsLeft--;
        nextBeatAt = Time.time + beatSeconds * Random.Range(0.85f, 1f)
            + (beatsLeft <= 0 ? Random.Range(glideSeconds.x, glideSeconds.y) : 0f);
        return true;
    }

    static bool OnScreen(Camera view, Vector3 point)
    {
        Vector3 p = view.WorldToViewportPoint(point);
        const float margin = 0.05f;
        return p.z > 0f && p.x > -margin && p.x < 1f + margin && p.y > -margin && p.y < 1f + margin;
    }

    // Gone past the shot's camera: out of the top of the picture or behind it.
    static bool PastCamera(Camera view, Vector3 point)
    {
        Vector3 p = view.WorldToViewportPoint(point);
        return p.z <= 0f || p.y > 1.05f || p.x < -0.05f || p.x > 1.05f;
    }

    // A smooth curve through the children (Catmull-Rom), as points close together.
    static List<Vector3> Curve(Transform path)
    {
        var points = new List<Vector3>();
        if (path == null || path.childCount == 0)
            return points;
        var keys = new List<Vector3>();
        foreach (Transform child in path)
            keys.Add(child.position);
        if (keys.Count == 1)
            return keys;
        for (int i = 0; i < keys.Count - 1; i++)
        {
            Vector3 p0 = keys[Mathf.Max(i - 1, 0)], p1 = keys[i], p2 = keys[i + 1], p3 = keys[Mathf.Min(i + 2, keys.Count - 1)];
            for (int s = 0; s < SamplesPerSegment; s++)
            {
                float t = s / (float)SamplesPerSegment;
                points.Add(0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t + (3f * p1 - p0 - 3f * p2 + p3) * t * t * t));
            }
        }
        points.Add(keys[keys.Count - 1]);
        return points;
    }

    void OnDisable()
    {
        if (brain != null && IsPlaying)
            brain.DefaultBlend = blendBefore;
    }

    void OnDrawGizmos()
    {
        DrawPath(risePath, new Color(1f, 0.3f, 0.3f));
        DrawPath(farPath, new Color(1f, 0.7f, 0.2f));
    }

    static void DrawPath(Transform path, Color color)
    {
        List<Vector3> points = Curve(path);
        Gizmos.color = color;
        for (int i = 0; i + 1 < points.Count; i++)
            Gizmos.DrawLine(points[i], points[i + 1]);
        if (path != null)
            foreach (Transform child in path)
                Gizmos.DrawSphere(child.position, 0.2f);
    }
}
