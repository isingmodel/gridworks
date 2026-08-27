# 현재 작업 범위

## 상태

**활성 scope가 없다.**

`FIRST_LIGHT` 학습 루프 보완을 완료했다. native release 경로에는 공사 완료·첫 공급 시험·결과까지의
문맥형 진행 action, 종결 결과의 다시 하기·임무 나가기, 실제 다음 행동을 보여 주는 더 크고 밝은 하단
안내, 올바른 원/만 원 표기, 부분 진행 실패와 성공 payoff 요약이 있다. Core 규칙, 시간, 결과 판정과
save schema는 바꾸지 않았다.

Debug build와 누적 full realtime layout/controller harness가 PASS했다. 이후 native window에서 발견한
single-modal replay lifecycle을 보완하고 final build와 실제 재시작을 다시 확인했다. clean success,
invalid-span recovery, partial-build failure의 native LLM playtest 3회는 각각 성공 payoff, 빨간 수치 오류의
즉시 회복, `진행 1/3` 실패와 17:00 briefing 재시작을 관찰했다. 이는 human evidence가 아니다.

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
