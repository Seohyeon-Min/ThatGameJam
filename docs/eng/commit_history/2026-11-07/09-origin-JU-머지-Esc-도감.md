# Merge origin/JU: encyclopedia key becomes Esc

## Why

Bring JU's new commit `9b30835 esc` from `origin/JU` into `sam`.

## What changed

| What | Where |
|------|-------|
| The encyclopedia open/close key changed from `` ` `` to Esc | `Encyclopedia.cs` |
| The debug intro/cutscene skip key changed from Esc to 0 (Esc is the encyclopedia key) | `IntroSequence.cs`, `Cutscene.cs` |
| An Esc hint image `EscHint` was added to the encyclopedia prefab | `Assets/03_Prefabs/UI/Encyclopedia.prefab` |

Git merged it with no conflicts. The scene did not change.

## Key decisions

- In a web browser, Esc also exits fullscreen and pointer lock. In fullscreen the encyclopedia may open at the same time or not open at all, so this needs a check in a web build. This merge takes JU's decision as is.

## Verification

| What | Result |
|------|--------|
| Recompile in the Editor | Succeeded, 0 compile errors |
| Play Mode, web build | Not run |

## Cross-model review

Not done. This is a conflict-free merge of another person's branch.
