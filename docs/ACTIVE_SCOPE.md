# 현재 작업 범위

## 상태

**활성 scope가 없다.**

Godot controller/UI evidence lane 분리를 완료했다. `./dev check controller [EXACT_CASE]`와
`./dev check ui`는 각각 controller와 UI integration evidence를 독립 실행하며, 인자 없는 전체 gate는
controller suite가 검증한 terminal save를 fresh product-title process에 전달한다. controller case 목록과
fixture 생성, UI harness, 전체 evidence orchestration은 서로 다른 단일 권위를 가진다. 실행 형태와 증거
경계는 [실행 안내](../INSTALL.md), current ownership은 [개발 구조](ARCHITECTURE.md)가 소유한다.

## 다음 변경을 여는 조건

- 읽기·설명·진단은 관련 질문 소유 문서를 read-only로 확인한다.
- 파일 변경은 사용자가 명시한 결과물 하나를 이 문서에 먼저 연다.
- 외부 gate 실행, push, PR, merge와 공개 배포는 각각 사용자의 명시적 권한이 있어야 한다.
- 시작 형식, 최소 검증과 종료 checklist는 [Agent 작업 안내](AGENT_GUIDE.md)를 따른다.

`NEXT_TASKS.md`, [외부 출시 gate](RELEASE_GATES.md), 준비된 코드와 과거 PASS는 자동으로
구현·평가·배포 권한을 만들지 않는다.
