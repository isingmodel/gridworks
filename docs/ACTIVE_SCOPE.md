# 현재 작업 범위

## 상태

**활성 scope가 없다.**

current source ownership 정렬을 완료했다. `Gridworks.Core`는 current V2/V3만, `Gridworks.LegacyCore`는
historical rules만 물리적으로 소유한다. current world contract·presenter·renderer·authoring scene은
`game/realtime/r2/world/` 한 feature slice에 있고, deterministic Godot 관찰 코드는 Debug-only
`game/realtime/evidence/`에서 controller/UI/product-entry/checkpoint feature로 나뉜다. 정확한 변경 경로는
[개발 구조](ARCHITECTURE.md)가 소유한다.

## 다음 변경을 여는 조건

- 읽기·설명·진단은 관련 질문 소유 문서를 read-only로 확인한다.
- 파일 변경은 사용자가 명시한 결과물 하나를 이 문서에 먼저 연다.
- 외부 gate 실행, push, PR, merge와 공개 배포는 각각 사용자의 명시적 권한이 있어야 한다.
- 시작 형식, 최소 검증과 종료 checklist는 [Agent 작업 안내](AGENT_GUIDE.md)를 따른다.

`NEXT_TASKS.md`, [외부 출시 gate](RELEASE_GATES.md), 준비된 코드와 과거 PASS는 자동으로
구현·평가·배포 권한을 만들지 않는다.
