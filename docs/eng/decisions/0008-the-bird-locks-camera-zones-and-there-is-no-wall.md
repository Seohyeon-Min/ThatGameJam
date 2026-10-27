# 0008. Camera zones lock when the bird crosses the trigger, and there is no wall stopping the bird

- Status: accepted (being tried; waiting for the director's feedback)
- Date: 2026-10-27
- Related: [`../implementation_plan.md`](../implementation_plan.md) 3.1, 4.1, 4.7, [ADR 0007](0007-lock-the-camera-in-device-zones-and-wall-off-the-bird.md)

## Context

In ADR 0007, a camera zone locked when the baby reached its trigger, and an invisible wall stopped the bird until the device was solved. The director changed two things. The trigger is about the player, the bird, not the baby. The invisible wall goes. After trying it, the user said the camera moved a lot upward (+Z) while locked, and asked for a much smaller range.

## Decision

The director decided. The user decided the smaller range.

**A zone locks when the bird crosses its trigger line.** The trigger is not a point but a line across the road (Z). The bird can fly anywhere on the road, so it could pass beside a small circle. Only the current stage's zones are checked. The zone releases when the device is solved, and does not lock again even if the bird flies back over the trigger.

**The wall that stops the bird (`BirdWall`) is removed.** The bird stays only on the road and on screen.

**Zone ranges are small.** The log zone went from 4 × 5 (z 17 to 22) to 2 × 1 (z 17 to 18). While locked the camera barely moves.

## Alternatives

**Alternative 1. Keep the trigger a circle (radius).**
Rejected. The baby walks the road through the circle, but the bird can pass beside it.

**Alternative 2. Keep the wall.**
Rejected. The director asked to remove it. The cost is that the bird can head for the next zone before solving the device. While locked, though, the screen range holds the bird.

## Consequences

**Gains.**

- The camera responds to the player's movement, without waiting for the baby
- In a locked zone the camera is almost fixed, so the framing is steady

**Costs.**

- If the bird enters a zone first, while locked it cannot go back to the baby coming behind. At the log it can go back only to z 12. The baby walks in from off screen
- Before a zone locks, the bird can fly past the device
- The trigger line assumes the stage runs along +Z
