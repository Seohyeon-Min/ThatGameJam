using UnityEngine;

// On a perch idle state: each time one starts, picks which idle plays after it, so the bird does not loop one idle forever.
public class RandomPerchIdle : StateMachineBehaviour
{
    static readonly int IdleVariant = Animator.StringToHash("IdleVariant");

    [SerializeField] int variantCount = 3;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) =>
        animator.SetInteger(IdleVariant, Random.Range(0, variantCount));
}
