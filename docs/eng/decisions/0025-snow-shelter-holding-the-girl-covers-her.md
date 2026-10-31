# 0025. Snow shelter: holding the click on the girl flies the bird ahead above her; the shadow is a shadow-only bird copy toward the sun; the snow is really removed

- Status: Accepted. Replaces the wing umbrella of design doc §6.2 (covering whenever the bird is above the baby)
- Date: 2026-10-31
- Related: director's order "Snow Protection Interaction", [`../implementation_plan.md`](../implementation_plan.md) 4.9

## Context

The director's order. In the intro, while the user holds a click on the girl, the bird flies a little ahead above her at her pace and keeps the snow off with its wings and shadow. She walks looking at the bird. The shadow shows only during the interaction. When she reaches the light pillar at the end of the intro, the interaction is gone.

Main's sun is low (about 32°). With the bird at altitude 4 over her, its real shadow lands about 6.5 m away. The project has no shadow or blob texture, and on the `sam` branch no art assets are made up.

## Decision

Proposed by this agent, all accepted by the user, who will give feedback later.

**Shadow.** A shadows-only copy of the bird model sits between the sun and the girl: 4 m toward the sun from 1 m up her body, back to the sun, wings spread, 1.6 times the bird's size. The real light casts a wing-shaped shadow over her and the ground around her. It grows and shrinks over 0.3 s.

**Snow.** Really removed. A 1.8 m wide trigger box from her feet to the bird's height; `Snowfall`'s Trigger module kills snowflakes inside it.

**Click.** Starts when the left button goes down on the walking girl (walking, pausing, reacting). It lasts while held, even if the pointer leaves her. On release the bird hovers where it is. While she sleeps, gets up, or lies at the light pillar, a click is an ordinary click.

**Bird.** First flies at its normal speed to 0.6 m ahead of her, then moves as far as she does (+ 0.5 m/s catch-up), facing the way she walks. The shadow and snow removal are on only while it is over her (within 0.3 m of the spot, at flying height).

**Girl.** Keeps walking and only turns her head to the bird (the existing head turn for nearby things). There is no look-up clip. Her occasional pauses stay, but she looks at the bird instead of around, so progress time is unchanged (design doc §6.2).

**Where.** Only in a stage with `Stage.snowShelter` on. Only Main's `Intro` has it.

## Alternatives

**Alternative 1. Decal Projector with a shadow texture.**
Postponed. Needs a texture from the art team.

**Alternative 2. Only the shadow gives the feeling; the snow stays.**
Dropped. The order says the snow is kept off.

## Consequences

**Gains.**

- A wing-shaped shadow covers her without new art assets, and follows the real light
- Snowflakes in a column around her drop to 0 (11 to 60 in 4 same-size columns beside her)

**Costs.**

- The shadow is the resting bird's wings-out pose. It does not flap
- The shadow stretches along the sun direction onto the ground behind her
- One more Trigger module check on the snow particles. To be checked on the web
