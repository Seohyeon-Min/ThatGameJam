# 0035. At the sunlit spot the girl sleeps only with the bird near; until then she leaves the road and plays

- Status: accepted
- Date: 2026-11-06
- Related: [`../game_design.md`](../game_design.md) 2.2, 10.1, chapter 17, [`../implementation_plan.md`](../implementation_plan.md) 4.3, 4.6

## Context

The director's order (`order_from_director.md` item 1). The girl lay down as soon as she reached the sunlit spot at a stage's end, wherever the bird was. The director wants her to sleep only with the bird near. With the bird far she should play with the snow on the ground, leave the road to look at trees, or run around. The reason given: "she only follows the path now, so she doesn't feel like a person", and leaving the road matters most. The user summed it up: she does something else, and when the bird comes to her she goes back to the light and sleeps. The director will make the animations.

## Decision

The director and the user decided. The details below were set in this work.

**When she reaches the sunlit spot (the route's last point) without the bird near, the girl plays nearby. When the bird comes near she looks up at it, walks back to the sunlit spot and lies down.**

- "Near": the bird within `GameTuning.babyRestBirdDistance` (10 m) of her horizontally, and her on the screen. The camera follows the bird, so this means both on one screen.
- Play spots (`PlaySpot`) sit in the editor under `PlaySpots` below each stage's sunlit spot (the route's last point). Five per stage: snow play ×2 (3 m either side of the road), tree look ×2 (1.5 m in front of the nearest tree on each side), run around ×1 (3 m behind the light, round four points on a 2.2 m radius). The builder is `AgentScripts/BuildGirlPlay.cs`.
- She picks plays at random, never the same one twice in a row. Each lasts `babyPlaySecondsMin` to `Max` (5 to 9 s). It ends at once when the bird comes near.
- Snow play and tree look play the Animator states `PlaySnow` and `PlayTree` directly. There are no clips yet, so they hold Hum and Breathing for now. When the director makes the clips, only those states' Motion changes. Tree look turns her body to the tree and lifts her head to its top. Run around uses `Girl_Run`.
- Stage 3 has no sunlit spot and is not affected.

## Alternatives

**Alternative 1. Wander at random round the light, with no play spots.**
Dropped. The forest starts about 4.9 m off the road and the trees have no colliders, so random points could walk her through trunks. Tree look needs a place in front of a tree anyway. Spots also let the director move them in the scene.

**Alternative 2. Play while walking too, whenever the bird is far.**
Not done now. The order and the user's summary are about sleeping at the light. It would overlap the waits at devices on the road (the log, the rabbits).

**Alternative 3. Have a cry call her back.**
Not done. The order says "when the bird comes to her". Cries keep their existing rule (she looks round while walking).

## Consequences

**Gains.**

- She no longer just follows the road: she goes to the forest edge round the light and plays.
- The stage ends only when the bird goes to her, which shows the bird keeping her company.

**Costs.**

- If the bird never comes, the stage never ends. One more exception to "the game goes on even if the player does nothing" (1.3, 3.1). Whether she should fall asleep by herself after a while is left in chapter 17.
- Tree spots use where the trees were when the builder ran. Replanting with `TreeGenerator` means running the builder again.
