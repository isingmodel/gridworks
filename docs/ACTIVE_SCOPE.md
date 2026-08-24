# 현재 작업 범위

## 상태

**활성 구현 scope가 없다.**

두 독립 subagent가 필요성을 확인한 R2 구조 문서 정합성 수정은 완료됐다. current 실행 명령의 실제 범위,
문서 질문 소유권, input/content/presentation 경계와 canonical wrapper 예시를 코드에 맞췄다. runtime,
개발 명령의 동작, gameplay와 historical evidence는 바꾸지 않았다. 추가 작업은 사용자가 새 scope를
명시하기 전까지 허용되지 않는다.

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
