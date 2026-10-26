# Put project rules in `AGENTS.md` and share it on main (ADR 0006)

## Why

The user decided to put rules for developing the game into `AGENTS.md` and share it with the team ([ADR 0006](../../decisions/0006-agents-md-shared-on-main.md)).

## What changed

| What | Where |
|------|-------|
| Rules for conversation and records, project, scenes and folders, Unity CLI, git | `AGENTS.md` (`sam`, commit `7427472`) |
| The same content committed to `main` | `AGENTS.md` (`main`, commit `f0fe1e3`) |
| Merged `main` into `sam` | commit `35264c3` |
| Added the decision record | `docs/` (commit `f8c569f`) |

Each rule in the draft cited something that actually happened in this session. It was written only after the user approved it.

## Key decisions

- The Editor had the `sam` working tree open, and switching branches would reimport assets. So `main` was committed from a temporary worktree, which was then removed
- Both branches have identical `AGENTS.md` content, so the merge had no conflict

## Mistake

In `f8c569f`, `git add docs` staged the whole folder. `docs/record-format-example.md`, which the user had just added, was committed along with it unintentionally. It was before any push, and the file was kept. From now on, each path to commit is named explicitly.
