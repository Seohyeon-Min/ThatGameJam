# 0031. Longer stage 3 ways, and its trees from the tree generator

- Status: accepted
- Date: 2026-11-05
- Related: the director's order, item 2, points 1 to 4 (`order_from_director.md`, repository root, not committed), ADR 0028, ADR 0029, implementation plan 4.12, 4.13

## Context

The director asked for five changes to stage 3. This decision covers the first four (the fifth, the girl not following onto a wrong way, comes separately).

1. Altitude limit at 70 % of the highest.
2. The ways are too short. In the quarter view the ways toward the screen's upper right feel even shorter. Up to twice as long is fine.
3. The sound ring of an animal off the screen does not show. When the animal crying is not on the screen, the ring should show at the screen's edge in its direction (the director's drawing: the ring's centre on the screen's edge, half of it showing).
4. The trees stand in neat rows along the ways, so the ways all show. In stage 3 they should stand rather chaotically, zigzagging. And every tree is a bare birch: they should follow the tree generator's per-stage shares.

Found:

- The maze's 762 trees were planted by the builder (`BuildStage3.cs`) itself: rows on both sides of every way, 2.6 m from its middle, 1.5 m apart. Only bare prefabs, and outside the tree generator's lists, so they did not follow the seasons. The world forest left the whole maze empty with a Clear zone.
- The animals' answer changed in main's "Fog1" commit from the yellow ring (`CryRing`, drawn on the edge when off the screen) to the bird's cry effect (`BirdCryPulse`). The effect played only at the animal, so the edge handling was gone.

## Decision

The user's decisions.

- Altitude: stage 3's `maxAltitudeShare` from 0.8 to 0.7.
- Ways: the straight way (21.5 m) stays. Right ways twice as long (44 to 56 m), dead ends one and a half times (21 to 27 m). The six forks and the cliff stretch (24 m) stay.
- The cry off the screen: the bird's cry effect plays centred on the screen's edge in that direction, so half of it shows. With the animal on the screen, at the animal as before.
- Trees: from now on a stage's trees are left to the tree generator. So are stage 3's maze and ending forest. The maze's trees are not hidden before their season (they may show from stage 2; we'll see). The forest thinning out toward the cliff comes from a new `TreeZone` option. The walls of trees at the dead ends are dropped for now.

Decided while building:

- The Clear zones over the ways cut each way into pieces of 3 to 6 m, and each piece and side draws its own width (1.8 to 3.2 m from the way's middle), so the forest's edge wanders. Turns and forks (5 m) and the animals (3 m) are cleared too. `TreeZone` already handled turned boxes, so the tree generator's placement code did not change.
- `TreeZone` gained `Thin`: from all of the trees at the box's -Z face to `farShare` at its +Z face. Whether to drop one is drawn from its own random sequence, so this does not move any other tree.
- The earlier stages' `Road` Clear zone (x ±4.6) had stretched with the ground (`WorldGap`) and was clearing the middle of the maze. It did not show while one Clear zone covered the whole maze. The builder cuts it at the maze's start (z 76.5).
- Rerunning the builder carries over the `DreamFog` and `HiddenUntilSeason` a teammate set, as JSON. The animal prefab is not made again.
- The cry on the edge is put 4 m in front of the camera (`edgeDepth`), so trees don't hide it, and scaled down to look as big as the bird's cry. Its particles simulate locally, so it follows the edge every frame until it ends.
- The ways reach out to x ±19.8 m, so the fog and the stage's area were widened to fit (fog 47.6 m).

## Alternatives

**Alternative 1. The builder keeps planting the trees, zigzagging, with leafy prefabs mixed in.**
Dropped. The builder would have to redo the season shares and the oak change. The user chose to leave a stage's trees to the tree generator.

**Alternative 2. The builder plants the ending forest and picks only the leaves by the season shares.**
Dropped. They would not turn into oaks. The user chose the `TreeZone` option.

**Alternative 3. Draw the old yellow ring (`CryRing`) on the edge.**
Dropped. The user chose the bird's cry effect.

## Consequences

**Gains.**

- The maze's forest stands on the world forest's grid with ragged edges, and the ways no longer show as rows. No tree is within 1.5 m of a way's middle.
- In season 3 the leaves and oaks follow the world forest's shares.
- The cry of an animal off the screen half shows on the screen's edge in its direction.

**Costs.**

- The right route alone is about 333 m. Walking by herself (0.9 m/s), the girl takes over 6 minutes.
- The maze's trees show from stage 2 on.
- The dead ends are closed by the forest, not by walls of trees, so their ends may read less clearly.
