# Merge origin/main: the stage 2 lost baby rabbit section, BGM, icons

## Why

This brings the 3 commits on `origin/main` into `sam`: `597bf99` (stage 2 becomes the lost baby rabbit section), `bd626e2` (fix bgm), `e695062` (icons). `sam` has the web build optimization (ADR 0037), the icicle xylophone (ADR 0038), the encyclopedia photos and the clearing replant (record 04).

## What changed

| What | Where |
|------|-------|
| The FMOD bank conflict takes main's banks (user's call). main added 8 new BGM tracks and rebuilt the banks | `FMODProject/ThatGameJam/Build/Desktop/Master.bank`, `Master.strings.bank` |
| The scene conflict was resolved by a three-way merge per object (fileID). An object changed on one side only takes that side. The 6 objects changed on both sides were settled as below | `Main.unity` |
| `RabbitBurrow`'s child list and the removal of its perch (`Perchable`) follow main. The `BurrowClick` collider added on `sam` was used only by that perch, so it went too | `Stages/Stage2/RabbitStage/RabbitBurrow` |
| The two stream water mesh references take main's. Both sides' meshes have the same content | `Stream_1_Water`, `Stream_2_Water` |
| The world forest is `sam`'s replanted forest (record 04). The trees at the spots of the 2 trees main removed (same spot, same prefab) were removed, and their slots in the generator's lists were emptied as main did | `01_SYSTEM/TreeGenerator` (local x -35.80 z 15.67, x -35.72 z 40.46) |

## Key decisions

- With main's banks, ADR 0037's "Vorbis for the 8 long music tracks only" is not in these banks. To shrink the web download again, the new BGM needs Vorbis and the banks need a rebuild.

## Verification

| What | Result |
|------|--------|
| Conflict markers | 0 |
| Local fileID references in the scene | 0 broken |
| Transform parent and child lists | 0 mismatches. SceneRoots matches too |
| Recompile in the Editor | Succeeds, no errors |
| Reopen the scene in the Editor | 127754 GameObjects, 0 missing scripts, 0 empty references |
| Play Mode | Not run |
