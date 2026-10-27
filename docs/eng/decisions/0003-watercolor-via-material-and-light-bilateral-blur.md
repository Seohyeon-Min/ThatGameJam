# 0003. Watercolor look comes from a material shader and a light bilateral blur

- Status: accepted
- Date: 2026-10-27
- Related: `Assets/01_Arts/Shaders/WatercolorLit.shader`, `Assets/01_Arts/Shaders/WatercolorBlur.shader`, `Assets/02_Scripts/Rendering/`

## Context

To make the whole screen look like watercolor we first built a screen-space post effect modelled on Blender's Anisotropic
Kuwahara (structure tensor, elliptical kernel, pigment / bleeding / paper / brush compositing). The target platform is the web
browser, so GPU time is tight. The user judged the filter too heavy for a real-time game and asked to get the watercolor feel from
materials instead, with post-processing reduced to a light blur that only mixes similar colours.

GPU time per pass, 1080p, RTX 3050 Ti Laptop, Play mode GPU Recorder:

| Approach | GPU time |
|----------|----------|
| Kuwahara, full resolution | about 14 ms (11.8 ms for the Kuwahara pass alone) |
| Kuwahara High / Medium / Low (half resolution) | 1.25 / 0.95 / 0.60 ms |
| Bilateral blur 3x3 | 0.135 ms |
| Bilateral blur 3x3 + depth | 0.26 ms |

In the same scene the `WatercolorLit` material gave about 1.3 ms shorter frames than URP Lit (edit-mode wall-clock measurement, so
the error is large). It does not compute specular or environment reflections.

## Decision

The user decided.

**Each object gets its watercolor shading and pigment variation from the `ThatGameJam/WatercolorLit` material, and
post-processing is one `WatercolorBlur` pass (3x3 bilateral).** Both renderers (PC, Mobile) have `WatercolorBlurFeature` active
and keep `WatercolorKuwaharaFeature` inactive. Both features only run when a Volume enables them, so `Main.unity` does not change.

## Alternatives

**Alternative 1. Keep the Anisotropic Kuwahara filter.**
Deferred. After moving to half resolution and replacing the per-pixel hash noise with a texture it costs 0.6 to 1.25 ms, and it
produces brush-like colour regions that a blur cannot. It is still 4 to 9 times the cost of the blur and may be slower in a web
browser. The code stays so it can be compared by ticking the feature on a renderer.

**Alternative 2. Run the blur at half resolution.**
Rejected. The blur is a single 9-sample pass at 0.135 ms. Half resolution needs extra downsample and upsample passes, which cost more
than they save.

**Alternative 3. Materials only, no post-processing.**
Possible. Turn the blur off in the Volume. The blur only softens colour boundaries a little, so decide by looking at the scene.

## Consequences

**Gains.**

- Post-processing costs 0.135 ms (0.26 ms with depth)
- Artists tune shadow colour, pigment strength and brush texture per material
- Pigment variation is attached to world (or object) space, so it does not slide across the screen when the camera moves

**Costs.**

- Every model has to use `WatercolorLit` to look like watercolor. Materials imported from Blender have to be switched one by one
- Leaf billboards (`LeafBillboard.shader`) use their own shader and have no pigment variation
- With no specular or environment reflection, grazing surfaces look darker and more saturated than with URP Lit. Adjust with Base Color
- The material ignores directional lights other than the main light (the Forward+ extra directional light loop is not included)
- Not yet checked in a WebGL build
