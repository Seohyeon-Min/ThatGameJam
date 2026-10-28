# 0016. The bird shakes its head when it reaches the closed line

- Status: accepted
- Date: 2026-10-28
- Related: [`../implementation_plan.md`](../implementation_plan.md) 4.1, [ADR 0011](0011-sent-where-the-way-is-closed-the-bird-shakes-its-head-on-a-branch.md), [ADR 0012](0012-limit-the-bird-per-stage.md)

## Context

Under ADR 0011 the bird went to a nearby branch and shook its head the moment a click landed past the closed line (since ADR 0012, the current stage's sunlit spot). Refusing the moment it touched the edge was dropped then. The user wanted this changed: refuse when the bird actually reaches that line, not based on where the click landed.

## Decision

The user decided.

**The bird flies toward the clicked spot as usual.** Clicked past the line, it still goes as far as the line.

**If it reaches the line while its target is still past it, it refuses.** It lands on the nearest branch on screen and shakes its head. With no branch it stays where it is.

Refusal now happens only while flying. Refusing while perched or landing is gone. Perched and clicked past the line, it takes off, flies to the line, then refuses.

## Alternatives

**Alternative 1. Refuse the moment of the click (ADR 0011).**
The user changed it. The bird going right up to the closed way reads better.

## Consequences

**Gains.**

- The bird shows it went to the end of the way and was stopped
- Cases such as refusing while heading for or landing on a device are gone, so there is less code

**Costs.**

- The refusal comes later, by the time it takes to fly to the line
- A click just short of the line that never reaches it does not refuse. Only a click past the line does
