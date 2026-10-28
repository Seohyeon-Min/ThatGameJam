# Game Design Document — Map & Level Design

> Source: `Game Design.pdf` at the project root (in Korean). This file is an English translation of that PDF, made from `docs/kor/game_design.md`. The content is unchanged. The tables follow the tables in the PDF.
> Korean version: [`../kor/game_design.md`](../kor/game_design.md)
> Code implementation plan: [`implementation_plan.md`](implementation_plan.md)

## 1. Overview

Spring comes in the end. The player becomes a bird who brings that spring a little sooner and stays with the Baby, who is spring, so the Baby does not walk through the winter alone.

### 1.1. Game Concept

The player controls the Bird.

The Baby stands for spring. The Baby walks on its own along paths it can move on, and stops when the path is blocked.

Blocked paths open on their own over time. The wind knocks down the log, the rabbit family moves away, and the fog and clouds clear in the end. The Bird can step into the environment directly and make this happen sooner.

The core of the game is the relationship between the Baby, who walks on its own, and the Bird, who opens the way sooner and stays by the Baby's side.

### 1.2. Core Message

> Spring will come anyway. The Bird only brings it a little sooner and makes sure the Baby does not walk that path alone.

### 1.3. Design Goals

- Avoid explicit control guides and tutorials as much as possible.
- In each section, show a new rule first, then let the player use it on their own.
- The game goes all the way to the end even if the player does nothing. The reward for helping is not only saved time but also the Baby's reaction.
- While waiting, the game must not look frozen. Keep showing signals that a change is coming.
- Each time the Baby falls asleep in the sunlight at the end of a main section, winter retreats a little. At the end, spring arrives.

### 1.4. Target Playtime

| Play style | Time for the play sections |
|---|---|
| Actively helps the Baby | About 5 minutes |
| Does nothing | About 8 minutes |
| Total (including cutscenes and transitions, maximum) | About 10 minutes |

## 2. Core Systems

### 2.1. Player Character — Bird

- A transcendent being that has appeared in the form of a migratory bird that brings spring. The player controls it directly.
- It flies through the air and always keeps its wings spread while flying.
- Flight altitude is fixed for each map. Only in the last section (3-3) does it rise automatically when moving in the direction of progress.
- It can fly over terrain and obstacles. The Baby cannot.
- It can affect interactable objects (logs, snow-laden branches).
- When it passes through fog, the fog along its path clears.
- It can cry. Living things nearby react to the cry.

### 2.2. Baby

- It stands for spring. The player cannot control it directly.
- After waking up, it moves forward automatically along paths it can move on.
- The Baby's movement is not affected by the Bird's position. However, in the fog sections it follows the trail the Bird has cleared (see chapter 9).
- It stops when it meets an obstacle or environment it cannot pass.
- It moves again when the obstacle is removed by the Bird or is resolved naturally over time.
- It falls asleep when it reaches a Sunlit Rest Area.
- A sleeping Baby wakes up at the Bird's cry. Even without a cry, it wakes up on its own after a set time.
- It reacts differently when the Bird helped and when the problem resolved on its own (see chapter 4).
- In the last section, it sits and rests on a flat stone (resting stone) in front of the cliff.

### 2.3. Controls

The game uses only left click and right click. Each button has a fixed function.

| Input | Function |
|---|---|
| Left click | Move the Bird and interact with objects |
| Right click | The Bird's cry |

- Movement: Left-click a spot on the terrain and the Bird moves there. The view is modeled on League of Legends, but movement uses left click.
- Interaction: Left-click an interactable object and the Bird perches on it or affects it.
- Clicking beyond the cliff: An invisible click plane is placed so the Bird can move even when clicking empty space with no terrain.
- Cry: Right-click and the Bird cries. It wakes the sleeping Baby and makes the animals in the fog answer.

### 2.4. Autonomous Movement

The Baby stops moving in these situations.

- A leaning log is standing, so the Baby cannot cross the water
- A sleeping rabbit family is on the narrow snowy path (the Baby stops on its own so it does not wake them)
- The path is hidden by fog
- A hidden obstacle in the fog blocks the way
- The Baby reaches terrain it cannot pass, such as a cliff

