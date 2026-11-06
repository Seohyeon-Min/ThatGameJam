# 0033. In the fog maze, a call from the bird has the girl run anywhere in the maze

- Status: accepted
- Date: 2026-11-05
- Related: the director's order, item 2, point 5 (`order_from_director.md`, repository root, not committed), ADR 0028, ADR 0032, implementation plan 4.12

## Context

The director: the girl rarely follows onto a wrong way. Check whether she is coded not to follow backward. She should follow when the bird calls, backward too.

Found. ADR 0028 had the girl run, on a cry, to the point nearest the bird among the way she is on and the two ways of the fork ahead (`FogMaze.Reachable`). With the bird behind her (over the dead end of a fork she passed, or a way before), she went only to the fork at the start of her way, stood 3 s, and walked on ahead. Reproduced in Play Mode: with the girl at the third fork and the bird crying over the second dead end, she stood at the fork and walked off away from the bird. Crying over the dead end ahead, she followed as she should. The bird flies 4 m up, so in the quarter view where it shows and where it is differ by 5.7 m, but that changed the dead-end choice only at a few points right by the forks.

## Decision

The user's decisions.

- Anywhere in the maze, behind her too, she goes along the ways to the point nearest the bird: back up through the forks and into the other ways.
- In the maze a call from the bird always has her run (`fogRunSpeed`, 2.5 m/s).
- A dead end she is called into also takes her to its wall and back, as now. If that fork's animal has answered or she has been to the wall, she turns back at once (ADR 0028 as it was).

Decided while building:

- The ways form a tree: a right way leaves from the end of the way before it, a dead end from the end of the way ending at its fork (`Parent`). She goes back from her way to the way she shares with the target's, then out along the ways to the target (`Travel`).
- Walking on again after going back, at a fork whose dead end's wall she has been to she takes the right way without waiting. A finding of the cross-model review: without it she went into a dead end she had already been to again.

## Alternatives

**Alternative 1. Follow back only one or two forks.**
Dropped. The user said anywhere in the maze.

**Alternative 2. Walking on again, take the same way as the first time at every fork passed.**
Dropped. It needs remembering the first choice. Knowing the forks whose wall she has been to is enough. A fork passed by its right way is 50:50 again.

## Consequences

**Gains.**

- Wherever the bird calls from, she comes.

**Costs.**

- Called far, she goes back a long way. The right route is about 333 m in all.
- At a fork she passed by its right way (never at its wall) she chooses again, so after going back she may enter a dead end.
