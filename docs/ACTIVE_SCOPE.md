# 현재 작업 범위

## 상태

**활성 구현 scope가 없다.**

제품 title과 새 게임 경로는 완료됐다. 인자 없는 기본 장면은 session 없는 title을 열고, production
`새 게임` 입력은 authored `FIRST_LIGHT` briefing으로 진입한다. R2 저장 권위가 없으므로 `이어하기`는
이유와 함께 비활성이다. 명시적 fixture, checkpoint와 native 개발 route는 product boot와 분리됐다.

이 완료는 build와 deterministic production-input smoke의 증거다. 사람 미감·사용성, save/resume,
fresh-install 또는 전체 캠페인 완결성을 주장하지 않는다. 완료 요약은
[완료 이력](archive/COMPLETED_HISTORY.md), 다음 후보는 [남은 작업](NEXT_TASKS.md)이 소유한다.

## 이 상태에서 할 수 있는 일

- README와 현재 문서를 읽어 사실을 확인한다.
- build, 기존 자동검사와 비파괴 진단을 실행한다.
- 사용자가 요청한 범위 안에서 결함을 조사하거나 계획을 제안한다.

다음 행동은 별도 active scope 없이 시작하지 않는다.

- [남은 작업](NEXT_TASKS.md)의 구현이나 native 직접 플레이
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