When the obstacle is resolved, the Baby starts moving again automatically without any command.

### 2.5. Camera

- Uses a quarter view similar to League of Legends.
- Intro: Starts zoomed out and follows the Bird. When the Bird gets close enough to the Baby, the camera switches to a set position.
- Stages: Each section uses a preset fixed camera. When the next section starts, the camera switches to that position.
- The camera work for the last cloud-rising section is not decided yet.

## 3. Common System — Natural Progression & Waiting Signals

Every obstacle resolves naturally over time. While waiting, the signals that the resolution is coming grow step by step.

### 3.1. Rules

- The game goes to the ending even if the player does nothing. There is no failure or penalty.
- The Bird's help brings the natural resolution sooner.
- While waiting, the screen must not look frozen. The signals grow stronger in 3 steps over time.
- The signals also work as hints that show where to interact. The object that gives the signal is the interaction target.
- Every signal has both a visual effect and a sound.

### 3.2. Natural Resolution and Signals by Spot

| Spot | Natural resolution | Bird's help | Waiting signal (weak → strong) | Time to natural resolution (draft) |
|---|---|---|---|---|
| Waking the Baby (intro + 3 stage starts) | The Baby wakes up on its own | Right-click cry | Tossing → rubbing eyes → stretching | About 10 seconds |
| 1-2. Leaning log | Falls in the wind | Perch on the top | Creaking → bigger swaying → the base splits | About 30 seconds |
| 2-2. Sleeping rabbit family | The family wakes up and moves out of the way | Perch on a snow-laden branch to open the side path | Snow dust slides off the branch → a baby rabbit stirs → the mother yawns and stretches | About 30 seconds |
| 3-1, 3-2. Fog | The fog slowly pulls back along the paths the Baby can move on | Fly through the fog to clear the path | The edge of the fog thins → sounds of hidden animals → the outline of the path shows | About 30 seconds per section |
| 3-3. Clouds | The clouds slowly part | Rise and break through the clouds | Faint light between the clouds → wind → beams of light | About 40 seconds |

In the draft, doing nothing adds about 3 minutes 20 seconds. This will be tuned to the target difference (about 3 minutes).

## 4. Common System — Baby Reactions

When the Bird helps, the Baby looks up at the Bird. When things resolve on their own, the Baby just walks past without looking at the Bird. This difference is the core reward for helping.

### 4.1. Reaction Rules

- When helped: The Baby stops, looks up at the Bird, then walks again.
- When resolved on its own: The Baby walks past as usual without looking at the Bird.
- The reaction language is the same across the whole game. "Looks up = knows the Bird is by its side."

### 4.2. Reactions by Situation

| Situation | When the Bird helped | When resolved on its own |
|---|---|---|
| Snow section | When the Bird blocks the snow with its wings, the Baby looks up | Walks as usual |
| Waking the Baby | Wakes at the cry, looks for the Bird and looks up, then starts walking | Wakes on its own, looks around, then starts walking |
| 1-2. Log | When the log falls, looks up at the Bird, then crosses | Just crosses the fallen log |
| 2-2. Sleeping rabbit family | Peeks at the sleeping family, looks up at the Bird, then goes around by the side path | Watches the woken family pass by, then walks |
| 3-1, 3-2. Fog | Follows the trail the Bird cleared | Walks through the fog as it pulls back |
| 3-3. Ending | Happy ending (chapter 12) | Normal ending (chapter 12) |

The fog sections are the only sections in the game where the Baby follows the Bird's trail. This is staged as a contrast to the very next section, 3-3, where the Bird leaves the Baby behind and goes off alone.

## 5. Intro

### 5.1. Environment

- Snow is falling.
- The Baby is placed asleep.
- The camera starts zoomed out.
- The Bird is high in the sky near the camera, looking at the Baby through the clouds.

### 5.2. Play Flow

**Phase 1 — Discovering the controls**

1. The player can move the Bird with left click.
2. Right-clicking makes the Bird cry.
3. No control guide is given.

