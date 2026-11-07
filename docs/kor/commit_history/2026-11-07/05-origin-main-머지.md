# origin/main 머지: 스테이지 2 아기 토끼 구간, BGM, 아이콘

## 왜 했나

`origin/main` 의 커밋 3개(`597bf99` 스테이지 2를 아기 토끼 찾기 구간으로, `bd626e2` fix bgm, `e695062` icons)를 `sam` 에 받는다. `sam` 쪽에는 웹 빌드 최적화(ADR 0037), 고드름 실로폰(ADR 0038), 사전 사진, 공터 되돌리기(04)가 있다.

## 무엇을 바꿨나

| 무엇 | 자리 |
|------|------|
| FMOD 뱅크 충돌은 main 것을 쓴다 (사용자 지시). main 이 BGM 8개를 새로 넣고 다시 빌드한 뱅크다 | `FMODProject/ThatGameJam/Build/Desktop/Master.bank`, `Master.strings.bank` |
| 씬 충돌은 오브젝트(fileID) 단위 3방향 머지로 풀었다. 한쪽만 바꾼 오브젝트는 그쪽을 따른다. 양쪽이 다 바꾼 오브젝트 6개는 아래처럼 정했다 | `Main.unity` |
| `RabbitBurrow` 의 자식 목록, 앉는 자리(`Perchable`)를 지운 것은 main 을 따른다. `sam` 쪽에서 붙인 `BurrowClick` 콜라이더는 그 앉는 자리에서만 쓰여서 함께 지웠다 | `Stages/Stage2/RabbitStage/RabbitBurrow` |
| 개울 물 메시 참조 2개는 main 것을 쓴다. 양쪽 메시는 내용이 같다 | `Stream_1_Water`, `Stream_2_Water` |
| 월드 숲은 `sam` 의 다시 심은 숲(04)을 쓴다. main 이 지운 나무 2그루와 같은 자리, 같은 프리팹의 나무를 지우고, 생성기 목록의 그 칸은 main 처럼 비웠다 | `01_SYSTEM/TreeGenerator` (로컬 x −35.80 z 15.67, x −35.72 z 40.46) |

## 중요한 결정

- 뱅크를 main 것으로 받아서, ADR 0037 의 "긴 음악 8개만 Vorbis" 설정은 이 뱅크에 들어 있지 않다. 웹 다운로드 크기를 다시 줄이려면 새 BGM 에 Vorbis 를 걸고 뱅크를 다시 빌드해야 한다.

## 검증

| 무엇 | 결과 |
|------|------|
| 충돌 표시 | 0 |
| 씬 안 로컬 fileID 참조 | 끊긴 참조 0 |
| Transform 부모·자식 목록 | 어긋남 0. SceneRoots 도 맞다 |
| 에디터에서 다시 컴파일 | 성공. 에러 없음 |
| 에디터에서 씬 다시 열기 | GameObject 127754개, Missing Script 0, 빈 참조 0 |
| Play Mode | 돌려 보지 않았다 |
