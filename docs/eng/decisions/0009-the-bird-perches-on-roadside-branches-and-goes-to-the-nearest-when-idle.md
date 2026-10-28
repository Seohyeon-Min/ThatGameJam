# 0009. The bird perches on roadside tree branches and, left idle, goes to the nearest one

- Status: Accepted (being tried; waiting for the director's feedback)
- Date: 2026-10-28
- Related: [`../implementation_plan.md`](../implementation_plan.md) chapter 2, 3.1, 4.1, 5.1 Q7, [ADR 0003](0003-implementation-plan-question-answers.md) Q7, [ADR 0007](0007-lock-the-camera-in-device-zones-and-wall-off-the-bird.md)

## Context

ADR 0003 Q7 kept roadside trees as decoration only, with no perching. The director reversed this. Every roadside tree has a branch to perch on, with a perch point on it. If the player does nothing and the bird stays still for 2 seconds, the bird goes to the nearest branch and perches. Clicking also perches it.

The roadside trees stand beyond the road's edge (x ±5), but the bird's range was the road (ADR 0007), so most branches could not be perched on.

## Decision

The director decided. The ranges and which perches the bird picks by itself were decided by this agent.

**Every tree has one perch branch.** A low, nearly level branch a little above the flying height (about 3 against 2.5). A small click area (radius 0.6) around the perch point lands the bird there.

**Idle for 2 seconds, the bird goes to the nearest branch.** When the bird is flying, has not moved, and has had no button held and no cry for `GameTuning.birdIdlePerchDelay` (2 s), it perches. It only picks branches marked as spots to land on when idle. Devices such as the leaning log's top are never picked: if the bird solved a device by itself, there would be nothing left for the player to help with.

**The bird flies on the road but perches anywhere on screen.** The range is split in two. The flying range is as before, the overlap of the road and the screen. The perching range is the screen. Taking off from a branch off the road, the bird glides back onto the road.

## Alternatives

**Alternative 1. Widen the road range to the tree rows.**
Rejected. The bird would also fly over the tree rows, and the camera's followed point would leave the road, breaking ADR 0007's "the bird stays on the road".

**Alternative 2. Point the branches only toward the road.**
Rejected. The outer row of trees is still off the road, and the random turn of each tree would have to go.

**Alternative 3. Make the leaning log an idle spot too.**
Rejected, for the reason above.

## Consequences

**Gains.**

- When the player lets go, the bird rests on a branch instead of hanging in the air
- Every roadside tree becomes a place to perch

**Costs.**

- A bird perched on a branch off the road can be hidden by camera-side trees or other trees. The silhouette shows it
- On camera-side trees, the perch click area can catch a click meant for the road
- If the player does nothing, the bird always perches on the nearest tree; it does not follow the baby
