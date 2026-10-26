# URP 템플릿 안내 파일 제거

## 왜 했나

`Assets/TutorialInfo/` 와 `Assets/Readme.asset` 은 URP 템플릿 안내다. 사용자가 필요 없다고 보고 에디터에서 지웠다.

## 무엇을 바꿨나

| 무엇 | 자리 |
|------|------|
| 안내 에셋, `Readme.cs`, `ReadmeEditor.cs`, 아이콘, 레이아웃 삭제 | `Assets/TutorialInfo/`, `Assets/Readme.asset` |

## 검증

| 무엇 | 결과 |
|------|------|
| `Readme.asset` 과 `Readme.cs` 의 GUID 를 `Assets/`, `ProjectSettings/` 의 씬·에셋·`.meta` 에서 찾았다 | 참조 없음 |
