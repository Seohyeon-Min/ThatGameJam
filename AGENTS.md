# AGENTS.md

## 대화와 기록
- 대화는 한국어로 한다.
- 세션을 시작하면 `docs/kor/game_design.md`와 `docs/kor/implementation_plan.md`를 먼저 읽는다.
- 작업 기록은 `docs/kor/`에 먼저 쓰고, `docs/eng/`에 같은 구조로 미러링한다. 지금은 `decisions/`와 `commit_history/`만 쓴다.
- 기록 형식은 `docs/record-format-example.md`를 따른다.

## 프로젝트
- Unity 6000.4.2f1, URP, Input System 전용이다. 예전 `UnityEngine.Input` API는 쓰지 않는다.
- 목표 플랫폼은 웹 브라우저다. 마우스 휠은 값의 크기 대신 방향만 쓴다. 휠 값의 크기가 브라우저마다 다르기 때문이다.

## 씬과 폴더
- 개인 테스트 씬 `Assets/00_Scenes/<이름>_Test.unity`에서 작업한다.
- 폴더 용도: `00_Scenes` 씬, `01_Arts` 아트와 머티리얼, `02_Scripts` 스크립트, `03_Prefabs` 프리팹, `04_Data` 데이터, `05_Sounds` 사운드.
- 오브젝트를 여러 개 만드는 작업은 `AgentScripts/`(Assets 밖)에 빌더 스크립트로 둔다. 실행은 `unity command script run --file ... --entry ...`로 한다.

## Unity CLI (Pipeline)
- 에디터가 열려 있으면 씬과 에셋 파일을 직접 고치지 않는다. CLI로 에디터를 조작한다.
- 명령 이름이 스킬 문서와 다를 수 있다. 쓰기 전에 `unity command <그룹>`으로 실제 이름을 확인한다.
- `unity open`으로 띄운 에디터가 바로 꺼지면 `Unity.exe -projectPath <경로>`로 직접 실행한다.
- Play Mode 테스트 전에 `editor set_auto_tick --enable true`와 `editor.playmode set_run_in_background --enable true`를 켠다. 테스트가 끝나면 run in background를 끈다.
- 에디터 창에 포커스가 없으면 Input System이 실제 키보드를 꺼 둔다. 입력 테스트는 가상 장치(`InputSystem.AddDevice`)로 하고, 끝나면 장치와 설정을 되돌린다.
- `capture`의 `--save_path`는 `Assets/` 아래에 파일을 만든다. 확인이 끝나면 그 파일과 `.meta`를 지운다.

## git
- 개인 작업은 각자 이름의 브랜치(예: `sam`)에서 한다. `main`에는 팀 공용 파일만 올린다.
- 줄바꿈만 바뀐 `ProjectSettings` 파일은 커밋하지 않는다.
