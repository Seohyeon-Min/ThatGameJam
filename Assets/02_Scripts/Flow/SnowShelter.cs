using UnityEngine;

// While the bird covers the walking baby (BirdController.IsCoveringBaby), no snow reaches her:
// Snowfall's Trigger module kills the snowflakes inside snowBlock, which follows her. Saved inactive; on only while covering.
public class SnowShelter : MonoBehaviour
{
    // Its box reaches from the baby's feet up to the bird's sheltering height.
    [SerializeField] Transform snowBlock;

    void LateUpdate()
    {
        bool covering = BirdController.Instance.IsCoveringBaby;
        snowBlock.gameObject.SetActive(covering);
        if (covering)
            snowBlock.position = Baby.Instance.transform.position;
    }
}
