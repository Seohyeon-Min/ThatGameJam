# 0002. The bird flies freely, including height, and uses WASD + mouse wheel + E

- Status: accepted
- Date: 2026-10-26
- Related: [`../rough_game_design.md`](../rough_game_design.md) Core Gameplay

## Context

Before building test characters, we had to choose how the player (the bird) moves and which inputs it uses. The target platform is the web browser.

## Decision

The user decided.

**The bird flies freely in 3D, including height,** because there will be game gimmicks that use the height axis.

**Input is WASD to move, the mouse wheel to go up and down, and E to interact.** The wheel uses only the scroll direction, not its size, and moves 0.5 per step.

## Alternatives

**Alternative 1. Move only on a plane (XZ) at a fixed height.**
The AI recommended it, but it was rejected. It is easier to control in quarter view, but gimmicks cannot use height.

**Alternative 2. Click-to-move with the mouse, or mobile touch.**
Rejected. For now only keyboard and mouse in desktop browsers are in scope.

## Consequences

**Gains.**

- Puzzles and gimmicks can use height

**Costs.**

- In quarter view the bird's height is hard to see. A shadow or a height marker may be needed
- Wheel delta sizes differ between browsers, so only the direction is used. On the web, a trackpad may change height too fast
