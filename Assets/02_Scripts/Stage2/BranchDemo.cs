using System.Collections;
using UnityEngine;

// A separate bird shows the branch rule as the baby passes; it never controls the player's bird.
public class BranchDemo : MonoBehaviour
{
    static readonly int MovingParam = Animator.StringToHash("Moving");
    static readonly int PerchedParam = Animator.StringToHash("Perched");

    [SerializeField] Transform trigger;
    [SerializeField] SnowBranch branch;
    [SerializeField] Transform bird;
    [SerializeField] Animator animator;
    [SerializeField] Transform start;
    [SerializeField] Transform exit;

    Stage stage;
    bool shown;

    void Awake()
    {
        stage = GetComponentInParent<Stage>();
        branch.Perch.CanPerch = false;
        bird.gameObject.SetActive(false);
    }

    void Update()
    {
        if (shown || Baby.Instance == null || GameFlow.Instance == null || GameFlow.Instance.CurrentStage != stage)
            return;
        if (Baby.Instance.transform.position.z < trigger.position.z)
            return;
        Vector3 view = Camera.main.WorldToViewportPoint(branch.Perch.PerchPoint.position);
        if (view.z <= 0f || view.x < 0.08f || view.x > 0.92f || view.y < 0.08f || view.y > 0.92f)
            return;
        shown = true;
        StartCoroutine(Demonstrate());
    }

    IEnumerator Demonstrate()
    {
        bird.SetPositionAndRotation(start.position, start.rotation);
        bird.gameObject.SetActive(true);
        animator.SetBool(MovingParam, true);
        animator.SetBool(PerchedParam, false);
        yield return FlyTo(branch.Perch.PerchPoint);

        animator.SetBool(MovingParam, false);
        animator.SetBool(PerchedParam, true);
        float landingDuration = GameTuning.Current.birdPerchDuration;
        for (float elapsed = 0f; elapsed < landingDuration; elapsed += Time.deltaTime)
        {
            bird.position = branch.Perch.PerchPoint.position;
            yield return null;
        }

        branch.ShedSnow();
        animator.SetBool(PerchedParam, false);
        animator.SetBool(MovingParam, true);
        yield return FlyTo(exit);
        bird.gameObject.SetActive(false);
    }

    IEnumerator FlyTo(Transform target)
    {
        while (bird.position != target.position)
        {
            Vector3 direction = target.position - bird.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
                bird.rotation = Quaternion.LookRotation(direction);
            bird.position = Vector3.MoveTowards(bird.position, target.position, GameTuning.Current.birdSpeed * Time.deltaTime);
            yield return null;
        }
    }
}
