# 아기가 걷다가 관심 있는 사물(나무, 통나무 랜드마크)을 머리만 돌려 쳐다보게 함

## 왜 했나

어제(10-29) 넣은 걷기 움직임(완만한 방향 전환, 속도 변화, 무작위 두리번거림)에 이어, "단순 무작위가 아니라 호기심과 주변 환경에
반응하는 것처럼" 보이게 해달라는 요청이 왔다. 기존 `CuriousPause`는 특정 대상 없이 멈춰서 `LookAround`를 재생할 뿐이라, 실제로
"무엇을" 보는지는 없었다.

함께, Google Drive에 업데이트된 애니메이션 중 쓰지 않고 남은 게 있는지(점프, 양팔 벌리고 중심잡기) 확인해 달라는 질문에 답하면서
필요한 애니메이션 전체 목록도 정리했다.

## 무엇을 바꿨나

| 무엇 | 자리 |
|------|------|
| 반경 안의 `PointOfInterest`를 찾아 머리 본(`NPC-Girl-Rig/ROOT/Hips/Spine/Chest/Neck/Head`)만 그쪽으로 돌리는 "glance". 걷기는 멈추지 않음(기존 `CuriousPause`의 전신 정지+두리번거림과는 다른, 더 가벼운 반응) | `Assets/02_Scripts/Baby/Baby.cs` (`LateUpdate`, `PickNearbyPointOfInterest`) |
| 걷는 중 쿨다운이 돌 때 주변에 POI가 있으면 glance, 없으면 기존 `CuriousPause`로 분기. 쿨다운 리셋을 호출부로 옮김(두 경로가 공유) | `Baby.cs` (`WalkTo`) |
| 관심 사물 마커 컴포넌트. 가중치(`weight`)와 바라볼 지점 오프셋을 가짐. 새(Bird)는 의도적으로 제외 — 새를 올려다보는 건 "새가 도와줬다"는 뜻의 별도 전용 제스처(design doc §4.1)라 섞이면 안 됨 | `Assets/02_Scripts/Core/PointOfInterest.cs` (새 파일) |
| glance 반경·지속시간·최대 회전각·블렌드 속도 튜닝값 | `Assets/02_Scripts/Core/GameTuning.cs` |
| `Stage1/RoadsideTrees`(56그루)와 랜드마크 3개(`FallenLog`, `Stump`, `LeaningLog`, 가중치 2)에 `PointOfInterest` 일괄 부착 | `AgentScripts/AddBabyGazeTargets.cs`(새 빌더 스크립트) 실행 → `Assets/00_Scenes/Sam_Test.unity` |

## 중요한 결정

- 리그가 Humanoid가 아니라 Generic(`NPC-Girl-Animated.fbx.meta` `animationType: 2`)이라 `Animator.SetLookAtPosition` IK를 못 쓴다.
  대신 `LateUpdate`에서 애니메이터가 이미 포즈를 잡은 뒤 머리 본 회전에 목표 방향으로의 오프셋을 얹는 표준적인 look-override 방식을
  썼다. 새 애니메이션 클립이 전혀 필요 없다.
- 최대 회전각(`babyGazeMaxAngle`, 60°)으로 클램프해서, POI가 뒤쪽에 있어 과하게 꺾여야 하는 경우엔 그냥 못 본 척 넘어가게 했다.
  고개가 부자연스럽게 꺾이는 것보다 안 보는 게 낫다고 판단.
- glance와 기존 `CuriousPause`(두리번거림)는 쿨다운 타이머를 공유한다 — 같은 "지금 한눈팔 때가 됐다" 타이밍에서 주변에 뭔가
  있으면 그걸 보고, 없으면 예전처럼 막연히 두리번거리는 것으로 분기했다. 둘이 독립 타이머였다면 너무 자주 한눈파는 아이가 됐을 것.
- 새(Bird)는 POI 후보에서 뺐다. 디자인 문서가 "올려다본다 = 새가 곁에 있다는 것을 안다"는 반응 언어를 게임 전체에서 하나로
  통일한다고 명시(§4.1)했는데, 걷다가도 무작위로 새를 흘끔거리면 그 신호가 희석된다.
- `RoadsideTrees`는 이름 그대로 경로 옆에 줄지은 나무라 glance 대상으로 자연스럽다고 보고 전부(56개) 붙였다. `TerrainTrees`는
  경로에서 먼 배경 나무라 제외.

## 검증

| 무엇 | 결과 |
|------|------|
| `Baby.cs`/`PointOfInterest.cs`/`GameTuning.cs` 컴파일 | `script recompile` → `completed`, 에러 0 |
| `AddBabyGazeTargets.cs` 빌더 실행 | "Added 59 PointOfInterest components, skipped 0 already present." |
| Play Mode 진입 후 실시간 진행 확인 | `Time.time`이 3회 폴링에서 계속 증가(3.1 → 5.8 → 8.1초), 멈춰 있지 않음을 확인 |
| 리플렉션으로 `Baby`의 private 필드(`gazeTarget`, `gazeTimeRemaining`, `gazeBlend`, `curiousPauseCooldown`)를 1초 간격 폴링 | 걷기 중 `target=Tree_2_Bare`로 선택, `blend`가 0→1.00→0으로 블렌드 인/아웃되는 것을 확인. 이후 `Waiting`(블로커 대기) → 자동으로 다시 `Walking` → 루트 끝 `Resting`까지 정상 진행 |
| Play Mode 동안 콘솔 에러/경고 | `console get --level error`/`warning` 모두 플레이 중 발생한 에러 0건(게임 시작 전 CLI 오타로 남은 에러 2건만 존재, 무관) |
| 임시 캡처 파일(`Assets/_tmp_gaze_check.png`) | 확인 후 파일과 `.meta` 삭제, `AssetDatabase.Refresh()` |
| `git status` | 의도한 파일만 변경/추가됨 확인 |

## 남은 것

- 실제 화면으로 glance의 "느낌"(고개 돌아가는 속도·각도, 얼마나 자연스러운지)은 못 봤다. 테스트 중 카메라가 새를 따라가고
  있어서 아기가 화면 밖에 있었다. `babyGazeMaxAngle`/`babyGazeBlendSpeed`/`babyGazeDuration`은 추정치라 직접 보면서 조정 필요.
- 애니메이터에게 넘길 필요 애니메이션 전체 목록(머리 시선은 이번에 애니메이션 없이 해결했지만, `Girl_LookUp`·쪼그려 들여다보기·
  쉼돌 앉기·걷기 시작/정지·추위 떨림 실제 클립·깨우기 신호 3단계용 누운 자세 애니메이션 등)은 세션 안에서 정리만 하고 전달은
  아직 안 했다.
- `Girl_Jump`는 여전히 애니메이터 상태만 있고 쓰는 곳이 없다.
