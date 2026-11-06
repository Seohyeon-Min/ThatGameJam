# The girl sleeps at the sunlit spot only with the bird near, and leaves the road to play until then. Zzz over the sleeping girl (ADR 0035)

## Why

`order_from_director.md` items 1 and 2.

1. The girl sleeps only when she has reached the sunlit spot and the bird is near. With the bird far she plays with the snow on the ground, leaves the road to look at trees, or runs around. When the bird comes to her she goes back to the light and sleeps.
2. A Zzz particle while the girl lies fully down, using the png on Ju's branch.

## What changed

| What | Where |
|------|-------|
| `PlayUntilBirdComes` at the route's end: without the bird near she goes round the play spots in the `Playing` state. When it comes she looks up, walks to the light and lies down | `Baby.cs` |
| `BirdClose`: horizontal distance within `babyRestBirdDistance` (10 m) and her on the screen | `Baby.cs`, `GameTuning.cs` |
| Play spot component `PlaySpot` (snow play, tree look, run around) | `Baby/PlaySpot.cs` |
| `running` widened to `directState`: states with no transitions (Run, PlaySnow, PlayTree) are played directly, and walking or standing leaves them | `Baby.cs` |
| During tree look her head lifts to the top of the tree (`lookAt`) | `Baby.LateUpdate` |
| Zzz: emits only while the state is asleep, lying down or waking, and the head is below 0.18 (model units) above her feet | `Baby.UpdateSleepZzz` |
| Animator states `PlaySnow` (Hum) and `PlayTree` (Breathing). When the director's clips come, only the Motion changes | `Girl.controller` |
| Five `PlaySpots` under the sunlit spots of the intro, stage 1 and 2, and `SleepZzz` under the girl's head bone | `Main.unity` |
| Zzz prefab and material (URP Particles/Unlit, tinted light blue). The texture is `SleepZ.png` from Ju's branch, with its GUID | `03_Prefabs/SleepZzz.prefab`, `Materials/vfx/SleepZ.mat`, `Textures/Particles/` |
| Builder and Play Mode check | `AgentScripts/BuildGirlPlay.cs`, `AgentScripts/VerifyGirlPlay.cs` |
| Docs | `game_design.md` 2.2, chapter 17 (kor), `implementation_plan.md` 4.3, 4.6 (kor/eng), ADR 0035 |

"Lying fully down" was measured from the clips. Head height (model units) is 0.16 asleep and 0.90 standing. Below 0.18 is from about 5.7 s of `Girl_LieDown` (of 7.27 s), and up to about 1.3 s of `Girl_GetUp`.

The Zzz sits on the head bone. The bone carries the rig's large scale, so at first the Zs came out metres away. Scaling Mode Local ignores the parents' scale. White Zs did not show on the snow and in the light, so they are 0.9 to 1.1 m and tinted light blue (0.72, 0.82, 1).

## Verification

| What | Result |
|------|--------|
| `VerifyGirlPlay.Run` (Play Mode, 3x speed) | Zzz while asleep in the intro, off once she gets up; `Playing` with the bird 20 m away; over 90 s she goes through Walk, Run, PlaySnow, PlayTree, up to 5.2 m off the light; with the bird beside her she returns, lies down within 0.2 m of the light, Zzz on; Zzz kept into stage 1; with the bird near all the way she sleeps at once without playing. All PASS |
| Game view captures | Blue Zs rise over the sleeping girl's head and fade |
| Console errors | 0 |

## Cross-model review

GPT-6.1 Sol, correctness, security, robustness.

| Level | Finding | Handling |
|-------|---------|----------|
| blocker | Already at a run-around point, the loop spins without a yield and hangs | **Fixed.** One frame's pause at each point |
| warn | A run-around spot with no children throws | **Dismissed.** The builder always makes four points |
| warn | Scenes without `sleepZzz` (Sam_Test) throw every frame | **Fixed.** Skipped when missing |
| warn | With no tree nearby the builder stops halfway | **Dismissed.** It stops before saving, so the scene is unchanged. Place the spot by hand then |

After the fixes `VerifyGirlPlay` passed again.

## Left

- The play clips (snow play, tree look) come from the director. Then only the Motion of `PlaySnow` and `PlayTree` in `Girl.controller` changes.
- If the bird never comes, the stage never ends (ADR 0035, consequences).
- Not tried by hand with a real mouse.
