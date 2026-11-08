# Merge origin/JU: encyclopedia discovery popup (popup)

## Why

Bring the 3 `origin/JU` commits (`5cfc0b8` popup, `6f5bff9` Encyclopedia prefab, `b972e93` bank) into `sam`. Conflicts were in `Main.unity`, the 2 FMOD banks, and one FMOD event.

## What changed

| What | Where |
|------|------|
| JU's discovery popup (`DiscoveryPopupLayout.cs`, popup top/bottom textures, quill pen animation) and `Encyclopedia.prefab` | `Assets/02_Scripts/Encyclopedia/`, `Assets/03_Prefabs/UI/Encyclopedia.prefab` |
| `Main.unity` merged with an object (fileID) level 3-way merge. Base is the common ancestor `9b30835`. JU changed only 10 documents | `Main.unity` |
| The 2 stream water meshes use JU's; the 2 mesh documents on `sam` were removed. Same handling as 05 and 10 | `WindingWater` meshes, 2 MeshFilters |
| The empty root object `p` that JU removed is removed | `SceneRoots` |
| The encyclopedia PrefabInstance uses JU's layout overrides, with `sam`'s one `sharedProfile` override put back | `Encyclopedia` (PrefabInstance) |
| The `icicleNote` event volume uses main's -8. It was 6; main lowered it to -8, JU to 4 | `FMODProject/.../{41637c10-...}.xml` |
| The FMOD banks were rebuilt from the merged project with FMOD Studio 2.03.15 | `FMODProject/.../Master.bank`, `Master.strings.bank` |

## Key decisions

- For the `icicleNote` volume both sides are "the other branch", so the rule could not decide. main's value was chosen since main is the shared branch. If JU's 4 is right, it must be changed.
- The bank was rebuilt instead of taking JU's (76.5 MB, no Vorbis settings). The new bank is 49.8 MB.

## Verification

| What | Result |
|------|------|
| Conflict markers | 0 |
| Local fileID references in the scene | 0 dangling |
| Reopen the scene in the Editor | 127832 GameObjects, 0 missing scripts, 0 compile errors. The 4 MeshFilters without a mesh are all `Ring` and unrelated to this merge |
| FMOD build | `Build completed successfully` |
| Play Mode | Not run |

## Cross-model review

Not done. A merge with no code change from our side.
