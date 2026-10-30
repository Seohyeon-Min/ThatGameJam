# Merged main into JU

## Why

Repo owner's request: "need to merge with main" → "need to bring main's updates into the JU branch." The merge-base of `JU` and `main` was `d3bad79` ("baby movement," the auto-commit seen early this session), and main had since pulled well ahead via the team's (Tommy, Seohyeon) watercolor shaders, season-transition system, and bulk tree generation. Main already had `ff31958 Bring JU's baby and fade content into Main...`, a commit where Seohyeon **manually ported** (not git-merged) my d3bad79-era work (59 PointOfInterest, balance-beam markers, camera fade) — but my 8 commits since then (blend bug fix, shiver tuning, leaf camera fade, bird bank fix, jump animation) weren't in main at all.

## What was done

### 1. Wired up UnityYAMLMerge

Added `.gitattributes` routing `*.unity`/`*.prefab`/`*.asset`/`*.mat` etc. through `merge=unityyamlmerge`, and registered the Unity Editor's bundled `UnityYAMLMerge.exe` as that driver in local git config — a plain text merge can silently corrupt fileID/GUID structure while still looking like valid YAML.

- **It hung on the first try.** Missed the headless (`-h`) flag, so a conflict tried to pop a GUI error dialog and sat there unresponsive (same PID, unchanged for minutes → killed it, reconfigured with `-h -p --fallback none`).

### 2. Resolved 8 conflicts

| File | Resolution |
|------|------|
| `.gitattributes` | add/add — simply combined both additions |
| `Perchable.cs` | Both sides added a different field in the same spot (my `alwaysExempt`, main's `alignBird`). Kept both |
| `GameTuning.asset` | **UnityYAMLMerge silently picked JU's values with no conflict flag.** `babyWalkSpeed` (main 0.9 vs mine 1.2), `birdSpeed` (5 vs 6), `birdAltitudeSpeed` (2.8 vs 3) — found by diffing base vs main vs the result field-by-field; main's tuned values were deliberate, so corrected to those |
| `Sam_Test.unity` | **Too structurally diverged for an automatic merge to even attempt** (my 219 objects vs main's 1153). Asked the repo owner directly — decided to keep JU's version; main's intro/extra stages/bulk trees/terrain are not reflected this time |
| `BillboardLeaf.mat` and 3 others | New files that exist only on main; git's similarity-based rename detection falsely paired them with placeholder files deleted on my side. Took main's content as-is |
| `Baby.cs`, `BirdController.cs` | Auto-merged with no conflict. Verified both sides' changes (my JumpTo/shiver-intensity/gaze, main's Idle state and landing-turn alignment) are present in the result |

`TreeLeaves_1-4.mat` (the placeholder leaf materials I turned camera-fade on) got deleted, since main replaced that whole placeholder system (and had already solved the same camera-fade problem on its own tree materials) — my fix there is naturally absorbed.

### 3. Fixed what broke after the merge

Keeping JU's `Sam_Test.unity` carried over references to "kind 4" trees (Tree_4_Bare/Leafy) that main's team had removed entirely, producing 38 missing-prefab errors. Dropped the stale 4th slot from each of the 4 `TreeGenerator`s and called `Generate()`, then re-ran `AddBabyGazeTargets.cs` to reattach `PointOfInterest` to the freshly regenerated trees (56 newly added, the 3 static landmarks skipped since they already had theirs — 59 total, same as before). Also wired main's new `SeasonSnow.season` field (null, throwing a `NullReferenceException` on `OnEnable`) to the `Season` component already on `GameFlow` in this scene.

## Verification

| What | Result |
|------|--------|
| Full reimport (`AssetDatabase.Refresh(ForceUpdate)`) + recompile after the merge | 0 errors, no compile failure |
| Console error count before/after the prefab fix in Play Mode | Before: several Tree_4-related errors plus a `SeasonSnow` NRE. After: 0 |
| Watched Play Mode for 10+ seconds | Baby wakes → walks → climbs the log (jump animation included, y position actually changes mid-jump) normally |
| `git log --graph` | Merge commit correctly folds in main's history |

## Remaining

- `Sam_Test.unity` deliberately does not have main's intro/extra stages/bulk trees/terrain. If that content is needed later, it'll need the same manual-porting approach Seohyeon has been using.
- Saw an FMOD `event:/SFX/Bird` duplicate-key warning once right after the big reimport; hasn't recurred since (looks like a one-time reimport side effect — worth watching).
- Not pushed yet.
