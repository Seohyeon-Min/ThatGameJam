# 0038. The icicle xylophone has one perch per branch

- Status: Accepted
- Date: 2026-11-07
- Related: [`../easter_eggs.md`](../easter_eggs.md)

## Context

The icicle xylophone had a `Perchable` and a `TapFrom` on each of its 8 icicles, so the bird moved for every icicle. The director asked for a change: two branches to sit on, and from a branch the bird taps every icicle under it without moving.

The model (`icy tree.fbx`) hangs the icicles four each from two branches. `Tube` (x 0 .. 2.7) holds `Cylinder`, `Cylinder 1`, `Cylinder 2` and `Cylinder 3`. `Tube 6` (x -2.7 .. 0.2) holds `Cylinder 4`, `Cylinder 5`, `Cylinder 6` and `Cylinder 7`.

## Decision

1. Remove the 8 icicle `Perchable`s and their `TapFrom`s. Add `Branch_Right` and `Branch_Left` under `icy tree/Cylinder 8`, each with one `Perchable`. Its `PerchPoint` takes the pose of the branch's old `TapFrom` nearest their middle.
2. The click colliders stay on the icicles. `Perchable` gains `Clicked Through` (a list of colliders). When `BirdController.PerchableAt` finds no `Perchable` in the parents, it uses the `Perchable` whose list holds the collider.
3. If the bird stands on the icicle's branch, `IcicleXylophone` pecks the clicked icicle at once. Before the peck, `BirdController.TurnToward` turns the bird on the spot toward that icicle (level only, at `birdTurnSpeed`). If not, it remembers the clicked icicle and taps it when the bird lands on that branch.

## Alternatives

**Alternative 1. Move the icicles under the branch objects so `GetComponentInParent` finds the branch.**
Rejected. The icicles are children of a model prefab instance and cannot be reparented.

**Alternative 2. `IcicleXylophone` takes the click and calls `BirdController.PerchOn` itself.**
Rejected. `BirdController` also handles the same click as a ground click, so the two would conflict.

## Consequences

- From one branch, the bird taps its 4 icicles without moving. Checked in Play Mode with `AgentScripts/VerifyIcicleBranches.cs`: all 8 icicles rang, the bird moved 0 m on the same branch, and it flew over to the other branch.
- The icicle at a branch's end is over 1 m from the beak. The bird still turns toward it and pecks in place.
- While perched, `BirdController` sets the bird's heading to `perchedYaw` every frame. Landing also sets `facingYaw` to the real heading, so taking off no longer snaps back to the old flight heading.
- `AgentScripts/BuildEasterEggs.cs` builds the same structure. It works out the old per-icicle spot, groups the icicles by the nearest branch mesh (`Tube`, `Tube 6`), and uses the spot nearest each branch's middle. `IcicleBranchPerches.cs` was a one-time migration of the current prefab.
