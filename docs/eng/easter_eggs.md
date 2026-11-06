# Easter eggs (small interactions)

> Source: `order_from_director.md` at the project root (the director's request). On 2026-11-06 the director wrote a nearly final plan at the end of this file, and this file was reorganised around it. The earlier list (13 items, with the frozen river and trout and the swallowtail) is in the git history.
> Korean version: [`../kor/easter_eggs.md`](../kor/easter_eggs.md)
> Status: all but the puddle are built as prefabs and placed in a clearing behind the intro's start (-Z) in `Main` ([ADR 0034](decisions/0034-easter-eggs-as-prefabs-in-a-clearing-behind-the-intro.md)). Their places in the stages are not decided yet.

Found easter eggs are recorded in the encyclopedia. When an easter egg shows itself, the game takes a close-up photo of it at that moment for the encyclopedia, not when the player clicks. The `` ` `` key opens it (the source says ESC; the user changed it).

## 1. Placement (the director's plan)

| Stage | Easter eggs |
|---|---|
| Intro | Snow on the bird (always there, but seen only in the intro, where it snows) |
| Stage 1 | Ladybug in the stump, snow whirl, flower in the snow |
| Stage 2 | Icicle xylophone, squirrel in the tree hollow, cornelian cherries |
| Stage 3 | Cliff echo, the baby reflected in a puddle |
| Every stage | Migrating birds (once per game, between stages 1 and 3) |

## 2. What happens and what sets it off

| # | Name | Trigger and effect |
|---|---|---|
| 1 | Snow on the bird | When the bird stays still in one place without flying (standing on the ground or sitting on a branch), snow piles up on it |
| 2 | Ladybug in the stump | Click the stump left after the old tree has fallen: the bird pecks it and a ladybug crawls out |
| 3 | Snow whirl | Click a snow pile: the bird circles round and round over it and makes a whirl |
| 4 | Flower in the snow | Click the glowing snow: the bird pecks a hole and shows a flower |
| 5 | Icicle xylophone | Click an icicle on the tree: it falls and rings on the rock below. Eight icicles, do re mi fa sol la ti do |
| 6 | Squirrel in the tree hollow | The bird cries near the hollow: a squirrel peeks out, then goes back in |
| 7 | Cornelian cherries | Click the berries on the tree (bush): the bird pecks one, eats it and puffs out its chest |
| 8 | Cliff echo | On the last map, the bird cries toward the cliff and an echo comes back |
| 9 | The baby reflected in a puddle | In stage 3, crossing a puddle, the baby is reflected as something else. Not built for now |
| 10 | Migrating birds | Zoomed all the way out, migrating birds fly past, with shadows on the ground |

## 3. Cautions for stage 3

- **Puddle**: the hidden obstacles under the fog include puddles too (GDD 9.2). Make the reflection puddle look different from obstacle puddles, or put it off the baby's path or in 3-3.
- **What answers a cry**: in the fog maze, a cry makes the animals on the way answer. The squirrel (stage 2) and the echo (3-3) are outside the maze, so they don't overlap.

## 4. What is built (2026-11-06)

Prefabs are in `Assets/03_Prefabs/EasterEggs/`, code in `Assets/02_Scripts/EasterEggs/`, and the builder is `AgentScripts/BuildEasterEggs.cs` (`BuildEasterEggs.Run`). Change models and places in the prefabs and the scene. Each prefab's `Discovery` holds its encyclopedia entry and photo subject.

| Prefab | Model | Behaviour | Recorded in the encyclopedia |
|---|---|---|---|
| `SnowOnBird` (under `Bird`) | White sphere (placeholder) | In season 0, after the bird stands still on the ground or a branch for 1.5 s, it piles up over 8 s; moving shakes it off | When fully piled |
| `StumpLadybug` | Cylinder stump (placeholder), ladybug `Ladybug.prefab` | Click: the bird lands, pecks twice, and the ladybug crawls out. Once only | When it has crawled out |
| `SnowWhirl` | `Snowbank` pile | Click: the bird lands, then circles over it for 4 s. Snow particle whirl, sound `event:/SFX/spiral`. Can be played again afterwards | Halfway through the circling |
| `SnowFlower` | Small `Snowbank` pile + `InteractiveSparkle`, flower `Flower01` | Click: the bird lands, pecks three times, the snow shrinks and the flower grows. Once only | When the flower is grown |
| `IcicleXylophone` | Tommy's `icy tree.fbx` (8 icicles), rock (placeholder) | Click an icicle: it falls, rings one note on the rock, and grows back after 3 s. Longest first, do re mi fa sol la ti do. The sound is `event:/SFX/highpitch_0` at another pitch | The first note |
| `HollowSquirrel` | Tommy's `thickTree.obj` (the hollow), capsule squirrel (placeholder) | A cry within 6 m of the hollow: the squirrel comes out and goes back in after 1.5 s | When it is out |
| `CornelianCherry` | Tommy's `cornelian_cherry` (bush, 6 berries) | Click: the bird lands, pecks twice, a berry goes, and it puffs out its chest. Again while berries are left | After the chest puff |
| `CliffEcho` | Low rock (cliff placeholder) | A cry within 8 m: the bird's cry (`event:/SFX/Bird`) comes back softer after 0.7 s and 1.4 s | The first echo |
| `MigratingBirds` | 5 bird models (0.4 scale) | Holding the farthest zoom for 2 s, they fly across 7 m above the middle of the view. Once per game, from `From Stage` on (default 1; 0 on the one in the clearing, for testing) | As they pass the middle of the view |

Placeholders and stand-ins (no new art or sound on the `sam` branch):

- There are no peck or chest-puff clips. `BirdController.Peck` and `PuffChest` tip and swell the body in code instead.
- Unity audio is off and sound is FMOD. The xylophone notes, the echo and the whirl use existing FMOD events. When their own sounds come, only the event on the prefab changes.
- Tommy's models are not baked in watercolor. They keep Tommy's materials.

The clearing: `Stages/Intro/EasterEggs` in `Main`, x -14 to 14, z -36.5 to -24.5. The intro's bird area now reaches z -35. The world forest has a Clear zone over the clearing and its bottom padding is down to 1.5 m (one row stays behind the clearing). `01_SYSTEM/TreeGenerator_ClearingBack` fills the ground toward the terrain's edge that the camera now sees.
