# 0003. The child walks straight along a road and waits at obstacles

- Status: accepted
- Date: 2026-10-26
- Related: [`../rough_game_design.md`](../rough_game_design.md) Core Gameplay

## Context

We had to decide how the child (AI) moves. The design document says only that the child moves forward on its own using AI.

## Decision

The user decided.

**The child's AI is not decided yet. For now, the child is on a straight road and blocked by obstacles.** The implementation (`ChildWalker`) walks straight toward the goal, probes ahead with a sphere, and stops when an `Obstacle` is there.

## Alternatives

**Alternative 1. Find the path to the goal with AI Navigation (NavMesh).**
The AI's first plan, put on hold. A straight road works without NavMesh baking.

**Alternative 2. Follow waypoints.**
On hold. The shape of the path is not decided yet.

## Consequences

**Gains.**

- Short code. No NavMesh baking

**Costs.**

- It does not work once the road turns or branches. Then this decision is reopened with alternative 1 or 2
