# commit_history / decisions 작성 방식 (다른 프로젝트 이식용 예시)

Sangtachi 저장소가 지금 쓰는 두 기록 폴더의 형식을 정리한 파일이다. 다른 프로젝트에 같은 방식을 넣을 때 참고용으로 준다.
아래 README 와 예시는 Sangtachi 의 실제 파일을 그대로 옮겼다. 예시 안의 상대 링크는 이 파일 위치에서는 열리지 않는다.

## 1. 두 폴더의 역할

| 폴더 | 무엇을 남기나 | 언제 쓰나 |
|------|---------------|-----------|
| `commit_history/` | 한 커밋이 무엇을 왜 바꿨고, 어떻게 확인했고, 리뷰에서 무엇이 나왔는지 | 의미 있는 커밋마다 하나 |
| `decisions/` | 설계 결정(ADR). 고른 것, 버린 대안, 그 대가 | 설계 판단을 내릴 때마다 하나 |

공통 규칙:

- 둘 다 **역사 기록**이다. 현재 사양이 바뀌어도 다시 쓰지 않는다.
- 결정을 뒤집으면 기존 ADR 을 고치지 않고 새 ADR 을 만든다.
- 현재 값과 규칙의 출처는 살아 있는 문서(spec, architecture 등)다. 살아 있는 문서는 `commit_history/` 를 규칙의 출처로
  링크하지 않는다. `decisions/` 링크는 근거와 버린 대안을 보여 주는 용도로 허용한다.
- 한 세션에서 배운 관찰이나 교훈을 바로 영구 규칙으로 만들지 않고, 우선 `commit_history/` 기록에 남긴다.

## 2. 폴더 배치

Sangtachi 는 한국어 원본과 영어 미러를 둔다. 한 언어만 쓰는 프로젝트라면 `docs/commit_history/`, `docs/decisions/` 로
줄이고 README 의 언어 링크 줄을 지우면 된다.

~~~text
docs/
  kor/
    commit_history/
      README.md
      2026-10-23/
        01-두-집-실측-결과-반영.md
        04-방화벽-통과와-allowshadow.md
      2026-10-25/
        01-6-5-바이트-대조-통과.md
    decisions/
      README.md
      0032-바이트-대조를-파일-전송으로-한다.md
      0033-allowshadow-를-사용자에게-열지-않는다.md
  eng/
    commit_history/      (kor 와 같은 경로, 같은 파일명. 파일명도 한국어 그대로 둔다)
    decisions/           (파일명은 영어로 번역. 앞 번호 NNNN 으로 kor 와 짝짓는다)
~~~

## 3. commit_history/

### 3.1 README.md (실제 파일 그대로)

`docs/kor/commit_history/README.md`:

~~~markdown
# Commit History

의미 있는 작업 단위의 변경 내역을 날짜순으로 남깁니다.

폴더: `YYYY-MM-DD/`. 하루에 한 폴더입니다.

파일명: `NN-주제.md`. `NN` 은 그 날 폴더 안의 커밋 시각 순서이고 `01` 이 가장 이릅니다.

> English version: [`../../eng/commit_history/README.md`](../../eng/commit_history/README.md)
~~~

`docs/eng/commit_history/README.md`:

~~~markdown
# Commit History

Records of meaningful units of work, in date order.

Folder: `YYYY-MM-DD/`. One folder per day.

File name: `NN-topic.md`. `NN` is the commit-time order inside that day folder, and `01` is the earliest.

> Korean version: [`../../kor/commit_history/README.md`](../../kor/commit_history/README.md)
~~~

### 3.2 파일 규칙

- 경로: `commit_history/YYYY-MM-DD/NN-주제.md`. 하루에 폴더 하나.
- `NN` 은 그 날 커밋 순서. `01` 이 가장 이르다.
- 주제는 짧은 하이픈 구분 이름.
- 제목(`#`)은 한 줄로 무엇을 했는지. 관련 ADR 이 있으면 제목에 번호를 적는다. 예: `(ADR 0033)`.

### 3.3 절 구성

고정 양식은 아니다. 실제 기록에서 자주 쓰는 절은 아래와 같다. 필요한 절만 쓴다.

| 절 | 내용 |
|----|------|
| `## 왜 했나` | 계기. 누가 무엇을 물었나, 어떤 문서와 무엇이 어긋났나 |
| `## 무엇을 바꿨나` | `무엇 / 자리` 표. 바꾼 내용과 그 파일·절 |
| `## 중요한 결정` | 이번에 정한 것. 큰 결정이면 ADR 로 빼고 링크한다 |
| `## 검증` | `무엇 / 결과` 표. 실행한 명령과 그 결과(예: 문서 게이트 `VERDICT: pass`, 테스트 결과) |
| `## 크로스 모델 리뷰` | 리뷰한 모델, 리뷰 렌즈, 지적마다 반영 또는 기각과 그 이유, 마지막 확인 리뷰 결과 |
| `## 남은 것` | 이번에 하지 않은 일과 그 이유 |

