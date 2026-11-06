# 0032. The off-screen cry's size and how much shows follow the distance, and a passed animal doesn't answer

- Status: accepted
- Date: 2026-11-05
- Related: ADR 0028 (the answering animals), ADR 0031 (the off-screen cry on the edge), implementation plan 4.12

## Context

Two points of the user's feedback after ADR 0031.

1. The cry of an animal off the screen always shows half on the screen's edge, at one size. It should vary with distance: very far, small and mostly cut off; nearer, bigger and less cut. At its best about 2/3 shows.
2. Crying after passing the first animal, the first animal still answers. Once passed, the second should.

The cause of 2. ADR 0028 measured "the first animal ahead of the bird" along the way nearest the bird, and a bird above a dead end counted as at its fork (to help the girl stuck in it). With the dead ends 1.5 times longer since ADR 0031, flying ahead over a dead end had that fork's animal answer though already passed. Counted on a 2 m grid over the maze, a passed animal answered at 42 % of the places.

## Decision

- The animal that answers is the first the bird has not passed yet. Every way of the maze runs forward (+Z, screen up-right), so passing is being ahead of the animal in Z, above a dead end too. Coming out of a dead end the girl takes the right way anyway, so its fork's animal need not answer.
- The off-screen cry follows the flat distance between the animal and the bird. Within `nearDistance` (25 m) it is as big as the bird's cry and 2/3 of the ring's width (`nearShown`) is on the screen; beyond `farDistance` (70 m) it is 40 % of that size (`farScale`) and 1/5 (`farShown`) shows; linear between. 25 m is about where an animal goes off the screen. At first it was 15 m, and an off-screen animal was never that near, so 2/3 never showed.
- The ring goes from the middle of the screen toward the animal, to the screen's rectangle moved in (or out) by as much as should show. Its place stays continuous at the corners too.
- All the values are on `FogForkAnimal.prefab`.

## Alternatives

**Alternative 1. Keep the dead-end rule, and above a dead end look at whether the bird passed that dead end's end.**
Dropped. Places would remain that don't match the user's words ("once passed, the second"), and it is more complex.

**Alternative 2. Go by how far off the screen it is (viewport distance) instead of the distance.**
Dropped. The user said distance. The same animal would look different with the camera's zoom.

## Consequences

**Gains.**

- A passed animal doesn't answer.
- The ring alone tells whether the animal is near or far.

**Costs.**

- Crying above the girl stuck in a dead end may have the next animal answer, not that fork's.
- A very far animal's cry (150 m) is a small arc in the corner of the screen, easy to miss.
