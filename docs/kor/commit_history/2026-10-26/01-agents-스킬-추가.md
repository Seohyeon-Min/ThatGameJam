# Unity 에이전트 스킬 `.agents/` 를 main 에 추가 (ADR 0001)

## 왜 했나

프로젝트 폴더에 Unity 에이전트 스킬 34개가 있었다. 팀 공용이므로 `main` 에 올리기로 했다([ADR 0001](../../decisions/0001-브랜치별-파일-배치.md)).

## 무엇을 바꿨나

| 무엇 | 자리 |
|------|------|
| 스킬 34개, 파일 222개 | `.agents/skills/` |

## 검증

| 무엇 | 결과 |
|------|------|
| `git push origin 4ce108b:refs/heads/main` | `a181027..4ce108b`. `main` 과 `sam` 이 같은 커밋을 가진다 |

## 막힌 것

첫 push 는 `Permission to Seohyeon-Min/ThatGameJam.git denied to Git-Mere` 로 거부되었다. 이 PC 의 SSH 키 계정에 쓰기 권한이 없었다. 저장소 주인이 collaborator 로 초대하고 수락한 뒤 성공했다.