크로스 모델 리뷰 절의 표 형식:

~~~markdown
| 등급 | 지적 | 처리 |
|------|------|------|
| warn | 지적 내용 | **반영.** 무엇을 고쳤나 |
| warn | 지적 내용 | **기각.** 왜 맞지 않는가. 근거가 되는 파일이나 규칙 |
~~~

### 3.4 빈 양식

~~~markdown
# <한 줄 요약> (ADR NNNN)

## 왜 했나

<계기>

## 무엇을 바꿨나

| 무엇 | 자리 |
|------|------|
| <바꾼 내용> | `<파일>` <절> |

## 검증

| 무엇 | 결과 |
|------|------|
| <명령 또는 확인 방법> | <결과> |

## 크로스 모델 리뷰

<모델>. <렌즈>.

| 지적 | 처리 |
|------|------|
| <등급>. <지적> | <반영 또는 기각과 이유> |

고친 뒤 전체 staged diff 의 확인 리뷰는 `<결과>` 였다.

## 남은 것

<없으면 이 절을 뺀다>
~~~

### 3.5 실제 예시

`docs/kor/commit_history/2026-10-23/04-방화벽-통과와-allowshadow.md`:

~~~markdown
# 방화벽 규칙의 트래픽 통과 반영, `-AllowShadow` 를 열지 않는다 (ADR 0033)

## 왜 했나

저장소 소유자가 "6-5 만 통과하면 Phase 1~7 이 닫히는가" 를 물었다. `plan.md` 와 살아 있는 문서를 대조하다 Phase 1~7 에 걸린 정리거리
둘을 찾았다.

- `windows-prereq.md` 2절이 클라이언트가 만든 방화벽 규칙이 트래픽을 통과시키는지 아직 보지 않았다고 한다. 2026-10-22 실측의
  6-2(가상 IP `ping` 양쪽 4/4)와 6-3(TCP 25565 로 64 MiB 전송 통과)이 그것을 봤다. 6-1 에서 B 는 규칙 둘을 새로 만들었다.
  결과를 반영한 [01](01-두-집-실측-결과-반영.md) 에서 놓쳤다
- `plan.md` 의 `-AllowShadow` 행이 "Phase 6 에서 서브넷 검사를 클라이언트에 이을 때" 정한다고 했으나 정하지 않은 채 Phase 6 이
  끝났다. 저장소 소유자가 사용자에게 열지 않기로 정했다

## 무엇을 바꿨나

| 무엇 | 자리 |
|------|------|
| 2절 검증을 실측으로. 트래픽 통과를 본 근거를 적고 머리말의 "아직 보지 않았다" 를 지웠다. 절 수를 실측 7, 부분 실측 3, 미검증 2, 사람 확인 1 로. 전에는 10절의 사람 확인을 미검증에 세었다 | `windows-prereq.md` 머리말, 2절 |
| 클라이언트는 `-AllowShadow` 를 사용자에게 열지 않는다 | `windows-prereq.md` 7절 |
| 위 결정과 버린 대안 | ADR 0033 |
| 대기 행을 3절 잔존 주소(Phase 8)만으로. `-AllowShadow` 행 삭제 | `plan.md` |

## 6-5 뒤에 Phase 1~7 에 남는 것

없다. `plan.md` 에 남은 대기(3절 잔존 주소, NFR-5 교수 확인, nat-probe 후속)와 구현 때 같이 볼 자리는 Phase 8 이후이거나 계기가
생길 때 보는 것이다.

## 검증

| 무엇 | 결과 |
|------|------|
| `docgate.py`, `--claims` | `VERDICT: pass` |
| 절 수 | 머리의 `검증:` 줄을 다시 셌다. 실측 7, 부분 실측 3, 미검증 2, 사람 확인 1 |
| 클라이언트가 `-AllowShadow` 를 넘기지 않음 | `client/` 에 그 문자열이 없다 |

## 크로스 모델 리뷰

Codex. 낡은 주장·정확성 렌즈와 근거·한영 미러 렌즈를 한 번에 돌렸다.

| 지적 | 처리 |
|------|------|
| warn. 10절은 "사람 확인" 이라 절 수의 미검증 3 이 틀렸다 | 반영(한·영). 미검증 2, 사람 확인 1 로 따로 센다 |

고친 뒤 전체 staged diff 의 확인 리뷰는 `LGTM - no blockers` 였다.
~~~

## 4. decisions/ (ADR)

### 4.1 README.md (실제 파일 그대로)

`docs/kor/decisions/README.md`:

~~~markdown
# Decisions

설계 결정 기록(ADR)을 `NNNN-제목.md` 형식으로 남깁니다.

각 문서 구성: 맥락 / 결정 / 대안 / 결과.

