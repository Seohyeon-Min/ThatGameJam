# 0024. Fill the forest as far as the camera can see, and let the snow fall from upwind by as far as it drifts

- Status: accepted. Changes the snow box of [ADR 0022](0022-change-only-the-snow-wind-and-widen-the-forest.md)
- Date: 2026-10-29
- Related: [`../implementation_plan.md`](../implementation_plan.md) 3.3, 4.9, [ADR 0021](0021-widen-the-birds-area-trees-behind-the-intro-start-and-snow-wind.md), [ADR 0022](0022-change-only-the-snow-wind-and-widen-the-forest.md)

## Context

The user reported two things.

- At the farthest point ahead the bird can reach, the top of the screen has no trees. The camera sees farther toward the top. Fill it, per the director's "no break in the trees may show"
- In the intro, at the lowest or highest point, the snow looks thinner

Measured, the snow was like this. With the wind (0.5, −5), a flake drifts 37 to 47 m along −Z while it falls. So of about 30,000 flakes only about 1500 were on screen. When the bird moved, the sky over the new view had almost no snow (about 270 right after moving).

## Decision

The user's instruction. This agent chose the method and values.

**The forest reaches 3 m past what the camera can see.** The builder works out the ground shown with the bird at any end of its area. The forest is x ±33 (ADR 0022), and wider on any side where the view reaches farther. Now the left (−X) goes to x −61. Past the last stage (`TreesAhead`) and behind the start follow the same rule. There are 1089 trees.

**The snow box sits upwind.** It covers the ground on screen moved back by a whole fall's drift, and the camera's spot moved back by the drift down to its height (for both the slowest and the fastest flakes). It covers 8 m more along the road (Z) both ways, so when the camera follows, snow is already falling over the new view.

**Flakes per square metre go from 1.5 to 0.9.** Left as it was, the bigger box would add a lot more snow. At 0.9 the bottom of the screen (near the camera) has the same amount as before, and the thin top of the screen fills in. Size, wind and lifetime are unchanged.

## Alternatives

**Alternative 1. Keep 1.5 per square metre.**
Dropped. About 92,000 flakes, about 3 times the snow on screen.

**Alternative 2. A fixed box over the whole intro.**
Dropped. Over 100,000 flakes.

## Consequences

**Gains.**

- The snow on screen is about the same everywhere (about 2700 to 2800 flakes), also right after the bird flies to an end
- The forest's edge does not show ahead or at the top left

**Costs.**

- Flakes about 30,000 → 55,000, about 113 FPS in the editor. Must be checked on the web
- Trees 471 → 1089
- The top of the screen has more snow than before (about 2.5 times). The bottom is the same
