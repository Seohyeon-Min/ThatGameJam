# 0036. Stage 2 becomes the blocked rabbit burrow

- Status: accepted
- Date: 2026-11-06
- Related: [`../game_design.md`](../game_design.md) chapter 8, chapter 17, [`../implementation_plan.md`](../implementation_plan.md) 4.11

## Context

Director's order (`order_from_director.md` item 1): change stage 2 to match the updated `game_design.md`. Chapter 8 now describes road rabbits blocking the way and a burrow blocked by snow: the bird calls the rabbits inside with a cry or clears the snow itself, and the burrow opens. `Main` still had the old design (demo bird, snow-laden branches, sleeping rabbit family, bypass). The design left several values open, so the user was asked and chose to go with the proposals.

## Decision

1. Remove the old stage 2's objects and scripts (`RabbitFamily`, `RabbitActor`, `SnowBranch`, `BranchDemo`) and `SnowBranch.prefab`. Keep `Stage2SnowMelt`, which melts the snowbanks.
2. Three road rabbits use the kit model; two burrow rabbits use the adult model. Only existing models and clips are used.
3. The burrow's hole and entrance snow are placeholders, made from a primitive (sphere), the existing `Snowbank.prefab` and existing materials. No new art or sound assets (the `sam` branch rule).
4. The bird clears the snow by landing on the ground in front of the mound and pecking (`Peck`) over and over. It digs only while perched there; what it dug stays when it leaves (the design's proposal).
5. A cry reaches the rabbits inside within 10 m (horizontal, `burrowCryRadius`) of the hole. They answer with `BirdCryPulse`, as in stage 3.
6. The only waiting signal is the yellow sparkle.
7. Rebuild `Stage2` in `Main` with a builder (`AgentScripts/BuildStage2.cs`). The 1-3 rabbit procession and the easter egg placement are left alone.

Details decided during the work:

- Digging is one progress value from 0 to 1. The burrow rabbits (after a cry) and the bird (while pecking) each add a rate of 1/8 per second. Naturally, the rabbits start digging by themselves with 8 s left and break through at 30 s.
- Any help from the bird (a cry or pecking) counts as the bird's help, also when the natural time runs out.
- Once through, the burrow rabbits come out, the road rabbits go into the burrow, and then the road opens, about 7 s after breaking through.

## Alternatives

**Alternative 1. The bird perches on top of the snow mound.**
Rejected. The bird would float as the mound gets lower. Pecking from the ground in front reads as clearing.

**Alternative 2. Open the road when the road rabbits set off for the burrow.**
Not done. The girl could walk into the rabbits. The cost is about 7 s before the road opens.

**Alternative 3. Adapt the old `BuildStage2` (for Sam_Test).**
Rejected. Most of it built the old bypass and branches. It was rewritten for `Main`.

## Consequences

**Gains.**

- The cry and the left click are used on one device, and together they are faster (design 8.2, design goals).
- The rule that animals answer a cry is shown before stage 3.

**Costs.**

- The burrow and snow mound are placeholders, and the bird's clearing is a temporary dip of its body. They need replacing when art arrives.
- The road opens about 7 s after breaking through. If that feels long, the rabbits' speed or spacing can be cut.
- The old stage 2 in `Sam_Test` is left with missing scripts.
