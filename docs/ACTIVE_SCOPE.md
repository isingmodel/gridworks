# 현재 작업 범위

## 상태

**active event·duty save/Continue scope가 활성화됐다.**

기존 v1 accepted-journal save가 이미 exact replay하는 Core-owned active event와 active duty를 제품 진행
저장 경계에 포함한다. story/application cursor나 schema를 추가하지 않고, 현재 한 shared session predicate의
두 과도한 차단 조건만 제거한다.

## 수정 범위

- `RealtimeSession`의 stable 전용 이름을 journal-restorable progress 계약으로 바꾸고 active event·duty 허용
- capture, title probe와 Resume가 계속 같은 predicate를 사용
- 실제 누적 product route의 mid-event/duty save→restore와 future event 종료 exact 회귀
- 기존 product fresh-process save-create→Continue를 대표 active event/duty 상태로 전진
- 이 단계의 current fact 문서와 완료 이력

## 단일 권위

- gameplay event·duty·accepted journal·canonical hash: `RealtimeCampaignRun`
- v1 strict decode·deterministic replay: `RealtimeCampaignSaveCodec`
- journal-restorable capture·paused resume와 story-idle 정책: `RealtimeSession`
- product title probe·write ownership·store lifecycle: `RealtimeSliceMain`

## 유지할 경계

- accepted Core command 1개 이상, active incomplete chapter, no node/line draft
- `PendingTransitions.Count == 0`; undelivered public-transition cursor는 v1 replay가 보존하지 못함
- active modal 없음, chapter story flow idle, epilogue 미시작, retained frame debt 없음
- Running 또는 PlayerPaused에서만 capture; Continue는 player-paused·normal speed·no-modal
- v1 schema/source/store와 prior exact standalone `FIRST_LIGHT` 호환 정책은 변경하지 않음

## 범위 밖

- undelivered pending transition, queued/active story, result/briefing handoff와 application cursor
- draft·catch-up debt·application auto-pause reason 저장
- 완료 run, finale/epilogue cursor와 완료 후 result/chapter/replay 선택
- overwrite/recovery/migration UI, settings/audio, package, 공식 평가, 사람 UX 판정, push/PR/merge

## 완료 검사

- actual `ProductCampaign`의 story-idle active event+duty 상태를 v1 save로 capture한다.
- restore가 snapshot/hash/journal/ordered transition history를 exact 복원하고 paused·normal speed·no-modal로
  시작한다.
- uninterrupted run과 restored run이 event 종료까지 같은 hash·transition·outcome을 만든다.
- 이미 닫은 event story를 다시 열지 않고 다음 authored story만 정확히 한 번 연다.
- undelivered pending transition과 나머지 범위 밖 상태는 계속 fail-closed한다.
- 기존 8장 stable replay, prior standalone `FIRST_LIGHT`, invalid/unsupported/I/O 회귀를 유지한다.
- focused 검사, Debug/Release build, `./dev check`와 독립 review를 통과한다.

이 단계는 Core-owned active event·duty만 추가한다. pending delivery cursor, story/result/handoff, completion 또는
전체 transient save를 완료했다고 주장하지 않는다.
