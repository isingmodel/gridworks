# 현재 작업 범위

## 상태

**활성 구현 scope가 없다.**

누적 4장 production-input 직접 플레이는 완료됐다. 서로 독립된 fresh process의
`./dev play through NORTH_BANK_PROMISE`에서 Keep과 명시적 Defer를 각각 authored 결과까지 진행했다.
첫 3장의 실제 망·완공/진행 공사·자금·시계가 보존됐고, 6개월 달력 전환 뒤 약속 기한과 주변 사건
상세를 실제 화면에서 확인했다.

Keep 경로는 canonical formative chapter·full-flow evidence를 기록했다. Defer는 authored Defer 결과까지
관찰했으며, 설계상 Keep 전용 PASS evidence를 만들지 않는다. production 입력에서 결함이 재현되지 않아
gameplay code와 regression은 변경하지 않았다.

이 관찰은 누적 4장 native 도달성만 증명한다. 남은 4장, save/resume, 전체 캠페인, 사람 참가자의
미감·사용성, 공식 UX 점수 또는 package 품질의 증거로 확대하지 않는다. 완료 요약은
[완료 이력](archive/COMPLETED_HISTORY.md), 다음 후보는 [남은 작업](NEXT_TASKS.md)이 소유한다.

## 이 상태에서 할 수 있는 일

- README와 현재 문서를 읽어 사실을 확인한다.
- build, 기존 자동검사와 비파괴 진단을 실행한다.
- 사용자가 요청한 범위 안에서 결함을 조사하거나 계획을 제안한다.

다음 행동은 별도 active scope 없이 시작하지 않는다.

- [남은 작업](NEXT_TASKS.md)의 구현이나 추가 native 직접 플레이
- LLM judge session 생성
- 새 에셋 생성·교체
- save/package/release 작업
- branch 통합, push, PR 또는 공개 배포

## 새 scope를 여는 방법

사용자가 다음 작업을 선택하면 구현 전에 이 문서를 갱신한다.

1. 플레이어가 얻게 될 하나의 결과
2. 수정 가능한 파일과 범위 밖 파일
3. 사용할 단일 데이터·규칙 권위
4. unit/checkpoint/E2E 중 필요한 검증
5. 완료로 인정할 관찰과 아직 주장하지 않을 증거

backlog 항목, 과거 완료 scope, 준비된 schema나 테스트 파일은 그 자체로 작업 승인이 아니다.
