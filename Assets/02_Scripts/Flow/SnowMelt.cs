using System.Collections;
using UnityEngine;

// The snow on the ground melting as the seasons go on: how far melted it is (0 to 1, the floor materials' Snow Melt in the
// WatercolorFlat shader) at each season level. Element 0 is level 0 (the intro), element 1 level 1, and so on; past the end, the
// last. A change comes a frame after the season's, once the season wipe has taken its photo of the ground as it was, so the
// melt spreads out from the spring ring with the rest of the new season.
public class SnowMelt : MonoBehaviour
{
    static readonly int MeltId = Shader.PropertyToID("_WC_SnowMelt");

    [SerializeField] Season season;
    [Tooltip("How far melted (0 to 1) the ground's snow is at each season level, from level 0.")]
    [SerializeField] float[] meltByLevel = { 0f, 0f, 0f, 0.3f, 1f };

    void OnEnable()
    {
        if (season == null)
            return;
        season.OnSeasonChanged.AddListener(OnSeasonChanged);
        Shader.SetGlobalFloat(MeltId, MeltAt(season.Level));
    }

    void OnDisable()
    {
        if (season != null)
            season.OnSeasonChanged.RemoveListener(OnSeasonChanged);
        Shader.SetGlobalFloat(MeltId, 0f);
    }

    void OnSeasonChanged(int level)
    {
        if (isActiveAndEnabled)
            StartCoroutine(SetNextFrame(MeltAt(level)));
    }

    static IEnumerator SetNextFrame(float melt)
    {
        yield return null;
        Shader.SetGlobalFloat(MeltId, melt);
    }

    float MeltAt(int level)
    {
        if (meltByLevel == null || meltByLevel.Length == 0)
            return 0f;
        return Mathf.Clamp01(meltByLevel[Mathf.Clamp(level, 0, meltByLevel.Length - 1)]);
    }
}
