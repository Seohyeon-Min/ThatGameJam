using System.Collections.Generic;
using UnityEngine;

// Debug only: tints the environment redder with each season step, to see that Season.OnSeasonChanged fires before any real
// environment change exists. Everything but the baby and the bird is tinted, through property blocks, so no material changes.
// Delete this component (the SeasonDebugTint object) to remove it; the tint goes with it.
public class SeasonDebugTint : MonoBehaviour
{
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    [SerializeField] Season season;
    // The level at which the tint is strongest.
    [SerializeField, Min(1)] int fullTintAt = 4;
    [SerializeField, Range(0f, 1f)] float maxStrength = 0.7f;

    readonly List<Renderer> tinted = new();
    MaterialPropertyBlock block;

    void OnEnable()
    {
        season.OnSeasonChanged.AddListener(Tint);
        Tint(season.Level);
    }

    void OnDisable()
    {
        season.OnSeasonChanged.RemoveListener(Tint);
        Clear();
    }

    void Tint(int level)
    {
        Clear();
        if (level <= 0)
            return;
        block ??= new MaterialPropertyBlock();
        float strength = maxStrength * Mathf.Clamp01((float)level / fullTintAt);
        foreach (Renderer renderer in FindObjectsByType<Renderer>())
        {
            if (renderer.GetComponentInParent<Baby>() != null || renderer.GetComponentInParent<BirdController>() != null)
                continue;
            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] == null || !materials[i].HasProperty(BaseColor))
                    continue;
                block.Clear();
                block.SetColor(BaseColor, Color.Lerp(materials[i].GetColor(BaseColor), Color.red, strength));
                renderer.SetPropertyBlock(block, i);
            }
            tinted.Add(renderer);
        }
    }

    void Clear()
    {
        block ??= new MaterialPropertyBlock();
        block.Clear();
        foreach (Renderer renderer in tinted)
        {
            if (renderer == null)
                continue;
            for (int i = 0; i < renderer.sharedMaterials.Length; i++)
                renderer.SetPropertyBlock(block, i);
        }
        tinted.Clear();
    }
}
