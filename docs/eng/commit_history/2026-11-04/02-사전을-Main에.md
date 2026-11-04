# Put the encyclopedia in the Main scene (no easter eggs)

## Why

The user asked for the encyclopedia in Main too. No easter eggs exist yet, so only the encyclopedia goes in. No test rabbit.

## What changed

| What | Where |
|------|------|
| The `Encyclopedia` prefab under `01_SYSTEM` | `Assets/00_Scenes/Main.unity` |
| While the encyclopedia is open, input is not handled and the perch cursor is reset | `BirdController.cs` `Update` |
| While it is open, wheel zoom is ignored | `BirdCamera.cs` |
| It does not open during the intro or a cutscene. The intro counts from `Play` until the white clears and it turns off (`IntroSequence.IsPlaying`); cutscenes use the existing `Cutscene.AnyPlaying` | `Encyclopedia.cs`, `IntroSequence.cs` |
| Encyclopedia canvas sortingOrder 100 → 110. Main's intro canvas (title, white fade) is at 100 | builder, `Encyclopedia.prefab` |
| With Main open, the builder places only the prefab under `01_SYSTEM` and saves | `AgentScripts/BuildEncyclopedia.cs` |

## Key decisions

- **It does not open during scripted sequences.** At first it was left open, since the intro and cutscenes pause with `timeScale` 0. The user asked to block it during them.
- **Changed along with the scene save.** Re-saving Main dropped `IntroSequence`'s old serialized fields (`diveChase`, `diveChaseFollow`, `diveCameraLean`) and wrote the current code's `diveCameraHeight` 15 and `diveCameraLag` 0.35. These match the code defaults, so behaviour is the same. `Snowfall.prefab` and `SunlitSpot.prefab`, which only got line-ending changes when Main opened, were reverted.

## Verification

| What | Result |
|------|------|
| `unity recompile` | No errors |
| `BuildEncyclopedia.Run` (with Main open) | `built Assets/03_Prefabs/UI/Encyclopedia.prefab and saved Assets/00_Scenes/Main.unity` |
| Play Mode, `` ` `` on a virtual keyboard at the intro title | `IsOpen` True, `timeScale` 0, blur 1. The capture shows all 11 slots as `???` over a blurred background |
| Virtual mouse click while open | Bird target unchanged; the intro did not take it as the first click |
| `` ` `` again | `IsOpen` False, `timeScale` 1, blur 0 |
| After the block: `` ` `` at the intro title | Does not open (`IntroSequence.IsPlaying` True) |
| `` ` `` during the cutscene after skipping the intro with ESC | Does not open (`Cutscene.AnyPlaying` True) |
| `` ` `` twice after the cutscene | Opens (`timeScale` 0) → closes (`timeScale` 1) |

## Cross-model review

GPT-6.1 Sol. Lenses: correctness, security, robustness. Reviewed before and after the block.

## Left to do

- The dev button row (IMGUI) draws over the encyclopedia. Pressing speed (F1) while it is open restarts time. Dev only, left as is.
- The `` ` `` key and the blur cost are not checked in a web build yet.
