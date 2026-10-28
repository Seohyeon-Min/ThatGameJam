# 0004. In the fog sections, natural resolution time keeps running, and when it comes the baby goes to the correct path

- Status: accepted
- Date: 2026-10-27
- Related: [`../implementation_plan.md`](../implementation_plan.md) 4.12, Section 5 Q10, [ADR 0003](0003-implementation-plan-question-answers.md)

## Context

In the fog sections the baby follows the trail the bird draws. If the bird does nothing, the fog slowly recedes along the correct path (design doc §9.2). The design doc does not say what happens when the two overlap. ADR 0003 left only this question (Q10) open. It must be decided before step 2 (the fog prototype) starts.

## Decision

The user decided. It is the same as the AI's proposal.

**Time runs continuously from when the baby first stops in front of the fog. It does not stop while the bird is drawing a trail. When natural resolution comes, the baby drops the remaining trail, goes to the nearest correct path point, and walks along the correct path.**

## Alternatives

**Alternative 1. Pause or restart the time while the bird is drawing a trail.**
Rejected. Players who draw the wrong path would wait longer. That does not fit "spring comes in the end" (§15.2).

**Alternative 2. After natural resolution, the baby still follows the remaining trail to its end.**
Rejected. If the remaining trail leads into a hidden obstacle, the baby stops even after natural resolution.

## Consequences

**Gains.**

- The fog sections, like the other obstacles, always end within a fixed time
- The baby's movement rule is simple: the trail before natural resolution, the correct path after

**Costs.**

- If natural resolution comes when the bird has almost finished drawing a path, the baby turns to the correct path instead of that trail. To the player, the help may look ignored
