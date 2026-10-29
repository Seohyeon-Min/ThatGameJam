# 0013. Widen the bird's area to the terrain beside the road, with more trees on it

- Status: accepted
- Date: 2026-10-28
- Related: [`../implementation_plan.md`](../implementation_plan.md) 4.1, 4.6, 4.7, [ADR 0007](0007-lock-the-camera-in-device-zones-and-wall-off-the-bird.md), [ADR 0009](0009-the-bird-perches-on-roadside-branches-and-goes-to-the-nearest-when-idle.md), [ADR 0010](0010-join-the-stages-on-one-road-and-drop-the-teleport.md)

## Context

Since ADR 0007 the bird flew only on the road (x ±5). The roadside trees were two rows on each side, and the bird left the road only to perch on their branches (ADR 0009). The director sent a sketch (`image.png`). A narrow road runs down the middle with terrain on both sides. A green box is the space the bird may move in, with trees scattered inside and outside it. Read as: widen the ground, add trees, and let the bird move between them.

## Decision

The director's request. The numbers are this agent's.

**The bird's area (`Stage.area`) is the road and the terrain beside it.** x ±13. The bird and the point the camera follows stay inside. The bird still never leaves the screen.

**The ground widens to x ±22.** The streams cross the same width. The ground is wider than the bird's area so ground is visible even when the camera follows the bird to the area's edge.

**Trees in two layers.** The two dense rows along the road's edges stay (spacing 1.7). Beyond them, sparser trees (spacing 3.2) stand out to the ground's edge. So there are trees inside the bird's area and beyond it.

**The bird does not collide with trees.** It passes between them at its fixed altitude. When hidden, its silhouette shows.

## Alternatives

**Alternative 1. Give trees colliders and have the bird avoid them.**
Dropped. The bird does not move by physics; it heads straight for its target (plan 4.1). Avoidance would need new pathfinding. The sketch asks for room to move, not collisions.

**Alternative 2. Widen the area to the ground's edge.**
Dropped. With the camera following to the ground's edge, the void beyond would show.

## Consequences

**Gains.**

- The bird can leave the baby's side and fly through the forest
- With trees inside and beyond the area, the forest's edge does not look like a wall

**Costs.**

- Many more trees and renderers (`Sam_Test`: 168 trees, about 2360 renderers). Web performance must be checked (plan 4.19)
- The bird passes through trees. The silhouette shows it
- At the area's edge (x ±13) the end of the ground may show at the camera's edge
