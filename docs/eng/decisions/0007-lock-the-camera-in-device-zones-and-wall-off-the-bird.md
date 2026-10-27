# 0007. Lock the camera in device zones, and wall off the bird until the device is solved

- Status: accepted (being tried; waiting for the director's feedback)
- Date: 2026-10-27
- Related: [`../implementation_plan.md`](../implementation_plan.md) 3.1, 4.1, 4.7, [`../game_design.md`](../game_design.md) §2.5, [ADR 0005](0005-camera-follows-the-bird.md), [ADR 0003](0003-implementation-plan-question-answers.md) Q3

## Context

The camera of ADR 0005 followed the bird but did not go past three things: the stage range, a range around the baby (so the baby stays on screen), and a Z limit line before an unsolved device (`CameraGate`). The director wanted the camera to be fixed per section. By default it follows the bird; when it enters a section with a device, the camera eases into that section and the range it follows within shrinks. Clearing the section releases it. Trying it in the test scene, the user decided three more things.

## Decision

The user summarized the director's request. The details are AI proposals the user accepted.

**Each device has a camera zone (`CameraZone`).**

- When the baby reaches the zone's trigger, the point the camera follows eases into the zone's rectangle over 1 second. The camera follows the bird inside it
- When the device is solved, it eases out over 1 second. It does not lock again even if the baby walks back over the trigger
- If the bird solves the device before the baby arrives, the zone never locks
- The range is a rectangle

**The baby is not a reference for the camera.** The director asked for this, to show that the bird and the baby are separate beings. The range around the baby (`babyRange`) was removed. If the bird stays put, the baby can walk off screen.

**The bird cannot leave the stage's road.** The stage range (`Stage.area`) is now the road's range, not a camera range. Both the bird and the point the camera follows stay inside it.

**The bird cannot go past a device until it is solved.** Each zone has an invisible wall (`BirdWall`). It stands even before the baby arrives. The bird does not move by physics, so the wall is not a collider but the Z end of the bird's movement range. The bird still cannot leave the screen (Q3). So the bird's range is the narrowest of the road, the screen around the camera point, and the walls. `CameraGate` was removed.

## Alternatives

**Alternative 1. Fix the camera to one point in a zone.**
Rejected. The director wanted the range to shrink, not a complete fix.

**Alternative 2. Keep the range around the baby.**
Rejected. The director asked to remove it. The cost is that the baby can leave the screen.

**Alternative 3. Ease locking and unlocking with a SmoothDamp-delayed offset.**
Built first, then rejected. The bird is pushed to stay within the screen around the camera point, and the pushed bird moved the point again, so at the moment of locking the camera overshot and came back. Replaced by a 0-1 weight per zone that blends by that ratio. It does not jump even when one zone releases as the next locks.

**Alternative 4. Make the wall a real collider.**
Rejected. The bird moves its position directly without a Rigidbody (plan 4.1). As the end of its movement range, the bird stays pressed against the wall and keeps going once it opens.

## Consequences

**Gains.**

- In a device zone, the camera stays close to a framing that makes the device easy to click
- The bird cannot move on to the next device before solving this one, so each zone has one thing to do. In the fog sections, the bird cannot clear the next fog early
- The bird and the baby move separately

**Costs.**

- The baby can leave the screen
- If the bird is behind the camera when a zone locks, it is pulled into the screen
- Walls and zones assume the stage runs along +Z and judge by Z. Stages whose road turns sharply need another look
- Level design must set each zone's trigger, range, and wall position. The current values are temporary
- Still differs from design doc §2.5. May be reopened after the director's feedback
