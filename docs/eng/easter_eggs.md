# Easter eggs (small interactions)

> Source: `order_from_director.md` at the project root (the director's request). This file organizes its content. On 2026-11-04, after another talk with the director, 9 Snow on the bird and 13 Feather and baby were dropped, and the migrating birds' frequency follows section 4 (once per game, between stages 1 and 3).
> Korean version: [`../kor/easter_eggs.md`](../kor/easter_eggs.md)
> Status: list only. Nothing has been built yet.

Found easter eggs are recorded in the encyclopedia. When an easter egg shows itself, the game takes a close-up photo of it at that moment for the encyclopedia, not when the player clicks. The `` ` `` key opens it (the source says ESC; the user changed it).

## 1. List

| # | Name | What happens |
|---|---|---|
| 1 | Icicle xylophone | A tree has icicles. Pecking one drops it with a xylophone-like sound. Pecking them in order plays the game's main melody |
| 2 | Squirrel in the tree hollow | Right-click near a tree hollow and a squirrel pokes its head out, then goes back in |
| 3 | Frozen river and trout | Left-click the frozen river and the bird pecks it until it cracks. Wait, and a masu trout appears |
| 4 | Migrating birds and shadows | Migrating birds coming from far away, with shadows on the ground |
| 5 | Flower in the snow | A flower in the snow. As above, the bird finds it by clicking and pecking |
| 6 | Swallowtail butterfly | After the snow melts, left-click a flower and a butterfly flies in |
| 7 | Cornelian cherries | Peck and eat the cornelian cherries in a bush and the bird puffs up its chest |
| 8 | Puddle reflection | Passing over a meltwater puddle, the character is reflected as something else, not itself. The baby is a metaphor for spring itself, so something mysterious would suit it |
| 9 | Ladybug in the stump | Peck the stump of the fallen tree and a ladybug comes out |
| 10 | Cliff echo | Make a sound at the cliff and an echo comes back |
| 11 | Snow whirl | Circle round and round over snowy ground and a snow whirl forms |

## 2. Where each is possible

○ possible · △ conditional · × not possible

| # | Easter egg | Tutorial | Stage 1 | Stage 2 | Stage 3 | Reason |
|---|---|---|---|---|---|---|
| 1 | Icicle xylophone | ○ | ○ | ○ | × | The icicles are melting in stage 3 |
| 2 | Squirrel in the tree hollow | ○ | ○ | ○ | △ | Stage 3 only in 3-3. Not in the fog sections (section 3) |
| 3 | Frozen river and trout | ○ | ○ | ○ | × | The ice melts in stage 3 |
| 4 | Migrating birds and shadows | × | ○ | ○ | ○ | Seen zoomed out, so possible anywhere. Once per game, between stages 1 and 3 |
| 5 | Flower in the snow | × | × | ○ | × | Assigned to stage 2 by design. The snow melts in stage 3 |
| 6 | Swallowtail butterfly | × | × | × | ○ | Only after the snow melts and flowers bloom |
| 7 | Cornelian cherries | ○ | ○ | ○ | △ | Stage 3 may use yellow cornelian cherry flowers instead of berries |
| 8 | Puddle reflection | × | × | × | ○ | Puddles appear only once the snow melts |
| 9 | Ladybug in the stump | × | ○ | × | × | Only at the stump in 1-1 |
| 10 | Cliff echo | × | × | × | ○ | Only at the 3-3 cliff |
| 11 | Snow whirl | ○ | ○ | ○ | × | Needs snow on the ground |

Grouped by stage:

- Tutorial: possible 1, 2, 3, 7, 11 / not possible 4, 5, 6, 8, 9, 10
- Stage 1: possible 1, 2, 3, 4, 7, 9, 11 / not possible 5, 6, 8, 10
- Stage 2: possible 1, 2, 3, 4, 5, 7, 11 / not possible 6, 8, 9, 10
- Stage 3: possible 4, 6, 8, 10 + conditional 2, 7 / not possible 1, 3, 5, 9, 11

## 3. Cautions for stage 3

- **2 Squirrel**: in the fog sections (3-1, 3-2), "cry and the animals on the path answer" is the path-finding rule. If the squirrel answers a cry, the player takes that way for the path. So it goes in 3-3 only.
- **8 Puddle**: the hidden obstacles under the fog include puddles too (GDD 9.2). Make the reflection puddle look different from obstacle puddles, or put it off the baby's path or in 3-3.

## 4. Placement (what goes in each stage)

| Stage | Easter eggs |
|---|---|
| Stage 1 | 9 Ladybug in the stump, 11 Snow whirl, 7 Cornelian cherries |
| Stage 2 | 5 Flower in the snow, 1 Icicle xylophone, 2 Squirrel in the tree hollow |
| Stage 3 | 6 Swallowtail butterfly, 10 Cliff echo, 8 Baby reflected in a puddle |
| Stages 1 ~ 3 | 4 Migrating birds and shadows (once per game, between stages 1 and 3) |

## 5. Triggers (only those written in the source)

| # | Easter egg | Condition | Stages |
|---|---|---|---|
| 4 | Migrating birds and shadows | Stay at maximum zoom-out, once per game | Stages 1 ~ 3 |
| 11 | Snow whirl | Circling round and round over snowy ground | Tutorial to stage 2 |
