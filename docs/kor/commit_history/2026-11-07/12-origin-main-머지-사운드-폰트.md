# origin/main 머지: 사운드와 폰트 변경(changeSound&Font)

## 왜 했나

`origin/main` 의 커밋 `554cb23` 를 `sam` 에 받는다. 충돌은 FMOD 뱅크 하나였다.

## 무엇을 바꿨나

| 무엇 | 자리 |
|------|------|
| main 의 새 사운드(`easteregg`, `tree_jump`, `tree_walk`)와 바뀐 `cutsceneEnding`, FMOD 이벤트 메타데이터를 받았다 | `FMODProject/ThatGameJam/` |
| FMOD 뱅크는 합쳐진 프로젝트로 FMOD Studio 2.03.15 에서 다시 빌드했다 | `FMODProject/.../Master.bank` |
| main 의 아기 동작(멀면 다가가서 들여다보기, 고개 갸웃), 철새 깃털 셰이더, Cinzel 폰트 등은 git 이 합친 그대로 | `Baby.cs`, `GameTuning.cs`, `EndingFlight.cs`, `MigratingBirds.prefab` |

## 중요한 결정

- 10 과 같은 이유로 뱅크는 main 것(78.6 MB)을 쓰지 않고 다시 빌드했다. main 뱅크에는 ADR 0037 Vorbis 설정이 빠져 있다. 새 뱅크는 49.6 MB 다.

## 검증

| 무엇 | 결과 |
|------|------|
| 충돌 표시 | 0 |
| FMOD 빌드 | `Build completed successfully` |
| 컴파일 | 13 에서 JU 머지 뒤 함께 확인 |

## 크로스 모델 리뷰

하지 않았다. 우리 쪽 코드 변경이 없는 머지다.
