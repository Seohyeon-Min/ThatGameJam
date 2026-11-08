# 0037. The Web build uses the single PC quality level, and only the long music is Vorbis

- Status: accepted
- Date: 2026-11-07
- Related: [`../implementation_plan.md`](../implementation_plan.md)

## Context

The director asked for Web build optimization. The audit found two things first.

- The WebGL default in `QualitySettings` was `Mobile` (`Mobile_RPAsset`, `Mobile_Renderer`). The Editor ran `PC`. The stage 3 renderer features `ForestMist`, `DreamFog`, `AboveFog` and `BabyPrism` were only on `PC_Renderer`, so the Web build drew no fog and no baby prism. `Mobile_Renderer` also had no depth prepass, so `OcclusionFade` could not smooth the tree window.
- JU switched the FMOD Desktop encoding to PCM in `e351405` ("change sound to pcm"). `Master.bank` became 42.6 MB, most of it a few long music files.

At first the missing features were copied to `Mobile_Renderer` to use it as the Web renderer (`AgentScripts/SyncWebRenderer.cs`). Playing it, the shadows were much worse than in the Editor (1024, 1 cascade, soft off). The Unity docs do not require the Mobile level for Web; they only recommend a lower level. So this was decided again.

## Decision

1. WebGL and the Editor both use the `PC` quality level. The Editor shows what the Web build shows, and a renderer feature goes in one place.
2. Remove from `PC` what is costly on Web and unused.
   - SSAO off. The watercolor shaders do not read AO.
   - Opaque Texture off. No shader reads it.
   - Forward+ to Forward. The only light on is one Directional.
   - Shadows: 2048, cascades 4 → 2, distance 50 → 40 m (the visible depth is about 40 m), Soft High → Medium.
   - Force the depth prepass (`Depth Texture Mode: Force Prepass`). Before, SSAO's DepthNormals made the prepass and `OcclusionFade` worked by accident.
3. In FMOD, only the 8 long streamed files (4 BGM, 2 cutscenes, wind, birds) are overridden to Vorbis (quality 40) on Desktop. Short sound effects stay PCM (user's decision): no decode cost or latency.

## Alternatives

**Alternative 1. Keep `Mobile` as the Web level and raise only its shadows.**
Rejected. With different renderers for the Editor and Web, a feature can land on one side again, as the fog did. The render scale 0.8 gain is available through the `PC` render scale.

**Alternative 2. Put all of FMOD back on Vorbis.**
Rejected. It drops JU's reason for PCM (latency and CPU for short effects).

## Consequences

**Gains.**

- The Web build draws the fog, the dream fog, the above-fog layer and the baby prism, and shadows and the tree window match the Editor.
- `Master.bank` 42.6 MB → 13.9 MB.

**Costs.**

- Web GPU load is higher than with `Mobile` (render scale 1.0, fog features, depth prepass). If FPS falls short, lower the render scale first.
- Teammates' Editors show these settings too.
- The features copied to `Mobile_Renderer` stay there but are unused now.
