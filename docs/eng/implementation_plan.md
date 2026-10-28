# Code Implementation Plan

> Basis: [`game_design.md`](game_design.md) (original `Game Design.pdf`). `§N` below is a section number in that document.
> Korean version: [`../kor/implementation_plan.md`](../kor/implementation_plan.md)
> Status: draft, 2026-10-27. Progress is in 6.1. 12 of the questions in Section 5 were decided on 2026-10-27 ([ADR 0003](decisions/0003-implementation-plan-question-answers.md), [ADR 0004](decisions/0004-fog-natural-resolution-rule.md)). Will be updated once the remaining questions are decided.

This document sets out what the code side will build from the design doc. For art, sound, and level layout, it only covers the points where code attaches (events, animator parameters, shader values).

## 1. Summary

The whole game repeats one rhythm. **Wake the sleeping baby → the baby walks along the path → stops in front of an obstacle → the bird helps, or it resolves on its own over time → falls asleep in the Sunlit Rest Area → seasonal change effect → next stage** (§11.3).

So the code builds a shared framework first. Each stage's devices are small components that plug into that framework.

| Shared framework | What it does | Design basis |
|---|---|---|
| Bird controls | Left click to move and perch, right click to cry, fixed altitude per map | §2.1, §2.3 |
| Cry | Notifies "living things" within range | §2.1, §9.3, §15.7 |
| Baby | Walk along the path, stop, react (look up), sleep, wake up | §2.2, §2.4, §4 |
| Natural resolution timer | Waiting time, 3-step signals, "did the bird solve it / did it resolve on its own" | §3 |
| Sunlit Rest Area and stage transition | Falling asleep, seasonal effect, screen transition, placement for the next stage | §10, §11 |
| Camera | A camera that follows the bird. When the bird crosses a device zone's trigger, it locks to a smaller range. The bird stays on the road | §2.5, ADR 0005, ADR 0007, ADR 0008 |

| Stage device | Design basis |
|---|---|
| Wing umbrella (tutorial) | §6.2 |
| Leaning log | §7.3 |
| Snow-laden branch, sleeping rabbit family, side path | §8.2, §8.3 |
| Clearing fog, breadcrumb trail, hidden obstacles, answering animals | §9.2 ~ §9.5 |
| Automatic climb beyond the cliff, breaking through the clouds | §9.6 |
| Two endings | §12 |

## 2. Gaps Between the Current Code and the Design

The code in the current `Sam_Test` scene is a test made before the design doc. Its controls differ from the design.

| File | Now | Design | To do |
|---|---|---|---|
| `BirdController.cs` | WASD to move, wheel for altitude, E to clear obstacles and perch on branches | Left click to move and interact, right click to cry, altitude fixed per map (§2.3) | Rewrite the input part. Keep the flying / perching / perched states |
| `ChildWalker.cs` | Walks in a straight line to a target point, stops if an `Obstacle` is ahead | Walks along a path (route), waits in a different pose for each obstacle, and reacts (§2.2, §4) | Rewrite. Also rename it to `Baby` to match the design doc's term |
| `Obstacle.cs` | Disappears when the bird clears it | Every obstacle resolves on its own over time and gives 3-step signals (§3) | Replace with the natural resolution timer in 4.4 |
| `PerchPoint.cs` | A perch point whose marker blinks when you get close | The interaction targets are logs and snow-laden branches (§2.1) | Keep using it as the perch position. Remove the marker that blinks on approach. On mouse hover, only the cursor changes (Q6) |
| `QuarterViewCamera.cs` | Always follows the target | Follows only in the intro; fixed cameras per section in stages (§2.5) | Replace with the camera rig in 4.7 |
| `Tree.prefab` | Roadside tree. You can perch on its branches | The design doc says nothing about perching on roadside trees | Keep it only as decoration and remove the perch point (Q7). Replaced on 2026-10-28 by 8 colour-coded kinds (`Trees/Tree_1` to `Tree_8`) |

The `AGENTS.md` rule "use only the direction of the mouse wheel" has no use now, because the design no longer uses the wheel. `AGENTS.md` was not changed.

## 3. Structure

### 3.1 Component List

