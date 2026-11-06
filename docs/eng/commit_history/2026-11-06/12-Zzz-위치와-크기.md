# Zzz off while she gets up, and smaller, from past her head

## Why

User feedback on 11's Zzz. No Zzz needed while she gets up. The particles seemed to come from the middle of the girl: they should come from outside, past her head as she lies. Make them smaller.

## What changed

| What | Where |
|------|-------|
| No emission while waking (`Waking`): only asleep and at the end of lying down (`Sleeping`, `Resting`) | `Baby.UpdateSleepZzz` |
| Emitter: from the head bone, 0.5 m the way the head points in the sleeping pose (`Girl_Sleep`, away from her body) and 0.3 m up. The builder poses a copy of the model that way and turns it into the head bone's local position | `BuildGirlPlay.ZzzLocalPosition` |
| Size 0.9 to 1.1 m → 0.6 to 0.75 m | `SleepZzz.prefab` |
| Rerunning the builder also remade `Main`'s play spots in the same places (why the scene diff is large) | `Main.unity` |
| Docs | `game_design.md` 2.2 (kor) |

## Verification

| What | Result |
|------|--------|
| `VerifyGirlPlay.Run` | All PASS. The Zzz goes off as soon as she is woken (still on the `IntroSleep` clip) |
| Game view captures | Small blue Zs rise from above and past the sleeping girl's head |

## Cross-model review

GPT-6.1 Sol, correctness, security, robustness. `LGTM - no blockers`.
