# Fixed a unit bug in the glance blend, verified it by eye in Play Mode (follow-up to [01](01-아기의-관심-사물-시선-glance.md))

## Why

Asked to actually watch the 01 glance system in Play Mode instead of just confirming it via logged values. Found and fixed a bug in the blend math along the way.

## What changed

| What | Where |
|------|-------|
| Fixed a unit bug: `babyGazeBlendSpeed` (240, meant as "degrees/second") was being multiplied directly into a 0–1 blend value. Replaced with `babyGazeBlendTime` (0.35s, the time for the blend to ramp fully 0→1 or 1→0) | `GameTuning.cs`, `Baby.cs` (`LateUpdate`) |

## Key decisions

- Feeding `babyGazeBlendSpeed = 240f` straight into `Mathf.MoveTowards(blend, target, speed * Time.deltaTime)` means a per-frame step of 240×(1/60)=4.0 — far past the blend's 0–1 range, so **the blend jumped from 0 to 1 in a single frame**. The head wasn't turning smoothly, it was snapping. Using a "degrees/second" unit on a unitless 0–1 scalar was wrong from the start; switched to a time-based ("seconds") ramp instead.
- Polling via reflection every ~1s in Play Mode never caught this (the poll interval was longer than the whole ramp, so it only ever saw 0.00 → 1.00, never the middle). Found it while trying to actually verify the pose on screen and working the numbers backward — the kind of bug plain value-polling alone doesn't surface.

## Verification

| What | Result |
|------|--------|
| Compile `GameTuning.cs` / `Baby.cs` | `script recompile` → `completed`, 0 errors |
| Play Mode auto-exiting right after entering | First two attempts exited after ~4.6s (`isPlaying=false`); the `PlayModeCommands:Stop()` log line turned out, on closer look, to be my own cleanup command from an earlier test round mixed into the same log tail — not reproducible within this session. The third attempt onward stayed stable. Found no evidence of an external process interfering |
| Set `gazeTarget` directly via reflection + computed `headBone.rotation` once using the same formula as `LateUpdate` (safe while Play Mode is paused, since Cinemachine won't overwrite it) | Moved the camera to a profile view beside the baby's head and captured it. Head turned to a natural ~32° angle — no neck clipping or unnatural twist |
| Temp capture files (`Assets/_tmp_gaze1.png`, `_tmp_gaze_check.png`, etc.) | Deleted along with their `.meta` files after checking, then `AssetDatabase.Refresh()` |
| `git status` | Only the intended files changed. Camera/bird positions touched directly during Play Mode reverted automatically on stop, confirmed not left in the saved scene |

## Remaining

- The bug is fixed, but whether the actual ramp speed (0.35s) "feels" natural is still an estimate. Even with `Time.timeScale` slowed down, the CLI round-trip latency itself (several real seconds per call) outweighed the scaled game-time change, so the in-between frames of the ramp couldn't be watched live — only the final, held pose's naturalness was confirmed.
- Didn't get to visually check the case where the max turn angle (`babyGazeMaxAngle`, 60°) actually clamps (a target further than 60° off).