Names are suggestions. Each component does one thing.

| Component | What it does |
|---|---|
| `GameFlow` | Runs the order intro → tutorial → stages 1·2·3 → ending. Knows the current stage |
| `Stage` | Settings for one stage. Start positions of the baby and the bird, bird altitude, road range, Sunlit Rest Area, seasonal effect |
| `ScreenFader` | Covers and uncovers the screen during stage transitions |
| `BirdCamera` | Decides the point the camera follows. It follows the bird but does not leave the road range, and stays inside a locked `CameraZone`'s range if there is one. It also sets the bird's movement range: on the road, and within the screen around this point. One Cinemachine camera follows this point (ADR 0005, ADR 0007, ADR 0008) |
| `CameraZone` | One device's camera zone. When the bird crosses the trigger line, the camera locks to a rectangle; when the device is solved, it unlocks (ADR 0007, ADR 0008) |
| `BirdController` | Click movement, perching, cry, altitude |
| `Perchable` | An object the bird can perch on. Fires a `Perched` event when the bird lands. Uses the current `PerchPoint` as the perch position |
| `CryListener` | The shared part of things that react to the cry. Has a reaction distance. Used by the baby, fog animals, and the migratory birds in the ending |
| `Baby` | The baby's state (4.3) and movement |
| `BabyRoute` | The path the baby walks. A list of points |
| `StopPoint` | A spot on the path where the baby stops. Holds which obstacle it waits for and in what pose |
| `NaturalTimer` | Time until natural resolution, 3-step signals, resolution. All five points in the §3.2 table use it |
| Sunlit Rest Area | The last point of the baby's path (`BabyRoute`) and the light on it. When the baby reaches the last point, it falls asleep and raises `ReachedRest`. There is no separate component or section for it (ADR 0006) |
| `GameTuning` | A ScriptableObject that holds time and speed values (`Assets/04_Data/`) |
| Stage devices | `WingUmbrella`, `LeaningLog`, `SnowBranch`, `RabbitFamily`, `FogField`, `FogTrail`, `FogAnimal`, `HiddenObstacle`, `CloudCover`, `Ending`, seasonal effects |
| `DevTools` | For development. Fast-forward time, skip stages. Turned off in release builds |

### 3.2 Flow

```text
GameFlow ──start──▶ Stage ──▶ Baby (placed asleep)
                               │ cry or NaturalTimer ends → wakes up
                               ▼
                         walk along BabyRoute
                               │ reach StopPoint
                               ▼
                 device (LeaningLog etc.).NaturalTimer starts
                               │ bird solves it (byBird) or time runs out (byNature)
                               ▼
               Baby reacts (looks up if byBird) → walks the next path
                               │ reach the end of the path (Sunlit Rest Area)
                               ▼
               fall asleep → seasonal effect → GameFlow switches to the next Stage
```

Between a device and the baby there is only one event: `Resolved(bool byBird)`. The baby does not know what kind of device it is. The `StopPoint` tells it the waiting pose and the path to take after resolution.

### 3.3 Scene Setup

All stages sit in one game scene, at separate places. A transition covers the screen, moves the baby and the bird to the next stage's start positions, and uncovers the screen. The web build only loads once, and there is no need to save state passed between stages (ending branch, etc.).

Each stage is a prefab. That way several people can build different stages in their own test scenes, and the shared scene only needs the prefabs placed. Scene file conflicts go down. The shared game scene is `Main.unity` (Q1).

## 4. Implementation by System

### 4.1 Input and Bird Movement (§2.1, §2.3)

- Input uses only Input System's `<Mouse>/leftButton`, `<Mouse>/rightButton`, and `<Mouse>/position`.
- On left click, cast a ray from the camera through the mouse position. It hits three layers.
  - `Interactable`: if it hits a `Perchable`, the bird flies to its perch position and lands.
  - `Ground`: the bird flies to the XZ of the hit point. The height is the stage's fixed altitude.
  - `ClickPlane`: a large invisible plane placed where there is no ground, such as beyond the cliff (§2.3). The baby's movement checks ignore this layer.
