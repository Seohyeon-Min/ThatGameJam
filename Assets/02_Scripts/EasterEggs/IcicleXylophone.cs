using System.Collections;
using FMODUnity;
using UnityEngine;
using UnityEngine.InputSystem;

// The icicle xylophone (easter_eggs.md): clicking an icicle drops it onto the rock below, where it rings one note of the scale
// (do re mi fa sol la ti do, eight icicles). It grows back after a while, so a tune can be played. The bird flies toward the click as usual.
// No note samples yet: each note is one sound at its own pitch.
public class IcicleXylophone : MonoBehaviour
{
    [System.Serializable]
    struct Icicle
    {
        [Tooltip("Its click collider must be on the Interactable layer.")]
        public Collider body;
        [Tooltip("Semitones above the sound's own pitch.")]
        public int semitone;
    }

    [SerializeField] Discovery discovery;
    [SerializeField] Icicle[] icicles;
    [SerializeField] EventReference note;
    [SerializeField, Min(0.1f)] float gravity = 9.8f;
    [SerializeField, Min(0f)] float regrowSeconds = 3f;

    int interactableMask;
    bool[] falling;

    void Awake()
    {
        interactableMask = LayerMask.GetMask("Interactable");
        falling = new bool[icicles.Length];
    }

    void Update()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame || Encyclopedia.IsOpen || Cutscene.PlayerLocked
            || DevTools.Covers(mouse.position.ReadValue()))
            return;
        Ray ray = Camera.main.ScreenPointToRay(mouse.position.ReadValue());
        if (!Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, interactableMask, QueryTriggerInteraction.Collide))
            return;
        for (int i = 0; i < icicles.Length; i++)
            if (hit.collider == icicles[i].body && !falling[i])
                StartCoroutine(Drop(i));
    }

    IEnumerator Drop(int index)
    {
        falling[index] = true;
        Icicle icicle = icicles[index];
        Transform body = icicle.body.transform;
        Vector3 home = body.localPosition;
        // Down onto whatever is under its tip: the rock, or the ground.
        Vector3 tip = icicle.body.bounds.center + Vector3.down * icicle.body.bounds.extents.y;
        float fall = Physics.Raycast(tip, Vector3.down, out RaycastHit below, 50f, ~interactableMask, QueryTriggerInteraction.Ignore)
            ? below.distance : 0f;
        Vector3 start = body.position;
        float speed = 0f, dropped = 0f;
        while (dropped < fall)
        {
            speed += gravity * Time.deltaTime;
            dropped = Mathf.Min(fall, dropped + speed * Time.deltaTime);
            body.position = start + Vector3.down * dropped;
            yield return null;
        }
        EggSound.Play(note, body.position, Mathf.Pow(2f, icicle.semitone / 12f));
        discovery.Reveal();
        body.gameObject.SetActive(false);

        yield return new WaitForSeconds(regrowSeconds);
        body.localPosition = home;
        body.gameObject.SetActive(true);
        falling[index] = false;
    }
}
