# 0010. Join the stages on one road and drop the teleport; the bird goes only as far as the next device

- Status: accepted
- Date: 2026-10-28
- Related: [`../implementation_plan.md`](../implementation_plan.md) 3.1, 3.3, 4.6, 4.7, [ADR 0007](0007-lock-the-camera-in-device-zones-and-wall-off-the-bird.md), [ADR 0008](0008-the-bird-locks-camera-zones-and-there-is-no-wall.md)

## Context

Until now the stages sat at separate places in the world. When the baby fell asleep at a sunlit spot, the screen was covered, the baby and bird were moved to the next stage's start, and the screen opened again (plan 3.3, 4.6). The director wanted this changed. In one scene, the stages' maps join as real ground on one long road, and the game moves on to the next section without a teleport. Each stage stays its own root object. The bird's range is limited to the section and opened when the stage is cleared.

The director said a long map was ready in the main scene, but it was not on any branch yet.

## Decision

The director's request, settled with the user.

**The stages join in one line along +Z.** Each stage's ground meets the previous stage's ground. Each stage has one root object. Stages 1 and 2 are joined in `Sam_Test` first. When the main map arrives, this moves there.

**The next stage starts where the baby fell asleep.** When the baby falls asleep at a sunlit spot, the season effect plays and the next stage starts at once. The baby stays asleep there and its wake timer runs again. Once awake it walks the next stage's route. Nothing covers the screen and nothing is moved.

**The bird goes only as far as the next device.**

- The bird may fly over the road of every stage up to the current one. Backwards is always open
- In the current stage it goes forward only up to the limit line (Z) of the first device not yet opened. Each device (`PathBlocker`) has one. The log's is the middle of the second stream. The bird can still perch on the log's top beyond it
- Once the device opens, the way is open to the end of the stage or to the next device
- When the baby falls asleep and the stage ends, the next stage's road opens
- The point the camera follows stays in the same range

**The dev tool's "next stage" teleports.** It puts the baby asleep at the current stage's sunlit spot and starts the next stage as if the stage had been cleared. The bird moves to the next stage's start.

The screen cover (`ScreenFader`) had no use left and was deleted.

## Alternatives

**Alternative 1. Limit by stage only.**
Open the next stage's road only when a stage is cleared. Dropped. The bird would get past a device first. The director's other request (when the bird tries to go where it cannot, it perches on a branch and shakes its head) limits per device and opens the way when the device opens.

**Alternative 2. Have the baby walk to the next stage's start.**
Extend the road past the sunlit spot and have the baby fall asleep at the next start. Dropped. If the sleeping spot is the start, no extra road is needed. ADR 0006 builds no road past the sunlit spot.

**Alternative 3. Wait for the director's main map.**
Dropped. The user chose to join the stages in `Sam_Test` first and move them later.

## Consequences

**Gains.**

- No break between stages. The season change is seen in the same place
- The bird cannot skip past a device

**Costs.**

- It relies on the stages' ground meeting along +Z. A map whose road turns would need the range rebuilt
- Each device's limit line is placed by hand
- When moving to the main map, the stage starts, areas, and limit lines must be fitted to that map
