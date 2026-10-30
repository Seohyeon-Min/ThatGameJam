# main을 JU로 머지

## 왜 했나

저장소 소유자 요청: "메인이랑 머지해야함" → "메인에서 업데이트된 걸 JU 브랜치로 가져와야함." `JU`와 `main`의 공통 조상은 `d3bad79`("baby movement", 이 세션 초반에 본 그 자동 커밋)였고, 그 사이 main은 팀(Tommy, Seohyeon)이 워터컬러 셰이더·시즌 전환·대량 나무 생성 등으로 크게 앞서 있었다. main에는 이미 `ff31958 Bring JU's baby and fade content into Main...`이라는, 제 d3bad79 시점 작업(PointOfInterest 59개, 균형대 마커, 카메라 페이드)을 Seohyeon이 git 머지가 아니라 **수동으로 포팅**해 둔 커밋이 있었다 — 그 이후 제가 추가한 8개 커밋(블렌드 버그 수정, 떨림 조정, 잎 카메라 페이드, 새 뱅크 방향, 점프 애니메이션)은 main에 전혀 반영돼 있지 않았다.

## 무엇을 했나

### 1. UnityYAMLMerge 연결

`.gitattributes`에 `*.unity`/`*.prefab`/`*.asset`/`*.mat` 등을 `merge=unityyamlmerge`로 지정하고, 로컬 git 설정에 Unity 에디터가 포함한 `UnityYAMLMerge.exe`를 머지 드라이버로 등록했다. 일반 텍스트 머지가 fileID/GUID 구조를 겉보기엔 멀쩡한 YAML로 조용히 망가뜨릴 수 있어서다.

- **처음엔 멈췄다.** 헤드리스(`-h`) 플래그를 안 줘서 충돌마다 GUI 오류창이 뜨려다 응답 없는 상태로 멈췄다(같은 PID로 몇 분째 안 바뀜 → 강제 종료 후 `-h -p --fallback none`으로 재설정).

### 2. 충돌 8개 해결

| 파일 | 처리 |
|------|------|
| `.gitattributes` | add/add. 양쪽 내용 단순 결합 |
| `Perchable.cs` | 양쪽이 서로 다른 필드를 같은 자리에 추가(제 `alwaysExempt`, main의 `alignBird`). 둘 다 유지 |
| `GameTuning.asset` | **UnityYAMLMerge가 충돌 표시 없이 조용히 제(JU) 쪽 값을 선택**했다. `babyWalkSpeed`(main 0.9 vs 제 1.2), `birdSpeed`(5 vs 6), `birdAltitudeSpeed`(2.8 vs 3) — main이 실제로 튜닝해 둔 값을 베이스·main·결과물을 필드 단위로 전부 대조해서 발견, main 값으로 고침 |
| `Sam_Test.unity` | **구조적으로 너무 갈라져서(제 219개 vs main의 1153개 오브젝트) 자동 머지가 사실상 불가능**했다. 저장소 소유자에게 직접 물어 "제 JU 버전 유지"로 결정 — main의 인트로/추가 스테이지/대량 나무/터레인은 이번엔 반영 안 함 |
| `BillboardLeaf.mat` 등 4개 | main에만 새로 생긴 파일들인데, git의 이름 유사도 기반 rename 추정이 제 쪽에서 지운 플레이스홀더 파일들과 엉뚱하게 짝지어 가짜 충돌을 만들었다. main 내용 그대로 사용 |
| `Baby.cs`, `BirdController.cs` | 충돌 없이 자동 병합됨. 양쪽 변경사항(제 JumpTo/떨림 강도/시선, main의 Idle 상태·착지 시 방향 정렬)이 결과물에 다 들어있는지 직접 확인 |

`TreeLeaves_1~4.mat`(제가 카메라 페이드를 켠 플레이스홀더 잎 머티리얼)은 main이 그 플레이스홀더 체계 전체를 교체하면서(자체적으로 "나무 머티리얼의 카메라 페이드 켜기"까지 이미 해결) 삭제됐다 — 제 수정은 자연스럽게 흡수됨.

### 3. 머지 뒤 깨진 것 수리

`Sam_Test.unity`를 제 쪽으로 유지하면서, main이 삭제한 "4번 나무"(Tree_4_Bare/Leafy) 참조가 끊겨 missing prefab 에러 38건이 났다. `TreeGenerator` 4개에서 빈 4번 슬롯을 빼고 재생성(`Generate()`), 그 뒤 `AddBabyGazeTargets.cs`를 다시 돌려 새로 생긴 나무들에 PointOfInterest를 재부착(56개 새로 추가 + 기존 랜드마크 3개 스킵 = 59개 그대로). 또한 main의 새 시즌 전환 기능(`SeasonSnow`)이 `season` 필드가 비어 있어 `NullReferenceException`을 내고 있어서, 이미 씬에 있던 `GameFlow`의 `Season` 컴포넌트로 연결해 고쳤다.

## 검증

| 무엇 | 결과 |
|------|------|
| 머지 뒤 전체 리임포트(`AssetDatabase.Refresh(ForceUpdate)`) + 재컴파일 | 에러 0, 컴파일 실패 없음 |
| Play Mode에서 missing prefab 수리 전/후 콘솔 에러 수 | 수리 전: Tree_4 관련 에러 다수 + `SeasonSnow` NRE. 수리 후: 0 |
| Play Mode에서 10초 이상 관찰 | 아기 깨기→걷기→통나무 오르기(점프 애니메이션 포함, y좌표가 점프 중 실제로 바뀜) 정상 동작 |
| `git log --graph` | 머지 커밋이 main의 히스토리를 정상적으로 편입함 확인 |

## 남은 것

- `Sam_Test.unity`는 의도적으로 main의 인트로/추가 스테이지/대량 나무·터레인을 받지 않았다. 나중에 그쪽 작업이 필요하면 Seohyeon이 해온 것처럼 수동 포팅이 필요할 것.
- FMOD `event:/SFX/Bird` 중복 키 경고를 리임포트 직후 한 번 봤는데, 이후 재현 안 됨(일회성 리임포트 부작용으로 보임, 지켜볼 것).
- push는 아직 안 함.
