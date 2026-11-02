using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// The game's opening, above the clouds. The title shows until the first click; the bird flies in from below the screen to that click.
// The sky camera then travels to its end point without turning while the player flies the bird by clicking, as in the game.
// Near the end the bird curves in, slowing, and goes straight into circling over the hole in the clouds. A click on the baby sends it diving, and the game
// starts behind a white fade. Everything here is saved in the scene: this only turns it on and off.
public class IntroSequence : MonoBehaviour
{
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
    [SerializeField, Min(0.01f)] float fadeInSeconds = 1f;
    [SerializeField, Min(0f)] float fadeHoldSeconds = 0.2f;
    [SerializeField, Min(0.01f)] float fadeOutSeconds = 1.2f;

    [Header("Bird")]
    [SerializeField, Min(0.01f)] float flyInSeconds = 1.2f;
    [Tooltip("The bird circles this point, at this point's height. That height is also where it flies during the whole opening.")]
    [SerializeField] Transform orbitCentre;
    [SerializeField, Min(0.1f)] float orbitRadius = 1.5f;
    [SerializeField, Min(0.01f)] float orbitSpeed = 1.5f;
    [Tooltip("Seconds before the camera arrives that the player's control ends and the bird curves into the circle, slowing down to Orbit Speed.")]
    [SerializeField, Min(0.1f)] float approachSeconds = 6f;
    [Tooltip("How far around the baby a click counts, as a fraction of the screen's height.")]
    [SerializeField, Range(0.01f, 0.5f)] float babyClickRadius = 0.1f;
    [SerializeField, Min(0.1f)] float diveSeconds = 4f;
    [Tooltip("Placeholder for the dive animation: how far the body tips over during the dive, in degrees. Negative tips it the other way.")]
    [SerializeField] float diveBodyPitch = 45f;

    float cameraSecondsLeft = float.MaxValue;
    bool running;

    public void Play()
    {
        running = true;
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
        diveCamera.gameObject.SetActive(true);
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

    IEnumerator MoveCamera()
    {
        Transform moved = skyCamera.transform;
        for (float t = 0f; t < flightSeconds; t += Time.deltaTime)
        {
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
            float u = t / approachSeconds, u2 = u * u, u3 = u2 * u;
            bird.MoveScripted((2f * u3 - 3f * u2 + 1f) * from + (u3 - 2f * u2 + u) * startTangent
                + (-2f * u3 + 3f * u2) * to + (u3 - u2) * endTangent);
            yield return null;
        }
        reachedAngle(angle);
    }

    IEnumerator CircleUntilBabyClicked(float angle)
    {
        BirdController bird = BirdController.Instance;
        while (!ClickedOnBaby())
        {
            angle += orbitSpeed / orbitRadius * Time.deltaTime;
            bird.MoveScripted(OnCircle(angle));
            yield return null;
        }
    }

    Vector3 OnCircle(float angle) => orbitCentre.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * orbitRadius;

    IEnumerator Dive()
    {
        BirdController bird = BirdController.Instance;
        Vector3 from = bird.transform.position;
        Stage first = GameFlow.Instance.CurrentStage;
        Vector3 to = first.birdStart.position;
        to.y = first.birdAltitude;
        for (float t = 0f; t < diveSeconds; t += Time.deltaTime)
        {
            float progress = t / diveSeconds;
            bird.MoveScripted(Vector3.Lerp(from, to, progress * progress), diveBodyPitch * Mathf.Sin(progress * Mathf.PI));
            SetAlpha(whiteFade, Mathf.InverseLerp(diveSeconds - fadeInSeconds, diveSeconds, t));
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
        BirdController.Instance.EndIntro();
        GameFlow.Instance.FinishIntro();

        // The brain starts its blend to the game camera in this frame's LateUpdate.
        yield return null;
        CinemachineBrain brain = Camera.main.GetComponent<CinemachineBrain>();
        if (brain != null)
            brain.ActiveBlend = null;

        yield return new WaitForSeconds(fadeHoldSeconds);
        for (float t = 0f; t < fadeOutSeconds; t += Time.deltaTime)
        {
            SetAlpha(whiteFade, 1f - t / fadeOutSeconds);
            yield return null;
        }
        gameObject.SetActive(false);
    }

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
