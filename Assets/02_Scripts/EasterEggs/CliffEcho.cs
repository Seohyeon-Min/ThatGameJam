using System.Collections;
using FMODUnity;
using UnityEngine;

// The cliff echo (easter_eggs.md): crying near the cliff's edge, the bird's cry comes back from far away, softer each time.
public class CliffEcho : MonoBehaviour
{
    [SerializeField] Discovery discovery;
    [Tooltip("The bird's cry.")]
    [SerializeField] EventReference cry;
    [Tooltip("How near the edge (this object) the bird must cry, in metres.")]
    [SerializeField, Min(0f)] float cryRadius = 8f;
    [Tooltip("Seconds after the cry each echo comes back, and how loud it is.")]
    [SerializeField] Vector2[] echoes = { new(0.7f, 0.45f), new(1.4f, 0.2f) };
    [Tooltip("Where the echoes come from: out beyond the edge.")]
    [SerializeField] Transform source;

    void OnEnable() => CryListener.Cried += OnCried;
    void OnDisable() => CryListener.Cried -= OnCried;

    void OnCried(Vector3 origin)
    {
        if (Vector3.Distance(origin, transform.position) <= cryRadius)
            StartCoroutine(Echo());
    }

    IEnumerator Echo()
    {
        float waited = 0f;
        foreach (Vector2 echo in echoes)
        {
            yield return new WaitForSeconds(echo.x - waited);
            waited = echo.x;
            EggSound.Play(cry, source.position, 1f, echo.y);
            discovery.Reveal();
        }
    }
}
