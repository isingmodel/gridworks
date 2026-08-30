# 현재 작업 범위

## 상태

**활성 scope가 없다.**

프로젝트 로컬 `godot-editor-ui` skill을 만들었다. Godot UI·Inspector·Scene tree·2D 배치 요청에 자동으로
발견되며 `$godot-editor-ui`로도 명시 호출할 수 있다. 실제 `./dev play layout` Editor와 DEBUG 게임창을
구분하고, Computer Use로 node 선택·Inspector/2D 편집·`⌘S` 저장 후 fresh normal game 재현까지 요구한다.

strict visual-layout의 exact ID·node type·uniform scale·world bounds·road point 불변조건, source gate 접점과
주변 구도 판단, 요청 시 중복 없는 `caffeinate -dims`, Editor를 남기고 검증 game만 닫는 절차도 포함했다.
`quick_validate.py`, placeholder·개발자 절대경로 audit, canonical command·scene·projection rule 대조와
`git diff --check`가 PASS했다. 게임 scene·art·runtime·Core 규칙과 전역 skill은 바꾸지 않았다. 이는 skill
구조와 프로젝트 절차의 정적 검증이며 실제 UI 변경이나 사람 미감 승인은 아니다. push·PR·merge·배포는
수행하지 않았다.

## 다음 변경을 여는 조건

- 읽기·설명·진단은 관련 질문 소유 문서를 read-only로 확인한다.
- 파일 변경은 사용자가 명시한 결과물 하나를 이 문서에 먼저 연다.
- 외부 gate 실행, push, PR, merge와 공개 배포는 각각 사용자의 명시적 권한이 있어야 한다.
- 시작 형식, 최소 검증과 종료 checklist는 [Agent 작업 안내](AGENT_GUIDE.md)를 따른다.

`NEXT_TASKS.md`, [외부 출시 gate](RELEASE_GATES.md), 준비된 코드와 과거 PASS는 자동으로
구현·평가·배포 권한을 만들지 않는다.
