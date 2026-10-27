# 0003. Answer 11 of the open questions in the implementation plan

- Status: accepted
- Date: 2026-10-27
- Related: Section 5 of [`../implementation_plan.md`](../implementation_plan.md), [`../game_design.md`](../game_design.md)

## Context

Section 5 of [`implementation_plan.md`](../implementation_plan.md) listed 13 questions. Each one affects code structure but has no answer in the design doc. The AI added a suggestion to each question.

## Decision

The user decided. Q10 and Q11 were not decided.

| # | Decision | Same as the suggestion? |
|---|---|---|
| Q1 | The shared game scene is `Main.unity`. Stages are built as prefabs and gathered there | No. The suggestion was to pick the scene at the gathering step |
| Q2 | Use Cinemachine | No. The suggestion was to build it ourselves |
| Q3 | The bird cannot leave the view of the current section's camera. Each section has a bounds box. In 3-3, the area above and beyond the cliff is included | The range was narrowed to the section's screen. The suggestion was the stage range |
| Q4 | Natural resolution time runs from when the baby stops in front of the device. The bird may solve it before the baby arrives, and the baby still gives the help reaction | Same |
| Q5 | When the log the bird perches on starts to fall, the bird takes off on its own and hovers right above | Same |
| Q6 | When the mouse is over something the bird can perch on, only the cursor changes. The current marker that blinks on approach is removed | Same |
| Q7 | Roadside trees stay only as decoration, and perching is removed | Same |
| Q8 | In the intro, when the camera switches to the fixed position, the bird also comes down to the stage altitude | Same |
| Q9 | The side path opens only when all branches blocking it are cleared | Same |
| Q12 | While left click is held, the target point keeps updating | Same |
| Q13 | A small circle briefly shows where you clicked | Same |

## Alternatives

**Alternative 1. (Q1) Pick the shared game scene at the gathering step.**
Rejected. The user chose `Main.unity`. Building the stages as separate prefabs stays the same.

**Alternative 2. (Q2) Build camera switching ourselves.**
Rejected. It is a few fixed cameras and one follow in the intro, so the code is small. But the user judged that using Cinemachine is better. Cinemachine already has blending between cameras and target following.

**Alternative 3. (Q3) Use the whole stage as the bird's movement range.**
Rejected. Cameras are fixed per section, so the bird could leave the screen.

## Consequences

**Gains.**

- Step 1 (base framework) can start. The remaining Q10 and Q11 belong to step 2 (fog) and step 4 (endings), so they do not block step 1
- We do not build camera blending and following ourselves

**Costs.**

- The `com.unity.cinemachine` package is added. The web build may get a little bigger
- `AGENTS.md` has the rule "do not touch `Main.unity`". This rule must change before the stages are gathered in `Main.unity`
- The bird cannot leave the section's screen, so it cannot go ahead of the baby to a later section and solve a device early. A device can be solved only when it is in the same section as the baby
