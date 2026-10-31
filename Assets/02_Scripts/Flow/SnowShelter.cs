using UnityEngine;

// While the bird shelters the walking baby (BirdController.IsCoveringBaby): the bird's shadow falls over her and no snow reaches her.
// The sun is low, so the bird's own shadow lands metres away. The shadow here comes from a shadow-only copy of the bird,
// placed between the sun and the baby. The snow is removed by Snowfall's Trigger module inside snowBlock.
// Both are saved inactive; they show only while sheltering.
public class SnowShelter : MonoBehaviour
{
    [SerializeField] Light sun;
    // A shadows-only copy of the bird; its saved scale is the full size of the shadow.
    [SerializeField] Transform shadowCaster;
    // The trigger the snowfall's Trigger module kills particles in. Its box reaches from the baby's feet up to the bird.
    [SerializeField] Transform snowBlock;
    // The shadow is aimed at this height on the baby, and the caster sits this far from it toward the sun.
    [SerializeField] float aimHeight = 1f;
    [SerializeField] float casterDistance = 4f;
    // Seconds for the shadow to grow in and shrink away.
    [SerializeField] float fadeTime = 0.3f;

    Vector3 shadowScale;
    float shown;

    void Awake() => shadowScale = shadowCaster.localScale;

    void LateUpdate()
    {
        bool sheltering = BirdController.Instance.IsCoveringBaby;
        shown = Mathf.MoveTowards(shown, sheltering ? 1f : 0f, Time.deltaTime / fadeTime);
        snowBlock.gameObject.SetActive(sheltering);
        shadowCaster.gameObject.SetActive(shown > 0f);
        Transform baby = Baby.Instance.transform;
        snowBlock.position = baby.position;
        if (shown <= 0f)
            return;
        Vector3 toSun = -sun.transform.forward;
        shadowCaster.position = baby.position + Vector3.up * aimHeight + toSun * casterDistance;
        // Back to the sun with the wings spread: seen from a low sun, a level bird would be a thin sliver and miss most of her.
        Vector3 forward = Vector3.ProjectOnPlane(baby.forward, toSun);
        if (forward.sqrMagnitude > 0.0001f)
            shadowCaster.rotation = Quaternion.LookRotation(forward, toSun);
        shadowCaster.localScale = shadowScale * shown;
    }
}
