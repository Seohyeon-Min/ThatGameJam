# Clean up sam's sound and snow-powder material, balance walk on the first log, align the bird builder

## Why

After the main merge (`01-main-머지와-sam-씬-맞추기.md`) the user decided three things:

1. Remove the sound and the snow-powder material made on sam. From now on, sound and assets always follow the other branch.
2. Mark the first fallen log for balance walking, as JU did in `Sam_Test_JU`.
3. Reconcile the controller builder with main's Bird transition change.

## What changed

| What | Where |
|------|-------|
| Deleted sam's `BranchRustle.wav` and `RabbitSnowPowder.mat` | `Assets/05_Sounds/`, `Assets/01_Arts/Materials/Test/` |
| Removed the snow branch `snowSound` (AudioSource) field and playback; removed the AudioSource from the 3 branches in the scene | `SnowBranch.cs`, `Sam_Test.unity` |
| The snow a branch sheds now uses main's snowflake material `Assets/01_Arts/Materials/Snow.mat` (the one Snowfall uses) | `Sam_Test.unity`, `BuildStage2.cs` |
| Builder no longer copies the WAV, adds AudioSources or creates the powder material. Added `BuildStage2.UseMainSnowAssets` for the saved scene | `AgentScripts/BuildStage2.cs` |
| First log: `BalanceBeamStart` on `Point_0` (ground before the log), `BalanceBeamEnd` on `Point_74` (last point before the z 18 stop). Same places JU used for the first log in `Sam_Test_JU` | `Sam_Test.unity`, `SetupStage1Logs.MarkBalanceBeams` |
| Builder's Flap→Glide transition now plays one full Flap first (exit time 1), as JU set on main | `BuildSamTestScene.BuildBirdAnimator` |
| Branch sound checks replaced with a "no AudioSource on branches" check | `AgentScripts/VerifyStage2Waiting.cs` |

`RabbitStageSnow.mat` and `RabbitStageWood.mat` (stage 2 snowbanks and branch wood) are not snowflake materials and were kept.

## Verification

| What | Result |
|------|--------|
| Compile | 0 errors |
| Stage 1 baby animation sequence (time scale 3) | `Walk` → z 5.8 `BalanceWalk` → z 13.5 `Walk` → leaning log stop → z 19.4 `BalanceWalk` → z 23.7 `Walk` → z 25 `LieDown`, Stage2 starts |
| `VerifyStage1Logs.Play`, `Natural` | Pass |
| `VerifyStage2.Run` bird, early, natural, partial | All 4 pass |
| `VerifyStage2Waiting.Run`, `Bird` | Pass |
| Console errors in Play | 0 |
| References to the deleted GUIDs and `snowSound` | None left in the repo |

## Cross-model review

GPT-6.1 Sol, on the whole staged diff (WAV binary excluded). `LGTM - no blockers`.
