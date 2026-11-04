using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// The game's opening, above the clouds. The title shows until the first click; the bird flies in from below the screen to that click.
// The sky camera then travels to its end point without turning while the player flies the bird by clicking, as in the game.
// Near the end the bird curves in, slowing, and goes straight into circling over the hole in the clouds. A click on the baby sends it up a little and then diving,
// and the game starts behind a white fade. Everything here is saved in the scene: this only turns it on and off.
public class IntroSequence : MonoBehaviour
{
    // Seconds into Bird_Dive at which the bird is falling, after its wing beat up and head down (the bird's export notes).
    const float DiveFallsAfter = 0.6f;

    [Header("Camera")]
    [Tooltip("Stays at its saved rotation and only moves, from Camera Start to Camera End. Its priority must be above the game camera's.")]
    [SerializeField] CinemachineCamera skyCamera;
    [SerializeField] Transform cameraStart;
    [SerializeField] Transform cameraEnd;
    [SerializeField, Min(0.1f)] float flightSeconds = 30f;
    [Tooltip("Turned on for the dive. Its priority must be above the sky camera's.")]
    [SerializeField] CinemachineCamera diveCamera;

    [Header("Screen")]
    [SerializeField] Graphic title;
    [SerializeField, Min(0.01f)] float titleFadeSeconds = 0.6f;
    [Tooltip("White, over the whole screen.")]
    [SerializeField] Graphic whiteFade;
    [Tooltip("Seconds from the moment the bird starts dropping until the screen is fully white. The game starts then, so the dive is cut short if it would run longer.")]
    [SerializeField, Min(0.01f)] float whiteSeconds = 1.8f;
    [SerializeField, Min(0f)] float fadeHoldSeconds = 0.2f;
    [SerializeField, Min(0.01f)] float fadeOutSeconds = 1.2f;

    [Header("Bird")]
    [Tooltip("Seconds the bird takes to fly in from the bottom of the screen to the first click, slowing as it arrives.")]
    [SerializeField, Min(0.01f)] float flyInSeconds = 1.2f;
    [Tooltip("The bird circles this point, at this point's height. That height is also where it flies during the whole opening.")]
    [SerializeField] Transform orbitCentre;
    [Tooltip("Radius of the circle over the baby, in metres.")]
    [SerializeField, Min(0.1f)] float orbitRadius = 1.5f;
    [Tooltip("Speed round the circle, in metres per second: one lap takes 2π × Orbit Radius ÷ this. The dive starts circling at it too.")]
    [SerializeField, Min(0.01f)] float orbitSpeed = 1.5f;
    [Tooltip("Seconds before the camera arrives that the player's control ends and the bird curves into the circle, slowing down to Orbit Speed.")]
    [SerializeField, Min(0.1f)] float approachSeconds = 6f;
    [Tooltip("How far around the baby a click counts, as a fraction of the screen's height.")]
    [SerializeField, Range(0.01f, 0.5f)] float babyClickRadius = 0.1f;
    [Tooltip("Length of the whole dive, in seconds: Dive Path and Dive Field Of View run across it. The white is full White Seconds after the drop starts, usually before it ends.")]
    [SerializeField, Min(0.1f)] float diveSeconds = 4f;
    [Tooltip("Turns of the spiral on the way down, on top of circling on as fast as it did. It goes on the way it was circling, from wherever it was")]
    [SerializeField, Min(0f)] float diveTurns = 0.75f;
    [Tooltip("How wide the spiral swings out halfway down, in metres from its middle. It starts at the circle's radius and closes on the stage's start")]
    [SerializeField, Min(0f)] float diveSpiralRadius = 6f;
    [Tooltip("How high above the spiral's middle the dive camera rides, in metres, looking straight down at the ground. It never turns: the bird circles below it. Keep it high enough for Dive Spiral Radius to fit in the picture.")]
    [SerializeField, Min(1f)] float diveCameraHeight = 15f;
    [Tooltip("Seconds the dive camera lags behind the spiral's middle on the way down, so the bird drops ahead of it. 0 = none.")]
    [SerializeField, Min(0f)] float diveCameraLag = 0.35f;
    [Tooltip("Seconds after the bird starts dropping (the lowest point of Dive Path) that the camera goes after it.")]
    [SerializeField, Min(0f)] float diveCameraBeat = 0.7f;
    [Tooltip("Seconds Dive Field Of View runs behind the rest of the dive.")]
    [SerializeField, Min(0f)] float diveFieldOfViewBeat = 0.175f;
    [Tooltip("How far along the way down the bird is, across the dive. 0 = circle height, 1 = the stage's start, below 0 = above the circle: the climb before it drops. Steeper is faster.")]
    [SerializeField] AnimationCurve divePath = new AnimationCurve(new Keyframe(0f, 0f, 0f, -3f), new Keyframe(0.18f, -0.3f), new Keyframe(0.5f, 0.75f, 1.6f, 1.6f), new Keyframe(1f, 1f, 0.2f, 0f));
    [Tooltip("Vertical FOV across the dive, as a fraction of the sky camera's. Over 1 widens it, pushing the far things away, while the bird climbs. Under 1 zooms in, which pulls the ground closer.")]
    [SerializeField] AnimationCurve diveFieldOfView = new AnimationCurve(new Keyframe(0f, 1f, 0f, 3f), new Keyframe(0.18f, 1.4f, 0f, 0f), new Keyframe(1f, 0.5f, -1f, 0f));
    [Tooltip("The most the body tips along its path during the dive, in degrees: nose down as it falls, up as it climbs. It points where the bird is actually going.")]
    [SerializeField, Range(0f, 90f)] float diveBodyPitch = 80f;
    [Tooltip("How quickly the body turns to its path, per second. Higher follows the path more tightly")]
    [SerializeField, Min(0.1f)] float divePitchFollow = 8f;

