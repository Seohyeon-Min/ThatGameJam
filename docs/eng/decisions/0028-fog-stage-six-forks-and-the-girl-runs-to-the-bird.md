# 0028. The fog stage becomes six forks, with the girl running to the bird

- Status: accepted. Replaces [ADR 0027](0027-fog-stage-becomes-a-fork-map-led-by-rabbits.md)
- Date: 2026-11-04
- Related: the director's order and the user's answers (`order_from_director.md`, repository root, not committed)

## Context

The director asked for a large change to the fog stage. The user answered six questions (A1 to A5, Q6).

## Decision

These are the user's and the director's decisions.

- Nobody clears the fog: neither the bird nor the girl.
- The stage starts with a rather long straight way. The fog thickens along it: none at its start, full at its end (the first fork).
- There are six forks. Each has one animal on its right way: squirrel, bird, fox, deer, elk, bear, in that order. The animals do not move. For now they are placeholder capsules.
- When the bird cries, an animal answers. A thick white ring about the animal's size grows from its middle and fades.
- The bird's place decides which animal answers: the first animal ahead of the bird along the right route. Distance does not count. A bird above a dead end counts as being at the fork that dead end leaves.
- Every way is at least twice as long as in ADR 0027's map. Dead ends are shorter than right ways (A4).
- When the bird cries, the girl runs to it (`Girl_Run`). She runs only along the ways. She can reach the way she is on and the two ways of the fork ahead. Inside a dead end, she can reach that dead end and the right way of its fork. She runs to the point among these nearest the bird (A1).
- When she has run there and the bird does not cry again for about 3 s, she walks on at her usual speed (A1).
- Walking by herself, she takes the right way at a fork whose animal has already answered. Otherwise she stands 3 s, as before, and picks 50:50. If the animal answers meanwhile, she takes the right way (A2).
- If she starts walking by herself inside a dead end, she turns back at once to its fork and takes the right way when that fork's animal has answered. Otherwise she walks to the wall and back (Q6).
- The map gets room by stretching the terrain. Stage 3's grass and flowers and the forest around it are laid again for the new map (A5).

Decided while building it:

- The two ways of every fork leave toward screen right (world +X+Z) and screen up (world -X+Z). With the quarter-view camera (yaw 315) they part at a right angle on the screen. Which one is right varies from fork to fork.
- The fog is thick (0.9) and thin over the ways (10 % of that). Tried first with ADR 0027's even 0.22: in the quarter view the tree trunks hid the forks. With fog thin only over the ways, the ground and shadows of the ways show and the forest sinks into white.
- The maze's trees are painted in watercolor like the world's trees. Only painted trees turn see-through when they hide the bird or the girl.
- The world forest (`01_SYSTEM/TreeGenerator`) gets a Clear zone over the maze. The builder plants the trees inside the maze along the ways.

## Alternatives

**Alternative 1. The girl runs to the bird with no limit on distance.**
Dropped. The ways show, so a single cry at the goal would send her down the right ways all the way, and the animals would be pointless.

**Alternative 2. In a dead end she always walks to the wall and back, whatever the animal did.**
Dropped. The user chose turning back at once when the animal has answered (Q6).

**Alternative 3. Light, even fog (0.22) as in ADR 0027.**
Dropped. The forks did not read on the screen.

**Alternative 4. Paint the ways on the ground.**
Dropped. The terrain has no layers (one snow material), and no new art assets are made on the `sam` branch.

## Consequences

**Gains.**

- One cry does two things: an animal shows the way, and the girl comes.
- Without help the girl still arrives, by 50:50 choices.

**Costs.**

- Stage 3 grew from about 47 m to about 174 m (the right route). Without help this stage alone takes about 4 minutes. The design doc's goal for the whole game is 5 to 8 minutes.
- The terrain was stretched and the world forest made again. The forest generator draws its random numbers column by column, so a longer ground moves every tree in stages 1 and 2 too.
- The maze's trees never leaf out. At season 3 the world forest around them does.
- The fog is thick, unlike ADR 0027's "light fog".