**Phase 2 — Approaching the Baby**

1. The player moves the Bird toward the sleeping Baby.
2. The camera follows the Bird. When the Bird comes within a set distance of the Baby, the camera switches to a set position.

**Phase 3 — Waking the Baby**

1. When the player cries with right click, the Baby wakes up and looks up at the Bird.
2. Even without a cry, the Baby wakes up on its own after the signals tossing → rubbing eyes → stretching.
3. When the Baby wakes up, a cutscene plays. The cutscene content is not decided yet.

**Phase 4 — Entering the tutorial**

1. When the cutscene ends, the Baby starts walking automatically.
2. When the next section starts, the screen switches and the snow section begins.

## 6. Tutorial — Snow Sequence

This section shows that the Baby walks on its own and that the Bird is a being who looks after the Baby. There is no puzzle.

### 6.1. Environment

- The Baby walks automatically along a path where snow is falling.
- The Bird can move freely.
- At the end of the section there is a Sunlit Rest Area where sunlight comes down through the clouds.

### 6.2. Core Mechanic — Autonomous Movement and the Wing Umbrella

- The Baby walks forward on its own no matter where the Bird is.
- When the Bird is above the Baby, its spread wings block the snow like an umbrella. The Bird's shadow covering the Baby shows the protected state.
- When the Bird blocks the snow with its wings, the Baby looks up at the Bird. If not, the Baby walks as usual.
- Blocking the snow does not affect movement, progress, or clearing.

### 6.3. Play Flow

1. When the intro cutscene ends, the Baby starts walking automatically.
2. The player can choose to keep the Bird above the Baby to block the snow.
3. The Baby reaches the Sunlit Rest Area and falls asleep.
4. The falling snow stops.
5. The game switches to the first stage.

### 6.4. Player Learning Goals

1. The Baby does not follow the Bird. It walks on its own.
2. The Bird protects the Baby, and the Baby notices it (looks up).
3. The sunlit space is the Baby's resting place and goal.

## 7. Stage 1 — Log Crossing

In this stage the player learns the rule that the Baby stops at obstacles and that the Bird can solve them sooner. A small bird knocking down a big tree is convincing not because of strength but because of balance. The log is already about to fall, and the Bird only adds the last bit of weight.

### 7.1. Stage Start (1-0)

1. The tutorial ends and the game switches to the first stage.
2. The Baby is asleep.
3. Cry with right click and the Baby wakes up. Even without a cry, it wakes up on its own after a set time.
4. The Baby starts walking automatically.

### 7.2. Section 1-1 — Crossing the Fallen Log

- A fallen log lies over the water like a bridge.
- The base of the fallen log remains as a broken stump. This shows that a log that once stood has fallen.
- The Baby crosses the water on its own using the fallen log.

Design purpose: Let the player observe that a fallen log becomes a bridge, and that a standing log can fall.

### 7.3. Section 1-2 — Leaning Log

**Environment**

- At the water's edge stands a log that is rotten inside and leans toward the water.
- Its base is cracked, and snow is piled on one side of its top.
- When the wind blows, it creaks and sways.

**Core Mechanic — The Last Feather**

- The Baby stops at the water's edge.
- When the Bird perches on the top of the log (left click), its weight makes the log fall and become a bridge.
- If the Bird does nothing, the wind grows stronger and finally knocks the log down.
- The log always falls toward the side it leans to (toward the water).

**Play Flow**

1. The Baby moves automatically to the water's edge with the leaning log and stops.
2. The log gives signals in this order: creaking → bigger swaying → the base splits.
3. The Bird perches on the top, or the wind knocks the log down.
4. The Baby crosses the fallen log. If the Bird helped, the Baby looks up at the Bird before crossing.

Design purpose: Based on what the player saw in 1-1, let them discover on their own that the Bird can make that result happen sooner.

### 7.4. Section 1-3 — Sunlit Rest Area

1. The Baby reaches the Sunlit Rest Area and falls asleep.
2. A mother rabbit and her baby rabbits pass by the sleeping Baby in a line.
3. The game switches to the next stage.

