# 0030. Holding follows the ground, and a short click perches on a branch

- Status: accepted
- Date: 2026-11-05
- Related: the director's order, item 1 (`order_from_director.md`, repository root, not committed), the implementation plan's bird controls (Q6, Q12), ADR 0016

## Context

The director said that when the left button is held to fly ahead, the bird goes "hesitantly". The cause was measured in Play Mode. In stage 1 one point on the screen was held for 7 seconds and the bird's target point was logged every frame.

- While held, the target point was the first thing hit under the pointer. That included the roadside trees' perch spots (trigger spheres about 3.6 m up, on the `Interactable` layer).
- The camera follows the bird, so roadside trees pass under the pointer even while it stays still. The target was on a tree in 33% of the frames.
- In the quarter view, a high point, taken down to the ground, is much nearer the camera. The target jumped 5 to 12 m nearer and sideways and back to the ground 7 times. The heading swung between about −10° and +25 to 30°.
- While held, the bird never actually perched. The trees did not block it either. Only the `Ground` layer (logs, banks) stops the bird.

## Decision

The user's decisions.

- While held, the target point is the ground (or water, or the click plane) under the pointer. Branches are ignored.
- A press on a branch is not decided at once. Let go within `birdPerchClickSeconds` (0.2 s), it lands on that branch. Held longer, it becomes a click on the ground under the pointer at that moment, and then follows the pointer.
- While held and following, the perch cursor does not show over branches. It shows while a press on a branch is still young (still possibly a click).
- Reaching the end of the current area (the way still closed) while holding still lands on the nearest branch and shakes the head (ADR 0016).

## Alternatives

**Alternative 1. A press on a branch goes to perch at once, as before.**
Dropped. The user wants a held move command to put going toward the mouse first. A hold that began on a branch is the same.

**Alternative 2. Start perching at the press, and cancel it when held long.**
Dropped. Take-off and landing states would start and be undone within 0.2 s, and the motion would jump.

## Consequences

**Gains.**

- Holding goes straight toward the pointer. In the same measurement the heading stayed within −7° to +2° and the target jumped at most 0.6 m (the one-frame lag of the log).
- Clicking a branch to perch works as before.

**Costs.**

- Perching on a branch starts when the button is let go, a little later than at the press.
