# 현재 작업 범위

## 상태

**활성 구현 scope가 없다.**

직전 initial briefing/zero-command bootstrap save·Continue scope는 완료됐다. cumulative route의 첫
`ChapterStarted`를 `RealtimeChapterStoryFlow`의 `CHAPTER_BRIEFING` candidate로 합치고, fresh cumulative
Session만 Core initial transition batch를 exact current minute에 한 번 drain한다. cumulative `Present()`의
synthetic initial 분기는 제거했고 standalone/fixture first briefing은 기존 경계를 유지한다.

current write schema는 같은 wire fields의 v3다. v3 `closedStoryCount`는 initial-inclusive이고, prior v2는
raw cursor를 보존한 채 Restore 결과만 checked `+1`, prior v1은 all-closed로 읽는다. prior schema write와
ambiguous empty journal, v2 overflow는 fail-closed한다. exact initial은 pending/event/duty/completion/
construction/draft가 없는 zero-command active `c0` 또는 closed story-idle `c1`만 저장·재개한다.

같은 product save path에서 initial briefing create→첫 fresh Continue의 active `FLOOD_ISOLATION_TEST` c3
write→둘째 Continue의 `SECOND_HEART` result c4 write→셋째 Continue의 `SECOND_SOURCE` briefing c5 write를
실제 disk reload로 확인했다. prior `FIRST_LIGHT` v1과 invalid/unsupported/I/O 차단 경로도 유지했다.

Debug/Release build, `./dev check`의 Realtime 26 suites/1,197 assertions와 Commercial 31 suites/7,084
assertions, 전체 Godot UI harness와 두 독립 review를 통과했다. 두 review가 command-bearing same-minute
`c1`의 cursor만 `c0`으로 바꾸면 initial briefing이 부활하던 P2를 독립적으로 찾았고, active initial은
command 수와 무관하게 exact zero-command snapshot을 요구하도록 수정한 뒤 finding 없이 재검토됐다.

## 유지되는 경계

- undelivered Core pending transition, general queued story suffix, final/completed run·finale·epilogue cursor와
  완료 후 result/chapter/replay 선택은 아직 지원하지 않는다.
- explicit chapter/through/fixture 개발 경로는 product save를 읽거나 쓰지 않는다.
- overwrite/recovery UI, settings/audio, package, 공식 평가와 사람 UX 판정은 포함되지 않았다.
- push, PR, merge는 이 완료에 포함되지 않았다.

다음 구현은 별도 active scope를 먼저 열어야 한다. [남은 작업](NEXT_TASKS.md)은 후보와 순서를 소유하지만
그 자체로 구현 권한을 만들지 않는다.
