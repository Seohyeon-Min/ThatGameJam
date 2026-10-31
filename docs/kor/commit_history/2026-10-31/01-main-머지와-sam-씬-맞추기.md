# origin/main 머지와 sam 씬 맞추기

## 왜 했나

사용자가 `origin/main`(커밋 48개 앞섬)을 `sam`에 머지하라고 했다. 충돌은 4개였다. 사용자 결정은 둘이다.

- 커밋하지 않은 `Bird.controller` 수정은 버린다.
- `BrokenTree_Animator.controller`의 GUID는 main을 따른다. 그래도 sam 씬이 계속 동작해야 한다.

## 충돌 처리

| 파일 | 처리 |
|------|------|
| `Assets/01_Arts/Models/Bird/Bird.controller` | 머지 전에 로컬 수정을 버렸다. main의 전환 수정이 그대로 들어온다 |
| `BrokenTree_Animator.controller(.meta)` | main 파일(GUID `6622a7ed…`)을 쓴다. Tommy의 `idle`을 기본 상태로 두고 `Break` 상태 하나만 더했다. `LeaningLog`가 `Break`를 재생한다 |
| `Assets/01_Arts/Models/Girl/Girl.controller` | sam 쪽(QuietWait 포함)을 쓰고, JU가 main에서 더한 `BalanceWalk`와 `Jump` 상태를 같은 fileID로 다시 넣었다 |
| `Assets/02_Scripts/Baby/Baby.cs` | 양쪽 모두 살렸다. sam의 QuietWait, 관찰 대상 바라보기, ByBirdRoute 우회 다음에 main의 LeaningLog 균형 걷기가 온다. 경로 끝은 main의 `LieDown()`을 쓰고, 그 안에서 QuietWait를 끈다 |

## sam 씬에 맞춘 것

| 무엇 | 자리 |
|------|------|
| 고목 컨트롤러 참조를 `1b351be4…`에서 main GUID로 바꿨다 | `Assets/00_Scenes/Sam_Test.unity` |
| 기울어진 고목을 건넌 뒤 첫 땅 지점(`Stage1/BabyRoute/Point_124`, z 23.2)에 `BalanceBeamEnd`를 붙였다. 없으면 아기가 고목 뒤로 계속 BalanceWalk에 머문다(이 상태에는 나가는 전이가 없다) | `Sam_Test.unity`, `SetupStage1Logs.MarkBeamEnd` |
| main의 `Snowfall.prefab`에 `SeasonSnow`가 생겨 씬에 둘이 되었다. 씬에서 더한 것을 지우고 프리팹 것의 `season`을 GameFlow의 Season으로 연결했다(main 빌더가 하는 방식). 그전에는 Play 시작 때 `SeasonSnow.OnEnable` NullReferenceException이 났다 | `Sam_Test.unity` |
| 빌더가 고목 컨트롤러의 상태를 모두 지우고 다시 만들던 것을, 없을 때만 `Break`를 더하도록 바꿨다. 소녀 컨트롤러 재생성에 `BalanceWalk`, `Jump`를 넣었다. 1스테이지 재생성 때 `BalanceBeamEnd`를 붙인다 | `AgentScripts/BuildSamTestScene.cs` |
| 고목 기본 상태 이름 확인을 `Idle`에서 `idle`로 바꿨다 | `AgentScripts/VerifyStage1Logs.cs` |

씬을 다시 저장하면서 main의 `Snowfall.Awake → Configure()`가 계산한 파티클 값(크기, 속도, 재질)이 씬 override에 함께 저장되었다. Snowfall 컴포넌트 설정값은 바꾸지 않았다.

## 검증

| 무엇 | 결과 |
|------|------|
| 컴파일 | 에러 0 |
| 컨트롤러 상태 | 고목: 기본 `idle`, `Break`. 소녀: 기존 9개 + `BalanceWalk`, `Jump` |
| `VerifyStage1Logs.Play`, `Natural` | 통과. 고목을 건너 2스테이지 시작. 그때 아기 애니메이션은 `Sleep` |
| `VerifyStage2.Run` bird, early, natural, partial | 4개 모두 통과 |
| `VerifyStage2Waiting.Run` | 아기 동작은 모두 통과. 마지막 `Actual branch clear lost its sound`에서 실패 |
| Play 중 콘솔 에러 | SeasonSnow 수정 뒤 0 |

## 크로스 모델 리뷰

GPT-6.1 Sol. 충돌 해결과 후속 수정 diff(Baby.cs의 양쪽 대비, 빌더, 컨트롤러, 씬 수정분). `LGTM - no blockers`.

## 남은 것

- main이 FMOD로 바꾸면서 `ProjectSettings/AudioManager.asset`의 `m_DisableAudio`를 1로 켰다. Unity `AudioSource`가 소리를 내지 않는다. sam의 눈가지 소리(`SnowBranch.snowSound`)도 지금은 들리지 않는다. FMOD 이벤트로 옮길지는 사용자와 정한다. sam 브랜치에서는 사운드 에셋을 만들지 않는다.
- 첫 번째 쓰러진 통나무(정적 다리)에는 균형 걷기 표시를 붙이지 않았다. JU의 `Sam_Test_JU`는 붙였다. 필요하면 사용자에게 묻고 더한다.
- 빌더의 새 컨트롤러 재생성은 main에서 JU가 바꾼 Bird 전환(ExitTime)을 덮는다. 빌더를 다시 돌리기 전에 맞춘다.