- Clicking the ground while perched makes the bird take off.
- The bird is not pushed or pulled by physics. It flies over terrain and obstacles (§2.1), so it is moved directly to the target point. The current Rigidbody velocity approach is dropped.
- The bird cannot leave the stage's road range or the screen (Q3) (ADR 0007). The clicked target point is not clamped. So the bird stays at the edge of its range, and keeps going when the camera follows. In 3-3, the area above and beyond the cliff is included in the range.
- While left click is held, the target point keeps updating (Q12). This is handy when drawing a path in the fog.
- A small circle briefly shows where you clicked (Q13).
- When the mouse is over something the bird can perch on, only the cursor changes. Nothing else is shown (Q6). This follows the goal of fewer control guides (§1.3) and the rule that signals are the hints (§3.1).
- The bird model and animations are the art's `Bird-Animated`. `BirdController` passes only two values to the Animator: `Moving` and `Perched` (landing or perched). While moving it glides, with one wingbeat every 2 seconds. Hovering in place it flaps; arriving at a perch it lands; perched it plays the three idles at random; leaving, it takes off. The flow is in `Bird.controller`, made by the builder.
- The bird's position is its feet. Its feet stand on the perch point (`Perchable`).

### 4.2 Cry (§2.1, §9.3, §15.7)

- On right click, `BirdController.Cry()` plays the cry animation and sound. A short cooldown prevents spamming.
- It tells every registered `CryListener` the bird's position. Each listener decides whether to react, using its own reaction distance.
  - `Baby`: if asleep and within distance, wakes up and looks up at the bird. Distance and check are not yet decided in the design (§16).
  - `FogAnimal`: makes a sound and briefly shows a silhouette or a small light (4.12).
  - Migratory birds in the ending: answer only in the happy ending (4.14).
- The listener list is a static list, like the current `PerchPoint.All`. No separate event system is built.

### 4.3 Baby (§2.2, §2.4, §4)

**States**

| State | Entered when | Left when |
|---|---|---|
| `Sleeping` | Stage start, arrival at the Sunlit Rest Area | Cry, or the wake-up timer ends |
| `Waking` | Waking up | The reaction (look up or look around) animation ends |
| `Walking` | Starts walking, obstacle resolved | Reaches a `StopPoint` or the end of the path |
| `Waiting` | Reaches a `StopPoint`. The `StopPoint` sets the pose: standing, crouching (rabbits), sitting (resting stone) | The device's `Resolved` |
| `Reacting` | `Resolved(byBird: true)`. Looks up at the bird. With the rabbits, peeks at them first and then looks up | The reaction animation ends |

**Movement**

- `BabyRoute` is a list of points. Each point has a height. Paths whose height changes, such as across the fallen log, are drawn with points.
- The baby walks to the next point at a constant speed. No pathfinding (NavMesh) is used. The path is a single line, and devices decide any branching.
- The only fork is the rabbit scene. When the side path opens, the `StopPoint` swaps in the side path's point list (4.11).
- Only in the fog sections does the baby follow the bird's trail instead of a path (4.12).
- The cliff (§2.4) is the end of the path. In 3-3 the path ends at the resting stone, and the baby sits down.

**Reaction** (§4.1)

- Use a single reaction: looking up. The baby turns its body and head toward the bird and pauses briefly.
- The resolution event says `byBird`. If `true`, the baby reacts and then walks. If `false`, it walks without reacting.
- Whether the reaction grows with the number of helps is not yet decided in the design (§16). If the baby just keeps a count of helps, this can be added later.

**Animation list** (to hand to art): walk, stand and wait, crouch, sit and rest, sleep, toss and turn, rub eyes, stretch, look up, look around, peek, watch (as the rabbits pass by).

### 4.4 Natural Resolution Timer and Waiting Signals (§3)

The five points in the §3.2 table (waking, log, rabbits, fog, clouds) all have the same shape. Time passes, signals grow in 3 steps, and it ends when time runs out or the bird solves it first. So it is built as one `NaturalTimer` component.

