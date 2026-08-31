# 현재 작업 범위

## 상태

**Godot controller/UI evidence lane 분리 scope가 활성 상태다.**

## 단일 결과물

`./dev check`는 deterministic controller suite를 한 번만 실행해 검증된 terminal save를 fresh product-title
process에 넘기며, 개발자는 controller case와 UI harness를 서로 독립적으로 실행할 수 있다.

## 단일 권위

- controller smoke case 목록·실행·terminal fixture: `RealtimeR2Smoke`와 전용 runner
- 전체 gate의 evidence 순서와 임시 fixture lifecycle: `dev`
- UI-only integration evidence: `RealtimeUiLayoutHarness`

## 범위 안

- controller smoke를 전용 headless scene으로 분리하고 exact case 선택을 fail-closed하게 제공한다.
- UI harness에서 controller suite 실행을 제거해 UI evidence만 소유하게 한다.
- all-controller PASS 뒤에만 fresh path에 terminal save를 쓰고 별도 product-title process가 이를 probe한다.
- 인자 없는 전체 gate의 기존 controller·save/Continue·UI·checkpoint 사실을 유지하면서 full campaign replay를
  한 번만 실행한다.
- 새 controller/UI 최소 검사 명령과 evidence ownership을 current 실행·구조 문서에 기록한다.

## 범위 밖

- Core·게임 규칙, authored content, presentation 의미, UI layout, input, save/settings wire와 audio 변경
- 기존 검사의 삭제, assertion 축소, 병렬화 또는 fresh-process persistence 경계 축소
- 일반 play build graph, package, qualification, 외부 device·사람 gate 변경
- push, PR, merge와 배포

## 완료 검사

- exact controller case 하나가 단독 PASS하고 unknown case는 전체 허용 목록과 함께 fail-closed한다.
- all-controller runner가 기존 case를 모두 PASS한 뒤에만 absent output에 current terminal save를 생성하고,
  existing/relative output은 거부한다.
- 별도 product-title process가 그 exact bytes를 completed로 probe하고 변경하지 않으며 UI-only harness가 PASS한다.
- 인자 없는 `./dev check`가 full-route controller marker를 한 번만 남기고 기존 전체 회귀를 통과한다.
- terminal route/minute/hash/command/story cursor, 관련 current 문서와 `git diff --check`가 유지된다.
