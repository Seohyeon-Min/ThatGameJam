# Cleanup after merging main: bird silhouette restored, tree 3 colour restored, ADR numbers sorted

## Why

By the user's instruction `origin/main` was merged into `sam` (`524ce13`). There were no git conflicts, but the merge broke two things and made two decision records share a number. The user chose to fix all three.

- main's `14e2106 shaderTest` edited both URP renderers and dropped the two Render Objects features for the bird silhouette (`BirdVisible`, `BirdSilhouette`). A bird hidden by trees no longer showed
- `Tree_3.mat` came in as a green material named `Leaves`. Tree 3's trunks turned green
- main's watercolor decision record was `0003`, sharing the number with our `0003` (answers to the implementation plan questions)

## What changed

| What | Where |
|------|-------|
| Re-ran `SetupBirdSilhouette.cs` to put the two bird silhouette features back on both renderers. The watercolor features (`WatercolorKuwahara`, `WatercolorBlur`) and SSAO stay | `Assets/Settings/PC_Renderer.asset`, `Mobile_Renderer.asset` |
| `Tree_3.mat`'s name and colour back to before the merge (`Tree_3`, light beige 0.85, 0.82, 0.75), through the Editor API | `Assets/01_Arts/Materials/Test/Tree_3.mat` |
| The watercolor decision record moved to `0017`, with its title number fixed. Nothing pointed to it. Every "ADR 0003" in other documents means the implementation plan one | `docs/kor/decisions/0017-수채화는-...md`, `docs/eng/decisions/0017-watercolor-...md` |

## Verification

| What | Result |
|------|--------|
| The renderers' feature lists | PC: `BirdSilhouette`, `WatercolorKuwahara`, `WatercolorBlur`, `BirdVisible`, SSAO. Mobile: `BirdVisible`, `BirdSilhouette`, `WatercolorBlur`, `WatercolorKuwahara` |
| Play Mode screen | The bird behind trees showed as a sky-blue silhouette. Tree 3 was beige again |
| `Tree_3.mat` against before the merge | Only one internal sub-object ID differs |
| Console | 0 errors |

## Cross-model review

No code changed (settings assets and documents only). Not run.

## Left

- `Assets/_Recovery/0.unity` and `Assets/New Terrain.asset` from main look temporary, but they are someone else's work and were left
- If Seohyeon's renderer changes go into main again, the bird silhouette may be dropped again. Ask to keep these two features when editing the renderers