    [Header("Game Start")]
    [Tooltip("The tree the bird drops onto as the white clears: the object, or one of its branches to choose where. It must be on screen in the game camera's opening frame. Empty: the bird just starts at the stage's start.")]
    [SerializeField] GameObject startTree;
    [Tooltip("How far above its perch on that tree the bird starts, behind the white. It drops at once, and lands as the white clears.")]
    [SerializeField, Min(0f)] float dropHeight = 2f;

    // From Play until the white clears and this turns off.
    public static bool IsPlaying { get; private set; }

    CloudGenerator[] clouds;
    Vector3[] cloudCentres;
    float[] cloudReaches;
    float cameraSecondsLeft = float.MaxValue;
    bool running;
    // Where on the circle the bird is, which the dive goes on from.
    float circleAngle;

    public void Play()
    {
        IsPlaying = true;
        running = true;
        // These may be off in the scene, to see past the title and the sky while working on the game.
        title.GetComponentInParent<Canvas>(true).gameObject.SetActive(true);
        skyCamera.gameObject.SetActive(true);
        foreach (CloudGenerator cloud in FindObjectsByType<CloudGenerator>(FindObjectsInactive.Include))
            cloud.gameObject.SetActive(true);
        // The game starts up there: a cut, not a blend from the game camera. The bird's first place also reads this camera.
        CinemachineBrain brain = Camera.main.GetComponent<CinemachineBrain>();
        if (brain != null)
        {
            CinemachineBrain.UpdateMethods updateMethod = brain.UpdateMethod;
            brain.UpdateMethod = CinemachineBrain.UpdateMethods.ManualUpdate;
            brain.ManualUpdate();
            brain.ActiveBlend = null;
            brain.ManualUpdate();
            brain.UpdateMethod = updateMethod;
        }
        SetAlpha(whiteFade, 0f);
        StartCoroutine(Run());
    }

