# Branch file placement: `.agents/` on main, personal files on sam

Date: 2026-10-26 14:15

## Topic
We had to decide which branch gets the first files: `.agents/` (Unity agent skills), `image.png`, and `AGENTS.md`.

## AI proposal
- `.agents/`: `main` if it is shared by the team, otherwise `sam`.
- `AGENTS.md`: either put it on `main` and bring it into `sam`, or keep it on `sam` only.

## User choice
- `.agents/` goes on `main`. `sam` has the same commit.
- `image.png` and `AGENTS.md` stay on `sam` only.

## Trade-off
- Gain: teammates get the skills by pulling `main`. Personal experiment files do not clutter `main`.
- Cost: `AGENTS.md` exists only on `sam`, so agent sessions started from `main` do not get the Korean conversation rule.
