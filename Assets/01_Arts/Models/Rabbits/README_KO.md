# 토끼 애니메이션 (Unity)

원본: `Rabbit.blend`(어른), `BabyRabbit.blend`(아기). 각 리그의 액션 12개를 구워서 내보냄.
Unity 6000.3.19f1에서 임포트 검증 통과(`unity_validation.txt`).

## 파일

| 파일 | 내용 |
|---|---|
| `RabbitAnimations.unitypackage` | **이것만 Import하면 됨.** 두 FBX + 프리팹 2개. 클립 이름, 루프 설정 완료 |
| `Rabbit-Animated.fbx` / `BabyRabbit-Animated.fbx` | FBX만 필요할 때 (아래 "FBX만 쓸 때" 설정 필요) |
| `Rabbit_clips.json` / `BabyRabbit_clips.json` | 클립별 프레임 범위, 루프 여부 |
| `unity_validation.txt` | Unity 검증 결과 |

Import 후: `Assets/RabbitAnims/Rabbit-Animated.prefab`, `BabyRabbit-Animated.prefab` (Animator 포함, Controller는 직접 연결).

## 클립 (30 FPS, 아기는 이름 앞에 `Baby`가 붙음: `BabyRabbit_Hop` 등)

| 클립 | 길이 | 루프 | 언제 |
|---|---|---|---|
| `Rabbit_Hop` | 0.73초 | O | 이동. 어른 **0.61 m/s**, 아기 **0.34 m/s**로 옮기면 발이 안 미끄러짐 |
| `Rabbit_Sniff` | 2초 | O | 웅크린 채 코 킁킁 |
| `Rabbit_SitIdle` | 3초 | O | 앉은 기본 대기 |
| `Rabbit_SitIdle_LookAround` | 5초 | O | 앉아서 두리번 (랜덤 대기) |
| `Rabbit_SitIdle_SniffGround` | 4초 | O | 앉아서 바닥 킁킁 (랜덤 대기) |
| `Rabbit_SitIdle_Scratch` | 3초 | O | 뒷발로 옆구리 긁기 (랜덤 대기) |
| `Rabbit_Sleep` | 4초 | O | 식빵 자세로 잠 |
| `Rabbit_Sleep_Flat` | 4초 | O | 턱을 바닥에 대고 잠 |
| `Rabbit_Sleep_Side` | 4초 | O | 옆으로 누워 웅크리고 잠 |
| `Rabbit_WakeUp` | 2.67초 | X | Sleep → 일어나 앉기 |
| `Rabbit_WakeUp_Flat` | 2.77초 | X | Sleep_Flat → 일어나 앉기 |
| `Rabbit_WakeUp_Side` | 3.67초 | X | Sleep_Side → 일어나 앉고 머리 털기 |

이어지는 포즈가 정확히 같음(검증 오차 0):
- SitIdle 4종은 모두 같은 앉은 포즈로 시작/끝 → 랜덤으로 이어 붙여도 됨
- `Sleep*` 시작 = 해당 `WakeUp*` 시작, `WakeUp*` 끝 = `SitIdle` 시작
- 흐름 예: SitIdle(랜덤) → (크로스페이드 0.5초) → Sleep → WakeUp → SitIdle
- Hop, Sniff는 웅크린 포즈 기준이라 앉은 대기와 오갈 때는 크로스페이드 0.2~0.3초

## FBX만 쓸 때

- Rig: Animation Type **Generic**, Avatar **Create From This Model**, Root node `Root`
- Animation 탭: `WakeUp` 3개만 Loop Time 끄고 나머지는 켜기

## 참고

- 제자리 애니메이션. Root는 움직이지 않으니 이동은 스크립트로.
- Unity에서 +Z(앞)를 보도록 내보냄.
- 자는 동안 감긴 눈은 `Eye.L/Eye.R` 뼈의 스케일로 표현됨(다른 클립에서는 1로 돌아옴).
- 조종용 뼈(`Torso`, `HindIK.*`, `FrontIK.*`, `KneePole.*`, `ElbowPole.*`)도 빈 Transform으로 들어있음. 메시에 영향 없음.
- 크기: 어른 몸길이 약 1.2 m, 아기 약 0.7 m. 게임 스케일에 맞게 프리팹 Scale 조절.
- 메시에 UV 없음(단색 머티리얼 `Rabbit_White`, `Rabbit_Eye`).
- Blender에서 고친 뒤에는 다시 내보내야 함.
