# Merge origin/main and keep the sam scene working

## Why

The user asked to merge `origin/main` (48 commits ahead) into `sam`. There were 4 conflicts. The user decided two things:

- Discard the uncommitted local change to `Bird.controller`.
- Follow main for the GUID of `BrokenTree_Animator.controller`, but keep the sam scene working.

## Conflict resolution

| File | Resolution |
|------|------------|
| `Assets/01_Arts/Models/Bird/Bird.controller` | Local change discarded before the merge. Main's transition change comes in as is |
| `BrokenTree_Animator.controller(.meta)` | Main's file (GUID `6622a7ed…`). Tommy's `idle` stays the default state; only a `Break` state is added. `LeaningLog` plays `Break` |
| `Assets/01_Arts/Models/Girl/Girl.controller` | sam's side (with QuietWait), plus the `BalanceWalk` and `Jump` states JU added on main, with the same fileIDs |
| `Assets/02_Scripts/Baby/Baby.cs` | Both sides kept. sam's QuietWait, observe-target turning and ByBirdRoute detour, then main's LeaningLog balance walk. The route end uses main's `LieDown()`, which now also clears QuietWait |

## Fitting the sam scene

| What | Where |
|------|-------|
| Broken tree controller reference changed from `1b351be4…` to main's GUID | `Assets/00_Scenes/Sam_Test.unity` |
| `BalanceBeamEnd` on the first ground point past the leaning log (`Stage1/BabyRoute/Point_124`, z 23.2). Without it the baby stays in BalanceWalk after the log (that state has no outgoing transitions) | `Sam_Test.unity`, `SetupStage1Logs.MarkBeamEnd` |
| Main's `Snowfall.prefab` now carries `SeasonSnow`, so the scene had two. Removed the scene-added one and linked the prefab one's `season` to GameFlow's Season (as main's builder does). Before this, Play started with a `SeasonSnow.OnEnable` NullReferenceException | `Sam_Test.unity` |
| The builder no longer wipes the tree controller's states; it adds `Break` only when missing. The girl controller rebuild includes `BalanceWalk` and `Jump`. Rebuilding stage 1 adds `BalanceBeamEnd` | `AgentScripts/BuildSamTestScene.cs` |
| Tree default-state check changed from `Idle` to `idle` | `AgentScripts/VerifyStage1Logs.cs` |

Re-saving the scene also stored the particle values (size, speed, material) that main's `Snowfall.Awake → Configure()` computes into the scene overrides. The Snowfall component settings were not changed.

## Verification

| What | Result |
|------|--------|
| Compile | 0 errors |
| Controller states | Tree: default `idle`, `Break`. Girl: previous 9 + `BalanceWalk`, `Jump` |
| `VerifyStage1Logs.Play`, `Natural` | Pass. Baby crosses the log and stage 2 starts, baby animation `Sleep` |
| `VerifyStage2.Run` bird, early, natural, partial | All 4 pass |
| `VerifyStage2Waiting.Run` | All baby checks pass. Fails at the last check, `Actual branch clear lost its sound` |
| Console errors in Play | 0 after the SeasonSnow fix |

## Cross-model review

GPT-6.1 Sol, on the conflict resolution and follow-up diff (Baby.cs against both sides, builder, controllers, scene edits). `LGTM - no blockers`.

## Remaining

- Main's switch to FMOD set `m_DisableAudio` to 1 in `ProjectSettings/AudioManager.asset`. Unity `AudioSource`s are silent, including sam's snow-branch sound (`SnowBranch.snowSound`). Whether to move it to an FMOD event is for the user to decide. No sound assets are created on the sam branch.
- The first fallen log (static bridge) has no balance-walk markers. JU's `Sam_Test_JU` has them. Add them if the user wants.
- The builder's bird controller rebuild would overwrite JU's Bird transition change (ExitTime) from main. Reconcile before rerunning the builder.
