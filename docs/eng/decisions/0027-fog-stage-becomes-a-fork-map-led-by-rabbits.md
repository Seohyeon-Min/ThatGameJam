# 0027. The fog stage becomes a fork map where rabbits show the way

- Status: accepted. Replaces design doc §9.2 - §9.5 and [ADR 0004](0004-fog-natural-resolution-rule.md)
- Date: 2026-11-01
- Related: the user's map sketch `20261101_215022.jpg` (repository root, not committed)

## Context

The user gave a new stage 3 map sketch. One path runs from the start to the goal, with 4 forks. At each fork one side is the right way and the other is a dead end closed by a wall. A small animal sits at each fork, with an arrow pointing down the right way. The arrow's end is where the animal disappears.

| Fork | Right way | Dead end |
|---|---|---|
| 1 | up (up and to the right) | down (to the right) |
| 2 | down and to the right | the path curving up and to the left |
| 3 | up | right |
| 4 | up and to the right (goal) | up |

## Decision

The user's decision.

- The girl follows the small animals, not the bird's trail. When the bird cries near a fork, that fork's animal runs down the right way and disappears. The girl takes that way.
- If the bird cried before the girl got there and the animal has already gone, the girl knows the right way at that fork.
- If the bird does not cry, the girl stands at the fork for a moment (3 s, a test value) and then picks 50:50. A cry in that time makes her follow the animal.
- On the wrong way she walks to the wall at the end of the dead end, comes back, and takes the right way. A cry while she walks there does not turn her back early.
- The walls are trees for now; they may become rocks or the like later. The small animals are rabbits for now; they may change later.
- One fog covers the whole map. There is no 3-1 (half) / 3-2 (whole) split.
- The fog is light: things look blurry, but the rabbits' movement and the shape of the paths are visible. The focus of the stage is helping the girl, not finding the way.
- The fog does not clear where the bird flies; it clears where the girl walks. This is not settled, so both are inspector checkboxes (default: bird off, girl on).
- No breadcrumb trail, hidden obstacles, loop removal, or natural fog recession. The girl reaches the goal 50:50 even without a cry, so no natural resolution time is needed.
- The goal has a sunlit spot where she falls asleep, as in the other stages. 3-3 (the cliff and the clouds) is not part of this.
- The scene is `Main.unity`, after stage 2.

## Alternatives

**Alternative 1. Keep the design doc's breadcrumb trail.**
Dropped. The user gave a new map and new rules.

**Alternative 2. Ignore cries before the girl arrives; accept only cries while she is at the fork.**
Dropped. If the player flew ahead and cried and the girl still took the wrong way, the cry would look wasted.

## Consequences

**Gains.**

- The rules are simple. One cry helps, and without help she still gets there.
- No trail recording, checks, or natural resolution, so the code is small.

**Costs.**

- The contrast in design doc §4.2 ("the fog is the only section where the baby follows the bird's trail") gets weaker. The girl follows rabbits, not the bird.
- The old `FogSection` and `Sam_Fog_Test` go unused.
