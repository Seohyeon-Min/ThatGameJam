# NPC 소녀 애니메이션 (Unity)

원본: `NPC-Girl-Walk-GetUp.blend` (NPC-Girl-Rig의 액션 11개를 구워서 내보냄. 내보내기 스크립트: `export_girl_animation.py`)

## 파일

| 파일 | 내용 |
|---|---|
| `NPC-Girl-Animated.fbx` | 메시 + 리그 + 클립 11개 + 텍스처(내장) + 눈 깜빡임·꽃 블렌드셰이프 애니메이션 |
| `clips.json` | 클립별 프레임 범위, 루프 여부 |

## 클립 (30 FPS)

| 클립 | 길이 | 루프 |
|---|---|---|
| `Girl_Walk` | 1.2초 | O |
| `Girl_Run` | 0.667초 | O |
| `Girl_BalanceWalk` | 1.867초 | O |
| `Girl_Jump` | 1.5초 | X |
| `Girl_Idle_Breathing` | 4.0초 | O |
| `Girl_Idle_LookAround` | 8.0초 | O |
| `Girl_Idle_Hum` | 6.0초 | O |
| `Girl_Idle_Stretch` | 8.0초 | O |
| `Girl_LieDown` | 7.267초 | X |
| `Girl_Sleep` | 3.0초 | O |
| `Girl_GetUp` | 12.567초 | X |

이어지는 포즈가 정확히 같습니다(검증 오차 0):
GetUp 끝 ≈ 서 있는 포즈 → LieDown 시작은 GetUp 370프레임 포즈 → LieDown 끝 = Sleep 시작/끝 = GetUp 시작.
흐름 예: Idle/Walk/Run → LieDown → Sleep(반복) → GetUp → Idle.

## Unity 설정

- Rig: Animation Type **Generic**, Avatar Definition **Create From This Model**
- Animation 탭에서 Walk, Run, BalanceWalk, Sleep, Idle 4종은 **Loop Time** 켜기
- Materials: 텍스처 없는 옷·머리·눈썹 등은 머티리얼 Base Color에 색이 들어 있음(`Dress`, `Hair`, `Circle`, `Plane`)

## 참고

- 모든 클립은 제자리 애니메이션입니다. 이동은 스크립트로 처리하세요(앞 = Blender +X).
  - 발이 미끄러지지 않는 속도: Walk 약 0.1 m/s, Run 약 0.59 m/s, BalanceWalk 약 0.05 m/s
- 눈 깜빡임과 꽃은 블렌드셰이프 애니메이션으로 클립 안에 들어 있습니다.
- Blender 뷰포트는 'Preserve Volume' 스키닝이라, 많이 구부러진 치마(눕기/일어나기)는 Unity에서 최대 약 2cm 다르게 보일 수 있습니다.
- `Girl_GetUp`은 Blender에서 10~387프레임이며, FBX에서는 0초부터 시작합니다.
- Blender에서 애니메이션을 고친 뒤에는 다시 내보내야 합니다.

## Girl_Jump (제자리 점프)

클립에는 몸이 위로 뜨는 높이가 없습니다. 위로 올라가는 이동은 Unity에서 처리하세요. 머리카락과 치마는 약 20cm 높이로 뛰는 것을 가정하고 흔들리게 만들었습니다.

| 시점 | 프레임 | 시간 |
|---|---|---|
| 웅크리기 시작 | 4 | 0.1초 |
| 발이 땅에서 떨어짐(도약) | 14 | 0.433초 |
| 착지 | 26 | 0.833초 |
| 서 있는 자세로 돌아옴 | 46 | 1.5초 |

- 클립대로라면 공중에 있는 시간은 0.4초입니다. 이에 맞는 위쪽 초기 속도는 약 1.96 m/s이고, 최고 높이는 약 0.2m입니다(중력 9.81 기준).
- 0.433초에 Animation Event를 걸어 그때 위로 힘을 주면 발 구르는 순간과 딱 맞습니다.
- 높은 곳으로 올라가느라 공중에 있는 시간이 0.4초와 다르면, 공중 구간(0.433~0.833초) 동안 Animator 속도를 맞춰 주세요.
- 첫 프레임과 마지막 프레임은 Idle의 서 있는 자세와 거의 같습니다(약 6mm 차이). Idle과 짧게 크로스페이드하면 이어집니다.
