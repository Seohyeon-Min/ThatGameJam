# Merge origin/JU: fog forest animal silhouettes, the leaning log stump prefab, FMOD banks rebuilt

## Why

This brings the JU branch (`origin/JU`) into `sam`. When JU merged main, JU kept its own old scene, so a plain merge would delete about 21650 objects (about 6000 flowers, the ending flock, `CliffBreeze`, the headphones hint and more). So the scene starts from `sam`'s, and only what JU's 4 commits (`a679f56`, `47e6cb9`, `7440c35`, `18910ba`) changed in the scene was laid back on, per object (fileID).

## What changed

| What | Where |
|------|-------|
| Taken from JU: fog forest animal silhouettes and sizes, the doe, squirrel and bird models, `FogMaze.birdCorridorWidth`, `FogBirdAmbience` on `Fog`, `SnowFlower`'s `alignBird` and `faceForward` | `Main.unity` |
| The leaning log stump is now the `LeaningLogStump` prefab + `Ladybug2`, as on JU | `Stages/Stage1/LeaningLog/broken tree/LeaningLogStump` |
| The `StumpLadybug` easter egg JU deleted stays, moved out of the intro to under `Stages/Stage1`. Its world position (2.77, 0, 21.83) is unchanged and inside Stage1's area (user's call: use both, not in the intro) | `Stages/Stage1/StumpLadybug` |
| Kept from `sam`: `Ending` (timeline, `sleepSpot`, ending audio), `CliffEnding.breeze`, `IntroSequence` (title video, headphones), encyclopedia UI positions, `FlowerGenerator.overshoot`, the stream meshes (same content on both sides). JU's values were left over from the old scene | `Main.unity` |
| `BloomSpot` takes both sides' overrides (`durationScale`, `reactsToSeason`) | `Main.unity` |
| Serialized field additions (`paintPetals` and so on) on about 45 flower objects that `sam` no longer has were dropped | `Main.unity` |
| FMOD: JU's removal of the `birdsTweeting` Spatialiser is in, and the banks were rebuilt with FMOD Studio 2.03.15 (the Unity plugin's version). main's bank lacked the Spatialiser removal and JU's lacked main's BGM | `FMODProject/.../Master.bank` |
| Scripts, textures, cursors and the other files are as git merged them | |

## Key decisions

- The new bank is 76.4 -> 49.6 MB. ADR 0037's encoding settings (Vorbis for the 8 long music tracks) applied on the rebuild. The 7 BGM tracks main added (`bgm_st_*`) do not have Vorbis yet.
- `Master.strings.bank` is byte-identical to main's. The event list did not change.
- With the leaning log's ladybug now JU's prefab, the shorter crawl (1 s) `sam` gave that spot is gone. The `StumpLadybug` easter egg prefab keeps it.

## Verification

| What | Result |
|------|--------|
| Local fileID references and Transform parent/child lists in the scene | 0 broken, 0 mismatches |
| Recompile in the Editor | Succeeds |
| Reopen the scene in the Editor | 127862 GameObjects, 0 missing scripts, 0 empty references |
| FMOD build | `Build completed successfully` |
| Play Mode | Not run |
