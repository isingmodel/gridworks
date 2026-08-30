# 현재 작업 범위

## 상태

**활성 scope가 없다.**

Godot Editor-native 시각 배치 scope는 완료됐다. `./dev play layout`은 게임 실행 창이 아니라 실제 Godot
Editor에서 strict `RealtimeVisualLayoutAuthoring.tscn`을 연다. 실제 campus `Sprite2D`, river/bridge/road
context, district footprint와 label을 2D 뷰에 표시하며, `Sprite2D` transform/scale과 `Line2D` point가 normal
renderer가 읽는 단일 시각 배치 권위다. 이전 DEBUG 게임창 handle overlay와 JSON authority는 제거했다.

실제 Editor Scene tree에서 `HOSPITAL_TERMINAL`을 선택하고 Inspector Position을 `(2505,1390)`으로 바꿔
`⌘S` 저장했다. Editor를 종료한 뒤 fresh normal `FIRST_LIGHT`에서 같은 배치가 재현되는 것을 확인하고 두
화면을 캡처했다. strict scene projection은 exact ID, node type, metadata, uniform scale, style와 bounds를
검증한다. Debug/Release build, 전체 UI harness와 `./dev check`가 PASS했다. Core node·전력망 기하·반경·열·
경제·story·save schema는 바꾸지 않았다. 이는 실제 Editor 조작 근거이며 사람 미감 승인이나 package/release
gate는 아니다. push·PR·merge·배포는 수행하지 않았다.

## 다음 변경을 여는 조건

- 읽기·설명·진단은 관련 질문 소유 문서를 read-only로 확인한다.
- 파일 변경은 사용자가 명시한 결과물 하나를 이 문서에 먼저 연다.
- 외부 gate 실행, push, PR, merge와 공개 배포는 각각 사용자의 명시적 권한이 있어야 한다.
- 시작 형식, 최소 검증과 종료 checklist는 [Agent 작업 안내](AGENT_GUIDE.md)를 따른다.

`NEXT_TASKS.md`, [외부 출시 gate](RELEASE_GATES.md), 준비된 코드와 과거 PASS는 자동으로
구현·평가·배포 권한을 만들지 않는다.
