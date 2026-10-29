# main 머지 뒤 정리: 새 실루엣 복구, 나무 3번 색 복구, ADR 번호 정리

## 왜 했나

사용자 지시로 `origin/main` 을 `sam` 에 머지했다(`524ce13`). git 충돌은 없었으나 머지로 둘이 깨졌고, 결정 기록 번호가 겹쳤다. 사용자가 셋 다 고치기로 했다.

- main 의 `14e2106 shaderTest` 가 두 URP 렌더러를 고치면서 새 실루엣용 Render Objects 기능 둘(`BirdVisible`, `BirdSilhouette`)이 빠졌다. 나무에 가려진 새가 보이지 않았다
- `Tree_3.mat` 이 이름이 `Leaves` 인 초록 머티리얼로 바뀌어 들어왔다. 3번 나무 줄기가 초록이 됐다
- main 의 수채화 결정 기록이 `0003` 이라, 우리의 `0003`(구현 계획 질문의 답)과 겹쳤다

## 무엇을 바꿨나

| 무엇 | 자리 |
|------|------|
| `SetupBirdSilhouette.cs` 를 다시 돌려 두 렌더러에 새 실루엣 기능 둘을 다시 넣었다. 수채화 기능(`WatercolorKuwahara`, `WatercolorBlur`)과 SSAO 는 그대로다 | `Assets/Settings/PC_Renderer.asset`, `Mobile_Renderer.asset` |
| `Tree_3.mat` 의 이름과 색을 머지 전으로(`Tree_3`, 밝은 베이지 0.85, 0.82, 0.75). 에디터 API 로 했다 | `Assets/01_Arts/Materials/Test/Tree_3.mat` |
| 수채화 결정 기록을 `0017` 로 옮기고 제목 번호도 고쳤다. 이 기록을 가리키는 곳은 없었다. 다른 문서의 "ADR 0003" 은 모두 구현 계획 쪽을 가리킨다 | `docs/kor/decisions/0017-수채화는-...md`, `docs/eng/decisions/0017-watercolor-...md` |

## 검증

| 무엇 | 결과 |
|------|------|
| 렌더러의 기능 목록 | PC: `BirdSilhouette`, `WatercolorKuwahara`, `WatercolorBlur`, `BirdVisible`, SSAO. Mobile: `BirdVisible`, `BirdSilhouette`, `WatercolorBlur`, `WatercolorKuwahara` |
| Play Mode 화면 | 나무 뒤의 새가 하늘색 실루엣으로 보였다. 3번 나무가 베이지로 돌아왔다 |
| `Tree_3.mat` 을 머지 전과 비교 | 내부 하위 오브젝트 ID 하나만 다르다 |
| 콘솔 | 에러 0 |

## 크로스 모델 리뷰

코드가 바뀌지 않았다(설정 에셋과 문서뿐). 하지 않았다.

## 남은 것

- main 에서 들어온 `Assets/_Recovery/0.unity`, `Assets/New Terrain.asset` 은 임시로 보이지만 남의 작업이라 두었다
- 서현 님의 렌더러 변경이 다시 main 에 들어가면 새 실루엣이 또 빠질 수 있다. 렌더러를 고칠 때 이 두 기능을 남겨 달라고 알려야 한다
