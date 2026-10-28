using UnityEngine;

// On an idle state: each time one starts, picks which idle plays after it, so a character does not loop one idle forever.
// The bird's perch idles and the baby's standing idles use it.
public class RandomIdle : StateMachineBehaviour
{
    static readonly int IdleVariant = Animator.StringToHash("IdleVariant");

    [SerializeField] int variantCount = 3;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) =>
        animator.SetInteger(IdleVariant, Random.Range(0, variantCount));
}
