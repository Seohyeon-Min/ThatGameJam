using UnityEngine;

// Per-model watercolor bake settings and results. Put it on a model's root and press Bake in the Inspector
// (or Tools > Watercolor > Bake Selected Models). Baking is editor-only and nothing here runs per frame.
//
// The bake (WatercolorBaker):
// - groups the model's renderers by material and distance, so nearby pieces of the same colour are painted as one mass;
// - replaces each piece's normals with the normal of its mass's enclosing ellipsoid, so the light-side paper area and
//   the outline follow the mass, not the pieces;
// - stores each vertex's position in model space (rest pose, 0-1 box) in UV3, so the pattern sticks to the body;
// - bakes a 3D pattern texture (pigment blotches, backrun blooms);
// - assigns ThatGameJam/WatercolorFlat materials and the baked mesh copies to the renderers.
// Instances of the same prefab share one bake (Assets/01_Arts/Baked/Shared/<prefab>): the shaders flip and shift the
// pattern per instance by its Variation, passed to every renderer as the shader user value, so they still differ.
// Re-bake after re-exporting the model from Blender, because the mesh copies are made from the FBX meshes.
[ExecuteAlways]
public class WatercolorPaintedModel : MonoBehaviour
{
    [Tooltip("Share the bake with every other instance of the same prefab (one set of meshes, textures and materials). Turn off to give this instance its own bake.")]
    public bool shareBake = true;

    [Tooltip("Upper limit of voxels per side for the 3D pattern (32 = 128 KB, 64 = 1 MB). The actual count follows the on-screen blotch size set in WatercolorManager.")]
    [Range(16, 128)] public int patternResolution = 32;

    [Tooltip("Same-coloured pieces closer than this (relative to the model's longest side) are painted as one mass. Raise it if strands of one mass stay separate; lower it if separate body parts merge.")]
    [Range(0f, 0.3f)] public float groupDistance = 0.03f;

    [Tooltip("How much each piece's normal is replaced by its colour group's mass normal. 1 = the group shades as one rounded shape; 0 = original normals.")]
    [Range(0f, 1f)] public float massNormal = 1f;

    [Tooltip("Only used when the scene has no Main Camera: size of pigment blotches relative to the model's longest side. Otherwise the size comes from WatercolorManager (% of screen).")]
    [Range(0.03f, 1f)] public float pigmentScale = 0.2f;

    [Tooltip("Only used when the scene has no Main Camera: size of backrun blooms relative to the model's longest side. Otherwise the size comes from WatercolorManager (% of screen).")]
    [Range(0.03f, 1f)] public float backrunScale = 0.25f;

    [Tooltip("Share of the surface covered by backrun blooms.")]
    [Range(0f, 1f)] public float backrunCoverage = 0.3f;

    [Tooltip("Changes the pattern. Two models with the same seed and size get the same blotches. Ignored for shared bakes.")]
    public int seed;

    [Header("Baked (written by the bake)")]
    public Texture3D pattern;
    public Bounds modelBounds;
    [Tooltip("How this instance flips and shifts the shared pattern (1-255; 0 = as baked).")]
    public int variation;

    void OnEnable() => ApplyVariation();
    void OnValidate() => ApplyVariation();

    // The shader user value is not saved with the scene, so it is set again whenever the model is enabled.
    public void ApplyVariation()
    {
        uint value = (uint)Mathf.Clamp(variation, 0, 255);
        foreach (var renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (renderer is MeshRenderer mesh)
                mesh.SetShaderUserValue(value);
            else if (renderer is SkinnedMeshRenderer skinned)
                skinned.SetShaderUserValue(value);
        }
    }
}
