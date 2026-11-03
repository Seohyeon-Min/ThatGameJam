using System.Collections;
using UnityEngine;

// Walks a creature around its points in a loop and rests at each one, for model test scenes.
// Turns in place toward the next point first, so it never slides sideways. Points sit on the ground.
public class WaypointWalker : MonoBehaviour
{
    [SerializeField] Animator animator;
    [Tooltip("Animator Bool that is on while moving (the ladybug's Crawl, the girl's Walking).")]
    [SerializeField] string moveParameter = "Crawl";
    [Tooltip("State played at the start, when the controller's default state is not a standing one. Empty keeps the default.")]
    [SerializeField] string startState;
    [SerializeField] Transform[] points;
    [Tooltip("Metres per second. The ladybug's crawl feet match 0.2.")]
    [SerializeField, Min(0.01f)] float speed = 0.2f;
    [Tooltip("Degrees per second while turning toward the next point.")]
    [SerializeField, Min(1f)] float turnSpeed = 120f;
    [Tooltip("Seconds it rests at each point.")]
    [SerializeField, Min(0f)] float restSeconds = 2f;

    IEnumerator Start()
    {
        int moveHash = Animator.StringToHash(moveParameter);
        if (!string.IsNullOrEmpty(startState))
            animator.Play(startState, 0, 0f);
        for (int next = 0; ; next = (next + 1) % points.Length)
        {
            Vector3 target = points[next].position;
            animator.SetBool(moveHash, true);
            while (transform.position != target)
            {
                Vector3 toTarget = target - transform.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.000001f)
                {
                    Quaternion facing = Quaternion.LookRotation(toTarget);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, facing, turnSpeed * Time.deltaTime);
                    if (Quaternion.Angle(transform.rotation, facing) > 1f)
                    {
                        yield return null;
                        continue;
                    }
                }
                transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
                yield return null;
            }
            animator.SetBool(moveHash, false);
            yield return new WaitForSeconds(restSeconds);
        }
    }
}
