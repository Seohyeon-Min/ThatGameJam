using System.Collections;
using FMODUnity;
using UnityEngine;

// The cliff echo (easter_eggs.md): crying anywhere by the cliff, out of the fog, the bird's cry comes back from far away, softer each
// time. The photo is of the bird.
public class CliffEcho : MonoBehaviour
{
    [SerializeField] Discovery discovery;
    [Tooltip("The bird's cry.")]
    [SerializeField] EventReference cry;
    [Tooltip("World Z past which, out of the fog, a cry anywhere up to the edge echoes (the yellow line).")]
    [SerializeField] float fromZ = 230f;
    [Tooltip("Seconds after the cry each echo comes back, and how loud it is.")]
    [SerializeField] Vector2[] echoes = { new(0.7f, 0.45f), new(1.4f, 0.2f) };
    [Tooltip("Where the echoes come from: out beyond the edge.")]
    [SerializeField] Transform source;

    void OnEnable() => CryListener.Cried += OnCried;
    void OnDisable() => CryListener.Cried -= OnCried;

    void OnCried(Vector3 origin)
    {
        if (origin.z >= fromZ)
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
            discovery.Reveal(BirdController.Instance != null ? BirdController.Instance.transform : null);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(new Vector3(-200f, transform.position.y, fromZ), new Vector3(200f, transform.position.y, fromZ));
    }
}
