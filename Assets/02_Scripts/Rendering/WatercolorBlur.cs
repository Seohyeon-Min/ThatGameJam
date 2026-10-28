using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Lightweight watercolor post effect in one pass: a 3x3 bilateral blur that blends only similar colours, and an
// optional paper texture multiplied flat over the screen.
// Watercolor patterns (blotches, backruns) are baked per model by WatercolorBaker, not drawn here,
// because a screen-space pattern would stay fixed to the camera instead of the models.
// Pairs with the ThatGameJam/WatercolorLit material shader, which does the pigment and lighting work per object.
[Serializable, VolumeComponentMenu("ThatGameJam/Watercolor Blur")]
[SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
public sealed class WatercolorBlur : VolumeComponent, IPostProcessComponent
{
    [Tooltip("Turns the effect (blur, paper) on. Off by default so adding the renderer feature changes nothing until a Volume enables it.")]
    public BoolParameter enableBlur = new BoolParameter(false);

    [Tooltip("Distance between the 3x3 samples, in pixels at 1080p. Scaled with screen height. Above about 2 the gaps between samples start to show.")]
    public ClampedFloatParameter blurRadius = new ClampedFloatParameter(1.5f, 0.5f, 3f);

    [Tooltip("How different (RGB distance) a neighbour may be and still blend. Low keeps more detail; high smooths more but starts to cross soft edges.")]
    public ClampedFloatParameter colorThreshold = new ClampedFloatParameter(0.15f, 0.02f, 0.5f);

    [Tooltip("Blend between the original image (0) and the blurred image (1).")]
    public ClampedFloatParameter blurIntensity = new ClampedFloatParameter(1f, 0f, 1f);

    [Tooltip("Also refuse neighbours at a different depth, so similar colours on different objects (e.g. a green character in front of foliage) do not bleed. Needs the depth texture and 9 more samples.")]
    public BoolParameter depthAware = new BoolParameter(false);

    [Tooltip("Relative depth difference at which a neighbour stops blending (0.05 = 5% of the view distance).")]
    public ClampedFloatParameter depthThreshold = new ClampedFloatParameter(0.05f, 0.005f, 0.5f);

    [Header("Paper")]
    [Tooltip("Paper texture laid flat over the whole screen (no perspective), like a painting on paper. Unpainted white areas show it as is. Leave empty to use no paper.")]
    public TextureParameter paperTexture = new TextureParameter(null);

    [Tooltip("How strongly the paper texture shows through.")]
    public ClampedFloatParameter paperStrength = new ClampedFloatParameter(1f, 0f, 1f);

    [Tooltip("How many times the paper texture repeats over the screen height.")]
    public ClampedFloatParameter paperTiling = new ClampedFloatParameter(1f, 0.25f, 8f);

    bool HasPaper => paperTexture.value != null && paperStrength.value > 0f;

    public bool IsActive() => enableBlur.value && (blurIntensity.value > 0f || HasPaper);

    public Vector4 PaperParams(float aspect) => new Vector4(paperStrength.value, paperTiling.value, aspect, HasPaper ? 1f : 0f);
}
