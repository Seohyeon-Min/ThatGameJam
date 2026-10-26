# 0004. Work in a personal test scene and leave `Main.unity` alone

- Status: accepted
- Date: 2026-10-26
- Related: [`../../../AGENTS.md`](../../../AGENTS.md) Scenes and folders

## Context

Unity scene files are hard to merge with git. When several people edit the same scene, they conflict.

## Decision

The AI proposed it and the user accepted.

**Leave `Main.unity` alone and work in `Assets/00_Scenes/Sam_Test.unity`.** Scripts go in `02_Scripts`, prefabs in `03_Prefabs`.

## Alternatives

**Alternative 1. Everyone works in `Main.unity`.**
Rejected. Scenes would conflict.

## Consequences

**Gains.**

- No scene conflicts with teammates

**Costs.**

- Moving the work into `Main.unity` later is a separate task. Making objects into prefabs makes that easier
