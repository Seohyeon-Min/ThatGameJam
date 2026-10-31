using System.Collections;
using UnityEngine;

// The leaning dead tree by the water (design doc §7.3). It falls toward the water and becomes the baby's bridge,
// when the bird perches on top or when its timer runs out (the wind wins).
public class LeaningLog : PathBlocker
{
    [SerializeField] NaturalTimer timer;
    [SerializeField] Perchable top;
    [SerializeField] Animator animator;
    [SerializeField] AnimationClip fallClip;

    public override NaturalTimer Timer => timer;

    void Awake()
    {
        top.Perched += timer.ResolveByBird;
        timer.Resolved += byBird => StartCoroutine(Fall(byBird));
    }

    public override void BeginWaiting() => timer.Begin();

    IEnumerator Fall(bool byBird)
    {
        top.Release();
        top.CanPerch = false;
        animator.Play("Break", 0, 0f);
        yield return new WaitForSeconds(fallClip.length);
        Open(byBird);
    }
}
