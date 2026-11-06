using System.Collections;
using UnityEngine;

// The ladybug in the stump (easter_eggs.md): the bird lands on the stump and pecks it, and a ladybug crawls out of it.
public class StumpLadybug : MonoBehaviour
{
    static readonly int CrawlParam = Animator.StringToHash("Crawl");

    [SerializeField] Discovery discovery;
    [SerializeField] Perchable stump;
    [Tooltip("Hidden until it crawls out.")]
    [SerializeField] Transform ladybug;
    [SerializeField] Animator ladybugAnimator;
    [Tooltip("Where it comes out, then where it crawls to.")]
    [SerializeField] Transform from, to;
    [SerializeField, Min(0)] int pecks = 2;
    [Tooltip("Metres per second at the ladybug's scale 1 (its notes: 0.2 keeps the feet from sliding).")]
    [SerializeField, Min(0.01f)] float crawlSpeed = 0.2f;

    void OnEnable() => stump.Perched += OnPerched;
    void OnDisable() => stump.Perched -= OnPerched;

    void OnPerched()
    {
        stump.CanPerch = false;
        StartCoroutine(CrawlOut());
    }

    IEnumerator CrawlOut()
    {
        yield return new WaitForSeconds(BirdController.Instance.Peck(pecks));
        ladybug.SetPositionAndRotation(from.position, Quaternion.LookRotation(Vector3.ProjectOnPlane(to.position - from.position, Vector3.up)));
        ladybug.gameObject.SetActive(true);
        ladybugAnimator.SetBool(CrawlParam, true);
        float speed = crawlSpeed * ladybug.lossyScale.x;
        while (ladybug.position != to.position)
        {
            ladybug.position = Vector3.MoveTowards(ladybug.position, to.position, speed * Time.deltaTime);
            yield return null;
        }
        ladybugAnimator.SetBool(CrawlParam, false);
        discovery.Reveal();
    }
}
