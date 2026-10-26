# Add project rules to AGENTS.md and move it to main

Date: 2026-10-26 15:04

## Topic
We decided to put rules for developing the game into `AGENTS.md`. Until now the file existed only on `sam` (decision of 2026-10-26 14:15).

## AI proposal
- The AI drafted the rules. Each rule cites something that actually happened in this session.
- The cross-model review rule was left out, because teammates may not have `codex`.
- The AI pointed out that shared rules could move to `main`.

## User choice
- Use the draft as is. Leaving out the cross-model review rule is right.
- Keep `AGENTS.md` on `main` as a shared file. This replaces the `AGENTS.md` part of the 14:15 decision.
- Because the file is now shared, the git rule "work on the `sam` branch" became "work on a branch with your own name (for example `sam`)".

## Trade-off
- Gain: agent sessions read the same rules no matter which branch they start from.
- Cost: the rules mention `docs/kor/`, `docs/eng/`, and `AgentScripts/`, which exist only on `sam` for now. Sessions started from `main` will create these folders fresh.
