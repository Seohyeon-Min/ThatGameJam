using System.Collections;
using FMODUnity;
using UnityEngine;

// The snow whirl (easter_eggs.md): the bird lands on the snow pile, then circles round and round over it and a whirl of snow rises.
// Can be played again once it has died down.
public class SnowWhirl : MonoBehaviour
{
    [SerializeField] Discovery discovery;
    [SerializeField] Perchable pile;
    [SerializeField] ParticleSystem whirl;
    [SerializeField] EventReference sound;
    [Tooltip("The centre of the bird's circle, at the height it circles.")]
    [SerializeField] Transform circleCentre;
    [SerializeField, Min(0.1f)] float radius = 1.2f;
    [SerializeField, Min(0.1f)] float secondsPerTurn = 1.2f;
    [SerializeField, Min(0.1f)] float seconds = 4f;
    [Tooltip("Seconds to glide from the pile onto the circle.")]
    [SerializeField, Min(0.01f)] float joinSeconds = 0.6f;

    void OnEnable() => pile.Perched += OnPerched;
    void OnDisable() => pile.Perched -= OnPerched;

    void OnPerched() => StartCoroutine(Circle());

    IEnumerator Circle()
    {
        pile.CanPerch = false;
        BirdController bird = BirdController.Instance;
        pile.Release();
        bird.BeginScripted();
        whirl.Play();
        EggSound.Play(sound, circleCentre.position);

        Vector3 start = bird.transform.position;
        Vector3 offset = Vector3.ProjectOnPlane(start - circleCentre.position, Vector3.up);
        float angle = offset.sqrMagnitude > 0.0001f ? Mathf.Atan2(offset.z, offset.x) : 0f;
        float speed = 2f * Mathf.PI / secondsPerTurn;
        bool revealed = false;
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            angle += speed * Time.deltaTime;
            Vector3 onCircle = circleCentre.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            bird.MoveScripted(Vector3.Lerp(start, onCircle, Mathf.SmoothStep(0f, 1f, t / joinSeconds)));
            if (!revealed && t >= seconds * 0.5f)
            {
                revealed = true;
                discovery.Reveal();
            }
            yield return null;
        }

        whirl.Stop();
        bird.EndIntro();
        bird.Teleport(bird.transform.position);
        yield return new WaitWhile(() => whirl.IsAlive());
        pile.CanPerch = true;
    }
}
