# 0006. Put project rules in `AGENTS.md` and share it on main

- Status: accepted
- Date: 2026-10-26
- Related: [`../../../AGENTS.md`](../../../AGENTS.md), [ADR 0001](0001-branch-file-placement.md)

## Context

We decided to put rules for developing the game into `AGENTS.md`. Under [ADR 0001](0001-branch-file-placement.md) the file existed only on `sam`.

## Decision

The user decided.

**Use the AI's rule draft as is, and keep `AGENTS.md` on `main` as a shared file.** This replaces the `AGENTS.md` part of ADR 0001. Because the file is now shared, the git rule "work on the `sam` branch" became "work on a branch with your own name (for example `sam`)".

## Alternatives

**Alternative 1. Keep it on `sam` only.**
Rejected. Sessions started from `main` would not read the rules.

**Alternative 2. Put cross-model review into the rules.**
Rejected. Teammates may not have `codex`.

## Consequences

**Gains.**

- Agent sessions read the same rules no matter which branch they start from

**Costs.**

- The rules mention `docs/kor/`, `docs/eng/`, and `AgentScripts/`, which exist only on `sam` for now. Sessions started from `main` will create these folders fresh
