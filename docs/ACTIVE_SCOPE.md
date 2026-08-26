# 현재 작업 범위

## 상태

**활성 구현 scope가 없다.**

직전 scope에서 기존 v1 accepted-journal save가 이미 exact replay하는 Core-owned active event와 active
duty를 제품 진행 저장 경계에 포함했다. schema·codec·store는 바꾸지 않았다.

## 현재 완료 사실

- `RealtimeSession.IsJournalRestorableProgressSnapshot` 하나가 accepted command, active incomplete chapter,
  pending-empty와 draft-free Core 경계를 capture·title probe·Resume에 동일하게 적용한다.
- application 경계는 active modal 없음, chapter story flow idle, epilogue 미시작, retained frame debt 없음과
  Running/PlayerPaused를 계속 요구한다.
- 모든 장의 stable 진행에 더해 story가 없거나 이미 닫혀 story flow가 idle인 active event·duty
  상태를 저장할 수 있다.
- Continue는 exact snapshot/hash/journal/ordered transition history를 player-paused·normal speed·no-modal로
  복원한다.
- 닫은 `FLOOD_ISOLATION_TEST` story를 재생하지 않고 다음 `SECOND_HEART` result와 `SECOND_SOURCE` briefing을
  각각 정확히 한 번 연다.
- undelivered pending transition은 v1 replay가 delivery cursor를 보존하지 못하므로 계속 fail-closed한다.
- prior exact standalone `FIRST_LIGHT`, product-only write ownership과 invalid/unsupported/I/O 원본 보존 정책은
  그대로다.

## 완료 검증

- focused strict replay: 1 suite/91 assertions, pending-transition negative 포함
- `./dev check`: Realtime 26 suites/1,168 assertions, Commercial 31 suites/7,084 assertions와 active event·duty
  product/legacy/blocked fresh-process smoke
- `dotnet build Gridworks.sln -c Release`: warning/error 0
- 전체 Godot UI harness: `REALTIME_R2_OFFSCREEN_CONTROL_TREE_PASS`
- 독립 review: accepted-command gate 중복과 result exact-once finding 2건 수정 후 재검토 통과

## 아직 증명하지 않은 경계

- undelivered pending transition의 Core delivery state
- queued/active story, result/briefing handoff의 application cursor
- draft·catch-up debt·application auto-pause reason 저장
- 완료 run, finale/epilogue cursor와 완료 후 result/chapter/replay 선택
- overwrite/recovery/migration UI, settings/audio, package, 공식 평가와 사람 UX 판정
- 전체 8장 production-input 직접 여정, 비정상 종료 journal, 다중 slot, cloud save, push/PR/merge

다음 구현은 [남은 작업](NEXT_TASKS.md)에서 한 단계만 골라 이 문서에 수정 범위·단일 권위·완료 검사를
먼저 적은 뒤 시작한다.
