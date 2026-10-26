# Work in a personal test scene and leave Main.unity alone

Date: 2026-10-26 14:36

## Topic
Unity scene files are hard to merge with git. When several people edit the same scene, they conflict.

## AI proposal
- Do not touch `Main.unity`.
- Work in `Assets/00_Scenes/Sam_Test.unity`.
- Put scripts in `02_Scripts` and prefabs in `03_Prefabs`.

## User choice
As proposed.

## Trade-off
- Gain: no scene conflicts with teammates.
- Cost: moving the work into `Main.unity` later is a separate task. Making objects into prefabs makes that easier.
