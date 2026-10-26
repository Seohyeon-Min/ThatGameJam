# docs: kor is the source, eng mirrors it

Date: 2026-10-26 14:53

## Topic
We had to decide the structure and language of the work records (`docs/`).

## AI proposal
The dev-workflow default puts `spec.md`, `plan.md`, `decisions/`, and `commit_history/` under `docs/`. The AI also asked whether to skip this because of the game jam.

## User choice
- For now, create only `commit_history/` and `decisions/`.
- Keep `docs/kor/` and `docs/eng/`, and mirror every document in both languages.
- Korean (`kor`) is written first, and English (`eng`) follows it.

## Trade-off
- Gain: teammates who do not read Korean can read the same records.
- Cost: every document is written twice. If only one side is edited, the two language versions can drift apart.
