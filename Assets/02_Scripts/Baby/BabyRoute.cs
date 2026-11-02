using UnityEngine;

// The path the baby walks: its child transforms, in order. Points carry height (for example along the fallen log).
// The last point is the sunlit rest area.
public class BabyRoute : MonoBehaviour
{
    // Sample count for the JumpTo arc preview below: dense enough to read as a smooth curve in the Scene view.
    const int JumpPreviewSamples = 20;

    public int Count => transform.childCount;
    public Transform this[int index] => transform.GetChild(index);

    void OnDrawGizmos()
    {
        for (int i = 0; i < Count; i++)
        {
            Transform point = this[i];
            Gizmos.color = point.TryGetComponent(out StopPoint _) ? Color.red : Color.yellow;
            Gizmos.DrawSphere(point.position, 0.15f);
            if (i > 0)
            {
                if (LaunchesJump(this[i - 1]))
                    DrawJumpArc(this[i - 1].position, point.position);
                else
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawLine(this[i - 1].position, point.position);
                }
            }
        }
    }

    // True for a point that starts a JumpTo at runtime (Baby.cs): one tagged BalanceBeamStart directly, or a
    // StopPoint whose blocker is a LeaningLog (that crossing isn't tagged; it starts the moment the log opens).
    static bool LaunchesJump(Transform point) =>
        point.TryGetComponent(out BalanceBeamStart _)
        || (point.TryGetComponent(out StopPoint stop) && stop.Blocker is LeaningLog);

    // Traces exactly what Baby.JumpTo will play (same Baby.EvaluateJump math, same GameTuning arc height), so
    // tuning babyJumpArcHeight or dragging either point updates this line with no Play Mode needed to check it.
    static void DrawJumpArc(Vector3 start, Vector3 target)
    {
        float arcHeight = GameTuning.Current != null ? GameTuning.Current.babyJumpArcHeight : 0f;
        Gizmos.color = Color.cyan;
        Vector3 previous = start;
        for (int s = 1; s <= JumpPreviewSamples; s++)
        {
            Vector3 point = Baby.EvaluateJump(start, target, s / (float)JumpPreviewSamples, arcHeight);
            Gizmos.DrawLine(previous, point);
            previous = point;
        }
    }
}
