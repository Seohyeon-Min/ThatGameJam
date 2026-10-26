# Add the Unity agent skills `.agents/` to main

## Why

The project folder had 34 Unity agent skills. They are shared by the team, so they went on `main`.

## What changed

| What | Where |
|------|-------|
| 34 skills, 222 files | `.agents/skills/` |

## Verification

| What | Result |
|------|--------|
| `git push origin 4ce108b:refs/heads/main` | `a181027..4ce108b`. `main` and `sam` share this commit |

## Blocker

The first push was rejected with `Permission to Seohyeon-Min/ThatGameJam.git denied to Git-Mere`. The SSH key account on this PC had no write access. It succeeded after the repo owner sent a collaborator invite and it was accepted.
