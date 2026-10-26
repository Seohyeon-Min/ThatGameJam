# The bird flies freely, including height, and uses WASD + mouse wheel + E

Date: 2026-10-26 14:36

## Topic
Before building test characters, we had to choose how the player (the bird) moves and which inputs it uses.

## AI proposal
- (a) Move only on a plane (XZ) at a fixed height. Easier to control in quarter view. The AI recommended this.
- (b) Fly freely in 3D, including height.
- For input, the AI asked to choose between keyboard, mouse click movement, and mobile touch.

## User choice
- (b) Fly freely, including height, because there will be game gimmicks that use the height axis.
- WASD moves, the mouse wheel goes up and down, and E interacts.

## Trade-off
- Gain: puzzles and gimmicks can use height.
- Cost: in quarter view the bird's height is hard to see. A shadow or a height marker may be needed.
- Cost: wheel delta sizes differ between browsers, so the wheel uses only the scroll direction and moves 0.5 per step. On the web, a trackpad may change height too fast.
