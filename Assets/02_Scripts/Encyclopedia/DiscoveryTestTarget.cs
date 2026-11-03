using UnityEngine;
using UnityEngine.InputSystem;

// Model test scenes only: stands in for the real interactions. Each left click on this object's collider
// finds the first entry not found yet, so the encyclopedia fills up one entry per click.
[RequireComponent(typeof(Collider))]
public class DiscoveryTestTarget : MonoBehaviour
{
    [SerializeField] Encyclopedia encyclopedia;

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
                encyclopedia.Discover(entry);
                return;
            }
        }
    }
}
