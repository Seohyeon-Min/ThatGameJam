# 0001. `.agents/` goes on main, personal files on sam

- Status: accepted
- Date: 2026-10-26
- Related: [ADR 0006](0006-agents-md-shared-on-main.md)

## Context

We had to decide which branch gets the first three files: `.agents/` (Unity agent skills), `image.png`, and `AGENTS.md`.

## Decision

The user decided.

**`.agents/` goes on `main`, and `sam` has the same commit. `image.png` and `AGENTS.md` stay on `sam` only.**

[ADR 0006](0006-agents-md-shared-on-main.md) later changed the `AGENTS.md` part.

## Alternatives

**Alternative 1. Keep every file on `sam` only.**
Rejected. Teammates would have to pull `sam` to use the skills.

**Alternative 2. Put `AGENTS.md` on `main` too.**
Rejected at that time. `AGENTS.md` then held only a one-line test rule.

## Consequences

**Gains.**

- Teammates get the skills by pulling `main`
- Personal experiment files do not clutter `main`

**Costs.**

- Agent sessions started from `main` do not get the `AGENTS.md` rules
