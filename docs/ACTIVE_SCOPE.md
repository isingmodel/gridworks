# 현재 작업 범위

## 상태

**chapter result→next briefing save/Continue scope가 활성화됐다.**

current v2 `closedStoryCount` 의미를 바꾸거나 필드를 더하지 않고, non-final chapter result와 그 Core
transition batch가 이미 만든 bounded next-briefing suffix를 복원한다. 긴 calendar gap의 result도 닫을 때
exact next chapter minute로 전진해 같은 handoff를 만든다.

## 수정 범위

- `RealtimeChapterStoryFlow`가 first unclosed candidate를 active로, 뒤의 제한된 handoff suffix를 pending으로
  live/restore에서 같은 순서로 유지
- 기존 queue-empty `EventStory | DecisionWindowStory` 외에 exact-minute non-final `ChapterResult`, 이어지는
  `ChapterBriefing`, optional same-chapter `DecisionWindowStory`만 허용
- `RealtimeSession`의 Core/story 조합 gate가 active started chapter와 typed between-chapter result를 구분
- product smoke의 같은 save path를 active FLOOD story→`SECOND_HEART` result→`SECOND_SOURCE` briefing으로
  fresh process마다 atomic v2 write/Continue
- session smoke에서 zero-gap handoff와 `SECOND_SOURCE`→`NORTH_BANK_PROMISE` long-gap handoff의 FIFO exact-once
  확인
- 이 단계의 current fact 문서와 완료 이력

## 단일 권위

- Core source·journal·hash replay와 unchanged v1/v2 wire shape: `RealtimeCampaignSaveCodec`
- authored candidate prefix와 bounded result/briefing suffix: `RealtimeChapterStoryFlow`
- Core snapshot + story position, live interaction과 Resume 정책: `RealtimeSession`
- product title probe·write ownership·atomic store lifecycle: `RealtimeSliceMain`

Main과 title view는 handoff phase, candidate count나 modal body를 계산하지 않는다.

## 유지할 경계

- v2 cursor는 계속 required nonnegative 32-bit `closedStoryCount` 하나이고 current writes만 v2
- active와 restored handoff suffix의 trigger minute는 saved minute와 같고 authored campaign 순서와 일치
- active result는 non-final이며 마지막 completed chapter와 일치; next briefing은 바로 다음 started chapter
- zero-gap result는 이미 queued인 briefing과 optional decision을 FIFO로 열고, long-gap result는 닫을 때
  `ChapterStartMinute`로 한 번 전진한 뒤 같은 FIFO를 만든다.
- story-idle save는 계속 started chapter만 허용하고 between-chapter는 active non-final result에서만 허용
- blocking Story modal·pause reason·AutoPaused·restorable Running/PlayerPaused interaction gate 유지
- v1 all-closed read-only, product-only writes, actual filename과 invalid/unsupported/I/O 원본 보존 유지

## 범위 밖

- initial briefing, arbitrary in-chapter queued event/story suffix와 overshot trigger
- undelivered Core pending transition과 raw delivery cursor
- final result, completed run, finale/epilogue cursor와 완료 후 result/chapter/replay 선택
- draft·catch-up debt·critical-incident auto-pause state
- overwrite/recovery UI, settings/audio, package, 공식 평가, 사람 UX 판정, push/PR/merge

## 완료 검사

- 기존 R2 smoke가 zero-gap result save/Resume→next briefing과 long-gap result save/Resume→briefing→decision
  FIFO를 exact snapshot/hash/journal/history와 함께 검증한다.
- 기존 product save path의 첫 fresh Continue가 active FLOOD regression을 유지하면서 active result v2를 쓰고,
  두 번째 fresh Continue가 같은 result를 복원해 `SECOND_SOURCE` briefing을 정확히 한 번 연다.
- current active-story, story-idle v2, prior standalone `FIRST_LIGHT` v1, invalid/unsupported/I/O와 product-only
  write 회귀를 유지한다.
- 새 schema·field·test suite를 만들지 않는다.
- `./dev check`, root Release build, 전체 Godot UI harness와 독립 review를 통과한다.

이 단계는 result→next briefing의 bounded application suffix만 연다. general queued story, raw pending,
completion 또는 전체 transient save를 완료했다고 주장하지 않는다.
