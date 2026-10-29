# 0018. Make the intro one stage without a device, and give the season five steps

- Status: accepted
- Date: 2026-10-29
- Related: [`../implementation_plan.md`](../implementation_plan.md) 4.8, 4.9, 4.15, [ADR 0010](0010-join-the-stages-on-one-road-and-drop-the-teleport.md)

## Context

The director asked for snow in the intro that stops when stage 1 begins, tied to the season system. The user said to treat the intro as one stage and sent a sketch of it (`image.png`). In the sketch the baby sleeps at the start of the road, snow falls, and there is a yellow circle in the middle of the road. Stage 1's log comes right after it.

The user also set the season steps.

| Step | Section | What it looks like |
|---|---|---|
| 0 | Intro | The baby wakes up and walks. Heavy snow falls |
| 1 | Stage 1, old log | The snow stops and things grow calm |
| 2 | Stage 2, rabbits | Small animals start to appear. Flowers can be seen |
| 3 | Stage 3, fog | Many animals are seen. Leaves grow on the trees |
| 4 | Ending stage | A flower field blooms |

The steps are to be told apart by the red tint, as now.

## Decision

This agent proposed it and the user decided.

**The intro is one stage without a device.** It merges the design doc's intro (§5) and tutorial snow section (§6). It runs in the same order as the other stages. The baby sleeps at the start of the road, wakes, walks about 21 m and falls asleep at the sunlit spot. Stage 1 starts there. The yellow circle is read as the sunlit spot.

**The design doc's intro-only elements are left out for now.** The zoomed-out camera, the bird starting high up, the waking cutscene, and the wing umbrella.

**The season has five steps, 0 to 4.** As now, it goes up one each time the baby falls asleep at a sunlit spot. The debug red tint is strongest at 4.

**Only the snow is tied to a real environment change.** Snow falls only in step 0. At step 1 only new snow stops, and the snow in the air falls on (`SeasonSnow`). Small animals, flowers, leaves, and the flower field are told apart only by the red tint. There is no ending stage map yet.

## Alternatives

**Alternative 1. Keep the intro and the tutorial apart, as in the design doc.**
Dropped. The sketch has only one road without a device and one sunlit spot. The user also said to treat the intro as one stage.

**Alternative 2. Turn the snow off at once at step 1.**
Dropped. The snow in the air would vanish all at once. "Stops and grows calm" means it dies down.

## Consequences

**Gains.**

- The intro runs on the same code as the other stages. No new device or flow code
- Season steps and stages line up. The stage number is the season step

**Costs.**

- The design doc's intro staging (§5: zoomed-out camera, cutscene) and the wing umbrella (§6) are missing. They must be added to this stage later
- "Falls asleep → snow stops", at the end of the tutorial in the design doc (§6.3), moves to the end of the intro. The baby still wakes four times (intro, stages 1 to 3)
