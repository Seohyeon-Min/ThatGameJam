# origin/main 머지: 꽃 다시 심기(reflower), 다시 베이크(rebake)

## 왜 했나

`origin/main` 의 커밋 2개(`394dcf2` rebake, `64c1190` reflower)를 `sam` 에 받는다. 씬 충돌이 7522군데로 나와 사용자가 "main 씬을 바탕으로 두고 `sam` 이 바꾼 것을 다시 얹는" 방법을 골랐다. 이 방법은 오브젝트(fileID) 단위 3방향 머지로 했다. 기준은 공통 조상 `597bf99` 다.

## 무엇을 바꿨나

| 무엇 | 자리 |
|------|------|
| 꽃은 main 것을 쓴다. main 이 `FlowerGenerator` 아래 꽃을 모두 지우고 다시 만들었다(문서 약 21460개 삭제, 21470개 추가) | `01_SYSTEM/FlowerGenerator` |
| 나무는 `sam` 것을 쓴다. `sam` 이 04 에서 숲을 다시 심었다. main 이 옛 나무에서 바꾼 것은 렌더러 bounds 의 소수점 차이뿐이라 버렸다 (2558개 + 양쪽이 바꾼 68개) | `01_SYSTEM/TreeGenerator` |
| 개울 물 메시 2개는 main 것을 쓰고, `sam` 쪽 메시 문서 2개는 지웠다. 05 와 같은 처리다 | `Stream_1_Water`, `Stream_2_Water` |
| `IcicleXylophone` 의 머티리얼, 메시, 패턴 참조는 `sam` 것을 쓴다. main 이 다시 베이크한 에셋 22개(guid)가 main 에도 커밋되어 있지 않아 참조가 끊긴다 | `IcicleXylophone` (PrefabInstance, `WatercolorPaintedModel`) |
| 한쪽만 바꾼 나머지 오브젝트는 그쪽을 따른다. main 의 새 다람쥐, 열매 등이 들어왔다 | `Main.unity` |
| FMOD 뱅크는 합쳐진 프로젝트로 FMOD Studio 2.03.15 에서 다시 빌드했다. main 의 엔딩 음악 길이 변경(41.5 → 53.3 초)과 JU 의 공간음 제거, ADR 0037 Vorbis 설정이 모두 들어간다 | `FMODProject/.../Master.bank` |
| 베이크 에셋, `FmodEndingLength.js` 등 나머지는 git 이 합친 그대로 | |

## 중요한 결정

- 메모리의 "에셋 충돌은 상대 브랜치를 따른다" 규칙과 달리 뱅크는 main 것을 쓰지 않고 다시 빌드했다. main 뱅크(78.6 MB)에는 JU 의 공간음 제거와 Vorbis 설정이 빠져 있다. 새 뱅크는 49.6 MB 이고 `Master.strings.bank` 는 바뀌지 않았다.
- main 의 `IcicleXylophone` 새 베이크 에셋은 서현이 커밋해야 한다. 커밋되면 그때 참조를 바꾼다.

## 검증

| 무엇 | 결과 |
|------|------|
| 충돌 표시 | 0 |
| 씬 안 로컬 fileID 참조 | 끊긴 참조 0 |
| Transform 부모·자식 목록 | 어긋남 0 (`sam` 에 원래 있던 2개는 그대로) |
| 씬이 쓰는 guid | 모두 프로젝트에 있음 |
| 에디터에서 씬 다시 열기 | GameObject 127839개, Missing Script 0, 빈 참조 0, 컴파일 에러 0 |
| FMOD 빌드 | `Build completed successfully` |
| Play Mode | 돌려 보지 않았다 |

## 크로스 모델 리뷰

하지 않았다. 코드 변경이 없는 머지다.
