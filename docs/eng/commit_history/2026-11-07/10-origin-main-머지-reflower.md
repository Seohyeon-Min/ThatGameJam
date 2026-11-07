# Merge origin/main: reflower and rebake

## Why

Bring the 2 new commits on `origin/main` (`394dcf2` rebake, `64c1190` reflower) into `sam`. The scene had 7522 conflicts, so the user chose "keep main's scene as the base and reapply what `sam` changed". This was done as a per-object (fileID) three-way merge. The base is the common ancestor `597bf99`.

## What changed

| What | Where |
|------|-------|
| Flowers come from main. Main deleted and regenerated every flower under `FlowerGenerator` (about 21460 documents removed, 21470 added) | `01_SYSTEM/FlowerGenerator` |
| Trees come from `sam`, which replanted the forest in record 04. Main's changes to the old trees were only float noise in renderer bounds, so they were dropped (2558, plus 68 changed on both sides) | `01_SYSTEM/TreeGenerator` |
| The 2 stream water meshes come from main; `sam`'s 2 mesh documents were removed. Same handling as record 05 | `Stream_1_Water`, `Stream_2_Water` |
| `IcicleXylophone` material, mesh, and pattern references come from `sam`. Main's 22 rebaked assets (guids) are not committed on main either, so those references would be broken | `IcicleXylophone` (PrefabInstance, `WatercolorPaintedModel`) |
| Every other object changed on one side follows that side. Main's new squirrels, berries, etc. came in | `Main.unity` |
| The FMOD bank was rebuilt from the merged project in FMOD Studio 2.03.15. It has main's ending music length change (41.5 -> 53.3 s), JU's spatialiser removal, and the ADR 0037 Vorbis settings | `FMODProject/.../Master.bank` |
| Baked assets, `FmodEndingLength.js`, and the rest are as git merged them | |

## Key decisions

- Unlike the "asset conflicts follow the other branch" rule in memory, the bank was rebuilt instead of taken from main. Main's bank (78.6 MB) lacks JU's spatialiser removal and the Vorbis settings. The new bank is 49.6 MB, and `Master.strings.bank` did not change.
- Seohyeon needs to commit main's new `IcicleXylophone` baked assets. Switch the references once they are in.

## Verification

| What | Result |
|------|--------|
| Conflict markers | 0 |
| Local fileID references in the scene | 0 broken |
| Transform parent/child lists | 0 mismatches (the 2 already in `sam` remain) |
| Guids used by the scene | All exist in the project |
| Reopen the scene in the Editor | 127839 GameObjects, 0 Missing Scripts, 0 empty references, 0 compile errors |
| FMOD build | `Build completed successfully` |
| Play Mode | Not run |

## Cross-model review

Not done. The merge has no code changes.