Design purpose: Through rabbits, which give birth in spring, show the seasonal change of new life being born, and foreshadow the rabbit family in the next stage.

## 8. Stage 2 — Rabbit Family

In this stage the Baby passes the sleeping rabbit family without waking them. The Bird does not touch the rabbit family. It opens a way around for the Baby.

### 8.1. Stage Start (2-0)

1. The log stage ends and the game switches to the rabbit stage.
2. Wake the sleeping Baby with right click. Even without a cry, it wakes up on its own after a set time.
3. The Baby starts walking automatically.

### 8.2. Section 2-1 — Snow-Laden Branch

- By the path there is a tree branch bent down by the weight of snow.
- As the Baby passes, a small bird lands on the branch and flies off. The piled snow falls, and the branch springs up.
- The Baby keeps moving forward.

Design purpose: Show the rule that when the snow on a branch falls off, the branch rises. The appearance of another small bird foreshadows the flock of migratory birds in the happy ending.

### 8.3. Section 2-2 — Sleeping Rabbit Family

**Environment**

- A mother rabbit and her baby rabbits sleep on a narrow snowy path with high snowbanks on both sides.
- There is a side path that goes around, but it is blocked by branches bent down by the weight of snow.

**Core Mechanic — Taking Off Weight**

- The Baby crouches in front of the sleeping family and waits so it does not wake them. It is not physically blocked. The Baby stops on its own.
- When the Bird perches on a bent branch, the piled snow falls. The lighter branch springs up and the side path opens.
- The Bird does not touch the rabbit family.
- If the Bird does nothing, the signals go: snow dust slides off the branch → a baby rabbit stirs → the mother yawns and stretches. Then the rabbit family wakes up and moves out of the way.

Symmetry with Stage 1: In Stage 1 the Bird adds weight to knock down the log. In Stage 2 it takes weight off to lift the branch. The same "perching" gives the opposite result.

**Play Flow**

1. The Baby stops in front of the sleeping rabbit family and crouches.
2. The Bird perches on a branch to open the side path, or over time the rabbit family wakes up and passes by.
3. If the side path opened, the Baby peeks at the sleeping family, looks up at the Bird, then goes around by the side path.
4. If the player waited, the Baby watches the woken family pass by, then walks again.

Design purpose: Let the player use the rule seen in 2-1, and show through action that the Baby is a being who does not harm life.

### 8.4. Section 2-3 — Sunlit Rest Area

1. The Baby reaches the Sunlit Rest Area and falls asleep.
2. The snow piled around starts to melt.
3. The game switches to the next stage.

## 9. Stage 3 — Fog Stage

When the Bird draws a path in the fog, the Baby follows that trail. In the last section, the Bird leaves the Baby behind, rises into the sky alone, and breaks through the clouds.

### 9.1. Stage Start (3-0)

1. The rabbit stage ends and the game switches to the fog stage.
2. Wake the sleeping Baby with right click. Even without a cry, it wakes up on its own after a set time.
3. The Baby starts walking automatically.

### 9.2. Fog Rules

- The fog along the path the Bird passed clears.
- Breadcrumb method: The Baby follows the cleared trail in the order the Bird passed. There is no fork detection.
- Hidden obstacles: Under the fog there are hidden obstacles the Baby cannot pass, such as rocks and puddles.
- The Bird can fly over obstacles. But if the next point on the trail is an obstacle, the Baby stops in front of it. The trail beyond the obstacle becomes invalid.
- The player must come back near where the Baby stopped and continue the path in another direction. This tells the player "this way is blocked" without a failure screen.
- Loop removal: When the Bird's trail crosses itself, the loop between the two points is cut out.
- Natural resolution: If the Bird does nothing, the fog slowly pulls back along the paths the Baby can move on.

### 9.3. Cry — Answers from Animals in the Fog

- Small animals hide in the fog, only on walkable ground.
- When the Bird cries, the animals within reaction range answer, and a silhouette or small light shows at their position for a moment.
- Where an animal answers, there is a path. No animals are placed on dead ends.
- The reaction range is limited to the area around the Bird. This creates a rhythm: move a little → cry → move again.
- The animals stay in place and step aside when the Baby comes near.

