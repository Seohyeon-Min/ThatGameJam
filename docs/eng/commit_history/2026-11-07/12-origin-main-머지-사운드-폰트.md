# Merge origin/main: sound and font change (changeSound&Font)

## Why

Bring `origin/main` commit `554cb23` into `sam`. The only conflict was the FMOD bank.

## What changed

| What | Where |
|------|------|
| main's new sounds (`easteregg`, `tree_jump`, `tree_walk`), the changed `cutsceneEnding`, and the FMOD event metadata | `FMODProject/ThatGameJam/` |
| The FMOD bank was rebuilt from the merged project with FMOD Studio 2.03.15 | `FMODProject/.../Master.bank` |
| main's baby behavior (walks up close and peers, head tilt sway), migrating bird feather shaders, Cinzel font, etc. as git merged them | `Baby.cs`, `GameTuning.cs`, `EndingFlight.cs`, `MigratingBirds.prefab` |

## Key decisions

- As in 10, the bank was rebuilt instead of taking main's (78.6 MB). main's bank lacks the ADR 0037 Vorbis settings. The new bank is 49.6 MB.

## Verification

| What | Result |
|------|------|
| Conflict markers | 0 |
| FMOD build | `Build completed successfully` |
| Compile | Checked together after the JU merge in 13 |

## Cross-model review

Not done. A merge with no code change from our side.
