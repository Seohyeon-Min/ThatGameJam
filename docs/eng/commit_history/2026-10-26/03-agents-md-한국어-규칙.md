# Add a Korean conversation rule to `AGENTS.md` and check it is read at session start

## Why

The user asked whether Prime Agent reads `AGENTS.md` at session start. At that point there was no `AGENTS.md` in the repo or the home folder, so it could not be checked.

## What changed

| What | Where |
|------|-------|
| One line: `- 대화는 한국어로 한다.` (talk in Korean) | `AGENTS.md` |

## Verification

| What | Result |
|------|--------|
| A new session `agents-md-probe` was asked to answer only from the instructions it already had, without reading files | Its `# Project Context` section contained `## C:\me\ThatGameJam2\ThatGameJam\AGENTS.md` and the content verbatim |

A global rule already defaults to Korean, so a Korean answer alone proves nothing. The new session was asked to state where the rule came from.

## Learned

- `AGENTS.md` is read only at session start. Edits apply from the next new session
- Only the file in the working folder was checked. Whether a parent-folder `AGENTS.md` is read was not tested
