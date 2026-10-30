# The baby glances at nearby objects of interest (trees, log landmarks) by turning only its head while walking

## Why

Following yesterday's (10-29) walking changes (gentle heading changes, speed variation, random curious pauses),
the request was to make it feel "not just random, but curious and reactive to the surroundings" rather than
plain randomness. The existing `CuriousPause` just stops and plays `LookAround` with no specific target — there
was nothing it was actually "looking at".

Alongside that, answering a question about whether any unused animations remained from the Google Drive update
(jump, arms-out balance) also prompted writing up the full list of animations still needed.

## What changed

| What | Where |
|------|-------|
| A "glance": finds a nearby `PointOfInterest` and turns only the head bone (`NPC-Girl-Rig/ROOT/Hips/Spine/Chest/Neck/Head`) toward it. Walking does not stop (lighter than the existing `CuriousPause`'s full stop + look-around) | `Assets/02_Scripts/Baby/Baby.cs` (`LateUpdate`, `PickNearbyPointOfInterest`) |
| When the walk cooldown fires, branch to a glance if a POI is nearby, otherwise fall back to the existing `CuriousPause`. Moved the cooldown reset to the call site so both paths share it | `Baby.cs` (`WalkTo`) |
| New marker component for things worth a glance: a weight and a look-point offset. The bird is deliberately excluded — looking up at the bird is a separate, reserved gesture meaning "the bird helped me" (design doc §4.1), and must not get diluted | `Assets/02_Scripts/Core/PointOfInterest.cs` (new file) |
| Tuning values for glance radius, duration, max turn angle, blend speed | `Assets/02_Scripts/Core/GameTuning.cs` |
| Bulk-attached `PointOfInterest` to `Stage1/RoadsideTrees` (56 trees) and 3 landmarks (`FallenLog`, `Stump`, `LeaningLog`, weight 2) | `AgentScripts/AddBabyGazeTargets.cs` (new builder script) run → `Assets/00_Scenes/Sam_Test.unity` |

## Key decisions

- The rig is Generic, not Humanoid (`NPC-Girl-Animated.fbx.meta` `animationType: 2`), so `Animator.SetLookAtPosition`
  IK isn't available. Used the standard look-override pattern instead: in `LateUpdate`, after the Animator has
  already posed the frame, add a rotation offset toward the target on top of the animated head bone rotation.
  Needs zero new animation clips.
- Clamped the max turn angle (`babyGazeMaxAngle`, 60°) so a POI behind the baby that would need an unnatural
  twist is simply not noticed instead of snapping the head too far. Not glancing reads better than a broken turn.
- Glance and the existing `CuriousPause` (look-around) share one cooldown timer — at the same "time to get
  distracted" moment, it looks at something nearby if there is one, else falls back to the old aimless
  look-around. Separate timers would have made the baby distracted too often.
- The bird is excluded from POI candidates. The design doc fixes "looking up = knowing the bird is there" as
  the single unified response language across the whole game (§4.1); random glances at the bird while walking
  would dilute that signal.
- Attached all 56 `RoadsideTrees` since, as the name says, they already line the route by design — a natural
  glance target. Skipped `TerrainTrees` (background trees far from the route).

## Verification

| What | Result |
|------|--------|
| Compile `Baby.cs` / `PointOfInterest.cs` / `GameTuning.cs` | `script recompile` → `completed`, 0 errors |
| Run `AddBabyGazeTargets.cs` builder | "Added 59 PointOfInterest components, skipped 0 already present." |
| Confirm Play mode actually advances | `Time.time` kept increasing across 3 polls (3.1 → 5.8 → 8.1s), not frozen |
| Polled `Baby`'s private fields (`gazeTarget`, `gazeTimeRemaining`, `gazeBlend`, `curiousPauseCooldown`) via reflection every ~1s | While walking, picked `target=Tree_2_Bare`, `blend` ramped 0 → 1.00 → 0 as expected. Then went `Waiting` (blocked) → auto-resumed `Walking` → reached `Resting` at route's end normally |
| Console errors/warnings during play | `console get --level error`/`warning` show 0 errors during play (2 pre-existing errors from an earlier CLI typo before play mode, unrelated) |
| Temp capture file (`Assets/_tmp_gaze_check.png`) | Deleted the file and `.meta` after checking, then `AssetDatabase.Refresh()` |
| `git status` | Only the intended files changed/added |

## Remaining

- Didn't get to see the glance's actual "feel" (head-turn speed/angle, how natural it reads) on screen — the
  camera was following the bird during testing, so the baby was off-screen. `babyGazeMaxAngle` /
  `babyGazeBlendSpeed` / `babyGazeDuration` are estimates that need tuning while watching it.
- The full animation wishlist for the animator (head-gaze itself needed none this time, but `Girl_LookUp`,
  a crouch-and-peek, a sit-on-rest-rock idle, walk-start/stop, a real cold-shiver clip, lying-down wake signals
  for the 3-stage timer, etc.) was compiled in-session but not yet handed off.
- `Girl_Jump` still has only an Animator state with nothing using it.