- Values: time until natural resolution (read from `GameTuning`), and when signal steps 1·2·3 start (as a ratio of the total time).
- Events: `Signal1`, `Signal2`, `Signal3`, `Resolved(bool byBird)`. Animator triggers, particles, and sounds are wired to the signal events in the Inspector. Every signal gets both a visual effect and a sound (§3.1).
- `Begin()`: called when the baby stops in front of that device. Time does not pass before the baby arrives (Q4).
- `ResolveByBird()`: called by the device when the bird solves it. Skips the remaining time and fires `Resolved(true)`.
- The object that gives the signal is the interaction target (§3.1). Level design decides which objects give signals.

### 4.5 Waking Up (§2.2, §5.2, §11)

- The baby has one `NaturalTimer`. About 10 seconds; signals are toss and turn → rub eyes → stretch.
- If woken by the cry, the baby looks up; if it wakes on its own, it looks around. Then it walks (§4.2).
- In the intro, a cutscene plays after waking (§5.2 Phase 3). The cutscene is made with Timeline (already installed). Its content is not yet decided in the design.
- The starts of stages 1·2·3 work the same way (§11.2). At the end of stage 3 the baby is not woken; the game goes to the ending.

### 4.6 Sunlit Rest Area, Stage Transition, Game Flow (§10, §11)

- The Sunlit Rest Area is not a special section (ADR 0006). Put one portal-like light at the end of the baby's path, right after the stage's last device. Do not build more path or ground past it.
- When the baby reaches the end of the path: the baby falls asleep → the stage's seasonal effect plays and the game waits for it to finish → `GameFlow` runs the transition.
- Transition: cover the screen → move the baby and the bird to the next stage's start positions, change the camera, change the bird's altitude → uncover the screen → the baby is `Sleeping`.
- The light of the Sunlit Rest Area (a gap in the clouds and sunlight) is art work (§16). The code only turns it on and off.
- The Sunlit Rest Area in the last stage starts off. It turns on when the clouds are broken through or part (§10.3, 4.13).

### 4.7 Camera (§2.5)

- The camera follows the bird (ADR 0005). The user changed the "fixed camera per section" in design doc §2.5. In device zones it locks to a smaller range instead of a fixed position (ADR 0007).
- Use Cinemachine (Q2). The main camera has a `CinemachineBrain`. One `CinemachineCamera`, fixed at the quarter-view angle, follows one point with `CinemachineFollow`.
- `BirdCamera` decides that point every frame. It clamps the bird's position to the road range. The baby is not used as a reference, to show that the bird and the baby are separate beings (ADR 0007).
- Each device has a `CameraZone`. When the bird crosses its trigger line (a Z across the road), the point eases into the zone's rectangle over 1 second and follows the bird inside it. The range is small, so the camera barely moves while locked. When the device is solved, it eases out over 1 second. A zone that has been released never locks again (ADR 0008).
- On a stage transition, the camera cuts to the new stage.
- In the intro, a zoomed-out camera follows the bird. When the bird comes within a set distance of the baby, it switches to the stage camera (§5.2 Phase 2).
- The camera for the 3-3 climb section is not yet decided in the design (§16). As a first try, use a camera that rises to follow the bird's height.

### 4.8 Intro (§5)

- Start state: snow is falling. The baby is asleep. The camera is zoomed out. The bird is high up, near the camera.
- There is no control guide UI (§5.2 Phase 1).
- When the bird gets close to the baby and the intro camera switches to the stage camera, the bird also comes down from the intro's high altitude to the stage altitude (Q8).
- Wake up → cutscene → baby starts walking → transition on entering the tutorial section (§5.2 Phase 4).

### 4.9 Tutorial Snow Section and Wing Umbrella (§6)

- `WingUmbrella`: checks every frame whether the bird is above the baby. If the horizontal distance is within a radius and the bird is higher than the baby, it is "covering".
- While covering, show the bird's shadow on the baby. A real shadow may not fall on the baby because of the light direction. So a fake shadow (a URP Decal Projector or a semi-transparent disc) is placed right below the bird.
- When covering starts, the baby looks up. A cooldown keeps this from happening too often.
- The umbrella has no effect on movement or progress (§6.2). Whether the baby looks up while walking or pauses briefly is decided after seeing the animation.
- Snow: one particle system that follows the camera. At the end of the tutorial, only new snowfall stops (§6.3). To keep snow from falling below the bird, use the particle Collision module to remove snow that touches the bird. Decide after checking web performance.

