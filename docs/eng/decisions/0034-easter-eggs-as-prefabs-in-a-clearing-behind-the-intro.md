# 0034. Easter eggs as prefabs, gathered in a clearing behind the intro

- Status: accepted
- Date: 2026-11-06
- Related: [`../easter_eggs.md`](../easter_eggs.md) section 4, [`../implementation_plan.md`](../implementation_plan.md) 6.1

## Context

The director has nearly settled the easter egg plan (`order_from_director.md`, `easter_eggs.md`). Their places in the stages and their models will change later. Some of the models are on Tommy's branch; there is no squirrel, no snow pile, no peck clip and no dedicated sound. On the `sam` branch no new art or sound is made. The project's Unity audio is off and sound is FMOD.

## Decision

The user decided.

**Each easter egg is a prefab, and all of them sit in a clearing cut out of the trees behind the intro's start (-Z) in `Main`.** The baby reflected in a puddle is not built for now.

- Only the needed meshes come from Tommy's branch (`icy_tree`, `thickTree`, `cornelian_cherry`). The branch is not merged.
- Missing models are stood in for by primitives with the existing test materials and by existing prefabs (`Snowbank`, `Flower01`, `Ladybug`, the bird model).
- Pecking and the chest puff are code that tips and swells the body. Sounds are existing FMOD events with their pitch and volume changed.
- Snow on the bird is built too (dropped on 11-04, back in the director's plan).
- Triggers follow the director's new list (snow whirl: click the snow pile; squirrel: cry near the hollow).
- The encyclopedia entries follow the new list: the frozen river and the swallowtail are out, snow on the bird and the snow whirl are in, the puddle stays.
- Only the clearing's migrating birds also fly in the intro, for testing (`From Stage` 0).

## Alternatives

**Alternative 1. Merge Tommy's branch whole.**
Dropped. Tommy's `BirdController.cs` and `Tommy_Test` changed too, which would mean conflicts and reverts.

**Alternative 2. Put each easter egg straight into its stage.**
Dropped. The places are not decided. Gathered in a clearing they can be tried in one spot, and the prefabs move once places are set.

**Alternative 3. Play the test wavs (`ClickStem`) with a Unity AudioSource.**
Dropped. Unity audio is off in the project (`m_DisableAudio: 1`).

## Consequences

**Gains.**

- Every easter egg can be tried right away in the clearing, and models and places change in the prefabs and the scene.
- Found ones go into the encyclopedia with a photo.

**Costs.**

- The intro's bird area grew to z -35. Behind the start is open, so in the intro the bird can go to the clearing. A cry there also wakes the baby sleeping nearby.
- The camera now sees toward the terrain's edge, so there is a second tree generator. The world forest's bottom padding is smaller and it was generated again.
- Placeholder models and stand-in sounds don't look or sound final. Tommy's models are not baked in watercolor.
- The clearing and the wider area must be undone once the easter eggs have their places.
