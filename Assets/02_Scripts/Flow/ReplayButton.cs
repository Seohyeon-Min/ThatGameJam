using FMODUnity;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// The replay button on the ending's last card (bottom left, once the credits are up): a click on it plays the game again from
// the start, every sound stopped and the scene loaded anew. The game reads the mouse itself (there is no EventSystem), and so does
// this: a click inside it counts once it has faded in (its CanvasGroup's alpha, which EndingFlock raises). It grows a little
// while the pointer is over it.
[RequireComponent(typeof(CanvasGroup))]
public class ReplayButton : MonoBehaviour
{
    [Tooltip("Where a click counts. Empty: this object's own rect.")]
    [SerializeField] RectTransform hitArea;
    [Tooltip("What grows while the pointer is over it. Empty: this object.")]
    [SerializeField] Transform grows;
    [SerializeField, Min(1f)] float hoverScale = 1.1f;
    [Tooltip("Clicks count once it is at least this faded in.")]
    [SerializeField, Range(0f, 1f)] float readyAt = 0.9f;

    CanvasGroup group;
    Vector3 rest;
    bool going;

    void Awake()
    {
        group = GetComponent<CanvasGroup>();
        if (hitArea == null)
            hitArea = (RectTransform)transform;
        if (grows == null)
            grows = transform;
        rest = grows.localScale;
    }

    void Update()
    {
        Mouse mouse = Mouse.current;
        if (going || mouse == null || group.alpha < readyAt)
            return;
        Vector2 pointer = mouse.position.ReadValue();
        // Screen Space Overlay: no camera.
        bool over = RectTransformUtility.RectangleContainsScreenPoint(hitArea, pointer, null);
        Vector3 scale = over ? rest * hoverScale : rest;
        grows.localScale = Vector3.Lerp(grows.localScale, scale, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
        if (over && mouse.leftButton.wasPressedThisFrame)
            Replay();
    }

    void Replay()
    {
        going = true;
        // The ending's music is a fire-and-forget instance: it would play on into the new game.
        RuntimeManager.GetBus("bus:/").stopAllEvents(FMOD.Studio.STOP_MODE.IMMEDIATE);
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
