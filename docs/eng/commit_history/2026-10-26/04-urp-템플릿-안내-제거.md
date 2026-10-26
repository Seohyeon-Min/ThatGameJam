# Remove the URP template guide files

## Why

`Assets/TutorialInfo/` and `Assets/Readme.asset` are URP template guides. The user judged them unnecessary and deleted them in the Editor.

## What changed

| What | Where |
|------|-------|
| Deleted the guide asset, `Readme.cs`, `ReadmeEditor.cs`, icon, layout | `Assets/TutorialInfo/`, `Assets/Readme.asset` |

## Verification

| What | Result |
|------|--------|
| Searched scenes, assets, and `.meta` files in `Assets/` and `ProjectSettings/` for the GUIDs of `Readme.asset` and `Readme.cs` | No references |
