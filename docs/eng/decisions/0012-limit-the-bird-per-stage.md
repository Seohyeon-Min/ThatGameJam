# 0012. Limit the bird per stage: it goes only as far as the current stage's sunlit spot

- Status: accepted
- Date: 2026-10-28
- Related: [`../implementation_plan.md`](../implementation_plan.md) 4.1, 4.6, [ADR 0010](0010-join-the-stages-on-one-road-and-drop-the-teleport.md), [ADR 0011](0011-sent-where-the-way-is-closed-the-bird-shakes-its-head-on-a-branch.md)

## Context

ADR 0010 limited the bird per device. In the current stage it went forward only up to the limit line of the first device not yet opened. The director wanted this per stage instead. When the bird tries to go past the light pillar that each stage has, it lands on a nearby branch and shakes its head, as now.

## Decision

The director decided.

**The bird goes forward only up to the Z of the current stage's sunlit spot (the last point of the baby's route).** Devices do not hold it back. When the baby falls asleep at that sunlit spot and the next stage starts, the limit moves to the next stage's sunlit spot. Backwards is always open.

**The refusal is as in ADR 0011.** Clicking or dragging past the limit makes the bird land on the nearest branch on screen and shake its head.

The per-device limit line (`PathBlocker.birdLimit`) was deleted.

## Alternatives

**Alternative 1. Keep it per device (ADR 0010).**
The director changed it. The bird can go past a device first. It still cannot go far ahead of the baby waiting beyond the device.

## Consequences

**Gains.**

- No limit to place by hand. The sunlit spot is the limit
- The bird can look around freely within a stage

**Costs.**

- The bird can get past a device (the log) first. A camera zone locks once the bird crosses its trigger until the device opens, so meanwhile the camera stays around the device (ADR 0008)
