# origin/JU 머지: 도감 발견 팝업(popup)

## 왜 했나

`origin/JU` 의 커밋 3개(`5cfc0b8` popup, `6f5bff9` Encyclopedia 프리팹, `b972e93` bank)를 `sam` 에 받는다. 충돌은 `Main.unity`, FMOD 뱅크 2개, FMOD 이벤트 하나였다.

## 무엇을 바꿨나

| 무엇 | 자리 |
|------|------|
| JU 의 발견 팝업(`DiscoveryPopupLayout.cs`, 팝업 위/아래 텍스처, 깃펜 애니메이션)과 `Encyclopedia.prefab` 을 받았다 | `Assets/02_Scripts/Encyclopedia/`, `Assets/03_Prefabs/UI/Encyclopedia.prefab` |
| `Main.unity` 는 오브젝트(fileID) 단위 3방향 머지로 합쳤다. 기준은 공통 조상 `9b30835` 다. JU 가 바꾼 문서는 10개뿐이다 | `Main.unity` |
| 개울 물 메시 2개는 JU 것을 쓰고, `sam` 쪽 메시 문서 2개는 지웠다. 05, 10 과 같은 처리다 | `WindingWater` 메시, MeshFilter 2개 |
| JU 가 지운 빈 루트 오브젝트 `p` 는 지웠다 | `SceneRoots` |
| 도감 PrefabInstance 는 JU 의 레이아웃 오버라이드를 쓰고, `sam` 의 `sharedProfile` 오버라이드 하나를 다시 얹었다 | `Encyclopedia` (PrefabInstance) |
| `icicleNote` 이벤트 볼륨은 main 의 -8 을 쓴다. 원래 6 이었고 main 은 -8, JU 는 4 로 낮췄다 | `FMODProject/.../{41637c10-...}.xml` |
| FMOD 뱅크는 합쳐진 프로젝트로 FMOD Studio 2.03.15 에서 다시 빌드했다 | `FMODProject/.../Master.bank`, `Master.strings.bank` |

## 중요한 결정

- `icicleNote` 볼륨은 두 쪽이 다 "상대 브랜치" 라서 규칙으로 정할 수 없었다. 공용 브랜치인 main 의 값을 골랐다. JU 의 4 가 맞으면 바꿔야 한다.
- 뱅크는 JU 것(76.5 MB, Vorbis 설정 없음)을 쓰지 않고 다시 빌드했다. 새 뱅크는 49.8 MB 다.

## 검증

| 무엇 | 결과 |
|------|------|
| 충돌 표시 | 0 |
| 씬 안 로컬 fileID 참조 | 끊긴 참조 0 |
| 에디터에서 씬 다시 열기 | GameObject 127832개, Missing Script 0, 컴파일 에러 0. 메시 없는 MeshFilter 4개는 모두 `Ring` 이고 이 머지와 관계없다 |
| FMOD 빌드 | `Build completed successfully` |
| Play Mode | 돌려 보지 않았다 |

## 크로스 모델 리뷰

하지 않았다. 우리 쪽 코드 변경이 없는 머지다.
