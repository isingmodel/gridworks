# 현재 작업 범위

## 상태

**활성 scope가 없다.**

문서 온보딩 정리를 완료했다. 새 agent의 공통 진입 순서는 루트 `README.md` → 이 문서 →
`AGENT_GUIDE.md` → `README.md`의 질문 소유권 표다. current R2 코드 변경이 필요할 때만
`ARCHITECTURE.md`의 ownership과 변경 경로를 추가로 읽는다.

저장소가 소유하는 제품 목표는 실시간 8장·finale/epilogue, product save/settings/audio wiring,
internal macOS package identity와 combined 2B를 포함하는 **current R2 내부 후보**다.
[남은 구현 작업](NEXT_TASKS.md)은 현재 비어 있다.

## 다음 변경을 여는 조건

- 읽기·설명·진단은 관련 질문 소유 문서를 read-only로 확인한다.
- 파일 변경은 사용자가 명시한 결과물 하나를 이 문서에 먼저 연다.
- 외부 gate 실행, push, PR, merge와 공개 배포는 각각 사용자의 명시적 권한이 있어야 한다.
- 시작 형식, 최소 검증과 종료 checklist는 [Agent 작업 안내](AGENT_GUIDE.md)를 따른다.

`NEXT_TASKS.md`, [외부 출시 gate](RELEASE_GATES.md), 준비된 코드와 과거 PASS는 자동으로
구현·평가·배포 권한을 만들지 않는다.
