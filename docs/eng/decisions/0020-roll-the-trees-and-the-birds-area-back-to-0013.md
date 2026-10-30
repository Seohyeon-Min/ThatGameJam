# 0020. Roll the tree placement and the bird's area back to ADR 0013, and keep the terrain

- Status: accepted. Rolls back the bird's area (x ±25) and the forest (the whole terrain) of [ADR 0019](0019-one-large-terrain-and-a-bird-area-five-road-widths-wide.md)
- Date: 2026-10-29
- Related: [`../implementation_plan.md`](../implementation_plan.md) 3.3, 4.1, 4.6, [ADR 0013](0013-widen-the-birds-area-to-the-terrain-beside-the-road.md)

## Context

ADR 0019 filled the whole terrain with trees (1645). The user found that too many trees, and asked to roll the area's width and the trees back to before for now.

## Decision

The user's instruction.

**The bird's area is x ±13 again (ADR 0013).**

**Trees are placed per stage again (ADR 0013).** Two dense rows along both sides of the road (spacing 1.7) and sparser trees beyond (spacing 3.2), out to x ±21.4. None in the streams. The intro is done the same way. The trees on the road behind the baby's start are gone. `Sam_Test` has 280 trees (stages 1 and 2 the same 168 as before, the intro 112).

**Kept.** The one terrain (wider than what the camera shows), Tommy's tree models, the intro and the snow. The terrain is sized from the stages' areas, so it narrowed with them (about 99 × 140 m).

## Alternatives

**Alternative 1. Keep the area at x ±25 and only thin out the trees.**
Not done. The user asked for both to go back.

## Consequences

**Gains.**

- Trees 1645 → 280, renderers about 8900 → 1700

**Costs.**

- With the camera at the area's edge, the forest ends and treeless snowfield shows. The terrain's edge does not show
- The director's "no break in the trees may show" and "five times the road's width" are not met now. Left as is until the user decides again
