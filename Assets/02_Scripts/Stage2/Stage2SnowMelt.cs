using System.Collections;
using UnityEngine;

// Snowbanks shrink away when the rabbit stage ends (design doc §8.4).
public class Stage2SnowMelt : MonoBehaviour
{
    [SerializeField] Season season;
    [SerializeField] Transform[] snowbanks;
    [SerializeField, Min(0)] int meltAt = 3;
    [SerializeField, Min(0f)] float duration = 3f;

    Vector3[] initialScales;
    bool melting;

    void Awake()
    {
        initialScales = new Vector3[snowbanks.Length];
        for (int i = 0; i < snowbanks.Length; i++)
            initialScales[i] = snowbanks[i].localScale;
    }

    void OnEnable()
    {
        season.OnSeasonChanged.AddListener(Apply);
        Apply(season.Level);
    }

    void OnDisable() => season.OnSeasonChanged.RemoveListener(Apply);

    void Apply(int level)
    {
        if (level < meltAt || melting)
            return;
        melting = true;
        foreach (Transform bank in snowbanks)
            foreach (Collider collider in bank.GetComponentsInChildren<Collider>())
                collider.enabled = false;
        StartCoroutine(Melt());
    }

    IEnumerator Melt()
    {
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float remaining = 1f - Mathf.SmoothStep(0f, 1f, elapsed / duration);
            for (int i = 0; i < snowbanks.Length; i++)
            {
                Vector3 scale = initialScales[i];
                scale.y *= remaining;
                snowbanks[i].localScale = scale;
            }
            yield return null;
        }
        for (int i = 0; i < snowbanks.Length; i++)
        {
            Vector3 scale = initialScales[i];
            scale.y = 0f;
            snowbanks[i].localScale = scale;
            snowbanks[i].gameObject.SetActive(false);
        }
    }
}