    void Update()
    {
        // Debug only: Debug.isDebugBuild is also true in the Editor.
        if (running && Debug.isDebugBuild && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            running = false;
            StopAllCoroutines();
            StartCoroutine(StartGame());
        }
    }

    IEnumerator Run()
    {
        BirdController bird = BirdController.Instance;
        bird.BeginScripted();
        bird.SkyForward = Vector3.ProjectOnPlane(cameraEnd.position - cameraStart.position, Vector3.up).normalized;
        bird.Teleport(AtFlightHeight(Camera.main.ViewportToScreenPoint(new Vector3(0.5f, -0.3f))));
        bird.SetAltitude(orbitCentre.position.y, true);

        yield return new WaitUntil(ClickedThisFrame);
        Vector2 click = Mouse.current.position.ReadValue();
        StartCoroutine(FadeOutTitle());
        StartCoroutine(MoveCamera());
        // The bird appears, flying up into view from below the screen.
        IntroSfx.PlayHighPitch();
        yield return FlyIn(click);

        bird.BeginSkyFlight();
        Vector3 velocity = Vector3.zero;
        Vector3 last = bird.transform.position;
        while (cameraSecondsLeft > approachSeconds)
        {
            yield return null;
            if (Time.deltaTime > 0f)
                velocity = (bird.transform.position - last) / Time.deltaTime;
            last = bird.transform.position;
        }

        bird.BeginScripted();
        float angle = 0f;
        yield return CurveIntoCircle(velocity, result => angle = result);
        yield return CircleUntilBabyClicked(angle);

        running = false;
        IntroSfx.PlayDive();
        yield return Dive();
        yield return StartGame();
    }

    IEnumerator FadeOutTitle()
    {
        float from = title.color.a;
        for (float t = 0f; t < titleFadeSeconds; t += Time.deltaTime)
        {
            SetAlpha(title, Mathf.Lerp(from, 0f, t / titleFadeSeconds));
            yield return null;
        }
        title.gameObject.SetActive(false);
    }

    // Measures each cloud once, so what it takes to clear the screen is known without looking at all its puffs every frame.
    void FindClouds()
    {
        clouds = FindObjectsByType<CloudGenerator>(FindObjectsSortMode.None);
        cloudCentres = new Vector3[clouds.Length];
        cloudReaches = new float[clouds.Length];
        for (int i = 0; i < clouds.Length; i++)
        {
            Renderer[] puffs = clouds[i].GetComponentsInChildren<Renderer>();
            if (puffs.Length == 0)
                continue;
            Bounds bounds = puffs[0].bounds;
            foreach (Renderer puff in puffs)
                bounds.Encapsulate(puff.bounds);
            cloudCentres[i] = bounds.center;
            cloudReaches[i] = bounds.extents.magnitude;
        }
    }

    // A cloud is turned off once all of it has left the screen on the side the camera is travelling away from. The camera never comes back.
    void HideCloudsBehindCamera()
    {
        Camera view = Camera.main;
        Vector3 forward = BirdController.Instance.SkyForward;
        for (int i = 0; i < clouds.Length; i++)
        {
            if (!clouds[i].gameObject.activeSelf)
                continue;
            Vector3 front = view.WorldToViewportPoint(cloudCentres[i] + forward * cloudReaches[i]);
            if (front.z <= 0f || front.y < 0f)
                clouds[i].gameObject.SetActive(false);
        }
    }

    IEnumerator MoveCamera()
    {
        FindClouds();
        Transform moved = skyCamera.transform;
        for (float t = 0f; t < flightSeconds; t += Time.deltaTime)
        {
            HideCloudsBehindCamera();
            Vector3 before = moved.position;
            moved.position = Vector3.Lerp(cameraStart.position, cameraEnd.position, Mathf.SmoothStep(0f, 1f, t / flightSeconds));
            if (Time.deltaTime > 0f)
                BirdController.Instance.SkyDrift = (moved.position - before) / Time.deltaTime;
            cameraSecondsLeft = flightSeconds - t;
            yield return null;
        }
        moved.position = cameraEnd.position;
        BirdController.Instance.SkyDrift = Vector3.zero;
        cameraSecondsLeft = 0f;
    }

