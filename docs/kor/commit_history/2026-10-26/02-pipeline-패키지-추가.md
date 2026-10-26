# Unity Pipeline 패키지와 `image.png` 추가 (ADR 0001)

## 왜 했나

Unity CLI 로 에디터를 조종하려면 `com.unity.pipeline` 이 필요하다. `image.png` 는 `sam` 에만 둔다([ADR 0001](../../decisions/0001-브랜치별-파일-배치.md)).

## 무엇을 바꿨나

| 무엇 | 자리 |
|------|------|
| `com.unity.pipeline` `0.9.1-exp.1` 추가 (`unity pipeline install`) | `Packages/manifest.json`, `Packages/packages-lock.json` |
| 이미지 | `image.png` |

## 검증

| 무엇 | 결과 |
|------|------|
| `unity status --until-ready` | 포트 7800, `ready` |
| `unity command editor status` | `"status":"ready"` |

## 막힌 것

`unity open` 으로 띄운 에디터는 라이선스 확인 직후 꺼졌다. 원인은 찾지 못했다. `Unity.exe -projectPath <경로>` 로 직접 실행하니 열렸다.
