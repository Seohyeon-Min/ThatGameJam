using System.Collections;
using UnityEngine;

// The cornelian cherries (easter_eggs.md): the bird lands in the bush and pecks a berry off, then puffs out its chest.
// One berry each time it lands, until none are left.
public class CornelianCherry : MonoBehaviour
{
    [SerializeField] Discovery discovery;
    [SerializeField] Perchable bush;
    [SerializeField] GameObject[] berries;
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
        berries[eaten++].SetActive(false);
        yield return new WaitForSeconds(bird.PuffChest());
        discovery.Reveal();
        bush.CanPerch = eaten < berries.Length;
    }
}