### 4.10 Stage 1 — Log (§7)

- 1-1 fallen log: not a device. Just draw the path points over the log.
- 1-2 `LeaningLog`:
  - It has a `Perchable` at the top. When the bird perches, `ResolveByBird()`.
  - `NaturalTimer` about 30 seconds. Signals are creaking sound → stronger swaying → the base splits. The swaying is driven by code so it grows along with the wind strength.
  - On resolution, play an animation of it falling toward the water, pivoting on its base. The fall direction is fixed (§7.3).
  - After it falls, the baby's path continues over the log.
  - When the log starts to fall, a bird perched on it takes off on its own and hovers right above that spot (Q5).
  - The bird may perch and knock the log down before the baby arrives. In that case the baby does not stop. It looks up in front of the log, then crosses (Q4, §4.2).
- 1-3: Sunlit Rest Area. The seasonal effect is a mother rabbit and baby rabbits passing in a line along a set path (§7.4). It is simple movement using the same point list as `BabyRoute`.

### 4.11 Stage 2 — Sleeping Rabbit Family (§8)

- 2-1 demonstration: when the baby passes a trigger, one small bird flies in along a set path, perches on a branch, and leaves. At that moment, the `SnowBranch` resolution animation plays too. Position and timing are not yet decided in the design (§16).
- `SnowBranch`: a bent snow-laden branch. When the bird perches, the snow falls and the branch springs up. 2-1 and 2-2 use the same component.
- `RabbitFamily` (2-2):
  - The baby crouches at the `StopPoint` in front of it. It is not physically blocked (§8.3). So the rabbits get no collider check; the baby stops only through the `StopPoint`.
  - `NaturalTimer` about 30 seconds. Signals are snow dust from the branches → the babies toss and turn → the mother yawns and stretches.
  - When all branches blocking the side path are cleared, `ResolveByBird()`. The baby peeks at the family, looks up at the bird, and goes to the side path's point list.
  - When time runs out, the rabbits wake up, move off the path, and pass by. The baby watches them, then takes the original path.
  - The number of branches is not yet decided in the design (§16). The code takes N branches, and the side path opens only when all are cleared (Q9).
- The bird does not touch the rabbits (§8.3). The rabbits are not `Perchable`, so clicking them just makes the bird fly to the ground at that spot.

### 4.12 Stage 3 — Fog (§9.2 ~ §9.5)

This is the hardest and riskiest part. A prototype built separately from the other work, first, is suggested (Section 6).

**Fog data and rendering (`FogField`)**

- One low-resolution grid (e.g. 128×128) covers the fog section. Each cell holds a fog density of 0~1.
- This grid is the single source of the fog. Both the on-screen display and the baby's checks read from it.
  - Display: upload the grid to a `Texture2D` every frame. The shader of the fog mesh (a few planes above the ground) reads this texture by world XZ to set transparency.
  - Check: "is this point cleared" is read from the grid value.
- Where the bird passes, values drop in a soft circle shape. The clearing width is not yet decided in the design (§16).
- 3-1 starts with fog on only half the path; 3-2 starts fully fogged. The initial state is painted in one mask texture.
- The fog does not fill back in. The design says nothing about that.

**The bird's trail (`FogTrail`)**

- Each time the bird moves a set distance (e.g. 0.5 m) over the fog section, add one point. The point's height is the ground height, found by casting a ray down.
- The trail must start from the baby. When the baby has no trail left to follow and the bird passes within a set distance of it, a new trail starts from then. This "near" distance is the design's "check range for continuing the trail again" (§16). While the baby is still walking, the bird coming close does not cut the trail. A bird just hovering close to the baby, on the exit side, still leads the baby to the spot under it.
- Loop removal (§9.2): if a new segment crosses an earlier segment, delete the points between the crossing points. It is a 2D segment intersection test on the XZ plane.

**The baby following the trail**

- The baby walks the trail points in order. No fork checks are made (§9.2).
- Before moving to the next point, check that point. If it is not on walkable ground (`Ground` layer, not water or a cliff), or there is a `HiddenObstacle` on the way, the baby stops. The trail after it is deleted (§9.2). There is no failure screen.
- If there is no trail, the baby waits where it is.