### 9.4. Section 3-1 — Path Half Covered by Fog

- Half of the path is covered by fog. The Baby stops at the edge of the fog.
- Right beyond the edge of the fog, place one animal and one hidden obstacle.
- When the player cries, the animal answers, and clearing the fog in that direction reveals a path. In the other direction there is an obstacle, so the player sees the Baby stop once.

Design purpose: Show the three rules — clearing fog, hidden obstacles, and animal answers — first while the view is still clear.

### 9.5. Section 3-2 — Path Fully Covered by Fog

- The whole path is covered by fog.
- The player listens to the animals' answers to the cry and draws a path to lead the Baby.
- When the Baby reaches the end of the section, the game moves to the next section.

Design purpose: Reuse the rules learned in 3-1 with a limited view. This is the only section in the game where the Baby follows the Bird.

### 9.6. Section 3-3 — Cliff & Resting Stone

**Environment**

- The path ends at a cliff.
- In front of the cliff there is a flat stone (resting stone) where the Baby can sit and rest.
- The sky is covered with clouds, so no sunlight reaches the resting stone.

**Core Mechanic — Breaking Through the Clouds**

- The Baby sits and rests on the resting stone.
- When the Bird keeps moving beyond the cliff, its altitude rises automatically. There is no separate interaction input.
- When the Bird breaks through the clouds, sunlight pours through the gap and lights the resting stone → happy ending.
- If the Bird does nothing, the signals go: faint light between the clouds → wind → beams of light. Then the clouds slowly part → normal ending.
- Something must draw the Bird away from the Baby (such as a faint light beyond the clouds). The exact method is not decided yet.

**Design Purpose**

- Until now the Baby went to find the light. At the end, the Bird can make the light itself.
- It is solved with the existing movement control, without any new control.
- For the first time, the Bird leaves behind the Baby who followed it and goes off alone. This scene is the emotional peak.

## 10. Common System — Sunlit Rest Area

At the end of each stage there is a space where sunlight comes down through a gap in the clouds. Each time the Baby falls asleep there, winter retreats one step.

### 10.1. Common Features

- Formed by sunlight coming down between the clouds in the sky.
- It is the Baby's resting place and the stage's completion point.
- When the Baby arrives, it falls asleep. The stage's seasonal effect plays, then the game switches to the next stage.
- In the last stage, the ending starts when the Baby falls asleep.

### 10.2. Effects by Stage

| Section | Baby's action | Seasonal change effect |
|---|---|---|
| End of tutorial | Falls asleep in the sunlight | The falling snow stops |
| 1-3. Log stage | Falls asleep in the sunlight | A mother rabbit and her baby rabbits pass by |
| 2-3. Rabbit stage | Falls asleep in the sunlight | The snow piled around melts |
| 3-3. Last stage | Falls asleep on the resting stone, in sunlight the Bird made or sunlight through clouds that parted on their own | Spring spreads out from the Baby and winter ends (chapter 12) |

### 10.3. What Is Different in the Last Stage

The first three Sunlit Rest Areas are places where sunlight already comes down. In the last stage, clouds block the sunlight, so the Sunlit Rest Area appears only when the Bird breaks through the clouds or the clouds part on their own.

## 11. Common System — Stage Transition & Awakening

Each stage starts with the Baby asleep. The player wakes it with the Bird's cry or waits until the Baby wakes up on its own.

### 11.1. Common Flow

1. The Baby reaches the Sunlit Rest Area and falls asleep.
2. The stage's seasonal effect plays.
3. The screen switches to the next stage, and the Baby starts asleep.
4. When the player cries with right click, the Baby wakes up and looks up at the Bird.
5. If the player does not cry, the Baby wakes up on its own after about 10 seconds, after the signals tossing → rubbing eyes → stretching.
6. The Baby starts walking automatically.

### 11.2. Applied by Stage

