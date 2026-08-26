# 현재 작업 범위

## 상태

**active in-chapter story save/Continue scope가 활성화됐다.**

기존 accepted-journal save에 하나의 application cursor만 더해, active incomplete chapter에서
`EventStory` 또는 `DecisionWindowStory`가 열린 진행을 같은 authored modal로 재개한다. UI
snapshot·modal body·raw transition queue를 저장하지 않는다.

## 수정 범위

- current save schema v2의 `closedStoryCount`: deterministic story candidate prefix 중 이미 닫은 개수
- v1 save는 당시 capture 계약에 맞게 모든 replay candidate를 닫은 story-idle 상태로 해석
- `RealtimeChapterStoryFlow`의 live/restore가 transition + selected campaign만 쓰는 하나의 pure candidate
  projection을 공유
- `RealtimeSession`이 story-idle 또는 queue-empty active in-chapter story만 capture·title probe·Resume에서
  같은 의미로 허용
- 실제 product fresh-process save-create→title Continue에서 같은 authored story 복원
- 기존 active event·duty future exact smoke를 active-story capture 경계로 옮겨 닫기 후의 미래까지 검증
- 이 단계의 current fact 문서와 완료 이력

## 단일 권위

- Core source·journal·hash replay와 v1/v2 wire shape: `RealtimeCampaignSaveCodec`
- authored story candidate 순서·closed prefix·active request 재구성: `RealtimeChapterStoryFlow`
- capture 상태·resume 의미·paused-under-modal 정책: `RealtimeSession`
- product title probe·write ownership·atomic store lifecycle: `RealtimeSliceMain`

Main과 title view는 story count를 계산하거나 modal 내용을 복제하지 않는다.

## 유지할 경계

- Core predicate의 command count > 0, active incomplete chapter, pending-empty, draft-free 조건은 변경하지 않음
- story-idle은 `closedStoryCount == projected candidate count`
- active story는 `closedStoryCount == projected candidate count - 1`, queue-empty,
  `EventStory | DecisionWindowStory`, trigger minute == saved minute에서만 허용
- active story Continue는 같은 authored modal을 먼저 열고, 닫으면 player-paused·normal speed로 복귀
- 이전 story를 다시 열거나 cursor에 없는 story를 건너뛰지 않음
- 현재 product write는 v2, prior v1은 bytes rewrite·migration 없이 읽음
- 실제 파일 경로 `gridworks-r2-campaign-save-v1.json`은 그대로 유지해 dual-path probe를 만들지 않음
- unknown schema, source/hash/replay 불일치와 I/O 실패의 fail-closed·원본 보존 정책 유지

## 범위 밖

- undelivered Core pending transition 정규화·delivery state 저장
- queued story, chapter result·next briefing handoff와 initial briefing 저장
- 완료 run, final result·epilogue cursor와 완료 후 result/chapter/replay 선택
- draft·catch-up debt·critical-incident 자동 일시정지 상태 저장
- overwrite/recovery UI, settings/audio, package, 공식 평가, 사람 UX 판정, push/PR/merge

## 완료 검사

- strict codec suite에서 v2 `closedStoryCount` shape·round-trip·invalid/unknown schema와 v1 호환을
  기존 suite 안에서 검증한다.
- actual `ProductCampaign`의 `FLOOD_ISOLATION_TEST` active story를 capture하고 fresh process title
  Continue가 같은 authored modal과 exact Core state를 복원한다.
- restored story를 닫으면 player-paused·normal speed가 되고 같은 story가 다시 열리지 않는다.
- uninterrupted/restored run의 snapshot·hash·journal·ordered transition history와 다음 chapter result→
  briefing이 exact-once로 같다.
- story-idle v2, prior standalone `FIRST_LIGHT` v1, invalid/unsupported/I/O와 product-only write 회귀를
  유지한다.
- `./dev check`, root Release build, 전체 Godot UI harness와 독립 review를 통과한다.

이 단계는 in-chapter active story 하나만 연다. queued story, result/briefing handoff, raw pending,
completion 또는 전체 transient save를 완료했다고 주장하지 않는다.
