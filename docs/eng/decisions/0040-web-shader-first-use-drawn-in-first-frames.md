# 0040. Web first-use shader stalls are removed by drawing every material once in the game's first frames

- Status: Accepted
- Date: 2026-11-07
- Related: [ADR 0039](0039-web-build-frame-stalls-reduction.md)

## Context

After shader variant preloading (ShaderVariantCollection, Preloaded Shaders), the web build still stalled during the intro flight (about 8-45 s after start). In the Development web capture (`Builds/profile/spikes_174526.txt`) the median frame there was about 9 ms; nearly every frame over 50 ms was `SRPBRender.ApplyShader`, `StdRender.ApplyShader` or `Shader.CreateGPUProgram` (64-705 ms). WebGL appears to finish GPU programs at the first real draw. The user asked whether to reduce cloud puffs.

## Decision

1. `01_SYSTEM/ShaderPrewarm` (`ShaderPrewarm.cs`) draws each of the scene's 141 materials with `Graphics.RenderMesh` in the game's first 3 frames, on the renderer's own mesh (or a quad), scaled to 1e-5 in front of the camera so nothing shows. Layer, rendering layer and shadow settings come from the original renderer.
2. The list is filled by the editor builder `AgentScripts/BuildShaderPrewarm.cs` from the scene's renderers (inactive ones too) and script-only materials whose shader a renderer uses. Re-run it when materials are added.
3. This is an exception to AGENTS.md's no-startup-code rule, allowed by the user (2026-11-07). It creates no objects and changes no settings; it only draws.
4. Cloud puff count stays. Normal frames are light, so it is unrelated to the stalls.

## Alternatives

**1. Fewer cloud puffs.** Rejected: the stalls are first shader use; puff count only affects normal frame cost, and the look changes (ADR 0039).

**2. Spread the draws over the logo video.** Rejected: one program can take hundreds of ms, so the logo would stutter. All draws happen in the first frames, before the video is ready.

## Consequences

**Gains.** The intro's cloud, forest and character first-use shader costs move to the game's first frames.

**Costs.**

- The first frame is longer (loading looks slightly longer).
- Variants from global keywords enabled later (fog, etc.) and post-process passes (watercolor Kuwahara, etc.) are not drawn, so those stalls may remain.
- Re-run the builder after adding materials.