**Hidden obstacles and animals**

- `HiddenObstacle`: rocks, puddles, etc. A collider is all it needs. It is under the fog mesh, so it cannot be seen.
- `FogAnimal` (`CryListener`): reacts to the cry with a sound and a brief light or silhouette. When the baby comes close, it moves aside. It is placed only on walkable ground (§9.3). This is a placement rule, so level design enforces it, not code.

**Natural resolution** (§9.2, §3.2)

- Level design draws the correct path (a point list) for each section.
- `NaturalTimer` about 30 seconds per section. Signals are the fog edge thins → sounds of hidden animals → the outline of the route shows through.
- When time runs out, the fog slowly clears along the correct path, and the baby walks along the correct path.
- Time runs continuously from when the baby first stops in front of the fog. It does not stop while the bird is drawing a trail (Q10).
- When natural resolution comes, the baby drops the remaining trail, goes to the nearest correct path point, and walks along the correct path (Q10). If it is away from the correct path, it retraces the trail it walked until it is near the correct path, so it never cuts across water, and then joins it. On the correct path it never goes back; it heads for the end of the nearest segment.

### 4.13 3-3 — Cliff and Breaking Through the Clouds (§9.6)

- The baby sits at the resting stone `StopPoint`. This `StopPoint` is the end of the path.
- Bird altitude: raise the altitude based on how far past the cliff line the bird is. The input stays left click to move (§9.6). Thanks to `ClickPlane`, empty air can be clicked too.
- When the altitude reaches the cloud height, `CloudCover.ResolveByBird()` → a hole in the clouds, sunlight pours in all at once → happy ending.
- `CloudCover`'s `NaturalTimer` about 40 seconds. Signals are faint light between the clouds → wind → beams of light. When time runs out, the clouds slowly part → normal ending.
- The pull that makes the bird leave the baby's side is not yet decided in the design (§16). The code only leaves a way to turn it on with a signal event.

### 4.14 Ending (§12)

- `Ending` branches on the `byBird` sent by `CloudCover`.

| Item | Normal (`byBird = false`) | Happy (`byBird = true`) |
|---|---|---|
| Sunlight | Turns on slowly | Turns on all at once |
| Baby | Falls asleep right away | Looks up at the bird, then falls asleep |
| Spread of spring | Spreads out from the baby | Same + a flock of migratory birds flies in |
| Last cry | No answer | The migratory birds answer (`CryListener`) |

- The design does not say when the last cry is made, or who makes it (Q11).
- Flock of migratory birds: a few birds flying along set paths. No flocking simulation.
- The final screen is not yet decided in the design (§16).

### 4.15 Seasonal Change Effects (§10.2, §15.9)

| When | Effect | Code approach |
|---|---|---|
| End of tutorial | The falling snow stops | Stop new emission from the snow particles |
| End of stage 1 | The rabbit family passes by | 4.10. Movement along a point list |
| End of stage 2 | The settled snow melts | Lower the global shader value `_SnowAmount`. Ground and prop materials read this value |
| Last | Spring spreads out from the baby | Raise the global shader values `_SpringCenter` and `_SpringRadius`. Materials turn to spring inside the radius, and props like flowers grow when the radius reaches them |

The global shader value names and approach must be agreed with art, because the materials (Shader Graph) have to read these values.

### 4.16 Sound

- Each signal has a sound (§3.1). Sounds play from each device's `AudioSource` through the signal events. No separate sound manager is built at first.
- Background music and ambient sound go in the stage settings. The design doc says nothing about background music.

### 4.17 Time Values and Playtime (§1.4, §3.2)

All times and speeds are collected in `GameTuning`. The draft values are taken directly from the design doc.

| Value | Draft | Count | Total |
|---|---|---|---|
| Baby wakes up | 10 s | 4 (intro + 3 stage starts) | 40 s |
| Leaning log | 30 s | 1 | 30 s |
| Sleeping rabbit family | 30 s | 1 | 30 s |
| Fog | 30 s per section | 2 (3-1, 3-2) | 60 s |
| Clouds | 40 s | 1 | 40 s |
| Total | | | 200 s = 3 min 20 s |