| Previous section | Next section | Starting action |
|---|---|---|
| Intro | Tutorial | Continues after waking the Baby in the intro |
| End of tutorial | Log stage | Wake with a cry, or the Baby wakes on its own |
| End of log stage | Rabbit stage | Wake with a cry, or the Baby wakes on its own |
| End of rabbit stage | Fog stage | Wake with a cry, or the Baby wakes on its own |
| End of fog stage | Ending | Ending starts when the Baby falls asleep. No extra input |

### 11.3. Design Purpose

- By waking the Baby with right click again and again, the player learns the role of the cry without any explanation.
- Progress is never blocked, even if the player does not know about the cry.
- Each stage has the rhythm: wake up → automatic movement → solve the obstacle → fall asleep in the sunlight.

## 12. Endings

There are two endings that differ only in presentation. Either way, spring comes. There is no bad ending.

### 12.1. Branch Conditions

- Happy ending: In 3-3, the Bird breaks through the clouds itself.
- Normal ending: In 3-3, the player waits until the clouds part on their own.

### 12.2. Presentation Comparison

| Item | Normal ending | Happy ending |
|---|---|---|
| Sunlight | Comes down quietly as the clouds slowly part | Pours down all at once through the hole the Bird made |
| Baby | Falls asleep in the sunlight | Looks up at the Bird in the sky, then falls asleep |
| Spread of spring | Spreads quietly out from the Baby | Spreads out from the Baby, and a flock of migratory birds flies in |
| Last cry | No answer | Other migratory birds answer |

### 12.3. Design Purpose

- A player who waited also welcomes spring. This keeps the message "Spring will come anyway."
- A spring the Bird took part in should look a little warmer.
- Through the whole game, only the Baby and small animals reacted to the cry. In the happy ending, birds of the same kind answer for the first time. This completes, without dialogue, the setting that the Bird is a migratory bird that brings spring.

## 13. Implicit Tutorial Design

Every rule is shown first without explanatory text, and the player uses it directly in the next section. Waiting signals also act as hints.

| Section | Rule the player learns |
|---|---|
| Intro | Left click moves the Bird, and right click makes it cry |
| Intro | Crying wakes the sleeping Baby, who looks up at the Bird |
| Tutorial | The Baby walks forward on its own |
| Tutorial | When the Bird blocks the snow with its wings, the Baby notices |
| End of tutorial | The Baby falls asleep in the Sunlit Rest Area, and the snow stops |
| 1-1. Fallen log | A fallen log becomes a bridge, and it was once a standing tree |
| 1-2. Leaning log | When the Bird perches, the log falls. If you wait, the wind knocks it down |
| 1-3. Sunlit Rest Area | When the Baby falls asleep, new life (the rabbit family) appears |
| 2-1. Snow-laden branch | When the snow on a branch falls off, the branch springs up |
| 2-2. Sleeping rabbit family | When the Bird perches on a branch and shakes off the snow, the side path opens. The Baby does not wake sleeping life |
| 2-3. Sunlit Rest Area | When the Baby falls asleep, the snow around melts |
| 3-1. Partial fog | Fog clears where the Bird passes, obstacles hide in the fog, and crying makes animals on the path answer |
| 3-2. Full fog | You can draw a path in the fog by following the animals' answers |
| 3-3. Cliff and resting stone | The Bird can break through the clouds to let sunlight in |

## 14. Level Flow

| Order | Section | Bird's help | Natural resolution | Presentation |
|---|---|---|---|---|
| 0 | Intro | Wake the Baby with a cry | The Baby wakes up on its own | Cutscene |
| T-1 | Snow section | Block the snow with the wings (optional) | The Baby walks on its own | The Baby looks up |
| T-2 | Sunlit Rest Area | — | — | The snow stops |
| 1-0 | Stage start | Wake with a cry | Wakes up on its own | — |
| 1-1 | Fallen log | — | The Baby crosses on its own | The stump shows the cause |
| 1-2 | Leaning log | Perch on the top to knock it down | Falls in the wind | Creaking and swaying signals |
| 1-3 | Sunlit Rest Area | — | — | A mother rabbit and her babies appear |
| 2-0 | Stage start | Wake with a cry | Wakes up on its own | — |
| 2-1 | Snow-laden branch | — | — | Demo of a small bird shaking snow off a branch |
| 2-2 | Sleeping rabbit family | Perch on a branch to open the side path | The family wakes up and moves out of the way | The Baby peeks at the sleeping family |
| 2-3 | Sunlit Rest Area | — | — | The snow melts |
| 3-0 | Stage start | Wake with a cry | Wakes up on its own | — |
| 3-1 | Partial fog | Clear fog, find the path with cries | The fog slowly pulls back | Demo of the 3 rules |
| 3-2 | Full fog | Draw a path to lead the Baby | The fog slowly pulls back | The Baby follows the Bird |
| 3-3 | Cliff and resting stone | Rise and break through the clouds | The clouds part on their own | Happy / normal ending |

