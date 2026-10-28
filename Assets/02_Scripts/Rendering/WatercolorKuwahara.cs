using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public enum WatercolorFilterMode
{
    // Four square quadrants. Cheap baseline; shows blocky artifacts at large radii.
    Classic,
    // Elliptical kernel aligned to local image structure. Strokes follow object contours.
    Anisotropic,
}

// Art-direction side of the watercolor filter. Lives in a Volume profile so it blends between areas and can be
// changed at runtime. Performance settings (resolution, sampling, sectors) live on WatercolorKuwaharaFeature.
[Serializable, VolumeComponentMenu("ThatGameJam/Watercolor Kuwahara")]
[SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
public sealed class WatercolorKuwahara : VolumeComponent, IPostProcessComponent
{
    [Header("Filter")]
    [Tooltip("Turns the watercolor filter on. Off by default so adding the renderer feature changes nothing until a Volume enables it.")]
    public BoolParameter enableFilter = new BoolParameter(false);

    [Tooltip("Classic: four-quadrant Kuwahara. Anisotropic: structure-tensor guided elliptical kernel.")]
    public EnumParameter<WatercolorFilterMode> filterMode = new EnumParameter<WatercolorFilterMode>(WatercolorFilterMode.Anisotropic);

    [Tooltip("Kernel radius in pixels at 1080p. Scaled with screen height so the look matches at any resolution. Cost grows with the square of this.")]
    public ClampedFloatParameter filterRadius = new ClampedFloatParameter(8f, 1f, 16f);

    [Tooltip("How far the kernel stretches along edges. 0 = circle. Higher values give longer strokes that follow contours.")]
    public ClampedFloatParameter anisotropyStrength = new ClampedFloatParameter(1f, 0f, 2f);

    [Tooltip("How strongly low-variance sectors win. Low values blur; high values give flat, crisp paint regions.")]
    public ClampedFloatParameter sectorSharpness = new ClampedFloatParameter(8f, 1f, 16f);

    [Tooltip("Blend between the original image (0) and the painted image (1).")]
    public ClampedFloatParameter filterIntensity = new ClampedFloatParameter(1f, 0f, 1f);

    [Tooltip("Shrinks the kernel and restores some original detail at depth (and optional normal) edges, keeping silhouettes, faces and thin branches readable.")]
    public ClampedFloatParameter edgePreservation = new ClampedFloatParameter(0.6f, 0f, 1f);

    [Header("Watercolor")]
    [Tooltip("Groups brightness into soft bands to reduce small colour variation.")]
    public ClampedFloatParameter colorSimplification = new ClampedFloatParameter(0f, 0f, 1f);

    [Tooltip("Low-frequency unevenness in pigment density and saturation.")]
    public ClampedFloatParameter pigmentVariation = new ClampedFloatParameter(0.35f, 0f, 1f);

    [Tooltip("Noise-driven wobble of colour boundaries, like wet pigment spreading.")]
    public ClampedFloatParameter pigmentBleeding = new ClampedFloatParameter(0.3f, 0f, 1f);

    [Tooltip("Darkens colour boundaries where pigment pools as a wash dries.")]
    public ClampedFloatParameter edgeDarkening = new ClampedFloatParameter(0.3f, 0f, 1f);

    [Tooltip("Grayscale paper grain. Leave empty to use built-in procedural grain.")]
    public TextureParameter paperTexture = new TextureParameter(null);

    [Tooltip("Paper grain visibility.")]
    public ClampedFloatParameter paperTextureStrength = new ClampedFloatParameter(0.2f, 0f, 1f);

    [Tooltip("Paper grain size. 1 = a paper texture tile covers 512 px at 1080p.")]
    public ClampedFloatParameter paperTextureScale = new ClampedFloatParameter(1f, 0.25f, 4f);

    [Tooltip("Streaky texture that runs along object contours (from the structure tensor).")]
    public ClampedFloatParameter brushTextureStrength = new ClampedFloatParameter(0.25f, 0f, 1f);

    public bool IsActive() => enableFilter.value && filterIntensity.value > 0f;
}
