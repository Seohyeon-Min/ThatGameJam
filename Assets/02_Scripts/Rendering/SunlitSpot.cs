using UnityEngine;

// The sunlit spot at the end of a route: one or more shafts of light. Each child with a LightBeam is one shaft; place and
// tilt them in the prefab. Level is how many are on, counted from the top of the hierarchy. Raising it in Play Mode brings
// the new shafts down from the sky, lowering it draws them back up. In Edit Mode the change shows at once.
[ExecuteAlways]
public class SunlitSpot : MonoBehaviour
{
    [Tooltip("How many shafts are on, counted from the first child.")]
    [SerializeField, Min(0)] int level = 1;
    [Tooltip("Seconds for a shaft to come down from its top to the ground. Its top is far off screen, so the first part isn't seen.")]
    [SerializeField, Min(0.01f)] float revealSeconds = 3f;

    LightBeam[] beams;
    float[] reveals;

    public void SetLevel(int newLevel) => level = Mathf.Max(0, newLevel);

    void OnEnable() => ShowLevelAtOnce();

    void OnValidate()
    {
        if (!Application.isPlaying)
            ShowLevelAtOnce();
    }

    void OnTransformChildrenChanged() => ShowLevelAtOnce();

    void Update()
    {
        if (!Application.isPlaying)
            return;

        for (int i = 0; i < beams.Length; i++)
        {
            float target = i < level ? 1f : 0f;
            if (reveals[i] == target)
                continue;
            reveals[i] = Mathf.MoveTowards(reveals[i], target, Time.deltaTime / revealSeconds);
            beams[i].SetReveal(reveals[i]);
        }
    }

    void ShowLevelAtOnce()
    {
        beams = GetComponentsInChildren<LightBeam>(true);
        reveals = new float[beams.Length];
        for (int i = 0; i < beams.Length; i++)
        {
            reveals[i] = i < level ? 1f : 0f;
            beams[i].SetReveal(reveals[i]);
        }
    }
}
