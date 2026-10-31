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
- `sam` 브랜치에서 작업할 때는 작업 중에 어떠한 아트, 사운드 에셋도 임의로 생성하지 않는다.

## 씬 구성과 프리팹
- 씬의 오브젝트와 설정 값은 에디터에서 만들어 씬이나 프리팹에 저장한다. 게임 설계상 의도한 경우가 아니면, 런타임에 오브젝트나 프리팹을 만들지 않고 저장된 설정 값을 덮어쓰지 않는다.
- 런타임 부트 코드(시작할 때 오브젝트를 만들거나 설정을 바꾸는 스크립트)를 만들지 않는다.
- 여러 번 쓰는 오브젝트는 프리팹으로 만들어 쓴다.
- 작업을 마치기 전에 확인한다. 새 코드의 `Awake`, `Start`, `OnEnable` 등에 의도하지 않은 생성(`Instantiate`, `new GameObject`, `AddComponent`)이나 값 덮어쓰기가 없는지, 반복되는 오브젝트가 프리팹인지 본다.

## Unity CLI (Pipeline)
- 에디터가 열려 있으면 씬과 에셋 파일을 직접 고치지 않는다. CLI로 에디터를 조작한다.
- 명령 이름이 스킬 문서와 다를 수 있다. 쓰기 전에 `unity command <그룹>`으로 실제 이름을 확인한다.
- `unity open`으로 띄운 에디터가 바로 꺼지면 `Unity.exe -projectPath <경로>`로 직접 실행한다.
- Play Mode 테스트 전에 `editor set_auto_tick --enable true`와 `editor.playmode set_run_in_background --enable true`를 켠다. 테스트가 끝나면 run in background를 끈다.
- 에디터 창에 포커스가 없으면 Input System이 실제 키보드를 꺼 둔다. 입력 테스트는 가상 장치(`InputSystem.AddDevice`)로 하고, 끝나면 장치와 설정을 되돌린다.
- `capture`의 `--save_path`는 `Assets/` 아래에 파일을 만든다. 확인이 끝나면 그 파일과 `.meta`를 지운다.

## git
- 개인 작업은 각자 이름의 브랜치(예: `sam`)에서 한다.
- 줄바꿈만 바뀐 `ProjectSettings` 파일은 커밋하지 않는다.
