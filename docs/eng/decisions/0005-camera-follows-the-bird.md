# 0005. The camera follows the bird, and cannot go past a device until the device is solved

- Status: accepted
- Date: 2026-10-27
- Related: [`../implementation_plan.md`](../implementation_plan.md) 3.1, 4.1, 4.7, [`../game_design.md`](../game_design.md) §2.5, [ADR 0003](0003-implementation-plan-question-answers.md) Q3

## Context

Design doc §2.5 says each stage uses a fixed camera per section. Step 1 was built that way. When the baby entered a section trigger, the camera switched to that section's camera, and the bird could not leave that section's view (ADR 0003 Q3). After seeing the gray-box stages, the user said it is better if the camera follows the bird by default. At a device, the user wanted the camera to stop moving forward even if the bird goes ahead, and wanted that limit to open when the device is solved.

## Decision

The user decided. For the details, the user accepted the AI's proposal.

**The camera follows the bird and keeps the quarter-view angle. The point the camera follows does not go past:**

- The stage range
- A set distance from the baby. This keeps the baby on screen. So the bird cannot go far ahead of the baby or go back and leave it (questions C1, C2)
- The limit line (`CameraGate`) beyond a device that is not yet solved. The moment the device is solved, the camera can move up to the next limit line (question C3)

The bird still cannot leave the screen (Q3). The bird's position is kept around the point the camera follows. The clicked target point is not clamped, so the bird stays at the edge of the screen and keeps going when the limit opens. The fixed cameras per section and the section triggers (`CameraZone`) are removed.

## Alternatives

**Alternative 1. Fixed cameras per section, as in the design doc.**
Rejected. The user chose a camera that follows the bird. Fixed cameras can set an exact view that makes the device easy to click, but the feeling of moving the bird is weaker.

**Alternative 2. Follow the bird, but do not limit by the baby.**
Rejected. If the bird goes ahead or back, the baby leaves the screen. The scene where the bird leaves the baby and goes alone is better kept for the emotional peak of 3-3 (§9.6). That fits the design.

**Alternative 3. Open the limit after the baby passes the device.**
Rejected. The user wanted it to open the moment the device is solved.

## Consequences

**Gains.**

- The feeling of moving the bird directly is strong
- The camera stopping in front of a device becomes a signal that "something must be done here"
- The camera range comes down to the stage range and a few limit lines, so placement is simple

**Costs.**

- It differs from design doc §2.5. This must be agreed with the person in charge of the design
- Limit lines assume the stage extends along +Z and are checked by Z value. Stages where the path turns sharply need another look
- A view that makes each device easy to click cannot be set separately. For tall things like the log, adjust the height the camera aims at
- The 3-3 climb effect and the intro's zoomed-out camera must be added on top of this separately
