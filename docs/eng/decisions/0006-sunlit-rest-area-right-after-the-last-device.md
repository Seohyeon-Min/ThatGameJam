# 0006. The Sunlit Rest Area is not a separate section and sits right after the last device

- Status: accepted
- Date: 2026-10-27
- Related: [`../implementation_plan.md`](../implementation_plan.md) 3.1, 4.6, [`../game_design.md`](../game_design.md) §10

## Context

The design doc puts a "Sunlit Rest Area" at the end of each stage. In the level progress table (§14) it has its own section number, such as 1-3 and 2-3. The step 1 gray box built it as a separate section. After crossing the leaning log, the baby walked over more ground to reach the Sunlit Rest Area. The user said the Sunlit Rest Area is not a special space.

## Decision

The user decided.

**The Sunlit Rest Area is not a separate section. Put one portal-like light at the end of the baby's path, right after the stage's last device, and end the stage there.** Stages built later work the same way.

## Alternatives

**Alternative 1. Keep the Sunlit Rest Area as a separate section, following the section numbers in the design doc.**
Rejected. The user did not want it. It adds time where the baby only walks and nothing happens after the device is solved.

## Consequences

**Gains.**

- The stage ends as soon as the device is solved, so the flow is short
- Each stage needs less ground and a smaller camera range

**Costs.**

- Seasonal effects such as the rabbit family's procession in 1-3 and the snow melting in 2-3 happen right next to the device. Space for these effects must be left around the device