    // From below the screen, under the click, easing into the click. The target moves along with the camera so it stays under the click on screen.
    IEnumerator FlyIn(Vector2 click)
    {
        BirdController bird = BirdController.Instance;
        Vector3 cameraBefore = skyCamera.transform.position;
        Vector3 to = AtFlightHeight(click);
        Vector3 from = AtFlightHeight(new Vector2(click.x, -0.2f * Screen.height));
        bird.Teleport(from);
        bird.SnapFacing(bird.SkyForward);
        for (float t = 0f; t < flyInSeconds; t += Time.deltaTime)
        {
            float eased = 1f - Mathf.Pow(1f - t / flyInSeconds, 3f);
            bird.MoveScriptedAhead(Vector3.Lerp(from, to, eased) + skyCamera.transform.position - cameraBefore);
            yield return null;
        }
    }

    // A curve from where the bird is, at the speed it has, onto the circle at the point where the circle runs the way it is heading,
    // at circling speed: no corner or sudden change of speed where the circling takes over.
    IEnumerator CurveIntoCircle(Vector3 velocity, System.Action<float> reachedAngle)
    {
        BirdController bird = BirdController.Instance;
        Vector3 centre = orbitCentre.position;
        Vector3 from = bird.transform.position;
        Vector3 toCentre = centre - from;
        toCentre.y = 0f;
        float distance = toCentre.magnitude;
        // The circle runs along (-sin, cos) at an angle, so it runs along the heading a quarter turn back from it.
        float angle = distance > 0.01f ? Mathf.Atan2(toCentre.z, toCentre.x) - Mathf.PI * 0.5f : 0f;
        Vector3 to = OnCircle(angle);

        velocity.y = 0f;
        // Rushing when control ended, it would swing wide off the screen if the curve kept all of that speed.
        velocity = Vector3.ClampMagnitude(velocity, Mathf.Max(1.5f * distance / approachSeconds, orbitSpeed));
        Vector3 startTangent = velocity * approachSeconds;
        Vector3 endTangent = new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * (orbitSpeed * approachSeconds);
        for (float t = 0f; t < approachSeconds; t += Time.deltaTime)
        {
            bird.MoveScripted(Curve(from, startTangent, to, endTangent, t / approachSeconds));
            yield return null;
        }
        reachedAngle(angle);
    }

    IEnumerator CircleUntilBabyClicked(float angle)
    {
        BirdController bird = BirdController.Instance;
        circleAngle = angle;
        while (!ClickedOnBaby())
        {
            circleAngle += orbitSpeed / orbitRadius * Time.deltaTime;
            bird.MoveScripted(OnCircle(circleAngle));
            yield return null;
        }
    }

    // Starts along the start tangent and ends along the end tangent. u is 0 to 1; a tangent is the velocity times the seconds the curve takes.
    static Vector3 Curve(Vector3 from, Vector3 startTangent, Vector3 to, Vector3 endTangent, float u)
    {
        float u2 = u * u, u3 = u2 * u;
        return (2f * u3 - 3f * u2 + 1f) * from + (u3 - 2f * u2 + u) * startTangent + (-2f * u3 + 3f * u2) * to + (u3 - u2) * endTangent;
    }

    Vector3 OnCircle(float angle) => orbitCentre.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * orbitRadius;

    // The moment the bird stops climbing and starts to drop, as a fraction of the dive.
    float DropStart()
    {
        Keyframe top = divePath[0];
        foreach (Keyframe key in divePath.keys)
            if (key.value < top.value)
                top = key;
        return top.time;
    }

