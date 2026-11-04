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
    // Lays new brush strokes over the picture: flat, crisp-edged dabs along the form, each a little off in value and hue.
    // For flat-coloured subjects (the baby), where a Kuwahara has too little variation to work with. Settings under Strokes.
    Strokes,
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

    [Tooltip("Paints only the characters (WatercolorFlat's Painted As = Character, stencil bit 128) and leaves the rest of the picture as it is. For close-ups of the baby, such as her getting up.")]
    public BoolParameter charactersOnly = new BoolParameter(false);

    [Tooltip("Kernel radius in pixels at 1080p. Scaled with screen height so the look matches at any resolution. Cost grows with the square of this.")]
    public ClampedFloatParameter filterRadius = new ClampedFloatParameter(8f, 1f, 32f);

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

    [Header("Strokes (Filter Mode = Strokes)")]
    [Tooltip("How far apart the strokes are, in centimetres on the character (PaintStrokeAnchor): about the width of one dab. Small enough keeps the eyes and the flowers readable. Without an anchor, 1 cm is 20 pixels at 1080p.")]
    public ClampedFloatParameter strokeSize = new ClampedFloatParameter(0.8f, 0.2f, 5f);

    [Tooltip("Stroke length, as a share of Stroke Size. Each stroke varies a little around it.")]
    public ClampedFloatParameter strokeLength = new ClampedFloatParameter(2.2f, 1f, 3f);

    [Tooltip("Stroke width, as a share of Stroke Size. Above about 0.8 they overlap and cover everything.")]
    public ClampedFloatParameter strokeWidth = new ClampedFloatParameter(0.9f, 0.2f, 1.5f);

    [Tooltip("Ragged stroke edges. 0 = clean ellipses.")]
    public ClampedFloatParameter strokeRoughness = new ClampedFloatParameter(0.35f, 0f, 1f);

    [Tooltip("Characters Only: how far the strokes reach out past the character's outline, along their own way, as a share of their length. 0 = kept inside it, 1 = whole strokes, above 1 = drawn out further.")]
    public ClampedFloatParameter strokeSpill = new ClampedFloatParameter(0.5f, 0f, 1.5f);

    [Tooltip("Wide areas of one colour (skin) get smoother: there the strokes differ less from each other, so they melt into one surface. 0 = as much as anywhere else, 1 = no difference.")]
    public ClampedFloatParameter strokeSmoothFlat = new ClampedFloatParameter(0.7f, 0f, 1f);

    [Tooltip("Share of the places that get a stroke. Below 1 lets the picture underneath show between them.")]
    public ClampedFloatParameter strokeCoverage = new ClampedFloatParameter(1f, 0f, 1f);

    [Tooltip("Which way the strokes run. 0 = along colour edges, 1 = around the form (along the surface's depth contours, like hair flowing round the head).")]
    public ClampedFloatParameter strokeFollowForm = new ClampedFloatParameter(0.7f, 0f, 1f);

    [Tooltip("How much each stroke's brightness differs from the colour under it.")]
    public ClampedFloatParameter strokeValueJitter = new ClampedFloatParameter(0.06f, 0f, 0.3f);

    [Tooltip("How far each stroke's hue turns from the colour under it, in degrees.")]
    public ClampedFloatParameter strokeHueJitter = new ClampedFloatParameter(6f, 0f, 30f);

    [Tooltip("How much each stroke's saturation differs from the colour under it.")]
    public ClampedFloatParameter strokeSaturationJitter = new ClampedFloatParameter(0.1f, 0f, 0.5f);

    [Tooltip("Share of strokes that stand out: their brightness, hue and saturation differ Strong Stroke Boost times as much.")]
    public ClampedFloatParameter strongStrokeShare = new ClampedFloatParameter(0.15f, 0f, 1f);

    [Tooltip("How much more the standing-out strokes differ.")]
    public ClampedFloatParameter strongStrokeBoost = new ClampedFloatParameter(2.5f, 1f, 5f);

    [Tooltip("Keeps fine marks a stroke would paint over (eyelids, the mouth): where the picture differs this clearly from the stroke, it shows through. 0 = strokes cover everything.")]
    public ClampedFloatParameter strokeKeepDetail = new ClampedFloatParameter(0.5f, 0f, 1f);

    public bool IsActive() => enableFilter.value && filterIntensity.value > 0f;
}
