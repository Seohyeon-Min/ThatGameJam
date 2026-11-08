# origin/JU 머지: 안개 숲 동물 실루엣, 기댄 통나무 그루터기 프리팹, FMOD 뱅크 다시 빌드

## 왜 했나

JU 브랜치(`origin/JU`)를 `sam` 에 받는다. JU 의 씬은 JU 가 main 을 머지할 때 자기 쪽 옛 씬을 골라서, 그대로 머지하면 꽃 약 6000개, 엔딩 새 떼, `CliffBreeze`, 헤드폰 안내 등 약 21650개 오브젝트가 지워진다. 그래서 씬은 `sam` 것을 바탕으로 두고, JU 커밋 4개(`a679f56`, `47e6cb9`, `7440c35`, `18910ba`)가 씬에서 바꾼 것만 오브젝트(fileID) 단위로 다시 얹었다.

## 무엇을 바꿨나

| 무엇 | 자리 |
|------|------|
| JU 에서 받음: 안개 숲 동물 실루엣과 크기, 암사슴, 다람쥐·새 모델, `FogMaze.birdCorridorWidth`, `Fog` 의 `FogBirdAmbience`, `SnowFlower` 의 `alignBird`·`faceForward` | `Main.unity` |
| 기댄 통나무 그루터기는 JU 처럼 `LeaningLogStump` 프리팹 + `Ladybug2` 로 바꿨다 | `Stages/Stage1/LeaningLog/broken tree/LeaningLogStump` |
| JU 가 지운 이스터에그 `StumpLadybug` 는 남기고, 인트로가 아닌 `Stages/Stage1` 아래로 옮겼다. 월드 위치(2.77, 0, 21.83)는 그대로이고 Stage1 범위 안이다 (사용자 지시: 둘 다 쓰고, 인트로에 두지 않는다) | `Stages/Stage1/StumpLadybug` |
| `sam` 을 유지: `Ending`(타임라인, `sleepSpot`, 엔딩 오디오), `CliffEnding.breeze`, `IntroSequence`(타이틀 영상, 헤드폰), 사전 UI 위치, `FlowerGenerator.overshoot`, 개울 메시(양쪽 내용이 같다). JU 쪽 값은 옛 씬에 남은 값이다 | `Main.unity` |
| `BloomSpot` 은 양쪽 오버라이드를 모두 넣었다 (`durationScale`, `reactsToSeason`) | `Main.unity` |
| `sam` 에 이미 없는 꽃 오브젝트 약 45개에 대한 직렬화 필드 추가(`paintPetals` 등)는 버렸다 | `Main.unity` |
| FMOD: JU 의 `birdsTweeting` 공간음(Spatialiser) 제거를 받고, FMOD Studio 2.03.15(Unity 플러그인과 같은 버전)로 뱅크를 다시 빌드했다. main 뱅크에는 공간음 제거가, JU 뱅크에는 main 의 BGM 이 빠져 있었다 | `FMODProject/.../Master.bank` |
| 스크립트, 텍스처, 커서 등 나머지 파일은 git 이 합친 그대로 | |

## 중요한 결정

- 새 뱅크는 76.4 → 49.6 MB 다. ADR 0037 의 인코딩 설정(긴 음악 8개 Vorbis)이 다시 빌드할 때 적용됐다. main 이 새로 넣은 BGM 7개(`bgm_st_*`)에는 아직 Vorbis 를 걸지 않았다.
- `Master.strings.bank` 는 main 것과 바이트가 같다. 이벤트 목록은 바뀌지 않았다.
- 기댄 통나무 쪽 무당벌레가 JU 프리팹으로 바뀌면서, `sam` 이 그 자리에서 줄였던 기어가는 거리(1초)는 빠졌다. 이스터에그 `StumpLadybug` 프리팹에는 남아 있다.

## 검증

| 무엇 | 결과 |
|------|------|
| 씬 안 로컬 fileID 참조, Transform 부모·자식 목록 | 끊긴 참조 0, 어긋남 0 |
| 에디터에서 다시 컴파일 | 성공 |
| 에디터에서 씬 다시 열기 | GameObject 127862개, Missing Script 0, 빈 참조 0 |
| FMOD 빌드 | `Build completed successfully` |
| Play Mode | 돌려 보지 않았다 |
