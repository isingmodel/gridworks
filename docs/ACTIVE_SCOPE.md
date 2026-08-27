# 현재 작업 범위

## 상태

**활성: `FIRST_LIGHT` 학습 루프 보완.**

## 승인된 결과

`FIRST_LIGHT`를 긴 수동 대기 없이 끝내고 다시 시작할 수 있으며, 안내·금액·결과 피드백이
플레이어가 실제로 만든 진행 상태를 정확히 설명한다.

## 권한

- 시간 건너뛰기와 재시작·복귀 동작은 기존 realtime session, typed intent/action, application
  transition 경로가 소유한다.
- 안내와 결과 문구는 realtime presentation helper와 기존 UI presenter가 소유한다.
- Core 상태를 복제하거나 규칙을 바꾸지 않는 범위에서 `FIRST_LIGHT` 전용 layout 상수를 보완한다.

## 범위 안

- `FIRST_LIGHT` 공사와 사건 beat에 문맥형 시간 진행 action 추가
- 종결 `FIRST_LIGHT` 결과에 보이는 다시 하기와 복귀/종료 action 추가
- 하단 안내의 크기, 대비와 위계 개선
- 현금 값을 올바른 원/만 원 단위로 표시
- 실패 결과가 부분 진행을 인정하고 성공 결과가 더 분명한 payoff 요약을 제공하도록 보완
- 재미 clean success, UI invalid-span recovery, design partial-build failure의 같은 native 평가 3회 재실행

## 범위 밖

- 새 Core 규칙, 공사·사건 시간, 결과 판정 변경
- save schema, 자동 routing, 새 chapter 또는 campaign 재설계
- 새 art, animation, audio 또는 전면 UI 재설계
- publish, 외부 release gate, push, PR 또는 merge

## 완료 확인

- 가장 작은 기존 deterministic smoke가 Core 소유권을 바꾸지 않고 새 action과 결과 전이를 증명한다.
- focused check 뒤 기존 build와 full layout harness를 한 번 통과한다.
- 새 native run 3회가 재미, UI, design 시나리오를 끝내고 관찰을 human evidence가 아닌 LLM
  playtest evidence로 기록한다.

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
