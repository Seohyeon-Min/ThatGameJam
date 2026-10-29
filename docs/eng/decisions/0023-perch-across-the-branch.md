# 0023. On a roadside tree branch the bird perches across the branch

- Status: accepted
- Date: 2026-10-29
- Related: [`../implementation_plan.md`](../implementation_plan.md) 4.1, [ADR 0009](0009-the-bird-perches-on-roadside-branches-and-goes-to-the-nearest-when-idle.md)

## Context

The bird perched facing the way it flew in. The user asked for it to line up with the branch when it perches.

## Decision

The user's instruction. This agent decided how to read "line up with the branch".

**The bird perches across the branch.** Like a real bird, its body is at a right angle to the branch and its feet wrap around it. Of the two ways, it faces the one nearer the way it flew in. It turns slowly over the 1.2 s landing.

- The branch's direction is, per tree, the way the branch runs out from the trunk at the perch (yaw). The builder sets the perch point's forward at a right angle to it
- It turns only where `Perchable`'s `alignBird` is on. Only roadside trees have it on. On devices such as the log it keeps the way it flew in, as before

## Alternatives

**Alternative 1. Put the body along the branch.**
Dropped. Real birds perch across a branch. If the user wants it, one angle in the builder (+90) changes it.

**Alternative 2. Give each perch one fixed facing.**
Dropped. Coming from the opposite way, the bird would turn a long way. The nearer of the two ways looks less odd.

## Consequences

**Gains.**

- The bird's feet hold the branch across it

**Costs.**

- Depending on the way it flew in, it turns up to 90° while landing
- The bird is big next to the trees, so thin branches and trunks still pass in front of and behind its body
