# 0039. Web build frame stalls are cut by spreading the season change over frames, lighter rings, fewer renderers and baked watercolor textures

- Status: accepted
- Date: 2026-11-07
- Related: [ADR 0037](0037-web-build-uses-the-pc-quality-level-and-vorbis-for-long-music.md), [ADR 0024](0024-forest-as-far-as-the-camera-sees-and-snow-from-upwind.md)

## Context

In the web build the sound breaks up at the game start, the clouds, each season change, the start of the fog and the ending. FMOD mixes on the main thread, so when a frame stalls the sound stalls too. The director asked for the stalls to be cut (`order_from_director.md`).

A Development build connected to the Profiler recorded 48 seconds (the FMOD development library logged so many warnings that the Profiler stopped). Nine frames took over 50 ms.

- Most of it was building GPU programs the first time a shader is used (`ApplyShader`, `CreateGPUProgram`, 50 to 690 ms a frame).
- The frame where DevTools advanced the season took 836 ms: about 690 ms first shader use, 44 ms `TreeGenerator.LateUpdate` (764 KB GC).

The user then chose to measure by playing once the optimisation is done.

## Decision

1. The season change no longer switches trees (leaves and bark) and flowers/grass in one frame. They are sorted by distance from the baby and a fixed number change each frame (150 trees, 400 flowers/grass). Anything within the ring radius + 2 m changes that frame even past that number. The photo hides the old look and the ring reveals the new one from the centre, so it looks the same. Bark is repainted only on the trees whose oak share range changed.
2. `SpringRing` builds its buffers once and caches the ground height on a 0.3 m grid around the ring centre, raycasting each grid point the first time it is needed. It interpolates only when all four corners hit and their heights differ little; otherwise it raycasts at the point itself.
3. `Flower01` (15 to 1) and `Grass01` (10 to 1), whose pieces share one material and all sit at the prefab origin, are merged by an editor builder. The merged meshes live in `Assets/01_Arts/Combined/`. The user allowed this exception to the `sam` branch's no-art-assets rule.
4. The leafy trees' leaf cards are not merged: each card spreads out to face the camera around its own origin (`WatercolorFlat`'s `LeafCardPositionWS`).
5. The three textures `WatercolorManager` drew in C# while loading are baked in the Editor into `Assets/04_Data/Resources/WatercolorTextures.asset`. The pencil texture is used only while a look's pencil settings match the baked ones. It is a separate commit so it can be reverted easily (the user asked).
6. The FMOD WebGL DSP buffer goes from 2048 x 4 to 1024 x 4. After it was raised, the breaks got longer.
7. The fog (`ForestMist`, `DreamFog`, seven fog planes) is left alone because its look would change (the user's decision). The snow is not cut: ADR 0024 already fitted its area to the screen and the wind. The intro cloud puff count is left alone because the clouds' look would change.

## Alternatives

**Alternative 1. Switch only by the ring radius.**
Rejected. The ending's ring starts late, on the music, and nothing would switch meanwhile. The per-frame number finishes early; the radius only keeps it from lagging at a low frame rate.

**Alternative 2. Merge the leaf cards too and have the shader read each card's origin from a vertex attribute.**
Rejected. It changes the shader and the bake tool together, and the trees' bake is stored as per-instance overrides in the scene, so the scene would have to be saved again.

**Alternative 3. Keep the pencil strokes' length by storing each piece's size in a vertex attribute.**
Rejected. The shader shortens strokes by the renderer's bounds, so on the merged flowers' small pieces the strokes get a little longer. The flowers are small on screen, so the difference is small; the user checks it in play.

## Consequences

**Gains.**

- Renderers in the scene go from 75,755 to 40,272.
- The season change's tree and flower work is spread over frames. While a ring plays, it raycasts only the grid points it newly needs.
- Drawing three textures at load, and its temporary allocations (about 7 MB), are gone.

**Costs.**

- The first-use shader cost (the largest in the measurement) remains. It needs a separate job: collect the variants the web player uses and build them during loading.
- Rebuilding or re-baking `Flower01` or `Grass01` means running `AgentScripts/CombinePrefabMeshes.cs` again.
- Changing the pencil settings means running `AgentScripts/BakeWatercolorTextures.cs` again; otherwise they are drawn at load as before.
- `WatercolorTextures.asset` is text-serialised, about 7.7 MB.
