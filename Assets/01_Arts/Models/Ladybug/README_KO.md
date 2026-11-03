# 무당벌레 애니메이션 (Unity)

원본: `Ladybug.blend`. 블렌더 5.2에서 파이썬 스크립트로 만들었다. 리그의 액션 2개를 구워서 내보냈다.

## 파일

| 파일 | 내용 |
|---|---|
| `Ladybug-Animated.fbx` | 메시, 리그, 클립 2개 |
| `Ladybug_clips.json` | 클립별 프레임 범위, 루프 여부 |
| `Ladybug.controller` | Idle(기본) ↔ Crawl. Bool 파라미터 `Crawl` 로 오간다. 전환 0.15초 |
| `Assets/03_Prefabs/Ladybug.prefab` | Animator와 컨트롤러가 붙은 프리팹. 수채화 머티리얼 연결 |

## 클립 (30 FPS)

| 클립 | 길이 | 루프 | 언제 |
|---|---|---|---|
| `Ladybug_Idle` | 3초 | O | 제자리. 발은 땅에 붙고 몸이 살짝 숨 쉬고 더듬이와 머리가 움직인다 |
| `Ladybug_Crawl` | 0.8초 | O | 기어가기. 다리 3개씩 번갈아 딛는다(삼각 보행). **0.2 m/s** 로 옮기면 발이 안 미끄러짐 |

- 두 클립 모두 같은 서 있는 자세 근처에서 시작/끝나서 크로스페이드 0.15~0.2초면 자연스럽다.

## FBX만 쓸 때

- Rig: Animation Type **Generic**, Avatar **Create From This Model**, Root node `Root`
- Animation 탭: 두 클립 모두 Loop Time 켜기
- 설정은 `AgentScripts/SetupLadybugModel.cs` 가 해 준다

## 참고

- 제자리 애니메이션. Root는 움직이지 않으니 이동은 스크립트로.
- Unity에서 +Z(앞)를 보도록 내보냈다.
- 크기: 몸길이 약 0.6 m(더듬이까지 약 0.7 m), 높이 약 0.3 m, 다리 폭 약 0.64 m. 어른 토끼(약 1.2 m)의 절반. 프리팹 Scale 1 기준.
- 뼈 17개: `Root`, `Body`, `Head`, `Antenna.L/R`, 다리 6개 x 2마디(`Leg{Front,Mid,Back}{Upper,Lower}.L/R`). 블렌더의 발 IK 뼈는 내보내지 않았다.
- 머티리얼 슬롯 3개(단색): `Ladybug_Red`, `Ladybug_Black`, `Ladybug_White`. 임포트 때 `WC_LadybugRed/Black/White`(WatercolorLit)로 바꿔 끼운다.
- 메시: 정점 약 2,100, 삼각형 약 4,000. UV는 있지만(Smart UV) 텍스처는 없다.
- Blender에서 고친 뒤에는 다시 내보내야 함.
