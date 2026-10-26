# 브랜치별 파일 배치: `.agents/`는 main, 개인 파일은 sam

Date: 2026-10-26 14:15

## 주제
레포에 처음 올릴 파일들을 어느 브랜치에 둘지 정해야 했다. 대상은 `.agents/`(Unity 에이전트 스킬), `image.png`, `AGENTS.md`다.

## AI의 제안
- `.agents/`: 팀 공용이면 `main`, 아니면 `sam`.
- `AGENTS.md`: 팀 공용이면 `main`에 올리고 `sam`에 가져오거나, `sam`에만 둔다.

## 사용자의 선택
- `.agents/`는 `main`에 둔다. `sam`도 같은 커밋을 가진다.
- `image.png`와 `AGENTS.md`는 `sam`에만 둔다.

## Trade-off
- 얻는 것: 팀원은 `main`만 받아도 스킬을 쓸 수 있다. 개인 실험 파일은 `main`을 어지럽히지 않는다.
- 비용: `AGENTS.md`는 `sam`에만 있다. 그래서 `main`에서 시작한 에이전트 세션에는 한국어 대화 규칙이 들어가지 않는다.
