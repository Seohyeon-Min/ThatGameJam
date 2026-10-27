# Remove the "do not touch `Main.unity`" rule from `AGENTS.md`

## Why

ADR 0003 made `Main.unity` the shared game scene. This did not match the `AGENTS.md` rule "do not touch `Main.unity`", so the user asked to remove the rule.

## What changed

| What | Where |
|------|-------|
| Deleted the rule sentence. The sentence about working in a personal test scene stays | `AGENTS.md` "씬과 폴더". Committed on `main` and merged into `sam` |
| Deleted the note that this rule had to change | `implementation_plan.md` 3.3 |

## Verification

| What | Result |
|------|--------|
| `git diff` (`AGENTS.md` on `main`) | Only that one line changed |
