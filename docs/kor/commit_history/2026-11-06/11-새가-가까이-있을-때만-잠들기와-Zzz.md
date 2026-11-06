# 빛나는 공간에서 새가 가까이 있을 때만 잠들고, 그 전에는 길을 벗어나 논다. 잠든 소녀 머리 위의 Zzz (ADR 0035)

## 왜 했나

`order_from_director.md` 1, 2번.

1. 소녀가 빛나는 공간에 도착하고 새가 근처에 있을 때만 잔다. 새가 멀면 바닥에서 눈을 갖고 놀거나, 길을 벗어나 나무를 보거나, 뛰어다닌다. 새가 찾아가면 빛나는 공간으로 돌아가 잠든다.
2. 소녀가 완전히 누워 있을 때 Zzz 파티클. 텍스처는 Ju 브랜치의 png 를 쓴다.

## 무엇을 바꿨나

| 무엇 | 자리 |
|------|------|
| 길 끝에서 `PlayUntilBirdComes`: 새가 가까이 없으면 `Playing` 상태로 놀이 자리를 돈다. 새가 오면 올려다보고 빛으로 걸어가 눕는다 | `Baby.cs` |
| `BirdClose`: 수평 거리 `babyRestBirdDistance`(10 m) 안이고 소녀가 화면 안 | `Baby.cs`, `GameTuning.cs` |
| 놀이 자리 컴포넌트 `PlaySpot`(눈 놀이, 나무 보기, 뛰어다니기) | `Baby/PlaySpot.cs` |
| `running` 을 `directState` 로 넓혔다. 전이가 없는 상태(Run, PlaySnow, PlayTree)를 바로 재생하고, 걷거나 서면 빠져나온다 | `Baby.cs` |
| 나무 보기 중에는 고개를 나무 위(`lookAt`)로 든다 | `Baby.LateUpdate` |
| Zzz: 상태가 잠·눕기·깨어남이고 머리가 발에서 0.18(모델 단위) 아래일 때만 방출 | `Baby.UpdateSleepZzz` |
| Animator 에 `PlaySnow`(흥얼거리기), `PlayTree`(숨쉬기) 상태. 디렉터 클립이 오면 Motion 만 바꾼다 | `Girl.controller` |
| 인트로, 스테이지 1, 2 의 빛나는 공간 아래 `PlaySpots` 5개씩, 소녀 머리 뼈 아래 `SleepZzz` | `Main.unity` |
| Zzz 프리팹과 머티리얼(URP Particles/Unlit, 연한 파랑으로 곱함). 텍스처는 Ju 브랜치의 `SleepZ.png` 를 GUID 그대로 가져왔다 | `03_Prefabs/SleepZzz.prefab`, `Materials/vfx/SleepZ.mat`, `Textures/Particles/` |
| 빌더와 Play Mode 검증 | `AgentScripts/BuildGirlPlay.cs`, `AgentScripts/VerifyGirlPlay.cs` |
| 문서 | `game_design.md` 2.2, 17장, `implementation_plan.md` 4.3, 4.6 (kor/eng), ADR 0035 |

"완전히 누워 있음" 은 클립을 재서 정했다. 머리 높이(모델 단위)가 잠 0.16, 서기 0.90 이다. 0.18 아래는 `Girl_LieDown` 의 약 5.7 초부터(7.27 초 중), `Girl_GetUp` 은 약 1.3 초까지다.

Zzz 는 머리 뼈에 붙였다. 뼈에는 리그의 큰 스케일이 있어 처음에는 Z 가 몇 m 밖에서 나왔다. Scaling Mode 를 Local 로 해 부모 스케일을 무시한다. 흰 Z 가 눈과 빛 위에서 보이지 않아 크기를 0.9 ~ 1.1 m 로 키우고 연한 파랑(0.72, 0.82, 1)을 곱했다.

## 검증

| 무엇 | 결과 |
|------|------|
| `VerifyGirlPlay.Run` (Play Mode, 3배속) | 인트로 잠 중 Zzz, 일어나면 꺼짐, 새가 20 m 밖이면 `Playing`, 90 초 동안 Walk·Run·PlaySnow·PlayTree 를 돌며 빛에서 최대 5.2 m 벗어남, 새를 옆에 두면 빛으로 돌아가 0.2 m 안에서 눕고 Zzz, 스테이지 1 에서 Zzz 유지, 새가 내내 가까우면 놀지 않고 바로 잠듦. 모두 PASS |
| 게임 화면 캡처 | 잠든 소녀 머리 위로 파란 Z 가 떠올라 사라진다 |
| 콘솔 에러 | 0 |

## 크로스 모델 리뷰

GPT-6.1 Sol, 정확성·보안·견고성.

| 등급 | 지적 | 처리 |
|------|------|------|
| blocker | 뛰어다니기 점에 이미 있으면 yield 없이 돌아 멈춘다 | **반영.** 점마다 한 프레임 쉰다 |
| warn | 뛰어다니기 자리에 자식이 없으면 예외 | **기각.** 빌더가 늘 네 점을 만든다 |
| warn | `sleepZzz` 가 없는 씬(Sam_Test)에서 매 프레임 예외 | **반영.** 없으면 건너뛴다 |
| warn | 근처에 나무가 없으면 빌더가 중간에 멈춘다 | **기각.** 저장 전에 멈추므로 씬은 그대로다. 그때는 자리를 손으로 둔다 |

고친 뒤 `VerifyGirlPlay` 를 다시 돌려 모두 PASS.

## 남은 것

- 놀이 클립(눈 놀이, 나무 보기)은 디렉터가 만든다. 오면 `Girl.controller` 의 `PlaySnow`, `PlayTree` Motion 만 바꾼다.
- 새가 오지 않으면 스테이지가 끝나지 않는다(ADR 0035 결과).
- 실제 마우스로 손으로 해 보지는 않았다.