    // The bird climbs a little, then swoops down, still circling on a tightening spiral; the camera goes after it a beat into the drop, zooming in.
    IEnumerator Dive()
    {
        BirdController bird = BirdController.Instance;
        Vector3 from = orbitCentre.position;
        Stage first = GameFlow.Instance.CurrentStage;
        Vector3 to = first.birdStart.position;
        to.y = first.birdAltitude;
        float skyFieldOfView = skyCamera.Lens.FieldOfView;
        float dropStartsAt = DropStart() * diveSeconds;
        float cameraStartsAt = dropStartsAt + diveCameraBeat;
        // Bird_Dive beats its wings up and tips its head down first, and is falling from DiveFallsAfter on: started that much before
        // the drop, its fall meets the drop. Bird_DiveLoop follows it in the animator.
        float diveAnimationAt = Mathf.Max(dropStartsAt - DiveFallsAfter, 0f);
        bool diving = false;
        Vector3 last = bird.transform.position;
        float pitch = 0f;
        float startAngle = circleAngle;
        float angularSpeed = orbitSpeed / orbitRadius;
        var chase = diveCamera.GetComponent<CinemachineFollow>();
        // Straight down, the top of the picture the way the sky camera faced, so going over to it only tips the view down.
        Quaternion lookingDown = Quaternion.Euler(90f, skyCamera.transform.eulerAngles.y, 0f);
        // The point the camera rides with: the spiral's middle, followed a little late so the bird drops ahead of it.
        Vector3 rideAlong = from;
        for (float t = 0f; t < diveSeconds; t += Time.deltaTime)
        {
            float progress = t / diveSeconds;
            if (!diving && t >= diveAnimationAt)
            {
                diving = true;
                bird.SetDiving(true);
            }
            // A spiral round a middle that slides from the circle's centre to the stage's start. It carries straight on from the circle:
            // the same centre, radius and way round at the start, so it begins wherever the bird was. It swings wide halfway down and
            // closes on the start at the end. How far along it is (down) eases in and out over the whole dive with no sudden change of
            // pace, so the bird never turns with a jolt; the height follows Dive Path on its own.
            float descent = divePath.Evaluate(progress);
            float down = progress * progress * progress * (progress * (progress * 6f - 15f) + 10f);
            circleAngle = startAngle + angularSpeed * t + diveTurns * 2f * Mathf.PI * down;
            float radius = (orbitRadius + (diveSpiralRadius - orbitRadius) * Mathf.Sin(Mathf.PI * down)) * Mathf.Sqrt(1f - down);
            Vector3 spiral = new Vector3(Mathf.Cos(circleAngle), 0f, Mathf.Sin(circleAngle)) * radius;
            Vector3 path = Vector3.Lerp(from, to, down);
            path.y = Mathf.LerpUnclamped(from.y, to.y, descent);
            // The body points along the way it is going: how steeply it falls (or climbs) against how fast it goes across.
            Vector3 next = path + spiral;
            Vector3 step = next - last;
            last = next;
            float across = new Vector2(step.x, step.z).magnitude;
            if (step.sqrMagnitude > 1e-8f)
            {
                float along = Mathf.Clamp(Mathf.Atan2(-step.y, across) * Mathf.Rad2Deg, -diveBodyPitch, diveBodyPitch);
                pitch = Mathf.Lerp(pitch, along, 1f - Mathf.Exp(-divePitchFollow * Time.deltaTime));
            }
            bird.MoveScripted(next, pitch);

            // The dive camera rides down over the spiral's middle, looking straight at the ground with a rotation that never changes:
            // the bird circles below it. It follows the middle a little late (Dive Camera Lag), so the bird drops ahead of it. Its follow
            // offset puts it exactly there (no position damping) whatever the bird does.
            rideAlong = Vector3.Lerp(rideAlong, path, 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(diveCameraLag, 0.01f)));
            Vector3 cameraAt = rideAlong + Vector3.up * diveCameraHeight;
            if (t >= cameraStartsAt && !diveCamera.gameObject.activeSelf)
            {
                // Off until now, it was never moved: put it there first, so its first frame and the blend into it start from there.
                diveCamera.transform.SetPositionAndRotation(cameraAt, lookingDown);
                diveCamera.PreviousStateIsValid = false;
                diveCamera.gameObject.SetActive(true);
            }
            if (chase != null)
                chase.FollowOffset = cameraAt - next;
            // On both cameras, so the blend between them doesn't change the zoom.
            skyCamera.Lens.FieldOfView = diveCamera.Lens.FieldOfView = skyFieldOfView * diveFieldOfView.Evaluate(Mathf.Max(t - diveFieldOfViewBeat, 0f) / diveSeconds);
            // Faint at first, so the drop can still be seen.
            float white = Mathf.InverseLerp(dropStartsAt, dropStartsAt + whiteSeconds, t);
            SetAlpha(whiteFade, white * white);
            if (white >= 1f)
                yield break;
            yield return null;
        }
    }

    // Behind full white: the game camera takes over at once and the bird is at the stage's start.
    IEnumerator StartGame()
    {
        SetAlpha(whiteFade, 1f);
        title.gameObject.SetActive(false);
        skyCamera.gameObject.SetActive(false);
        diveCamera.gameObject.SetActive(false);
        // The clouds are only for up here, wherever in the scene they were placed.
        foreach (CloudGenerator cloud in FindObjectsByType<CloudGenerator>(FindObjectsSortMode.None))
            cloud.gameObject.SetActive(false);
        BirdController bird = BirdController.Instance;
        Perchable perch = startTree != null ? startTree.GetComponentInChildren<Perchable>() : null;
        if (perch == null)
            Debug.LogWarning(startTree == null
                ? "IntroSequence: Start Tree is empty, so the bird starts at the stage's Bird Start instead of landing. Set it on IntroSequence."
                : $"IntroSequence: Start Tree {startTree.name} has no Perchable under it, so the bird can't land on it.", this);
        if (perch != null)
            bird.HoldStill();
        else
            bird.EndIntro();
        GameFlow.Instance.FinishIntro(perch, dropHeight);

        // The brain starts its blend to the game camera in this frame's LateUpdate.
        yield return null;
        CinemachineBrain brain = Camera.main.GetComponent<CinemachineBrain>();
        if (brain != null)
            brain.ActiveBlend = null;

        // Right away: the landing is seen as the white clears.
        if (perch != null)
        {
            bird.EndIntro();
            bird.PerchOn(perch);
        }

        yield return new WaitForSeconds(fadeHoldSeconds);
        for (float t = 0f; t < fadeOutSeconds; t += Time.deltaTime)
        {
            SetAlpha(whiteFade, 1f - t / fadeOutSeconds);
            yield return null;
        }
        SetAlpha(whiteFade, 0f);
        gameObject.SetActive(false);
    }

    void OnDisable() => IsPlaying = false;

    bool ClickedThisFrame()
    {
        Mouse mouse = Mouse.current;
        return mouse != null && mouse.leftButton.wasPressedThisFrame && !DevTools.Covers(mouse.position.ReadValue());
    }

    bool ClickedOnBaby()
    {
        if (!ClickedThisFrame())
            return false;
        Vector3 baby = Camera.main.WorldToScreenPoint(Baby.Instance.transform.position);
        return baby.z > 0f && Vector2.Distance(baby, Mouse.current.position.ReadValue()) <= babyClickRadius * Screen.height;
    }

    // The point under a screen position at the height the bird flies.
    Vector3 AtFlightHeight(Vector2 screenPoint)
    {
        Ray ray = Camera.main.ScreenPointToRay(screenPoint);
        var height = new Plane(Vector3.up, orbitCentre.position);
        return height.Raycast(ray, out float distance) ? ray.GetPoint(distance) : orbitCentre.position;
    }

    static void SetAlpha(Graphic graphic, float alpha)
    {
        Color color = graphic.color;
        color.a = alpha;
        graphic.color = color;
    }
}
