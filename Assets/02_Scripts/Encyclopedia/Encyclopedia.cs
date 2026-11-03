using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

// The list of interactions the player has found in this play session. The ` key slides the page up from the bottom
// and pauses the game; the scene behind it blurs through a depth of field Volume. The same key closes it.
// Gameplay input checks IsOpen so clicks on the page do not reach the world.
public class Encyclopedia : MonoBehaviour
{
    [Tooltip("In page order. The number shown for an entry is its place here, from 1.")]
    [SerializeField] EncyclopediaEntry[] entries;
    [Tooltip("One per entry, in the same order.")]
    [SerializeField] EncyclopediaCard[] cards;
    [SerializeField] DiscoveryPopup popup;
    [SerializeField] RectTransform page;
    [SerializeField] UnityEngine.UI.Text counter;
    [Tooltip("Its weight follows the page, from 0 when hidden to 1 when fully up.")]
    [SerializeField] Volume blur;
    [SerializeField, Min(0.01f)] float slideSeconds = 0.35f;

    public static bool IsOpen { get; private set; }

    readonly HashSet<EncyclopediaEntry> found = new HashSet<EncyclopediaEntry>();
    float shown;
    float timeScaleBeforeOpen = 1f;

    public IReadOnlyList<EncyclopediaEntry> Entries => entries;

    public bool IsFound(EncyclopediaEntry entry) => found.Contains(entry);

    public void Discover(EncyclopediaEntry entry)
    {
        if (!found.Add(entry))
            return;
        int index = Array.IndexOf(entries, entry);
        ShowCard(index);
        popup.Show(index + 1, entry);
    }

    void Start()
    {
        for (int i = 0; i < entries.Length; i++)
            ShowCard(i);
        Apply();
    }

    void ShowCard(int index)
    {
        cards[index].Show(index + 1, entries[index], found.Contains(entries[index]));
        counter.text = $"{found.Count} / {entries.Length}";
    }

    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.backquoteKey.wasPressedThisFrame)
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
}
