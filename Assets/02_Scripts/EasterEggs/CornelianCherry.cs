using System.Collections;
using UnityEngine;

// The cornelian cherries (easter_eggs.md): the bird lands by a cluster of berries on the bush and pecks it off, then puffs out its chest.
// One cluster each time it lands, until none are left. The perch moves to the next cluster's spot.
public class CornelianCherry : MonoBehaviour
{
    [System.Serializable]
    struct Berry
    {
        public GameObject cluster;
        [Tooltip("Where the bird stands to peck it, facing it.")]
        public Transform eatFrom;
    }

    [SerializeField] Discovery discovery;
    [Tooltip("Its perch point starts at the first berry's Eat From.")]
    [SerializeField] Perchable bush;
    [SerializeField] Berry[] berries;
    [SerializeField, Min(0)] int pecks = 2;

    int eaten;

    void OnEnable() => bush.Perched += OnPerched;
    void OnDisable() => bush.Perched -= OnPerched;

    void OnPerched()
    {
        if (eaten < berries.Length)
            StartCoroutine(Eat());
    }

    IEnumerator Eat()
    {
        bush.CanPerch = false;
        BirdController bird = BirdController.Instance;
        yield return new WaitForSeconds(bird.Peck(pecks));
        berries[eaten++].cluster.SetActive(false);
        yield return new WaitForSeconds(bird.PuffChest());
        discovery.Reveal();
        if (eaten == berries.Length)
            yield break;
        // Once it has left: moving the perch under it would move the bird.
        yield return new WaitWhile(() => bird.Perch == bush);
        bush.PerchPoint.SetPositionAndRotation(berries[eaten].eatFrom.position, berries[eaten].eatFrom.rotation);
        bush.CanPerch = true;
    }
}
