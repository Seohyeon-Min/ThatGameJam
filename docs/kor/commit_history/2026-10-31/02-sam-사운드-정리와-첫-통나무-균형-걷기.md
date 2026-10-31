# sam 사운드·눈가루 머티리얼 정리, 첫 통나무 균형 걷기, 새 빌더 맞추기

## 왜 했나

main 머지(`01-main-머지와-sam-씬-맞추기.md`) 뒤 사용자가 세 가지를 정했다.

1. sam에서 만든 사운드와 눈가루 머티리얼을 정리한다. 앞으로 사운드·에셋은 무조건 상대 브랜치를 따른다.
2. 첫 번째 쓰러진 통나무에도 JU가 `Sam_Test_JU`에 넣은 대로 균형 걷기 표시를 붙인다.
3. 새 컨트롤러 빌더와 main의 Bird 전환 차이는 알아서 맞춘다.

## 무엇을 바꿨나

| 무엇 | 자리 |
|------|------|
| sam이 만든 `BranchRustle.wav`, `RabbitSnowPowder.mat` 삭제 | `Assets/05_Sounds/`, `Assets/01_Arts/Materials/Test/` |
| 눈가지의 `snowSound`(AudioSource) 필드와 재생 삭제. 씬의 가지 3개에서 AudioSource 제거 | `SnowBranch.cs`, `Sam_Test.unity` |
| 가지가 털어 내는 눈가루 재질을 main의 눈송이 재질 `Assets/01_Arts/Materials/Snow.mat`(Snowfall이 쓰는 것)로 바꿈 | `Sam_Test.unity`, `BuildStage2.cs` |
| 빌더에서 WAV 복사, AudioSource 생성, 눈가루 재질 생성을 지움. 이미 저장된 씬용 `BuildStage2.UseMainSnowAssets` 추가 | `AgentScripts/BuildStage2.cs` |
| 첫 통나무: `Point_0`(통나무 앞 땅)에 `BalanceBeamStart`, `Point_74`(z 18 정지점 앞 마지막 점)에 `BalanceBeamEnd`. JU가 `Sam_Test_JU`에서 첫 통나무 앞 땅과 끝 점에 붙인 것과 같은 자리다 | `Sam_Test.unity`, `SetupStage1Logs.MarkBalanceBeams` |
| 빌더의 Flap→Glide 전환을 main(JU)대로 Flap 한 번을 다 친 뒤 넘어가게 함(exit time 1) | `BuildSamTestScene.BuildBirdAnimator` |
| 가지 소리 검사를 지우고 "가지에 AudioSource 없음" 검사로 바꿈 | `AgentScripts/VerifyStage2Waiting.cs` |

`RabbitStageSnow.mat`, `RabbitStageWood.mat`(2스테이지 눈둑과 가지 나무색)은 눈송이 재질이 아니라서 남겼다.

## 검증

| 무엇 | 결과 |
|------|------|
| 컴파일 | 에러 0 |
| 1스테이지 아기 애니메이션 순서(배속 3) | `Walk` → z 5.8 `BalanceWalk` → z 13.5 `Walk` → 고목 정지 → z 19.4 `BalanceWalk` → z 23.7 `Walk` → z 25 `LieDown`, Stage2 시작 |
| `VerifyStage1Logs.Play`, `Natural` | 통과 |
| `VerifyStage2.Run` bird, early, natural, partial | 4개 통과 |
| `VerifyStage2Waiting.Run`, `Bird` | 통과 |
| Play 중 콘솔 에러 | 0 |
| 지운 GUID와 `snowSound` 참조 | 저장소에 남은 참조 0 |

## 크로스 모델 리뷰

GPT-6.1 Sol. 스테이징된 diff 전체(WAV 바이너리 제외). `LGTM - no blockers`.
