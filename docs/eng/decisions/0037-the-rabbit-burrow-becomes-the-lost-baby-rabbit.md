# 0037. Stage 2's rabbit burrow becomes the lost baby rabbit section

- Status: accepted
- Date: 2026-11-07
- Related: [`../../kor/game_design.md`](../../kor/game_design.md) chapter 8, [ADR 0036](0036-stage-2-becomes-the-blocked-rabbit-burrow.md)

## Context

In ADR 0036, stage 2 was a burrow blocked by snow. The cry (the rabbits inside dig) and the left click (the bird pecks from outside) broke it open. The repository owner gave a new design. This section previews stage 3's "cry, animal answers, find the way" in a simpler, more emotional form. So the snow digging goes. First the stage shows that a mother rabbit's cry leads her young home. Then one young rabbit left behind is sent home by the same rule.

## Decision

The repository owner set the design. The details below were settled during the work.

1. **Showing the rule first.** When the girl passes the first point of stage 2's route (`Point_0`, radius 1.2 m), the mother rabbit, sitting 3 m out in front of the mouth, cries many times at no regular beat (the repository owner asked for this: like a player right-clicking at will). A yellow ring (`BirdCryPulse`) spreads with each cry. At each cry the two young rabbits scattered about her prick their ears and hop a step toward her, so they are seen following the cry. Once gathered, they follow her, still crying, into the hole. The girl notices the first cry too: her head comes up, she holds still a moment, then trots a little that way. From then on, a cry from the bird while she walks has her trot a little toward the bird as well.
2. **The one left behind.** The last young rabbit sleeps from the start, 1.6 m ahead of the girl's stop point. Once the others are all in, it wakes (`WakeUp` clip). Then it lays its ears back, shivers, and wanders a little within 0.6 m of its place.
3. **The girl.** She stops at the stop point. Her body and head follow the young rabbit, and her head tilts 14° to the side, swaying a little (`StopPoint.watchWithHead`, `headTilt`).
4. **The cry.** It is taken only while the young rabbit is awake and wandering. A cry within 10 m (horizontal) of the hole gets an answer after 0.6 s (`burrowAnswerDelay`). A yellow ring spreads at the mouth and the mother peeks out. The young rabbit pricks its ears, looks toward the hole, and runs in fast (1.6 m/s). The mother goes back in just before it gets there. The way opens once it is in. A cry while the mother is still calling the others gets no answer.
5. **Natural resolution.** The natural time counts only after the young rabbit has woken and the girl has stopped (`rabbitBurrowTime`, still 30 s). When it runs out, the mother cries by herself and the same answer follows, with no delay.
6. If the bird helped, the girl looks up at the bird before walking on. If not, she looks toward the hole the young rabbit went into first. This is the existing stop point handling.
7. Removed: the entrance snow (`EntranceSnow`), the bird's spot (`PerchPoint`) and its two yellow sparkles, the dig snow (`DigSnow`), the spot the road rabbits paced to (`PacePoint`), the second mother (`BurrowRabbit_2`), the two wait points, and the tuning value `burrowDigTime`. The decorative snow round the hole (`RimSnow_1`, `RimSnow_2`, `LipSnow`) stays.
9. **Cry hint.** Wherever a cry gets an answer, clusters of two to four tiny, faint cry rings (`CryHint.prefab`, 6 to 12 cm across) keep rising, overlapping like bubbles. At the burrow it shows in front of the mouth while the young rabbit is lost. At the squirrel's tree hollow it shows while the squirrel is inside. The repository owner asked for this.
8. The ears are raised or laid back by turning the ear bones (`Ear1.L`, `Ear1.R`) over the clip. The shiver shakes the model's position a little. No new clips or sounds were made.

## Alternatives

**Alternative 1. Keep the snow digging and only add the opening scene.**
Rejected. The new design has no digging. Keeping a left-click way to clear it would blur this section's one rule: hear the answer and follow it.

**Alternative 2. Start the opening scene once the girl has stopped.**
Rejected. It has to play while she comes closer, so the rule is read before she stops. Instead, if she arrives first, cries get no answer and the natural time does not count until the scene is over.

**Alternative 3. Keep the yellow sparkle on the hole.**
Rejected. The sparkle marks something to left-click, and there is nothing to click here. The mother's cry, the yellow ring and the small ring at the mouth (decision 9) point to the hole instead.

## Consequences

**Gains.**

- The answering animals of stage 3 do not come out of nowhere. The player sees the same rule here first: the cry, the yellow ring, going that way.
- The bird never touches the rabbits. It only connects the family's voices.

**Costs.**

- The left click learned in the intro and stage 1 is not used in this stage. ADR 0036's discovery that two kinds of help together are faster is gone.
- There is no rabbit sound. The answer shows only through the yellow ring and the mother's movement. It goes in once the sound team makes one.
- The ears and the shiver move bones and positions from code. They change once dedicated clips exist.
- The positions and timings (about 10 s for the opening scene, 1.6 m in front of the stop point, a 14° tilt) have not been seen in play yet.
