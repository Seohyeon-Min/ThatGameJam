# 0019. The ground is one large terrain, and the bird's area is five times the road's width

- Status: accepted. Changes the area (x ±13) and the ground (x ±22) of [ADR 0013](0013-widen-the-birds-area-to-the-terrain-beside-the-road.md)
- Date: 2026-10-29
- Related: [`../implementation_plan.md`](../implementation_plan.md) 3.3, 4.1, 4.6, [ADR 0009](0009-the-bird-perches-on-roadside-branches-and-goes-to-the-nearest-when-idle.md), [ADR 0013](0013-widen-the-birds-area-to-the-terrain-beside-the-road.md)

## Context

The director asked for it.

- Replace the floor with a terrain and the trees with Tommy's trees
- The floor is one large terrain, big enough that its end does not show when the camera moves
- No break in the tree placement may show either
- Instead, the bird cannot go further than about five times the road's width

Until then the ground was boxes of snow bank and stream (x ±22). With the bird at the area's edge (x ±13), the ground's end showed at the edge of the screen (ADR 0013).

## Decision

This agent proposed it and the user decided.

**The bird's area is x ±25.** The road is 10 m wide, so this is a 50 m band with the road in the middle. "Five times the road's width" was read as the width of the whole band. It could also have been read as 50 m on each side from the road's edges (x ±55).

**The ground is one terrain.** The builder works out its size. The point the camera follows stays inside the stages' areas. Wherever that point is, the terrain covers the ground the camera shows (at a 16:9 screen), plus 12 m. In `Sam_Test` now that is about 123 × 140 m.

- Where the bird flies (within x ±28) it is flat at height 0. The baby's road and the bird's altitude stay as they were
- Beyond that are low hills (up to 3 m)
- The streams are channels dug across the terrain. The water boxes cover the terrain's full width. Near a stream the hills flatten

**The trees are Tommy's three kinds.** Each comes bare (`tree01 ~ 03`) and with leaves (`tree_l_01 ~ 03`). Every stage uses the bare ones. Leaves belong to season step 3 (ADR 0018).

**Trees cover the whole terrain.** Only the streams are left clear. The two dense rows on both sides of the road stay. Beyond them, sparser trees fill the terrain to its edge. Behind where the baby starts, trees stand on the road too. `TreeGenerator` stands the trees on the terrain's surface.

**Every tree has a perch branch (ADR 0009).** The perch is a low branch near the first fork, picked from the model's vertices (about 2.2 to 2.4 m up). At the question stage the agent proposed a split: "light trees without branches outside the bird's reach". But the heavy part is drawing, and a branch (one trigger collider) costs almost nothing. Trees off screen are not drawn anyway. A split would gain nothing, so there is none.

## Alternatives

**Alternative 1. Make the area five road widths on each side from the road's edges (x ±55).**
Dropped. The user chose x ±25.

**Alternative 2. Make the ground very large but put trees only near the bird's area.**
Dropped. The top of the screen shows ground tens of metres from the camera. Where the trees end would show.

**Alternative 3. Place the trees with the terrain's own tree feature (Terrain trees).**
Dropped. Terrain trees cannot carry components, so they cannot have a perch branch.

## Consequences

**Gains.**

- Wherever the bird goes in its area, the end of the ground and the forest does not show (checked at the four corners)
- When stages are added, the builder widens the terrain and the forest to match

**Costs.**

- There are many trees. `Sam_Test` has 1645 trees and about 8900 renderers. Only what is on screen is drawn, but performance must be checked in a web build (plan 4.19)
- Tommy's trees are 5.5 m tall, smaller than the old grey trees (8.5 m). The bird, with a 2.2 m wingspan, looks big next to them
- The tree material (`ArnoldStandardSurface`) has no `_BaseColor`, so the debug red tint does not reach the trees. The ground is tinted
