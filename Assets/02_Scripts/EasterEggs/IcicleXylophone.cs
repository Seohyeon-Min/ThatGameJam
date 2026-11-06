using System.Collections;
using FMODUnity;
using UnityEngine;
using UnityEngine.InputSystem;

// The icicle xylophone (easter_eggs.md): clicking an icicle sends the bird to the branch it hangs from (its Perchable); the bird pecks it,
// and as the beak touches it the icicle swings where it hangs and rings one note of the scale (do re mi fa sol la ti do, eight icicles).
// Clicking the icicle the bird is standing at pecks it again; another one, and the bird goes there.
// No note samples yet: each note is one sound at its own pitch.
public class IcicleXylophone : MonoBehaviour
{
    [System.Serializable]
    struct Icicle
    {
        [Tooltip("Its click collider must be on the Interactable layer.")]
        public Collider body;
        [Tooltip("Where the bird lands to tap it. Not under the icicle, which swings.")]
        public Perchable perch;
        [Tooltip("Semitones above the sound's own pitch.")]
        public int semitone;
    }

    [SerializeField] Discovery discovery;
    [SerializeField] Icicle[] icicles;
    [SerializeField] EventReference note;
    [Tooltip("How far it swings at first, in degrees, about the top where it hangs.")]
    [SerializeField, Range(0f, 45f)] float swingDegrees = 10f;
    [Tooltip("Swings per second.")]
    [SerializeField, Min(0.1f)] float swingRate = 5f;
    [Tooltip("Seconds until it hangs still again.")]
    [SerializeField, Min(0.1f)] float swingSeconds = 1.5f;

    int interactableMask;
    bool pecking;
    System.Action[] onPerched;
    Coroutine[] swings;
    Vector3[] homePositions;
    Quaternion[] homeRotations;
    // Where each hangs from: the top of its collider, hanging still.
    Vector3[] tops;

    void Awake()
    {
        interactableMask = LayerMask.GetMask("Interactable");
        swings = new Coroutine[icicles.Length];
        homePositions = new Vector3[icicles.Length];
        homeRotations = new Quaternion[icicles.Length];
        onPerched = new System.Action[icicles.Length];
        tops = new Vector3[icicles.Length];
        for (int i = 0; i < icicles.Length; i++)
        {
            icicles[i].body.transform.GetPositionAndRotation(out homePositions[i], out homeRotations[i]);
            Bounds bounds = icicles[i].body.bounds;
            tops[i] = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
            int index = i;
            onPerched[i] = () => StartCoroutine(PeckAndRing(index));
        }
    }

    void OnEnable()
    {
        for (int i = 0; i < icicles.Length; i++)
            icicles[i].perch.Perched += onPerched[i];
    }

    // Its swings stop with it: back to hanging still.
    void OnDisable()
    {
        for (int i = 0; i < icicles.Length; i++)
            icicles[i].perch.Perched -= onPerched[i];
        StopAllCoroutines();
        pecking = false;
        for (int i = 0; i < icicles.Length; i++)
        {
            icicles[i].body.transform.SetPositionAndRotation(homePositions[i], homeRotations[i]);
            swings[i] = null;
        }
    }

    // The bird is already at this icicle: clicked again, it pecks again. Clicks on the others are the bird's (it goes there).
    void Update()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame || Encyclopedia.IsOpen || Cutscene.PlayerLocked
            || DevTools.Covers(mouse.position.ReadValue()))
            return;
        BirdController bird = BirdController.Instance;
        Ray ray = Camera.main.ScreenPointToRay(mouse.position.ReadValue());
        if (!bird.IsPerched || !Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, interactableMask, QueryTriggerInteraction.Collide))
            return;
        for (int i = 0; i < icicles.Length; i++)
            if (hit.collider == icicles[i].body && bird.Perch == icicles[i].perch)
                StartCoroutine(PeckAndRing(i));
    }

    // Rings as the beak reaches the bottom of the peck.
    IEnumerator PeckAndRing(int index)
    {
        if (pecking)
            yield break;
        pecking = true;
        BirdController bird = BirdController.Instance;
        float seconds = bird.Peck(1);
        yield return new WaitForSeconds(seconds * 0.5f);
        // Not if it has flown off meanwhile.
        if (bird.Perch == icicles[index].perch)
            Tap(index);
        yield return new WaitForSeconds(seconds * 0.5f);
        pecking = false;
    }

    void Tap(int index)
    {
        Icicle icicle = icicles[index];
        EggSound.Play(note, icicle.body.bounds.center, Mathf.Pow(2f, icicle.semitone / 12f));
        discovery.Reveal();
        if (swings[index] != null)
            StopCoroutine(swings[index]);
        swings[index] = StartCoroutine(Swing(index));
    }

    // About the top where it hangs, side to side across the view, dying away.
    IEnumerator Swing(int index)
    {
        Transform body = icicles[index].body.transform;
        Vector3 home = homePositions[index];
        Quaternion homeRotation = homeRotations[index];
        Vector3 top = tops[index];
        Vector3 axis = Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up).normalized;
        for (float t = 0f; t < swingSeconds; t += Time.deltaTime)
        {
            float fade = 1f - t / swingSeconds;
            float angle = swingDegrees * fade * fade * Mathf.Sin(2f * Mathf.PI * swingRate * t);
            Quaternion turn = Quaternion.AngleAxis(angle, axis);
            body.SetPositionAndRotation(top + turn * (home - top), turn * homeRotation);
            yield return null;
        }
        body.SetPositionAndRotation(home, homeRotation);
        swings[index] = null;
    }
}