> English version: [`../../eng/decisions/README.md`](../../eng/decisions/README.md)
~~~

`docs/eng/decisions/README.md`:

~~~markdown
# Decisions

Architecture decision records go here as `NNNN-title.md`.

Each document has four sections: Context / Decision / Alternatives / Consequences.

> Korean version: [`../../kor/decisions/README.md`](../../kor/decisions/README.md)
~~~

### 4.2 파일 규칙

- 파일명: `NNNN-제목.md`. `NNNN` 은 0001 부터 하나씩 늘리는 번호. 번호는 다시 쓰지 않는다.
- 제목(`#`)은 `NNNN. 결정 한 문장`. 예: `# 0033. -AllowShadow 를 사용자에게 열지 않는다`.
- 제목 아래에 메타데이터 세 줄:
  - `- 상태: 채택` (영어: `- Status: accepted`)
  - `- 날짜: YYYY-MM-DD`
  - `- 관련:` 관련 살아 있는 문서의 절과 관련 ADR 링크
- 결정을 뒤집을 때는 기존 ADR 을 고치지 않는다. 새 ADR 을 만들고 `관련:` 에 이전 ADR 을 링크한다.

### 4.3 절 구성

네 절은 모든 ADR 에 있다.

| 절 | 내용 |
|----|------|
| `## 맥락` | 무엇이 문제였고 왜 지금 정해야 했나 |
| `## 결정` | 고른 것. 누가 정했는지(예: 저장소 소유자)도 적는다 |
| `## 대안` | `**대안 N. ...**` 마다 무엇이고 왜 버렸나 |
| `## 결과` | `**얻는 것.**` 과 `**치르는 것.**` 목록. 이 결정을 다시 열 조건이 있으면 여기 적는다 |

범위를 분명히 해야 하면 `## 이 ADR 이 정하지 않는 것` 을 더할 수 있다.

### 4.4 빈 양식

~~~markdown
# NNNN. <결정 한 문장>

- 상태: 채택
- 날짜: YYYY-MM-DD
- 관련: [`../<문서>.md`](../<문서>.md) <절>, [ADR NNNN](NNNN-<제목>.md)

## 맥락

<문제와 지금 정해야 하는 이유>

## 결정

<누가 정했나>

**<결정>.** <구체적 범위>

## 대안

**대안 1. <대안>.**
버렸다. <이유와 trade-off>

## 결과

**얻는 것.**

- <장점>

**치르는 것.**

- <대가>
~~~

### 4.5 실제 예시

`docs/kor/decisions/0033-allowshadow-를-사용자에게-열지-않는다.md`:

~~~markdown
# 0033. `-AllowShadow` 를 사용자에게 열지 않는다

- 상태: 채택
- 날짜: 2026-10-23
- 관련: [`../windows-prereq.md`](../windows-prereq.md) 7절, [`../architecture.md`](../architecture.md) 7장 어댑터 수명, [ADR 0026](0026-phase-6-착수-전-결정.md)

## 맥락

`windows-prereq.md` 7절의 서브넷 충돌 검사는 우리 `/24` 가 더 짧은 기존 라우트(예 `10.0.0.0/8`)의 대역을 가리면 차단한다
(`BLOCK-shadow`). 그 대역이 실제로 비어 있다는 것을 사람이 알 때를 위해 스크립트에 `-AllowShadow` 가 있다. 클라이언트가 이
검사를 부를 때 이 우회를 사용자에게 열지는 Phase 6 에서 정하기로 했으나 정하지 않은 채 Phase 6 이 끝났다. 지금 클라이언트는
이 우회를 넘기지 않는다.

## 결정

저장소 소유자가 정했다.

**클라이언트는 `-AllowShadow` 를 사용자에게 열지 않는다.** 기동 인자, 콘솔 명령, 설정 어디에도 두지 않는다. 클라이언트가 부르는
검사는 늘 기본값(차단)이다. `-AllowShadow` 는 사람이 스크립트를 직접 돌릴 때만 쓴다.

## 대안

**대안 1. 기동 인자로 연다.**
버렸다. 사내 VPN 처럼 넓은 대역을 쓰는 기기에서도 쓸 수 있게 된다. 대가는 그 대역이 비었는지를 사용자가 판단해야 하고,
틀리면 연결된 동안 그 대역의 실제 호스트에 닿지 못한다는 것이다. 7절은 "동작하는데 남의 것을 끊는 것이 동작하지 않는 것보다
낫지 않다" 를 기준으로 둔다.

## 결과

**얻는 것.**

- 클라이언트가 남의 대역을 가리는 경로가 없다
- 지금 코드 그대로다. 바꿀 코드가 없다

**치르는 것.**

- 우리 대역을 덮는 더 짧은 라우트가 있는 기기에서는 그 인터페이스를 내리기 전에는 v1 을 쓸 수 없다. 7절의 "한계" 와 같은 대가다
~~~