This matches the design doc's "about 3 min 20 s". The target is about 3 minutes (§3.2). Baby walking speed, bird speed, cry distance, umbrella radius, fog clearing width, and signal timing ratios also go here. One playthrough is at most 10 minutes, so values must be testable right away when changed, without code edits.

### 4.18 Development Tools

- Time scale (2×), skip to the next stage, resolve the current device on its own, resolve the current device as if the bird solved it.
- Draw paths, trails, `StopPoint`s, the road range, and camera zones and triggers with Gizmos.
- Uses keyboard shortcuts, but they are on only in development builds and the Editor. Game controls stay just the two mouse buttons (§15.6).

### 4.19 Things to Check in the Web Build

- Check that the browser menu does not appear on right click. If it does, block it in the web template.
- Browsers block sound until the user's first input. Check whether the intro's snow sound is heard before the first click.
- Measure the cost of fog texture updates and snow particles in a real browser.

## 5. Decisions and Open Questions

Most of the undecided items in Section 16 of the design doc are numbers or presentation, so the code only needs a place to change those values. Below are the items that affect code structure but had no answer in the design doc. On 2026-10-27 the user decided all of them except Q11. The reasons and the rejected alternatives are in [ADR 0003](decisions/0003-implementation-plan-question-answers.md) and [ADR 0004](decisions/0004-fog-natural-resolution-rule.md).

### 5.1 Decided

| # | Question | Answer |
|---|---|---|
| Q1 | Which scene is the shared game scene | `Main.unity`. Stages are built as prefabs and gathered in `Main.unity` |
| Q2 | Should we add Cinemachine | Yes |
| Q3 | Can the bird leave the fixed camera's view | No. In 3-3, include the area above and beyond the cliff in the range. ADR 0005 changed the camera to follow the bird, and the bird cannot leave that view. ADR 0007 added the road range |
| Q4 | When does natural resolution time start. Can the bird solve it before the baby arrives | From when the baby stops in front of that device. Solving first is allowed, and the baby still gives the help reaction |
| Q5 | What happens to the bird when the log it perches on falls | When the log starts to fall, the bird takes off on its own and hovers right above |
| Q6 | Show a marker when the mouse is over something the bird can perch on | Only change the cursor. Remove the current marker that blinks on approach |
| Q7 | Keep the current roadside trees (perchable) | Keep them only as decoration and remove perching |
| Q8 | How does the bird come down from the intro's high altitude to the stage altitude | When the camera switches to the fixed position, the bird also comes down to the stage altitude |
| Q9 | If several branches block the side path, must all be cleared to open it | All must be cleared to open it |
| Q10 | Fog: does natural resolution time run while the bird is drawing a trail. If a partly drawn trail and natural resolution overlap, which one does the baby follow | Time runs continuously from when the baby first stops in front of the fog. When natural resolution comes, the baby drops the remaining trail and goes to the nearest correct path point ([ADR 0004](decisions/0004-fog-natural-resolution-rule.md)) |
| Q12 | Should holding left click keep following | Keep updating the target point while the button is held |
| Q13 | Show a destination marker where you clicked | Briefly show a small circle |

### 5.2 Open Questions

| # | Question | Current state |
|---|---|---|
| Q11 | When is the last cry in the ending made, and who makes it | Not decided. The table in §12.2 of the design doc has a "Last cry" row. The normal ending is "No answer", and the happy ending is "Other migratory birds answer". §12.3 explains that in the happy ending, migratory birds of the same kind answer for the first time. This reads as a scene in the ending where the bird cries once. The design doc does not say whether the player makes that cry with right click or the cutscene makes it automatically, or when it happens. We need to ask the design lead |

## 6. Work Order

