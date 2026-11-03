# Easter eggs (small interactions)

> Source: `order_from_director.md` at the project root (the director's request). This file organizes its content without changing it. Places where the source disagrees with itself are listed in the last section.
> Korean version: [`../kor/easter_eggs.md`](../kor/easter_eggs.md)
> Status: list only. Nothing has been built yet.

Found easter eggs are recorded in the encyclopedia. The `` ` `` key opens it (the source says ESC; the user changed it).

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
| 9 | Snow on the bird | In a place like the tutorial, snow piles up on the bird while it keeps still. When it moves it shakes the snow off and flies |
| 10 | Ladybug in the stump | Peck the stump of the fallen tree and a ladybug comes out |
| 11 | Cliff echo | Make a sound at the cliff and an echo comes back |
| 12 | Snow whirl | Circle round and round over snowy ground and a snow whirl forms |
| 13 | Feather and baby | A sharp turn drops a feather, and the baby reaches out to catch it |

## 2. Where each is possible

○ possible · △ conditional · × not possible

| # | Easter egg | Tutorial | Stage 1 | Stage 2 | Stage 3 | Reason |
|---|---|---|---|---|---|---|
| 1 | Icicle xylophone | ○ | ○ | ○ | × | The icicles are melting in stage 3 |
| 2 | Squirrel in the tree hollow | ○ | ○ | ○ | △ | Stage 3 only in 3-3. Not in the fog sections (section 3) |
| 3 | Frozen river and trout | ○ | ○ | ○ | × | The ice melts in stage 3 |
| 4 | Migrating birds and shadows | ○ | ○ | ○ | ○ | Seen zoomed out, so possible anywhere. More birds each stage |
| 5 | Flower in the snow | × | × | ○ | × | Assigned to stage 2 by design. The snow melts in stage 3 |
| 6 | Swallowtail butterfly | × | × | × | ○ | Only after the snow melts and flowers bloom |
| 7 | Cornelian cherries | ○ | ○ | ○ | △ | Stage 3 may use yellow cornelian cherry flowers instead of berries |
| 8 | Puddle reflection | × | × | × | ○ | Puddles appear only once the snow melts |
| 9 | Snow on the bird | ○ | × | × | × | Snow falls only up to the tutorial |
| 10 | Ladybug in the stump | × | ○ | × | × | Only at the stump in 1-1 |
| 11 | Cliff echo | × | × | × | ○ | Only at the 3-3 cliff |
| 12 | Snow whirl | ○ | ○ | ○ | × | Needs snow on the ground |
| 13 | Feather and baby | ○ | ○ | ○ | △ | Only while the baby is awake. Needs a cooldown in the fog sections (section 3) |

Grouped by stage:

- Tutorial: possible 1, 2, 3, 4, 7, 9, 12, 13 / not possible 5, 6, 8, 10, 11
- Stage 1: possible 1, 2, 3, 4, 7, 10, 12, 13 / not possible 5, 6, 8, 9, 11
- Stage 2: possible 1, 2, 3, 4, 5, 7, 12, 13 / not possible 6, 8, 9, 10, 11
- Stage 3: possible 4, 6, 8, 11 + conditional 2, 7, 13 / not possible 1, 3, 5, 9, 10, 12

## 3. Cautions for stage 3

- **2 Squirrel**: in the fog sections (3-1, 3-2), "cry and the animals on the path answer" is the path-finding rule. If the squirrel answers a cry, the player takes that way for the path. So it goes in 3-3 only.
- **8 Puddle**: the hidden obstacles under the fog include puddles too (GDD 9.2). Make the reflection puddle look different from obstacle puddles, or put it off the baby's path or in 3-3.
- **13 Feather**: in the fog sections the bird turns often while drawing the path, so feathers could keep dropping. Use a long cooldown.

## 4. Placement (what goes in each stage)

| Stage | Easter eggs |
|---|---|
| Tutorial | 9 Snow on the bird (always active, but only visible here, where snow falls) |
| Stage 1 | 10 Ladybug in the stump, 12 Snow whirl, 7 Cornelian cherries |
| Stage 2 | 5 Flower in the snow, 1 Icicle xylophone, 2 Squirrel in the tree hollow |
| Stage 3 | 6 Swallowtail butterfly, 11 Cliff echo, 8 Baby reflected in a puddle |
| All stages | 4 Migrating birds and shadows (once per game, between stages 1 and 3), 13 Feather and baby (except stage 3) |

## 5. Triggers (only those written in the source)

| # | Easter egg | Condition | Stages |
|---|---|---|---|
| 13 | Feather and baby | The baby is awake and the bird turns sharply nearby (with a cooldown) | All stages |
| 4 | Migrating birds and shadows | Stay at maximum zoom-out, once per stage | All stages |
| 12 | Snow whirl | Circling round and round over snowy ground | Tutorial to stage 2 |

## 6. To confirm in the source

Places where the source disagrees with itself or is unclear. Ask the director.

| Item | What the source says in different places |
|---|---|
| How often 4 (migrating birds) happens | Section 2 "more birds each stage", section 4 "once per game, between stages 1 and 3", section 5 "once per stage" |
| 13 (feather) in stage 3 | Section 2 marks stage 3 △ (cooldown), section 4 "except stage 3", section 5 "all stages" |
| Range of 12 (snow whirl) | Sections 2 and 5 say tutorial to stage 2, section 4 places it in stage 1 only |
| `1, 3, 5, 8, 7,` after the list | Written with no explanation. Unclear whether it is a priority order |
| "Tutorial" | In the current game the intro stage covers the tutorial snow section (ADR 0018) |
