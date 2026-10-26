# 0005. `docs/` uses kor as the source and mirrors it in eng

- Status: accepted
- Date: 2026-10-26
- Related: [`../../record-format-example.md`](../../record-format-example.md)

## Context

We had to decide the structure and language of the work records (`docs/`). The dev-workflow default has `spec.md`, `plan.md`, `decisions/`, and `commit_history/`.

## Decision

The user decided.

**For now, only `commit_history/` and `decisions/`. Keep `docs/kor/` and `docs/eng/` and mirror every document. Korean is written first, and English follows.** Records follow `docs/record-format-example.md`. That file is not mirrored.

## Alternatives

**Alternative 1. Follow the default and also keep `spec.md` and `plan.md`.**
Rejected for now. It is a game jam, so time spent on records is kept small.

**Alternative 2. Skip records.**
Rejected.

**Alternative 3. Write in one language only.**
Rejected.

## Consequences

**Gains.**

- Teammates who do not read Korean can read the same records

**Costs.**

- Every document is written twice. If only one side is edited, the two language versions can drift apart
