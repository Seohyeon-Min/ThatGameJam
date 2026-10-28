# 0011. Sent where the way is closed, the bird lands on a nearby branch and shakes its head

- Status: accepted
- Date: 2026-10-28
- Related: [`../implementation_plan.md`](../implementation_plan.md) 4.1, [ADR 0010](0010-join-the-stages-on-one-road-and-drop-the-teleport.md), [ADR 0009](0009-the-bird-perches-on-roadside-branches-and-goes-to-the-nearest-when-idle.md)

## Context

Under ADR 0010 the bird goes forward only up to the limit line of a device not yet opened. Clicking past it left the bird stuck against the edge. Nothing showed that the way was closed. Instead of an invisible wall, the director wanted the bird, when it tries to go where it cannot, to land on a nearby branch and shake its head. The art sent a new head shake (`Bird_Refuse`, 0.77 s) and an in-place flight (`Bird_Hover`).

## Decision

The director's request, settled with the user.

**Clicking past the limit line makes it shake its head.** This happens when the clicked ground point is ahead of the limit line (Z), or when the button is held and dragged past it. The bird goes to the nearest "idle perch" branch on screen (the logic of ADR 0009), lands, plays `Bird_Refuse` once, and returns to its usual idles. If already perched it shakes its head in place. With no branch on screen it stays where it is.

**When the device opens the limit line goes and the bird moves as usual** (ADR 0010).

**In-place flight is switched to `Bird_Hover` on trial.** Before, it repeated the wingbeat (`Bird_Flap`). The user decides after seeing it. The wingbeat now plays once only, right after taking off.

## Alternatives

**Alternative 1. Shake the head the moment the bird touches the edge.**
Dropped. The bird also ends up against the edge while the camera follows it. It tells the player only when they chose the closed way.

**Alternative 2. Land on a branch set per device.**
Dropped. The director said the nearest-branch logic could be reused. No branch has to be set per device.

## Consequences

**Gains.**

- The closed way shows without words, as a refusing gesture
- No new range mechanism; it reuses ADR 0009's and 0010's

**Costs.**

- If the nearest branch is behind, the bird flies back to land there
- Clicking the device itself past the limit line (the log's top) is not refused, because the device must be clicked to open it
