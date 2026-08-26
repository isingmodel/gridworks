# 현재 작업 범위

## 상태

**initial briefing/zero-command bootstrap save·Continue scope가 활성화됐다.**

cumulative route의 첫 authored briefing을 Session의 synthetic special case가 아니라
`RealtimeChapterStoryFlow`의 첫 counted candidate로 만든다. new-game bootstrap에서 Core initial transition
batch를 exact current minute에 한 번 drain해 raw pending 상태를 application 경계 밖으로 없애고, active
initial briefing과 그 modal을 닫은 exact initial story-idle 상태를 저장·재개한다.

## 수정 범위

- `RealtimeChapterStoryFlow`가 first `ChapterStarted`도 `CHAPTER_BRIEFING` ID의 candidate로 투영하고 이후
  briefing/event/decision/result와 같은 cursor·close 경로 사용
- cumulative fresh Session만 neutral interaction에서 `AdvanceTo(run.Minute)`로 initial pending batch를 한 번
  drain·observe한 뒤 Flow active request를 열고, `Present()`의 synthetic initial request 분기 제거
- current write schema를 동일 wire fields의 v3로 올림: v3 cursor는 initial briefing을 포함, prior v2는
  read-only로 restore 결과에서 checked `+1`, prior v1은 read-only all-closed
- active initial `c0`과 닫힌 exact-initial story-idle `c1`의 zero-command capture/Resume; 다른 zero-command와
  ambiguous prior v1/v2 empty journal은 fail-closed
- product 같은 save path를 initial briefing→active FLOOD→`SECOND_HEART` result→`SECOND_SOURCE` briefing으로
  fresh process마다 current write하며 기존 v1·blocked-save 회귀 유지
- 이 단계의 current fact 문서와 완료 이력

## 단일 권위

- v1/v2/v3 strict wire와 prior cursor normalization: `RealtimeCampaignSaveCodec`
- authored candidate projection·closed prefix·active request: `RealtimeChapterStoryFlow`
- cumulative bootstrap delivery, Core/story 조합과 Resume interaction: `RealtimeSession`
- product title probe·write ownership·atomic store lifecycle: `RealtimeSliceMain`

Main과 title view는 initial phase, candidate count나 modal body를 계산하지 않는다.

## 유지할 경계

- v3는 필드를 추가하지 않고 required nonnegative 32-bit `closedStoryCount` 의미만 initial-inclusive로 명시
- v2 object의 raw cursor는 보존하고 Restore 결과만 checked `+1`; overflow·v2 write는 거부
- v1 cursor 없음은 새 initial candidate까지 모두 닫힌 상태이며 prior schema probe/restore는 원본을 rewrite하지
  않고 정상 종료 때만 v3로 씀
- zero-command은 cumulative first chapter가 시작된 exact `ChapterStartMinute`, completed/event/duty/construction/
  pending/draft 없음, active initial 또는 closed-idle 두 상태만 허용
- cumulative new session만 Core pending batch를 한 번 drain; Resume replay와 standalone/fixture synthetic
  briefing은 중복 drain하거나 Flow에 섞지 않음
- 기존 story-idle, active Event/Decision, bounded result→briefing(+decision), interaction·product-only write
  불변식 유지

## 범위 밖

- raw undelivered Core transition 자체의 persistence나 delivery cursor
- arbitrary/general queued story suffix와 overshot trigger
- zero-command initial boundary 이후의 시간 진행
- final result, completed run, finale/epilogue cursor와 완료 후 result/chapter/replay 선택
- draft·frame/catch-up debt·critical-incident auto-pause state
- overwrite/recovery UI, settings/audio, package, 공식 평가, 사람 UX 판정, push/PR/merge

## 완료 검사

- 기존 R2 smoke가 initial pending drain exact-once, active c0 Resume→same authored modal, close c1
  Resume→PlayerPaused/no-modal과 v2 cursor normalization/tamper 거부를 확인한다.
- 기존 product save path에 initial create/Continue 한 단계를 앞에 붙이고 이후 FLOOD→result→briefing chain의
  fresh-process disk write를 유지한다.
- 기존 Core save suite가 empty-journal v3 roundtrip, v2 raw/read-only→normalized restore, v1 all-closed/read-only,
  prior empty-journal·overflow·unsupported v4 거부를 검증한다.
- 새 wire field와 새 test suite를 만들지 않는다.
- `./dev check`, root Release build, 전체 Godot UI harness와 독립 review를 통과한다.

이 단계는 first briefing을 cumulative Flow의 단일 권위로 합치고 exact initial save seam만 연다. general
queued story, raw pending persistence, completion 또는 전체 transient save를 완료했다고 주장하지 않는다.
