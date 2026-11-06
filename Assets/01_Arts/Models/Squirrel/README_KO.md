# 다람쥐 애니메이션 (Unity)

원본: `Squirrel.blend`. 리그의 액션 3개를 구워서 내보냄 (`../export_squirrel_animation.py`).
Unity 6000.3.19f1에서 임포트 검증 통과(`unity_validation.txt`).

## 파일

| 파일 | 내용 |
|---|---|
| `SquirrelAnimations.unitypackage` | **이것만 Import하면 됨.** FBX + 프리팹. 클립 이름, 루프 설정 완료 |
| `Squirrel-Animated.fbx` | FBX만 필요할 때 (아래 "FBX만 쓸 때" 설정 필요) |
| `Squirrel_clips.json` | 클립별 프레임 범위, 루프 여부 |
| `unity_validation.txt` | Unity 검증 결과 |

Import 후: `Assets/SquirrelAnims/Squirrel-Animated.prefab` (Animator 포함, Controller는 직접 연결).

## 클립 (30 FPS)

| 클립 | 길이 | 루프 | 언제 |
|---|---|---|---|
| `Squirrel_Hop` | 0.6초 | O | 이동. **0.53 m/s**로 옮기면 발이 안 미끄러짐 |
| `Squirrel_SitIdle` | 3초 | O | 앉은 기본 대기 (숨쉬기, 고개 살짝, 귀 털기, 눈 깜빡, 꼬리 흔들기) |
| `Squirrel_SitIdle_LookAround` | 5초 | O | 앉아서 두리번거리며 킁킁 (랜덤 대기) |

- `SitIdle`, `SitIdle_LookAround`는 같은 앉은 포즈로 시작/끝 → 랜덤으로 이어 붙여도 됨 (예: SitIdle 몇 번 → 가끔 LookAround).
- Hop은 몸을 앞으로 숙인 자세라서, 앉은 대기와 오갈 때는 크로스페이드 0.2~0.3초.

## FBX만 쓸 때

- Rig: Animation Type **Generic**, Avatar **Create From This Model**, Root node `Root`
- Animation 탭: 모든 클립 Loop Time 켜기

## 참고

- 제자리 애니메이션. Root는 움직이지 않으니 이동은 스크립트로.
- Unity에서 +Z(앞)를 보도록 내보냄. Blender에서는 다람쥐가 +Y를 보고 있어서, 내보낼 때만 180° 돌려서 저장함(.blend는 그대로).
- 눈 깜빡임은 `Eye.L/Eye.R` 뼈의 스케일로 표현됨.
- 조종용 뼈(`Torso`, `HindIK.*`, `FrontIK.*`, `KneePole.*`, `ElbowPole.*`)도 빈 Transform으로 들어있음. 메시에 영향 없음.
- 크기: 앉은 키 약 0.69 m, 몸통은 어른 토끼의 약 55%. 게임 스케일에 맞게 프리팹 Scale 조절.
- 메시 260 삼각형, 스무스 셰이딩, UV 없음. 단색 머티리얼 `Squirrel_Fur`, `Squirrel_Cream`, `Squirrel_Eye` (`Squirrel_Nose`는 쓰이는 면이 없어서 Unity에는 안 들어옴).
- Blender에서 고친 뒤에는 `export_squirrel_animation.py`로 다시 내보내야 함.
