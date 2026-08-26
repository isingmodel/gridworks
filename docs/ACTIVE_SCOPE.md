# 현재 작업 범위

## 상태

**활성 구현 scope가 없다.**

직전 scope에서 accepted-journal save에 하나의 application cursor를 더해, active incomplete chapter의
queue-empty `EventStory` 또는 `DecisionWindowStory`를 같은 authored modal로 재개했다. UI snapshot,
modal body와 raw transition queue는 저장하지 않는다.

## 현재 완료 사실

- current write schema v2는 deterministic story candidate prefix 중 닫은 개수인 required nonnegative
  32-bit `closedStoryCount`를 기록한다. 실제 파일명 `gridworks-r2-campaign-save-v1.json`은 그대로다.
- v1은 read-only로 복원하며 cursor가 없으므로 replay에서 projected candidate를 모두 닫은 story-idle로
  해석한다. probe/restore 자체는 원본을 rewrite하지 않고, Continue 뒤 정상 종료는 current v2 write
  정책을 따른다.
- `RealtimeChapterStoryFlow`의 live와 restore는 Core transition history + selected campaign의 같은 pure
  projection으로 candidate 순서, closed prefix와 active request를 만든다.
- story-idle 진행과 더불어 pending story queue가 없고 trigger minute == saved minute인 active
  `EventStory | DecisionWindowStory` 진행을 capture·title probe·Resume에서 같은 의미로 허용한다.
- live active-story capture는 같은 blocking Story modal·pause reason, AutoPaused와 Running/PlayerPaused
  `ModalRestore`까지 일치해야 한다. Continue는 저장한 interaction DTO 없이 authored request에서 같은 modal과
  AutoPaused를 재구성하고, 닫으면 PlayerPaused·Normal로 돌아간다. trigger minute를 지난 채 열린 story는
  저장하지 않는다.
- product fresh process는 `SECOND_HEART/FLOOD_ISOLATION_TEST` active story를 같은 modal로 복원한다.
  session harness는 exact Core snapshot/hash/journal/history와 닫기 뒤 다음 result→`SECOND_SOURCE` briefing의
  exact-once 미래까지 검증한다.
- prior exact standalone `FIRST_LIGHT`, product-only write ownership과 invalid/unsupported/I/O 원본 보존
  정책은 그대로다.

## 완료 검증

- focused `campaign-save-strict-replay`: 1 suite/105 assertions
- `./dev check`: Realtime 26 suites/1,182 assertions, Commercial 31 suites/7,084 assertions와 active
  `FLOOD_ISOLATION_TEST` product save-create→fresh Continue, prior v1와 blocked title smoke
- `dotnet build Gridworks.sln -c Release`: warning/error 0
- 전체 Godot UI harness: `REALTIME_R2_OFFSCREEN_CONTROL_TREE_PASS`
- 독립 code review: trigger-minute live gate, v1 read-only writer 경계와 v2 null/overflow strict finding 수정 후
  재검토에서 actionable finding 0
- 독립 Markdown audit 2회: current-facing 8개 문서 식별, 상대 링크 missing 0

## 아직 증명하지 않은 경계

- undelivered pending transition의 Core delivery state
- queued story suffix, initial/chapter briefing과 chapter result→next briefing handoff
- draft·catch-up debt·critical-incident auto-pause reason 저장
- 완료 run, finale/epilogue cursor와 완료 후 result/chapter/replay 선택
- overwrite/recovery UI와 지원 대상 v1 밖 구버전의 migration 정책, settings/audio, package, 공식 평가와
  사람 UX 판정
- 전체 8장 production-input 직접 여정, 비정상 종료 journal, 다중 slot, cloud save, push/PR/merge

다음 구현은 [남은 작업](NEXT_TASKS.md)에서 한 단계만 골라 이 문서에 수정 범위·단일 권위·완료 검사를
먼저 적은 뒤 시작한다.
