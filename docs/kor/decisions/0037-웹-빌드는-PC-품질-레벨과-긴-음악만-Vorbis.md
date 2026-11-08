# 0037. 웹 빌드는 PC 품질 레벨 하나를 쓰고, 긴 음악만 Vorbis 로 압축한다

- 상태: 채택
- 날짜: 2026-11-07
- 관련: [`../implementation_plan.md`](../implementation_plan.md)

## 맥락

디렉터가 웹 빌드 최적화를 시켰다. 감사해 보니 두 가지가 먼저 걸렸다.

- `QualitySettings` 의 WebGL 기본 레벨은 `Mobile`(`Mobile_RPAsset`, `Mobile_Renderer`)이었다. 에디터는 `PC` 로 돌았다. 스테이지 3 의 `ForestMist`, `DreamFog`, `AboveFog`, `BabyPrism` 렌더러 기능은 `PC_Renderer` 에만 있어서 웹 빌드에서는 안개와 아기 프리즘이 나오지 않았다. `Mobile_Renderer` 에는 깊이 프리패스도 없어서 `OcclusionFade` 가 나무 창을 부드럽게 만들지 못했다.
- JU 가 `e351405`("change sound to pcm")에서 FMOD Desktop 인코딩을 PCM 으로 바꿨다. `Master.bank` 가 42.6 MB 가 됐다. 긴 음악 몇 개가 대부분이다.

처음에는 `Mobile_Renderer` 에 빠진 기능을 복사해 웹 렌더러로 썼다(`AgentScripts/SyncWebRenderer.cs`). 플레이해 보니 그림자가 에디터보다 크게 나빴다(1024, 캐스케이드 1, Soft 끔). Unity 문서에는 웹이 Mobile 레벨을 써야 한다는 규칙이 없다. 낮은 레벨을 권장할 뿐이다. 그래서 다시 정했다.

## 결정

1. WebGL 과 에디터 모두 `PC` 품질 레벨을 쓴다. 에디터에서 보는 모습이 웹과 같고, 렌더러 기능을 한 곳에만 넣으면 된다.
2. `PC` 에서 웹에 비싸고 쓰지 않는 것을 뺀다.
   - SSAO 를 끈다. 수채화 셰이더가 AO 를 읽지 않는다.
   - Opaque Texture 를 끈다. 읽는 셰이더가 없다.
   - Forward+ 를 Forward 로 바꾼다. 켜진 라이트가 Directional 하나다.
   - 그림자: 2048, 캐스케이드 4 → 2, 거리 50 → 40 m(보이는 깊이 약 40 m), Soft High → Medium.
   - 깊이 프리패스를 강제한다(`Depth Texture Mode: Force Prepass`). 전에는 SSAO 의 DepthNormals 가 프리패스를 만들어 `OcclusionFade` 가 우연히 동작했다.
3. FMOD 는 Streaming 이 켜진 긴 파일 8 개(BGM 4, 컷신 2, 바람, 새소리)만 Desktop 에서 Vorbis(quality 40)로 덮어쓴다. 짧은 효과음은 PCM 그대로 둔다(사용자 결정). 디코딩 비용과 지연이 없다.

## 대안

**대안 1. `Mobile` 을 웹 레벨로 두고 그림자만 올린다.**
버렸다. 에디터와 웹이 다른 렌더러를 쓰면 이번 안개처럼 기능이 한쪽에만 들어가는 일이 다시 생긴다. 렌더 스케일 0.8 의 이득은 `PC` 의 렌더 스케일로도 얻을 수 있다.

**대안 2. FMOD 전체를 Vorbis 로 되돌린다.**
버렸다. JU 가 PCM 으로 바꾼 의도(짧은 효과음의 지연과 CPU)를 버린다.

## 결과

**얻는 것.**

- 웹 빌드에 안개, 꿈 안개, 안개 위 레이어, 아기 프리즘이 나오고, 그림자와 나무 창이 에디터와 같다.
- `Master.bank` 42.6 MB → 13.9 MB.

**치르는 것.**

- 웹의 GPU 부하가 `Mobile` 보다 크다(렌더 스케일 1.0, 안개 기능, 깊이 프리패스). FPS 가 모자라면 렌더 스케일부터 낮춘다.
- 팀원 에디터도 이 설정으로 보인다.
- `Mobile_Renderer` 에 복사한 기능은 남아 있지만 지금은 쓰지 않는다.
