using System.Collections;
using UnityEngine;

// The squirrel in the tree hollow (easter_eggs.md): when the bird cries near the hollow, a squirrel peeks out, then goes back in.
public class HollowSquirrel : MonoBehaviour
{
    [SerializeField] Discovery discovery;
    [SerializeField] Transform squirrel;
    [Tooltip("Where the squirrel sits hidden, inside the hollow, and where it peeks out to.")]
    [SerializeField] Transform inside, peek;
    [Tooltip("How near the hollow the bird must cry, in metres.")]
    [SerializeField, Min(0f)] float cryRadius = 6f;
    [SerializeField, Min(0.01f)] float moveSeconds = 0.35f;
    [SerializeField, Min(0f)] float lookSeconds = 1.5f;

    bool busy;

    void OnEnable() => CryListener.Cried += OnCried;
    void OnDisable() => CryListener.Cried -= OnCried;

    void OnCried(Vector3 origin)
    {
        if (!busy && Vector3.Distance(origin, peek.position) <= cryRadius)
            StartCoroutine(PeekOut());
    }

    IEnumerator PeekOut()
    {
        busy = true;
        yield return Move(inside, peek);
        discovery.Reveal();
        yield return new WaitForSeconds(lookSeconds);
        yield return Move(peek, inside);
        busy = false;
    }

    IEnumerator Move(Transform from, Transform to)
    {
        for (float t = 0f; t < moveSeconds; t += Time.deltaTime)
        {
            float eased = Mathf.SmoothStep(0f, 1f, t / moveSeconds);
            squirrel.SetPositionAndRotation(Vector3.Lerp(from.position, to.position, eased), Quaternion.Slerp(from.rotation, to.rotation, eased));
            yield return null;
        }
        squirrel.SetPositionAndRotation(to.position, to.rotation);
    }
}
