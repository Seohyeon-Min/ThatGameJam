# 0014. A camera zone locks when the bird enters the trigger and unlocks when it leaves the wider lock area

- Status: accepted
- Date: 2026-10-28
- Related: [`../implementation_plan.md`](../implementation_plan.md) 3.1, 4.7, [ADR 0007](0007-lock-the-camera-in-device-zones-and-wall-off-the-bird.md), [ADR 0008](0008-the-bird-locks-camera-zones-and-there-is-no-wall.md), [ADR 0012](0012-limit-the-bird-per-stage.md)

## Context

Under ADR 0008 a camera zone locked when the bird crossed the trigger line (Z) and stayed locked until the device opened. The bird cannot leave the screen, so while locked it was tied to the device's surroundings. Once ADR 0012 limited the bird per stage, while the log stood the bird stopped silently at z 23, not at the sunlit spot (z 25). The director answered with a sketch (`2.png`): flying far should unlock it. Entering the trigger locks it, and leaving the lock area unlocks it. The areas for going in and going out differ in size.

## Decision

The director decided. Re-locking and the sizes were settled with the user.

**The trigger is a band across the road.** The bird entering it locks the zone.

**The lock area is a wider rectangle around the trigger.** The bird leaving it unlocks the zone. The camera eases back to following the bird over 1 second.

**While the device is closed, entering the trigger again locks again.** Once the device opens it no longer locks.

**While locked, the camera stays nearly fixed in a small area by the device, as before.**

**The lock area must fit inside the locked view.** The bird cannot leave the screen, so a lock area larger than the bird can reach would never unlock. The log's zone: camera area 6 × 1, lock area x ±8 and z 13.5 to 21.5, trigger band at z 16. The lock area covers the two roadside tree rows, so perching on their branches does not unlock it.

## Alternatives

**Alternative 1. Remove camera zones.**
Dropped. The director wants the camera to lock at gimmick sections.

**Alternative 2. Let the bird leave the screen while locked.**
Dropped. The bird never leaves the screen (ADR 0003 Q3).

## Consequences

**Gains.**

- The bird is not tied to a gimmick section. Flying far makes the camera follow again
- Going in and going out at different places keeps the lock from flickering at the edge

**Costs.**

- The lock area's size must be set by hand to fit the camera area and the bird's screen range
- Going around the trigger band sideways does not lock