| Step | Content | Done when |
|---|---|---|
| 1. Base framework | 4.1 ~ 4.7, 4.17, 4.18. One stage 1 built from gray boxes | Wake up → walk → stop at the log → (bird perches or 30 s) → cross → fall asleep in the Sunlit Rest Area → transition to the next stage runs in one go. In both cases |
| 2. Fog prototype | 4.12. Separate from step 1, in an empty test scene | Trail drawing, loop removal, stopping at hidden obstacles, continuing the trail, and natural resolution work |
| 3. Stage 2 | 4.11, the rabbits passing by in 1-3 | Both the side path case and the waiting case run |
| 4. Finale and endings | 4.13, 4.14, 4.15 | Happy and normal each reach the end screen |
| 5. Opening | 4.8, 4.9 (intro, tutorial) | The game runs from start to end without intervention |
| 6. Wrap-up | Gather stage prefabs in `Main.unity`, attach art and sound, web build, tune time values | On the web, about 5 minutes when playing actively, about 8 minutes when only waiting (§1.4) |

Steps 1 and 2 do not depend on each other, so two people can split them. Steps 3·4·5 can also be done separately from each other once the step 1 framework exists.

### 6.1 Progress (as of 2026-10-27)

| Step | Status | Where |
|---|---|---|
| 1. Shared framework | Done | `Assets/00_Scenes/Sam_Test.unity`. Work records 04, 05 |
| 2. Fog prototype | Done | `Assets/00_Scenes/Sam_Fog_Test.unity`. Work record 06 |
| 3. Stage 2 | Next. Not started | "Stage2" in `Sam_Test` is a temporary stage for checking the transition (only ground and a sunlit spot). Step 3 replaces it with the real stage 2 |
| 4 to 6 | Not started | |

Changes outside the plan:

- The camera follows the bird and cannot go past a device until the device is solved (ADR 0005, work record 07)
- In device zones the camera locks to a smaller range. The baby-based limit was removed. The bird stays on the road and cannot pass a wall until the device is solved (ADR 0007, 2026-10-27 work record 10)
- Camera zones lock when the bird, not the baby, crosses the trigger. The wall that stopped the bird was removed. The log zone's range was made much smaller (ADR 0008, 2026-10-27 work record 13)
- The fog map (`Sam_Fog_Test`) is neither rebuilt nor tested until the basics are settled. When the user says the fog map work starts, it is rebuilt with the code of that time
- The Sunlit Rest Area is not a separate section; it sits right after the last device (ADR 0006, work record 08)
- The dev time scale is 2x (work record 09)
- Roadside trees come in 8 kinds, told apart only by colour for now. They are tall bare trunks, placed at random in two rows along both edges of the road, and neighbours differ in kind. Trees on the camera side may hide the road. A stage uses only some kinds (stage 1 uses 5) (2026-10-28 work records 01, 02)
- The bird now uses the art's model and animations (2026-10-27 work record 11)
- The comparison branch `sam-fixed-camera` holds the fixed per-section camera version. Only the camera differs (work record 10). It was pushed to the remote together with `sam`

Remaining work and open issues:

- Q11 (the last cry in the ending) must be asked to the design lead
- The camera differs from design doc §2.5. ADR 0007 and 0008 are the director's requests but still being tried. Waiting for the director's feedback
- Temporary values: the bird's screen range (`birdRange` on `BirdCamera`), the camera zones' triggers and ranges, the 1-second ease, and the fog clearing width, re-join range, and animal answer range
- Each builder run changes `_SrcBlend` on the transparent test materials (`ClickMarker.mat`, `RestLight.mat`, `AnimalGlow.mat`). The builder setting blend values directly and the Editor's automatic adjustment seem to use different values. These changes are left uncommitted; fix them when the builder is next touched
- Only the user's own test was done by hand with a real mouse. All other checks used a virtual mouse

## 7. Risks

| Risk | Response |
|---|---|
| The fog section is the most complex. Trail, checks, and display are tangled together | Test it separately first as step 2. Keep one grid as the shared source for both checks and display |
| Just waiting may be boring (§3.1) | Test while changing signal timing and durations directly in `GameTuning`. Run the whole game often with the dev time scale |
| The bird leaves the screen or is hidden by trees | The bird cannot leave the screen (Q3, ADR 0005, ADR 0007). Check hiding when choosing the camera angle and prop placement |
| Web performance (particles, several transparent fog layers, texture updates) | Check 4.19 once, right after step 1 is done |
