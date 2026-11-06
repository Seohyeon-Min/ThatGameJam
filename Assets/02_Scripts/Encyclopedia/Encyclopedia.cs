using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// The list of interactions the player has found in this play session, each with a close-up photo of what was found,
// taken by the game at the moment the interaction shows itself.
// The ` key slides the page up from the bottom and pauses the game; the scene behind it blurs through a depth of field Volume.
// The same key closes it. Gameplay input checks IsOpen so clicks on the page do not reach the world.
public class Encyclopedia : MonoBehaviour
{
    [Tooltip("In page order. The number shown for an entry is its place here, from 1.")]
    [SerializeField] EncyclopediaEntry[] entries;
    [Tooltip("One per entry, in the same order.")]
    [SerializeField] EncyclopediaCard[] cards;
    [SerializeField] DiscoveryPopup popup;
    [SerializeField] RectTransform page;
    [Tooltip("Its weight follows the page, from 0 when hidden to 1 when fully up.")]
    [SerializeField] Volume blur;
    [SerializeField, Min(0.01f)] float slideSeconds = 0.35f;
    [Tooltip("Off. Takes the photos: it copies the main camera's settings, then goes to the photo view or moves in on the subject.")]
    [SerializeField] Camera photoCamera;
    [Tooltip("Size of the photo, in pixels. Match the cards' photo aspect.")]
    [SerializeField] Vector2Int photoSize = new Vector2Int(640, 360);
    [Tooltip("Room around the subject in the photo. 1 = its bounds just touch the picture's edge.")]
    [SerializeField, Min(1f)] float photoMargin = 1.15f;

    public static bool IsOpen { get; private set; }
    public static Encyclopedia Instance { get; private set; }

    readonly HashSet<EncyclopediaEntry> found = new HashSet<EncyclopediaEntry>();
    RenderTexture[] photos;
    float shown;
    float timeScaleBeforeOpen = 1f;

    public IReadOnlyList<EncyclopediaEntry> Entries => entries;

    public bool IsFound(EncyclopediaEntry entry) => found.Contains(entry);

    // Call this when the interaction shows itself (the squirrel's head is out, the trout has surfaced), not on the player's click.
    // The photo is taken now. With a photo view (an off camera placed in the scene for this interaction) it is taken from
    // that camera's place, angle and field of view. Without one, it is framed on the subject's renderers from the game camera's angle.
    public void Discover(EncyclopediaEntry entry, Transform subject, Camera photoView = null)
    {
        if (!found.Add(entry))
            return;
        int index = Array.IndexOf(entries, entry);
        photos[index] = TakePhoto(subject, photoView);
        cards[index].Show(index + 1, entry, photos[index]);
        popup.Show(index + 1, entry);
    }

    void Awake()
    {
        Instance = this;
        photos = new RenderTexture[entries.Length];
    }

    void Start()
    {
        for (int i = 0; i < entries.Length; i++)
            cards[i].Show(i + 1, entries[i], photos[i]);
        Apply();
    }

    // Screen space overlay UI (the popup, the page) is not part of a camera's render.
    RenderTexture TakePhoto(Transform subject, Camera photoView)
    {
        photoCamera.CopyFrom(Camera.main);
        photoCamera.aspect = (float)photoSize.x / photoSize.y;
        if (photoView != null)
        {
            photoCamera.fieldOfView = photoView.fieldOfView;
            photoCamera.transform.SetPositionAndRotation(photoView.transform.position, photoView.transform.rotation);
        }
        else
        {
            FrameSubject(subject);
        }

        var photo = new RenderTexture(photoSize.x, photoSize.y, 24);
        var request = new UniversalRenderPipeline.SingleCameraRequest { destination = photo };
        if (RenderPipeline.SupportsRenderRequest(photoCamera, request))
            RenderPipeline.SubmitRenderRequest(photoCamera, request);
        return photo;
    }

    // From the game camera's angle, as close as it can while every corner of the subject's bounds stays in the picture.
    void FrameSubject(Transform subject)
    {
        Bounds bounds = new Bounds(subject.position, Vector3.zero);
        foreach (Renderer part in subject.GetComponentsInChildren<Renderer>())
            bounds.Encapsulate(part.bounds);

        Quaternion rotation = Camera.main.transform.rotation;
        float tanHalfHeight = Mathf.Tan(photoCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) / photoMargin;
        float tanHalfWidth = tanHalfHeight * photoCamera.aspect;
        float distance = 0f;
        for (int corner = 0; corner < 8; corner++)
        {
            Vector3 offset = Vector3.Scale(bounds.extents, new Vector3(corner & 1, (corner >> 1) & 1, (corner >> 2) & 1) * 2f - Vector3.one);
            Vector3 view = Quaternion.Inverse(rotation) * offset;
            distance = Mathf.Max(distance, Mathf.Abs(view.x) / tanHalfWidth - view.z, Mathf.Abs(view.y) / tanHalfHeight - view.z,
                photoCamera.nearClipPlane - view.z);
        }
        photoCamera.transform.SetPositionAndRotation(bounds.center - rotation * Vector3.forward * distance, rotation);
    }

    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        // It does not open during the intro or a cutscene.
        if (keyboard != null && keyboard.backquoteKey.wasPressedThisFrame && (IsOpen || !IntroSequence.IsPlaying && !Cutscene.AnyPlaying))
            SetOpen(!IsOpen);

        float target = IsOpen ? 1f : 0f;
        if (shown == target)
            return;
        shown = Mathf.MoveTowards(shown, target, Time.unscaledDeltaTime / slideSeconds);
        Apply();
    }

    void SetOpen(bool open)
    {
        IsOpen = open;
        if (open)
        {
            timeScaleBeforeOpen = Time.timeScale;
            Time.timeScale = 0f;
        }
        else
        {
            Time.timeScale = timeScaleBeforeOpen;
        }
    }

    void Apply()
    {
        float eased = Mathf.SmoothStep(0f, 1f, shown);
        float hiddenY = -((RectTransform)page.parent).rect.height;
        page.anchoredPosition = new Vector2(page.anchoredPosition.x, Mathf.LerpUnclamped(hiddenY, 0f, eased));
        page.gameObject.SetActive(shown > 0f);
        blur.weight = eased;
    }

    void OnDisable()
    {
        if (IsOpen)
            SetOpen(false);
    }

    void OnDestroy()
    {
        foreach (RenderTexture photo in photos)
            if (photo != null)
                Destroy(photo);
    }
}
