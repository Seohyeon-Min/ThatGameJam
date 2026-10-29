# 0015. Keep the bird inside the actual screen; a camera zone unlocks near the map's edge

- Status: accepted
- Date: 2026-10-28
- Related: [`../implementation_plan.md`](../implementation_plan.md) 4.1, 4.7, [ADR 0003](0003-implementation-plan-question-answers.md) Q3, [ADR 0007](0007-lock-the-camera-in-device-zones-and-wall-off-the-bird.md), [ADR 0014](0014-camera-zone-locks-on-the-trigger-and-unlocks-outside-the-lock-area.md)

## Context

The user wanted the camera zone's lock to feel clearer, and proposed lengthening the distance to unlock so it unlocks almost at the map's edge. But the check that keeps the bird on screen was a fixed box (x ±6, z ±5) around the camera's followed point. Normally the box is centred on the bird and does nothing, but while locked this small box held the bird. A lock area larger than it would never unlock, since the bird could not reach its edge.

The actual screen is much wider than this box. In the quarter view the ground seen at the bird's height is a slanted four-sided patch, wide on the side far from the camera (−X, +Z) and narrow on the near side (+X, −Z). Measured: over 20 m on the far side, 9 to 10 m on the near side.

## Decision

The user decided.

**The bird stays inside the actual screen.** Inside what the main camera actually shows, a little in from the screen's edges (8% of the screen). The screen's four corners cast onto the flying-height plane make a quad, which is clipped by the fly area into the polygon where they overlap. A target outside it moves to the nearest point on its edges. This is done in the world: clamped in screen coordinates, a point behind the camera's near plane would come out mirrored. The fixed box (`birdRange`) is gone.

**The branches it may perch on are inside the actual screen too.** A perch point is turned into screen coordinates and checked against the edges. It does not perch on branches beyond the front and back ends of the fly area (ADR 0012).

**The log's camera zone unlocks near the map's edge.** Lock area x ±10 (the bird's area is ±13), z 10 to 24 (the sunlit spot is at z 25). While locked the camera area is 3 × 1, so the bird reaches even the edge on the narrow side of the screen (+X, toward the camera).

## Alternatives

**Alternative 1. Make the lock clearer by changing the framing or adding a vignette on lock.**
The user chose to try a longer unlock distance first. These can be added later.

**Alternative 2. Enlarge the fixed box.**
Dropped. An axis-aligned box does not fit the slanted screen. Sized for the far side, the bird would leave the screen on the near side.

**Alternative 3. Let the locked camera slide far sideways.**
Dropped. It would no longer feel locked.

## Consequences

**Gains.**

- While locked, the bird goes anywhere on screen. It must go near the map's edge to unlock
- The branches it may perch on match the branches actually visible

**Costs.**

- The camera follows the bird 0.4 s behind. A fast bird may touch the screen's edge first and hesitate briefly. Tune with the edge margin (`screenMargin`)
- The lock area's size relies on the camera angle and the screen's aspect and is set by hand. A very different aspect (a tall window) would need another look
