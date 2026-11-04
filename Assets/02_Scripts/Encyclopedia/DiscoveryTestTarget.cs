using UnityEngine;
using UnityEngine.InputSystem;

// Model test scenes only: stands in for the real interactions. A left click on this object's collider counts as
// the interaction showing itself: it finds the first entry not found yet, with this object as the photo's subject.
[RequireComponent(typeof(Collider))]
public class DiscoveryTestTarget : MonoBehaviour
{
    [SerializeField] Encyclopedia encyclopedia;
    [Tooltip("Optional. An off camera placed where the photo should be taken from. Empty: framed from the game camera's angle.")]
    [SerializeField] Camera photoView;

    Collider ownCollider;

    void Awake() => ownCollider = GetComponent<Collider>();

    void Update()
    {
        Mouse mouse = Mouse.current;
        if (Encyclopedia.IsOpen || mouse == null || !mouse.leftButton.wasPressedThisFrame)
            return;
        Ray ray = Camera.main.ScreenPointToRay(mouse.position.ReadValue());
        if (!Physics.Raycast(ray, out RaycastHit hit) || hit.collider != ownCollider)
            return;
        foreach (EncyclopediaEntry entry in encyclopedia.Entries)
        {
            if (!encyclopedia.IsFound(entry))
            {
                encyclopedia.Discover(entry, transform, photoView);
                return;
            }
        }
    }
}
