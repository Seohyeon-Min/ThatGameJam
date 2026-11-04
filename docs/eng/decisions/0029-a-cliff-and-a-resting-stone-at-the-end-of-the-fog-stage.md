# 0029. A cliff and a resting stone at the end of the fog stage (first version)

- Status: accepted. A rough first version; the director will change it later
- Date: 2026-11-04
- Related: the director's order and the user's answers (`order_from_director.md`, repository root, not committed), design doc §9.6, implementation plan 4.13

## Context

The director asked for the ending place. Past the fog maze there is no sunlit spot; the girl goes on to a big stone before a cliff. When the bird flies out over the cliff near the edge of the screen, the ending starts. Before the work we asked nine questions, and the user answered them (A1 to A9).

## Decision

These are the user's and the director's decisions.

- The ending place is part of stage 3. The season does not advance (A1).
- Past the maze, a right-click cry no longer brings the girl to the bird. No animal answers either. The cry itself stays as it is (A9).
- The girl walks to the stone and sits. There is no sit clip, so she stands by the stone facing the cliff and only breathes (A3).
- No new assets. The big stone is a sphere primitive stretched flat, with the existing watercolor rock material (`WC_Rock.mat`) (A4).
- The cliff is where the ground ends, in the direction the road runs (+Z, up and right on the screen), about 25 m past the maze exit (A5).
- When the bird comes near, the camera holds still where it shows the cliff and the stone. The bird can fly out past the cliff. When the bird, past the cliff, reaches 10% from an edge of the screen, the ending starts (A6).
- The ending starts only after the girl has reached the stone. If the bird is already at the edge, it starts as soon as she arrives (A7).
- Design doc §9.6's automatic climb, breaking through the clouds, and the happy and normal endings are not part of this (A8).
- Instead of an ending animation, the clip of the girl first getting up in the intro (`Girl_Intro_GetUp`) plays (A2).

Decided while building it:

- The cliff is the end of the terrain. The ground is not cut down. The terrain's height range is 3.9 m, too little to cut deep, and the watercolor ground shader takes no light, so lower ground looks the same white as the ground above and no cliff shows. Where the terrain ends the sky colour shows beyond, and that reads as a cliff. The ground already reached 24 m past the maze goal, so it was not stretched.
- So clicks out over the drop work, the large click plane under the ground (`02_WORLD/ClickPlane`) now reaches as far as the bird may fly.
- The ending is a blocker on the road (`PathBlocker`), `CliffEnding`. The `StopPoint` at the maze goal waits for it, and it leads the girl to the stone. It never opens, so the girl never lies down, the stage never ends, and the season stays. While she waits at a blocker she does not react to cries, so nothing else has to stop that.
- The camera hold uses the existing camera zone (`CameraZone`). The area that unlocks it (the lock area) has to fit on screen toward the maze but reach past the screen out over the cliff, so a value to move the lock area's centre (`lockOffset`) was added.
- Forest: the world forest is cleared across the whole width from the end of the maze to the end of the ground (Clear zone `Stage3 Ending`). There the builder plants trees on the world forest's 3 m grid, all of them where the maze's forest ends, thinning out to 4% at the cliff. The cliff closes the road off now, so the band that closed the world forest's top end (`topPadding`) is 0.
- The fog thins out to none over the last 8 m of the maze (`FogField.fadeOutLength`).

## Alternatives

**Alternative 1. Cut the ground past the cliff down deep.**
Dropped. For the reasons above no cliff shows, and the terrain's height range and the forest and grass on the floor would all need work.

**Alternative 2. Start the ending when the bird is some metres past the cliff, instead of at the screen's edge.**
Dropped. The director said "the edge of the screen" (A6).

**Alternative 3. Have `CliffEnding` hold the camera itself.**
Dropped. The camera zone already does this. Adding one value is smaller.

## Consequences

**Gains.**

- The flow maze → road through a thinning forest → stone → the bird leaves → ending runs from start to end.
- Only the existing device structure (`StopPoint`, `PathBlocker`, `CameraZone`) is used.

**Costs.**

- The cliff's edge is the terrain's straight end. There is no cliff face.
- The stone is a flattened sphere. The girl stands instead of sitting.
- Nothing follows once the ending starts. After the get-up clip she stands and rests.
- Once the camera is held, the bird has to go a little further down the screen to release it on the way back to the maze.
