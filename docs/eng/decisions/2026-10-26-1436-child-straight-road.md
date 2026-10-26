# The child walks straight along a road and waits at obstacles (NavMesh on hold)

Date: 2026-10-26 14:36

## Topic
We had to decide how the child (AI) moves.

## AI proposal
- The AI asked to choose between following a fixed path (waypoints) and finding its own path to the goal.
- The first plan was to walk to the goal with AI Navigation (NavMesh).

## User choice
- The child's AI is not decided yet.
- For now, the child is on a straight road and blocked by obstacles.

## Trade-off
- Gain: short code is enough (`ChildWalker`). It walks straight toward the goal and probes ahead with a sphere. No NavMesh baking.
- Cost: this does not work once the road turns or branches. Then it must move to NavMesh or waypoints.
