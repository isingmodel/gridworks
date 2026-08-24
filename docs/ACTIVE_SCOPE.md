# 현재 작업 범위

## 상태

**R2 구조 문서 정합성 후속 수정 scope가 활성화됐다.**

이 scope는 두 독립 문서 검토에서 확인된 current 문서의 과장·누락·삭제 식별자를 실제 `dev`와 코드에
맞춘다. runtime, 개발 명령의 동작, gameplay와 historical evidence는 바꾸지 않는다.

## 수정 가능한 범위

- `README.md`, `docs/README.md`, `docs/ARCHITECTURE.md`, `docs/ACTIVE_SCOPE.md`
- 문서 소유권 경계를 함께 설명하는 `docs/archive/COMPLETED_HISTORY.md`
- canonical wrapper 예시를 소유한 `docs/product/COMMERCIAL_UX_EVALUATION_PROTOCOL_KO.md`와
  `tools/commercial-ux/README.md`

## 범위 밖

- `dev`, solution/project, runtime·test code와 resource 변경
- test 종류나 native chapter coverage 확대
- historical playtest/evidence 원문 재작성
- branch 밖 사용자 소유 untracked 파일

## 단일 권위와 완료 검사

- 실행 명령과 포함 범위: `dev`
- runtime ownership: current R2 code와 project allowlist
- 문서 질문 소유권: `docs/README.md`
- `git diff --check`, `./dev help`, stale 표현 검색과 tracked Markdown local-link 검사를 통과한다.
- 두 독립 subagent가 실제 수정 필요성을 먼저 판정한 항목만 최소 범위로 고친다.

## 이 상태에서 할 수 있는 일

- README와 현재 문서를 읽어 사실을 확인한다.
- build, 기존 자동검사와 비파괴 진단을 실행한다.
- 사용자가 요청한 범위 안에서 결함을 조사하거나 계획을 제안한다.

다음 행동은 별도 사용자 지시 없이 시작하지 않는다.

- [남은 작업](NEXT_TASKS.md)의 구현
- native 직접 플레이 또는 LLM judge session 생성
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
