# 사전을 Main 씬에 넣기 (이스터에그 없이)

## 왜 했나

사용자가 사전을 Main 에도 넣으라고 했다. 이스터에그는 아직 없으므로 사전만 넣는다. 시험용 토끼는 넣지 않는다.

## 무엇을 바꿨나

| 무엇 | 자리 |
|------|------|
| `Encyclopedia` 프리팹을 `01_SYSTEM` 아래에 놓았다 | `Assets/00_Scenes/Main.unity` |
| 사전이 열려 있으면 입력을 처리하지 않고, 앉기 커서를 되돌린다 | `BirdController.cs` `Update` |
| 사전이 열려 있으면 휠 줌을 무시한다 | `BirdCamera.cs` |
| 인트로와 컷신 중에는 사전이 열리지 않는다. 인트로는 `Play` 부터 흰 화면이 걷히고 꺼질 때까지 `IntroSequence.IsPlaying`, 컷신은 기존 `Cutscene.AnyPlaying` 으로 본다 | `Encyclopedia.cs`, `IntroSequence.cs` |
| 사전 캔버스 sortingOrder 100 → 110. Main 의 인트로 캔버스(제목, 흰 화면)가 100 이다 | 빌더, `Encyclopedia.prefab` |
| 빌더가 Main 이 열려 있으면 프리팹만 `01_SYSTEM` 아래에 놓고 저장한다 | `AgentScripts/BuildEncyclopedia.cs` |

## 중요한 결정

- **연출 중에는 열리지 않는다.** 처음에는 인트로와 컷신도 `timeScale` 0 이면 같이 멈추므로 막지 않았다. 사용자가 연출 중에는 못 열게 하라고 했다.
- **씬 저장 때 함께 바뀐 것.** Main 을 다시 저장하면서 `IntroSequence` 의 예전 직렬화 필드(`diveChase`, `diveChaseFollow`, `diveCameraLean`)가 빠지고 지금 코드의 `diveCameraHeight` 15, `diveCameraLag` 0.35 가 들어갔다. 코드의 기본값과 같아 동작은 같다. Main 을 열 때 줄바꿈만 바뀐 `Snowfall.prefab`, `SunlitSpot.prefab` 은 되돌렸다.

## 검증

| 무엇 | 결과 |
|------|------|
| `unity recompile` | 오류 없음 |
| `BuildEncyclopedia.Run` (Main 열고) | `built Assets/03_Prefabs/UI/Encyclopedia.prefab and saved Assets/00_Scenes/Main.unity` |
| Play Mode, 인트로 제목 화면에서 가상 키보드 `` ` `` | `IsOpen` True, `timeScale` 0, 블러 1. 캡처에 칸 11 개가 모두 `???` 로 보이고 뒤가 흐리다 |
| 열린 채 가상 마우스 클릭 | 새 목표 그대로, 인트로가 첫 클릭으로 넘어가지 않았다 |
| 다시 `` ` `` | `IsOpen` False, `timeScale` 1, 블러 0 |
| 연출 막기 뒤 다시: 인트로 제목에서 `` ` `` | 열리지 않음 (`IntroSequence.IsPlaying` True) |
| ESC 로 인트로를 건너뛴 뒤 컷신 중 `` ` `` | 열리지 않음 (`Cutscene.AnyPlaying` True) |
| 컷신이 끝난 뒤 `` ` `` 두 번 | 열림(`timeScale` 0) → 닫힘(`timeScale` 1) |

## 크로스 모델 리뷰

GPT-6.1 Sol. 렌즈: 정확성, 보안, 견고성. 연출 막기 전과 뒤 두 번 리뷰했다.

## 남은 것

- 개발용 버튼 줄(IMGUI)은 사전 위에 그려진다. 사전이 열린 채 배속(F1)을 누르면 시간이 다시 흐른다. 개발용이라 그대로 두었다.
- 웹 빌드에서 `` ` `` 키 입력과 블러 비용은 아직 보지 않았다.