## 15. Design Principles

### 15.1. Autonomous Movement and Indirect Intervention

The Baby walks on its own along paths it can move on. The player does not control the Baby directly. The player uses the Bird to step into the environment.

### 15.2. Spring Comes in the End

Every obstacle resolves naturally over time. The Bird's help only brings spring sooner. Waiting is also a valid way to play.

### 15.3. The Reward for Helping Is the Relationship

When the Bird helps, the Baby looks up at the Bird. The value of helping is shown through the Baby's reaction and the ending presentation, more than through saved time.

### 15.4. Never Look Frozen

While waiting, the signals that a change is coming grow step by step. The signals are the hints.

### 15.5. Observation Over Explanation

New rules are shown first. After that, the player uses them directly.

### 15.6. Minimal Controls

Only left click (move and interact) and right click (cry) are used. No controls are added for new situations.

### 15.7. The Cry Wakes Living Things

Every use of the cry follows one rule. The Baby wakes up, the animals in the fog answer, and in the happy ending the migratory birds answer.

### 15.8. Repeated Rest and Restart

At the end of a stage, the Baby falls asleep in the sunlight. In the next stage, it wakes up again and walks.

### 15.9. Gradual Seasonal Change

1. End of tutorial: The falling snow stops.
2. End of log stage: A mother rabbit and her baby rabbits pass by.
3. End of rabbit stage: The snow around melts.
4. Last stage: Spring spreads out from the Baby and winter ends.

### 15.10. Nonverbal Narrative

The setting — the Baby is spring, and the Bird is a transcendent being that has appeared as a migratory bird — is told through the environment and actions, without dialogue or explanatory text.

## 16. Open Items (TBD)

| Category | Open item |
|---|---|
| Playtime | Final waiting time for natural resolution at each spot (draft total about 3 minutes 20 seconds, target about 3 minutes) |
| Cry | Distance and conditions for waking the Baby |
| Camera | Position for each section, switch conditions, rising camera work in 3-3 |
| Intro | Content of the cutscene where the Baby wakes up |
| Baby | Movement speed, stop check in front of obstacles, timing for moving again |
| Baby reactions | Whether to make reactions stronger as the number of helps grows |
| Snow section | Terrain, path, wing umbrella presentation |
| Log | Whether to add a background demo in 1-1 (a scene where a bird perches and another log falls) |
| Log | Whether to add a variation where the falling direction changes with branch position |
| Rabbit | Position and timing of the 2-1 demo (a small bird shaking snow off a branch) |
| Rabbit | Layout of the narrow snowy path and side path, number of blocking branches, size of the rabbit family, and its waking presentation |
| Fog | Width of clearing, range for continuing the trail after the Baby stops |
| Fog | Kinds of answering animals, reaction range, answer presentation |
| Fog | Path and hidden obstacle layout of the 3-1 and 3-2 maps |
| 3-3 | What draws the Bird away from the Baby |
| 3-3 | Layout of the cliff and resting stone, visual for breaking through the clouds |
| Ending | Presentation of spring spreading, flock of migratory birds, final screen |
| Sunlit Rest Area | Visual effects for the gap in the clouds and the sunlight |
| Stage transition | Transition presentation, the Baby's start position in the next stage |
| Seasonal change | Detailed presentation of the snow stopping, the rabbit family, and the snow melting |
