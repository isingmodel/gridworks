# Gridworks 완료 이력

> 이 문서는 완료·중단된 task의 **유일한 요약**이다. 현재 구현 권한이나 남은 작업을 정의하지 않는다.
> 세부 scope, 실행 로그와 당시 문구는 Git 이력과 `playtests/`에 남아 있다.

## 1. 초기 prototype과 release v1

초기 Godot slice에서 다음 규칙을 분리해 검증했다.

- 변전소 service area는 연결 자격이며 발전원이 아니다.
- 전기적으로 다른 회로도 같은 공간 회랑 사고에 함께 영향을 받을 수 있다.
- 공사 중 설비는 무전압이고, 한 공사는 원자적으로 완공된다.

이후 33×21 격자 기반 `ReleaseMain`은 분기·합류, 공유 정격, 사건 projection, 8개 임무,
저장·재개와 ad-hoc macOS 내부 ZIP까지 연결했다. 이 빌드는 현재 제품이 아닌 동결 기술 기준선이다.

## 2. 상용 V2 단계 B–G

별도 `CommercialMain`과 V2 Core에서 다음을 완료했다.

- 자유 배치, 점유영역·수면·건물·선로 기하와 제한된 camera
- 선로·전신주 접속부·변전소의 연속/비상 열 한계, 보호정지·냉각
- 안전 의무·도시 약속·결정 기한·국면 preview와 save v3
- 동일한 망·자금·선택을 잇는 8개 임무, 결과와 epilogue
- 병목 설명, 발주 checklist, 설정·접근성·날씨·초상·음향
- 빈 user-data의 내부 macOS 후보에서 저장, fresh continue, 전체 캠페인과 완료 후 재개

규칙·wiring·내부 package 검증은 완료했지만 사람 전체 플레이, 한국어·전력설비 전문 교정,
Developer ID 서명·공증과 공개 출시는 승인되지 않았다. V2 저장과 패키지는 현재 R2의 저장·패키지가
아니다.

## 3. 실시간 전환 R0–R2

### R0 — 방향 전환

turn/승인 중심 흐름을 fixed-tick realtime으로 바꾸는 계약을 만들었다. pause·1×·2×·4×, 계속 흐르는
공사와 예고 사건을 제품의 중심으로 정했다.

### R1 — 실시간 Core

`FIRST_LIGHT` vertical slice에서 결정론적 시계, 공사, 사건 경계, 세 설비군의 과부하→보호정지→복귀,
forecast와 actual 상태를 구현했다. 이 단계는 Core 규칙 기준선이며 당시 Game UI, 제품 데이터와
persistence는 범위 밖이었다.

### R2 — 실시간 UX 기반

reducer, 상단 HUD, 수평 사건 시간축, 조건부 inspector/build/action UI와 code-native world를 만들었다.
초기 R2 종료 gate는 사용자 지시로 중단됐지만 이후 작업은 이 기반을 보존해 현재 live R2로 발전했다.
중단 당시의 “비기본 장면”이나 “전체 미완료” 문구는 현재 상태가 아니다.

## 4. 상용 UX 평가 기반

### UX-R0 — text-plan 형성평가

8장/16개 사건을 실시간 일정에 결속하고, briefing·window·result·epilogue를 34개 story part로 단독
실행할 수 있게 했다. 세 fresh judge의 두 번째 안정 panel은 `TextPlanProxy = 83.4475`였다. 이는 계획
평가이며 native 게임 점수나 공식 상용 UX 점수가 아니다.

### UX-R1 — 비점수 평가 도구

targeted checkpoint, story-part unit, session/attempt, evidence chain과 fail-closed 비점수 도구를
구축하고 독립 검토를 마쳤다. 로컬에서 요청 model/effort를 확인하는 controlled transcript도 만들었지만
platform attestation, 실제 judge 실행 또는 공식 점수를 주장하지 않았다.

### UX-R2.1 — 첫 장과 한 줄 future-event bar

실제 release `FIRST_LIGHT`의 briefing→live play→authored result를 R2에 연결했다. 사건·공사·결정·열
경계를 한 줄 chronological rail의 compact marker로 합치고 hover/선택 상세 구조를 만들었다. 첫 장과
두 targeted checkpoint는 실제 macOS 입력으로 비점수 관찰했다.

### UX-R2.2 — 튜토리얼 3장

`FIRST_LIGHT → SECOND_HEART → SECOND_SOURCE`를 동일한 망·현금·시계에서 이어지게 했다. 병원 2회선,
범람 안전 회랑과 전체 경로 용량을 장 전환과 authored result에 연결했다. fresh process의 production
mouse/keyboard 입력으로 3장 누적 경로를 끝까지 관찰했다.

### UX-R2.3 — 네 번째 장

`NORTH_BANK_PROMISE`까지의 누적 4장 경로를 구현했다. 6개월 달력 전환이 이전 망·공사·자금을
보존하며, 약속 마감이 같은 한 줄 rail에 표시되고 Keep/Defer가 Core 결과로 이어진다. 자동검사와
독립 review는 완료했지만 사용자 중단 지시에 따라 4장의 native 직접 플레이는 수행하지 않았다.

### UX-R2.4 — 제품 title과 새 게임 진입

인자 없는 기본 장면이 session을 만들기 전에 제품 title을 표시하도록 했다. 저장 권위가 없으므로
`이어하기`는 이유와 함께 비활성이고, production `새 게임` button 입력은 canonical `FIRST_LIGHT`와
authored briefing을 연다. `RealtimeLaunchCatalog`가 이 product boot를 명시적 DEBUG fixture,
checkpoint와 세 native 개발 route에서 분리한다.

작은 headless smoke가 실제 default scene의 pointer 입력, title input ownership, briefing wiring과
fixture/native resource 경계를 확인했다. 이는 자동 wiring 증거이며 사람 미감·사용성, fresh-install,
save/resume 또는 전체 캠페인 완결성의 증거가 아니다.

### UX-R2.5 — 누적 4장 production-input 직접 플레이

`./dev play through NORTH_BANK_PROMISE`를 서로 독립된 fresh process에서 Keep과 명시적 Defer로 각각
결과까지 진행했다. 첫 3장의 실제 망·완공/진행 공사·자금·시계가 네 번째 장에 보존됐고, 6개월 달력
전환, 약속 기한과 주변 사건 상세, 두 authored 결과를 production mouse/keyboard 입력으로 확인했다.

Keep은 canonical formative chapter·full-flow evidence를 기록했다. Defer는 authored Defer 결과로
도달성을 관찰했으며 설계상 Keep 전용 PASS evidence를 만들지 않는다. 재현된 결함이 없어 gameplay
code와 regression은 바꾸지 않았다. 이는 사람 참가자의 미감·사용성, 남은 4장, save/resume, 전체
캠페인 또는 공식 UX 점수의 증거가 아니다.

### UX-R2.6 — 다섯 번째 장 native 구현

`WHOSE_MARGIN`까지 앞선 망·공사·자금·시계를 잇는 누적 5장 경로를 구현했다. briefing, 두 planning
window와 세 사건을 authored reveal 순서로 진행하며, 산업 야간 증산 약속은 실제 duty가 있는
`NIGHT_SHIFT`에만 표시된다. 보강 회랑 Keep과 명시적 Defer가 각각 exact authored result로 이어지고,
Keep만 5장 full-flow evidence를 만든다.

일반 회랑에서는 Core가 기록한 비상 노출·보호정지·복귀를 자산별 stable rail marker와 detail history로
투영해 약속 실패가 성공 결과로 보이지 않게 했다. 이 연결은 chapter ID별 Session/Main/UI 분기 없이
typed promise fact와 Core transition history를 사용하는 공통 presentation 경로에 놓였다. Release
build, `./dev check`, WHOSE_MARGIN story selector, 누적 Godot UI harness와 독립 review를 통과했다.
production-input 직접 플레이의 관찰 상한은 여전히 `NORTH_BANK_PROMISE`까지 4장이며, 남은 3장,
save/resume, 전체 캠페인 또는 사람 UX 품질의 증거는 아니다.

### UX-R2.7 — 여섯 번째 장 native 구현

`BEFORE_WATER_RISE`까지 앞선 망·공사·자금·시계와 결과를 잇는 누적 6장 경로를 구현했다. 이미 상속된
동부 접속 2회를 새 공사로 세지 않고, 범람 구역을 피하는 남부 고지대 보완 회랑을 완공했다. Keep은
의료원·정수장·동부 생활권을, 명시적 Defer는 필수시설을 공급한 exact authored result로 이어지며
Keep만 6장 full-flow evidence를 만든다.

forecast와 active flood는 `RIVER_FLOOD_ZONE` 및 `WEST_SOURCE_NODE` 사용 불가를 보이고, active thermal
결과에서 실제 수요가 남부 발전원으로 공급되는 것까지 확인했다. Release build, `./dev check`,
BEFORE_WATER_RISE story selector, 누적 Godot UI harness와 두 독립 review를 통과했으며 canonical native
route는 3개를 유지했다. production-input 직접 플레이의 관찰 상한은 여전히 `NORTH_BANK_PROMISE`까지
4장이고, 남은 2장이나 전체 캠페인·사람 UX 품질의 증거로 확대하지 않는다.

### UX-R2.8 — 일곱 번째 장 native 구현

`SWITCH_OFF_TO_PROTECT`까지 앞선 망·공사·자금·시계와 결과를 잇는 누적 7장 경로를 구현했다. 267270분에
이전 종료 현금을 상속하고 1,600,000원 grant를 더한 뒤, 기존 정수장 player corridor 1/2에서 범람 장의
`(1950, 850)` 보강 전신주→새 `(2300, 900)` 소형 변전소→정수장 보강 회선을 267417분까지 완공해
첫 사건 시작인 267690분에 접속 2/2를 고정했다.

267690–267810분 계획정지에는 서부 전원을 제외하고 남부 3,900 kW로 의료원 1,800 kW·정수장
1,400 kW·동부 700 kW를 연속 공급했으며 비상·보호정지를 만들지 않았다. 267870–267990분 복귀에는
서부 전원을 다시 사용해 의료원·정수장 각 900 kW와 동부 700 kW를 연속 공급했다. exact standard
result, 7장의 ordered result와 full-flow evidence, canonical 3-route cap을 Release build, story selector,
`./dev check`, 누적 Godot UI harness와 두 독립 review로 확인했다. production-input 직접 플레이의 관찰
상한은 여전히 `NORTH_BANK_PROMISE`까지 4장이며 남은 native 장은 1개다.

### UX-R2.9 — 여덟 번째 장 native 구현

`LONGEST_NIGHT`까지 앞선 망·공사·자금·시계와 결과를 잇는 누적 8장 경로를 구현했다. 267990분에
이전 종료 현금과 망을 상속하고 1,800,000원 grant를 더한 뒤, 새 공사나 Core command 없이 briefing과
`FINAL_OPERATING_PLAN_WINDOW`를 닫고 세 사건을 진행했다. 최대수요에서는 authored 5개 수요를 모두
연속 공급했고, 폭염 정점에서는 의료원 1,600 kW·정수장 1,400 kW를 공급하며 같은 실제 회선의
268770분 비상 진입→268845분 보호정지→268935분 복귀를 기록했다. 마지막 범람 국면은
`RIVER_FLOOD_ZONE`을 반영하면서 의료원·정수장 각 900 kW를 모든 duty segment에서 공급하고 새
비상·보호정지를 만들지 않았다. 회선은 범람 시작 15분 전에 실제 복귀하므로 마지막 국면에 비상 설비가
멈춘다고 말하는 authored planning 문구와 Core timing 사이에는 차이가 남는다. strict V2 문구를
재작성하거나 불필요한 공사로 정지 구간을 늘리지 않고, authored card와 실제 열 기록을 각 권위대로
보존했다.

exact standard result, 8장의 ordered result와 full-flow evidence, `CampaignComplete=true`, canonical
3-route cap을 Release build, 세 LONGEST_NIGHT story selector, `./dev check`, 누적 Godot UI harness와
두 독립 review로 확인했다. 추가 authored native 장은 없지만 production-input 직접 플레이의 관찰 상한은
여전히 `NORTH_BANK_PROMISE`까지 4장이다. finale·epilogue, save/package, 사람 UX 품질이나 공식 점수의
증거로 확대하지 않는다.

### UX-R2.10 — finale와 세 epilogue card

8장 cumulative route의 exact `LONGEST_NIGHT` standard result를 별도 카드로 복제하지 않고 finale로
유지했다. 성공한 finale를 닫으면 strict base campaign의 city report→medical witness→closing 세 카드가
한 번씩 순서대로 열리고, 마지막 카드를 닫으면 기존 완료 망을 `Ended` 읽기 전용으로 보여 준다. 실패한
마지막 장은 성공 epilogue에 진입하지 않는다.

chapter story queue에 epilogue 목적을 섞지 않고 작은 `RealtimeEpilogueFlow`가 세 카드의 순서와 완료만
소유한다. city report는 authored promise line을 completed chapter outcome과 generic join해 세 Keep/Defer
문장과 남은 운영 자금을 표시한다. production flow에는 chapter ID·고유 수요처 분기가 없으며, finale
close부터 epilogue 종료까지 Core canonical hash, 시각, command count, 현금과 망이 바뀌지 않는다.

Debug/Release build, `./dev check`, 전체 누적 Godot UI harness와 두 독립 review를 통과했다. 이 완료는
명시적 8장 개발 route의 completion presentation만 증명하며, 제품 title의 standalone `FIRST_LIGHT`,
save/resume, 완료 후 result/chapter/replay 선택, package, production-input 직접 관찰 또는 사람 UX 품질의
증거를 바꾸지 않는다.

### UX-R2.11 — standalone FIRST_LIGHT stable progress save·Continue

standalone `FIRST_LIGHT`에서 briefing을 닫고 Core command가 하나 이상 수락된 stable mid-construction
상태를 정상 종료해 한 current R2 save에 atomic하게 기록했다. save는 canonical route와 base world/
campaign, realtime world/overlay, selected/full composed campaign source hash, saved minute, ordered accepted
journal과 final canonical hash만 담고 snapshot이나 immutable seed를 중복 저장하지 않는다. strict decode와
결정론적 replay가 ordered transition history와 다음 advance/command까지 uninterrupted run과 같음을
검증한다.

저장 파일이 없으면 `새 게임`만, 유효 save면 `이어하기`만 활성화한다. 형식 손상·지원하지 않는 schema/
version·route/source/hash/replay 불일치·I/O 실패에서는 두 action을 차단하고 원본을 보존한다. 별도 fresh
process의 `이어하기`는 exact clock·cash·world·construction·
journal/hash를 player-paused·normal speed·no-modal 상태로 복원한다. Debug/Release build, strict Core suite,
`./dev check`, 전체 Godot UI harness와 독립 review를 통과했고 review의 uppercase hash canonicalization
finding을 수정했다. 이 완료는 사건·장 전환·완료 save, 누적 8장 product 새 게임, overwrite/recovery UI,
package, production-input 직접 관찰이나 사람 UX 품질의 증거로 확대하지 않는다.

### UX-R2.12 — 누적 8장 product stable save·Continue

저장 파일이 없는 제품 title의 `새 게임`을 canonical `FIRST_LIGHT`→`LONGEST_NIGHT` 누적 8장
`ProductCampaign`에 연결했다. session 없는 product title/Main만 기존 v1 accepted-journal save를
probe·strict restore하고, title New Game/Continue가 만든 product-owned session만 정상 종료 때 쓴다.
명시적 chapter/through/fixture 개발 실행은 같은 native route여도 product save lifecycle을 소유하지
않는다. native data가 한 곳에서 canonical source identity를 만들고, active와 pending story가 모두 없는
상태까지 stable capture 계약에 포함했다.

모든 장의 stable in-progress 상태를 source·journal·hash·ordered transition history까지 exact replay한다.
별도 fresh process의 product save-create→Continue는 clock·cash·world·construction을
player-paused·normal speed·no-modal 상태로 복원하며, 직전 exact-current standalone `FIRST_LIGHT` v1 save도
원 route 그대로 Continue할 수 있다. migration은 하지 않는다. 형식 손상·지원하지 않는 schema/version·
다른 route/source/hash/replay·I/O 실패는 원본을 보존하고 두 title action을 차단한다.

Debug/Release build, `./dev check`, 전체 Godot UI harness와 독립 review를 통과했다. review에서 찾은
stale/programmatic title action의 availability 우회는 handler 재검사로 수정했다. 이 완료는 사건·duty·
story·result/handoff·완료 save, overwrite/recovery UI, 완료 후 선택, 전체 8장 production-input 직접 여정,
package 또는 사람 UX 품질의 증거로 확대하지 않는다.

### UX-R2.13 — active event·duty progress save·Continue

기존 v1 accepted-journal이 이미 exact replay하는 Core-owned active event와 active duty를 product 저장
경계에 포함했다. `RealtimeSession.IsJournalRestorableProgressSnapshot` 하나가 accepted command·active
incomplete chapter·pending-empty·draft-free 조건을 capture, title probe와 Resume에 공통 적용한다. active
modal, queued/active story, epilogue, retained frame debt와 completion은 계속 차단하며 schema·codec·store는
바꾸지 않았다.

별도 fresh process의 `FIRST_LIGHT_SUPPLY` active event·duty save-create→Continue가 exact Core
snapshot/hash/journal과 paused·normal speed·no-modal 정책을 복원했다. session harness는
snapshot/hash/journal/ordered transition history를 exact 복원하고, 닫은 `FLOOD_ISOLATION_TEST` story 뒤
active event·duty를 재개해 과거 story를 다시 열지 않고 `SECOND_HEART` result와 `SECOND_SOURCE`
briefing을 각각 한 번 열었다. undelivered pending transition은 v1
delivery cursor가 exact replay되지 않으므로 계속 fail-closed한다.

Debug/Release build, `./dev check`, 전체 Godot UI harness와 독립 review를 통과했다. review에서 accepted-command
조건의 세 경로 중복을 shared predicate로 합치고 result exact-once 검사를 강화했다. 이 완료는 pending·
queued/active story·result/handoff·completion save, overwrite/recovery UI, package, 전체 8장 production-input
직접 여정 또는 사람 UX 품질의 증거로 확대하지 않는다.

### UX-R2.14 — active authored story save·Continue

current write schema를 v2로 올리고 deterministic story candidate prefix 중 닫은 개수 하나인 required
nonnegative 32-bit `closedStoryCount`를 application cursor로 추가했다. `RealtimeChapterStoryFlow`의 live와 restore는 Core
transition history + selected campaign의 같은 pure projection으로 candidate 순서, closed prefix와 active
request를 만든다. prior v1은 read-only이고 cursor가 없으므로 모든 projected candidate를 닫은 story-idle로
해석한다. probe/restore 자체는 원본을 rewrite하지 않고, Continue 뒤 정상 종료는 current v2 write 정책을
따른다. 실제 파일명은 `gridworks-r2-campaign-save-v1.json`을 유지했다.

pending story queue가 없고 trigger minute == saved minute인 active `EventStory | DecisionWindowStory`만
저장한다. 별도 fresh process의 product `FLOOD_ISOLATION_TEST` save-create→Continue가 같은 authored modal을
먼저 열고, 닫으면 PlayerPaused·Normal로 돌아갔다. session harness는 exact Core snapshot/hash/journal/history와
닫기 뒤 `SECOND_HEART` result→`SECOND_SOURCE` briefing의 exact-once 미래를 확인했다. trigger minute를 지난
채 열린 story는 capture 단계에서 거부한다.

Debug/Release build, focused 1 suite/105 assertions, `./dev check`의 Realtime 26 suites/1,182 assertions와
Commercial 31 suites/7,084 assertions, 전체 Godot UI harness와 독립 review를 통과했다. review에서 live
trigger-minute gate, v1 read-only writer 경계와 v2 null/overflow strict case를 수정한 뒤 actionable finding
0건으로 재검토됐다. 이 완료는 undelivered pending transition, queued story, initial/result/briefing handoff,
completion/finale/epilogue, overwrite/recovery UI, package, 전체 8장 production-input 직접 여정 또는 사람 UX
품질의 증거로 확대하지 않는다.

### UX-R2.15 — non-final result→next briefing save·Continue

schema나 필드를 더하지 않고 current v2 `closedStoryCount`의 first-unclosed 해석을 bounded handoff suffix로
확장했다. `RealtimeChapterStoryFlow`가 live/restore 모두에서 exact saved minute의 non-final
`ChapterResult`→next `ChapterBriefing`→optional same-chapter `DecisionWindowStory` shape만 허용하고,
`RealtimeSession`은 active started chapter와 마지막 completed chapter의 typed between-chapter result를 Core
snapshot에 맞춰 fail-closed한다. zero-gap result는 이미 projected된 queue를 FIFO로 열고, 긴 gap은 result를
닫을 때 exact next `ChapterStartMinute`로 한 번 전진한 뒤 같은 briefing(+decision)을 연다.

같은 product save path의 active `FLOOD_ISOLATION_TEST` create→첫 fresh Continue의 `SECOND_HEART` result v2
write→두 번째 fresh Continue의 result 복원·`SECOND_SOURCE` briefing v2 write를 검증했다. session smoke는
zero-gap result와 briefing을 각각 복원하고, `SECOND_SOURCE`→`NORTH_BANK_PROMISE`의 long-gap result를
복원해 exact minute `265260`의 briefing→decision FIFO와 story-idle cursor tamper 거부를 확인했다. prior
`FIRST_LIGHT` v1도 Continue 정상 종료 뒤 같은 route의 current v2로 실제 disk에 쓰이는지 함께 검증한다.

Debug/Release build, `./dev check`의 Realtime 26 suites/1,182 assertions와 Commercial 31 suites/7,084
assertions, 전체 Godot UI harness와 두 독립 review를 통과했다. review에서 각 Continue가 `_ExitTree` 뒤
disk write를 재검증하지 않아 마지막 write 실패를 놓칠 수 있던 smoke를 찾았고, same-path reload의
route/minute/hash/journal count/schema/cursor 대조로 수정한 뒤 finding 0건으로 재검토됐다. 이 완료는
undelivered pending transition, general queued story, initial briefing, final/completed run·finale·epilogue,
overwrite/recovery UI, package, 전체 8장 production-input 직접 여정 또는 사람 UX 품질의 증거로 확대하지
않는다.

### UX-R2.16 — initial briefing/zero-command save·Continue

첫 `ChapterStarted`를 `RealtimeChapterStoryFlow`의 `CHAPTER_BRIEFING` candidate로 합쳐 cumulative
Session의 synthetic initial 분기를 제거했다. fresh cumulative Session만 Core initial transition batch를
exact current minute에 한 번 drain하고, Resume replay와 standalone/fixture는 이 bootstrap을 반복하거나
같은 Flow에 섞지 않는다. exact initial의 authored briefing active `c0`와 이를 닫은 story-idle `c1`만
zero-command으로 저장·재개한다.

같은 wire fields의 current schema를 v3로 올려 `closedStoryCount`를 initial-inclusive로 정의했다. prior
v2 object의 raw cursor는 보존하고 Restore 결과만 checked `+1`, prior v1은 새 initial candidate까지
all-closed로 읽는다. prior schema write, ambiguous v1/v2 empty journal, v2 cursor overflow와 `exact initial`
밖의 zero-command는 fail-closed한다.

같은 product save path에서 initial c0 create→첫 fresh Continue의 active `FLOOD_ISOLATION_TEST` c3 write→둘째
Continue의 `SECOND_HEART` result c4 write→셋째 Continue의 `SECOND_SOURCE` briefing c5 write를
fresh process마다 disk reload로 확인했다. Debug/Release build, `./dev check`의 Realtime 26 suites/1,197 assertions와
Commercial 31 suites/7,084 assertions, 전체 Godot UI harness와 두 독립 review를 통과했다. review에서
command-bearing same-minute c1 cursor를 c0으로 변조하면 initial briefing이 부활하던 P2를 찾았고, active
initial은 command 수와 무관하게 exact zero-command snapshot을 요구하도록 수정한 뒤 finding 없이
재검토됐다. 이 완료는 undelivered pending transition, general queued story, final/completed
run·finale·epilogue, overwrite/recovery UI, package 또는 사람 UX 품질의 증거로 확대하지 않는다.

### UX-R2.17 — terminal completed save·Continue

current v3 wire를 바꾸지 않고 canonical full `ProductCampaign`의 exact terminal 완료를 product save 경계에
포함했다. 모든 chapter story가 닫혀야 하며, 성공은 세 epilogue card까지 닫힌 `Ended`·World·no-modal,
실패는 result를 닫고 epilogue가 시작되지 않은 상태만 허용한다. codec Restore가 원본 schema를 보존해
current v3 completion만 승인하고 prior v1/v2 completion, partial route와 nonterminal cursor는 차단한다.

제품 title의 fresh `이어하기`는 같은 terminal Core/world/outcome을 epilogue 재생 없이 복원한다.
Debug/Release build, `./dev check`, 전체 Godot UI harness와 두 독립 review를 통과했고, review에서 발견한
terminal frame debt와 실패/write 증거 공백도 수정했다. 이 완료는 transient cursor, 완료 저장 뒤 새
캠페인 시작, recovery, package 또는 사람 UX 품질의 증거로 확대하지 않는다.

### UX-R2.18 — completed save의 새 게임

exact current-v3 terminal title에서 기존 `이어하기`와 `새 게임`을 함께 열었다. `새 게임`은 chapter
checkpoint나 새 UI를 만들지 않고 missing-save와 같은 canonical `ProductCampaign` bootstrap을 재사용한다.
선택만으로 terminal bytes를 바꾸지 않으며, saveable 지점의 정상 tree exit가 same slot을 current-v3
initial/progress로 교체한다.

completed create→fresh terminal Continue·terminal write; 별도 fresh completed title→New Game→initial write→
fresh Continue 제품 연쇄와 `./dev check`,
Debug build 및 두 독립 review를 통과했다. in-progress/blocked save의 New Game은 계속 차단하며, 이 완료는
safe-point preservation/reset, transient cursor, package 또는 사람 UX 품질의 증거로 확대하지 않는다.

### UX-R2.19 — safe-point 보존과 확인형 reset

새 transient cursor/schema 없이 non-saveable node-draft 정상 종료가 직전 valid save bytes를 그대로 두고,
다음 saveable 정상 종료만 primary를 current v3로 갱신하게 했다. valid in-progress와 raw bytes를 읽을 수
있는 invalid/unsupported/source/replay save는 기존 `새 게임` action의 확인 상태를 공유한다. 두 번째
activation은 원본을 unique raw-byte sibling backup으로 보존한 뒤에만 canonical `ProductCampaign`을
시작하며, I/O와 backup 실패는 continuation·ownership·primary를 바꾸지 않고 fail-closed한다.

node-draft exit→fresh Continue→safe write, 진행 저장의 확인→backup 실패→재시도→byte-exact backup→initial
write→fresh Continue 연쇄, invalid/unsupported 확인 상태, I/O 차단, completed/missing 즉시 New Game 회귀와
`./dev check`, 두 독립 review를 통과했다. invalid/unsupported는 대표 확인 상태까지만 fresh-process로
검사했고 source/replay는 같은 typed production 분류를 공유한다. 이 완료는 transient cursor, backup
browser/restore/delete UI, packaged 전체 캠페인 E2E 또는 사람 UX 품질의 증거로 확대하지 않는다.

### UX-R2.20 — current R2 제품 설정

title과 gameplay가 하나의 `RealtimeSettingsSurface`를 공유하게 했다. windowed/fullscreen, UI
100/125/150/200%, Master/Ambient/SFX 0/25/50/75/100%와 Reduce Motion을 strict
`gridworks.realtime-settings.v1` authority로 묶었다. 인자 없는 product boot만 load/write하고 explicit
fixture/chapter/through는 같은 surface를 read-only로 사용한다. Main은 store 성공 뒤에만 window/audio
engine seam, UI scale와 Session Reduce Motion을 적용한다.

missing은 write 없이 기본값을 쓰고 invalid/unsupported/read failure는 원본과 보이는 오류를 보존한다.
same-directory temp·flush·atomic replace의 실제 write failure도 prior bytes, committed controls/runtime,
Core hash·journal·camera·selection과 save ownership을 그대로 두는지 확인했다. running gameplay는 settings
동안만 player-pause하고 닫을 때 원래 상태와 opener focus를 복구하며, 기존 player-paused는 유지한다.

create→fresh restore, invalid/unsupported/read/write failure, explicit fixture read-only, 설정 surface layout·
focus와 전체 `./dev check`를 통과했다. 두 독립 review가 찾은 unsupported, explicit-route, native window-mode
증거 공백을 보강했고, 격리 경로의 두 macOS non-headless process에서 Windowed→Fullscreen 적용과 fresh
Fullscreen 복원을 engine mode assertion으로 확인한 뒤 finding 0건으로 재검토됐다. 이 완료는 product
audio playback, packaged candidate 복원, Reduce Motion의 사람 관찰, 물리 display 품질이나 공식 UX 증거로
확대하지 않는다.

### UX-R2.21 — current R2 기본 제품 오디오

외부 음원 자산이나 새 subsystem 없이 `RealtimeAudio` node 하나가 22,050Hz mono PCM16 ambient와
Breaker/Energize/Outage SFX를 결정론적으로 생성한다. accepted order는 Breaker, construction completion·
emergency clear·recovery는 Energize, emergency enter·protective trip은 Outage다. `RealtimeSession` 한 곳이
`Outage > Energize > Breaker` 우선순위로 live public operation당 최대 하나를 고르고, Main은 typed C#
event를 playback node에 전달한다. ambient는 title부터 한 번만 시작하며 bootstrap·save replay·Continue
history는 SFX를 내지 않는다.
volume/mute는 기존 Master/Ambient/SFX settings projection을 그대로 쓴다.

pure selector, actual scene stream/bus, title/settings/Continue one-start·history silence, accepted order의
Breaker와 completion+emergency 복합 batch의 Outage, 고정 Core hash를 검사했다. `./dev check`와 두 독립
review를 통과했다. headless에서는 `AudioStreamPlayer.Play()`를 호출하지 않고 stream·routing·request만
검증한다.
이 완료는 speaker 출력, loudness·loop seam·상태 전반 coverage의 사람 청감, packaged candidate 또는 출시
품질의 증거로 확대하지 않는다.

### UX-R2.22 — current R2 macOS package identity

큰 fresh-install gate를 가장 작은 2A/2B로 나누고 2A에서 current R2 package bytes와 identity만 닫았다.
`ExportRelease`는 current/legacy selector 중 정확히 하나를 요구한다. current graph는 strict V2 base+V3
Core, `realtime/r2`·`realtime/ui`와 중립 map leaf만 포함하고 DEBUG fixture와 historical V2 runtime을
제외한다. product main scene, default audio bus와 G3 57개만 고른 universal macOS 14.0 ad-hoc preset을
추가했다.

`tools/r2_candidate.py`와 `./dev candidate build | verify` 한 경로가 clean HEAD와 고정 Godot/.NET,
canonical manifest, bounded ZIP/tree, plist·signature·architecture, exact managed runtime, parsed PCK의 G3
import/backing, legal closure를 독립 재구성한다. 별도 임시 설치 위치에서 no-arg product title marker도
headless로 확인했다. archive/manifest 변조, path/alias/NUL/AppleDouble/symlink/outer closure mutation, current/legacy
graph build, `./dev check`와 두 bounded independent review를 통과했다.

manifest의 `freshUserDataQualified`, `fullProductionInputE2E`, `humanQa`, `evaluationReady`,
`developerIdSigned`, `notarized`, `scoreBearing` claim은 모두 false다. packaged settings/audio도 별도로
qualification하지 않았다. title smoke는 설치 위치만 임시화했으며 `user://`가 빈 상태임을 증명하지
않는다. 후속 2B가 exact package identity를 소비해 이 qualification을 별도로 소유한다.

### UX-R2.23 — packaged app-owned persistence qualification

큰 2B를 가장 작은 2B1/2B2로 나누고, 2B1에서 exact current R2 package가 별도 Gridworks-owned root의
save/settings bytes를 fresh process마다 읽는 경계만 닫았다. `RealtimeSliceMain`의 release-safe env seam은
기존 absolute real directory 아래 current 두 fixed filename만 사용하며 env 없음은 기존 `user://`를
유지한다. invalid·relative·missing·symlink root는 title bootstrap 전에 fail-closed한다.

`tools/r2_qualification.py`와 `./dev qualify run | verify`가 2A verifier를 재사용하고 manifest/archive를
private copy로 고정한다. exact-empty root의 missing/missing, source actual-scene이 만든 settings,
initial save와 terminal save를 user-argument 없는 fresh packaged title process가 각각
loaded/missing, loaded/restorable, loaded/completed로 분류해야 한다. source/package/tool, 각 stage marker와
file hash를 canonical record로 결속하고 verify는 새 extraction/root에서 전부 재구성한다. 실제 account
home의 current 두 파일과 package app tree는 전체 실행 전후 동일해야 한다. 각 packaged title stage의
expected root files/bytes도 그 stage 전후 동일해야 한다.

invalid root, type/key/canonical record mutation, targeted Debug/ExportRelease build, qualification run/verify,
전체 `./dev check`와 두 bounded independent review를 통과했고 finding 0으로 닫혔다. 이 완료는 Godot engine
`user://` 전체 격리, packaged InputEvent 전체 8장·reset/settings UI, audio device·speaker, 사람 UX,
evaluation readiness, Developer ID·공증·출시를 뜻하지 않는다. push, PR, merge는 수행하지 않았다.

### UX-R2.24 — packaged lifecycle InputEvent qualification

2B2를 authored 8장 재생 harness가 아니라 exact package에서 달라질 수 있는 default-scene lifecycle seam으로
줄였다. release-safe dormant `RealtimeSliceMain.Qualification.cs` 하나가 두 qualification env가 모두 유효할
때만 실제 `Viewport.PushInput`을 넣는다. empty New Game, progress Continue, completed Continue/New Game,
restorable progress save의 reset New Game, settings Apply와 fresh Restore 일곱 scenario를 app user argument
없이 실행했다. exact pointer/key input 수는 각각 `6/0`, `3/0`, `3/0`, `3/0`, `6/0`, `39/2`, `3/2`이며
title/session/settings/save/audio 분류 marker 하나로 닫힌다. env가 없으면 marker·입력·새 파일이 없고,
invalid scenario/root는 title marker 전에 exit 1과 정확히 한 bootstrap error로 거부된다.

기존 `tools/r2_qualification.py`를 새 runner/tool 없이 확장했다. canonical record v2가 2B1 네 data stage와
2B2 일곱 lifecycle stage, source/package/tool identity, exact input marker, before/after save/settings bytes,
reset의 한 normalized GUID sibling backup을 결속한다. settings는 actual popup input으로 Apply한 뒤 같은
package fresh process의 control·UI scale·audio bus 값으로 복원하고, generated PCM의 Ambient bus one-start와
history SFX quiet wiring을 확인한다. record·candidate를 private pinned bytes로 재구성하며 unset marker,
input-count·type/key/canonical/identity mutation과 장시간 verify 중 record 교체도 fail-closed한다.

targeted Debug/ExportRelease build, candidate build/verify, qualification run/fresh verify, 전체 `./dev check`와
두 bounded independent code review를 통과했고 최종 finding 0으로 닫혔다. 이 완료는 engine `user://` 전체,
authored 8장 packaged production-input E2E, OS hardware input, 실제 window/display·audio playback/device·speaker,
live cue 전체 coverage, 사람 UX·미감·접근성, evaluation readiness, Developer ID·공증·출시를 뜻하지 않는다.
push, PR, merge는 수행하지 않았다.

### UX-R2.25 — repository-owned 내부 후보 완료선

가장 단순한 구현 목표로 current R2 내부 후보와 외부 release 증거·승인을 분리했다.
production `RealtimeWorldPresenter`는 active risk area를 `Storm`, thermal-limit override를 `Heat`,
그 외를 `Clear`로 투영하고 복합 상태의 flood를 우선한다. `Reduce Motion`은 비·폭우
스트릭의 minute phase를 고정해 현재 renderer에서 실제 효과를 갖는다. 기존 full-campaign·G3
smoke에 Clear/Heat/Storm 우선순위와 motion 고정 검사만 추가했다.

current `./dev`·package·combined 2B와 연결되지 않고 historical editor-native First Light
non-score 정책에 고정된 `tools/commercial-ux/native/` 30개 파일 26,732줄을 제거했다.
과거 기준선은 Git 이력이 보존한다. `NEXT_TASKS.md`는 repository-controlled implementation
backlog만 소유하며 현재 비어 있다. full packaged journey, 실제 display/input/audio,
사람·전문가 검토, score-bearing 평가, 권리·서명·공증·공개 승인은 새
`RELEASE_GATES.md`에 `NOT_RUN | PENDING | NOT_APPROVED`로 보존했다. non-spatial 여정은
keyboard로 완결하되 연속 좌표 pan/zoom/free placement는 pointer-owned spatial interaction으로
제품 계약을 명확히 했다.

Debug build, `./dev check`, final clean candidate/qualification build·fresh verify, Markdown 링크 감사와
두 bounded independent review를 통과했다. 이 완료는 current R2 내부 후보의 repository 구현
완료선이며, 외부 gate나 판매 가능한 1.0·공개 배포 승인을 뜻하지 않는다. push, PR,
merge는 수행하지 않았다.

### UX-R2.26 — FIRST_LIGHT 초반 안내·피드백

세 번의 `FIRST_LIGHT` 직접 플레이 review에서 공통으로 드러난 초반 경로 이해, 실시간 계획 손실,
모호한 경간 거부와 약한 결과 설명을 한 bounded 변경으로 고쳤다. 새 tutorial cursor나 save state 대신
완공된 현재 망에서 단계를 유도해 HUD가 변전소 1/3, 서부 발전 경로 2/3, 동부 생활권 경로 3/3을
표시한다. native release/product 경로는 건설 도구 진입, 초안 거부와 각 공사 완공에서 시간을 멈추며,
기술 fixture의 기존 입력 의미는 유지한다.

선로 preview와 거부 문구는 현재 경간/허용 경간을 함께 표시하고, 거부된 ghost segment와 끝점은
붉고 굵게 그린다. `FIRST_LIGHT` 실패 결과는 안전 충족 수, 동부 미공급 시간, 첫 공급·열 실패 원인,
다음 시도 경로와 최종 자금을 함께 설명한다. 고정 높이 build shelf는 두 줄로 제한하되 전체 문구를
tooltip과 accessibility name에 보존한다.

기존 controller/presentation smoke, Debug build와 전체 Godot UI layout harness, 한 번의 fresh native
`FIRST_LIGHT` 초반 입력을 확인했다. 전체 `./dev check`와 새 test suite는 사용자의 단순성 요청에 따라
실행·추가하지 않았다. native 확인은 LLM formative direct-play이며 사람 재미·사용성·미감 증거,
Core 규칙 변경, 자동 경로, 패키지·외부 gate·배포를 뜻하지 않는다. push, PR, merge는 수행하지 않았다.

### UX-R2.27 — 누적 8장 native 품질 review와 보완

고정 `gpt-5.6-sol` xhigh LLM sample이 `./dev play through LONGEST_NIGHT`를 production native 입력으로
누적 8장 끝까지 진행했다. 모든 도시 약속은 Defer를 골랐고 마지막 safety 0/3의 실패 finale와 완료 망까지
관찰했다. 이 실패 경로는 설계대로 성공 전용 epilogue를 시작하지 않았으므로 세 epilogue card의 native
직접 관찰 증거로 확대하지 않는다. macOS full-screen과 Computer Use window 탐색의 일시 실패는 같은
process에서 회복됐고 게임 crash·soft-lock으로 분류하지 않았다.

review에서 재현된 고영향 문제를 기존 authority 안에서 보완했다. chapter briefing·decision story를 닫거나
새 build tool에 진입하면 선택 속도를 보존한 `PlayerPaused` 계획 상태가 되고, 플레이어 입력만 진행을
재개한다. 불완전한 `FIRST_LIGHT` active test는 결과 건너뛰기 action을 표시하지 않으며, 실패 debrief는
시험 시작 시점의 미완공과 시험 뒤 늦은 완공을 구분한다. 접속 조건 결과에는 frozen 평가 시각을 함께
표시하고, 실패 finale는 종료된 읽기 전용 도시와 성공 전용 세 기록의 경계를 설명한다. 약속 context는
세 칸을 두 칸으로 합쳤고, critical auto-pause는 `P`·상단 재생 입력을 안내하며, 겹친 후보 badge는
`Q/E 전환`을 직접 노출한다. accessibility scale에서 build shelf의 실제 typography 높이와 간결한 1/3–3/3
안내도 함께 맞췄다. decision 창의 숫자 `0` 후보는 native screenshot과 source를 대조해 Computer Use click
indicator로 판단했고 게임 결함으로 채택하지 않았다.

같은 reviewer의 native 재관찰에서 story 직후 HUD의 이전 실행 표기와 `FIRST_LIGHT` 후보·완료 feedback의
다음 장 유출을 추가로 재현해 보완했다. 최종 확인에서는 `SECOND_HEART` briefing 직후 22:00 계획 정지가
3.5초 유지되는 동안 이전 후보 badge, pointer feedback, build tool·quote·draft가 모두 없었고, 새 mouse 입력
뒤에만 겹친 후보 badge가 다시 나타났다. 이어 `E` 입력이 2/2에서 1/2로 후보를 바꿨으며 process는 crash나
soft-lock 없이 정상 종료했다.

Debug build, `A1_NORMAL_READY`, `A1_CONSTRUCTION_DUE_1M`, 전체 `./dev check`의 Core 8,289 assertions,
product save/settings failure matrix, 누적 controller와 FHD/QHD/UHD 100–200% offscreen UI harness를 통과했다.
이 review는 한 고정 LLM formative sample이며 사람 재미·사용성·미감 증거, 공식 UX 점수, 성공 epilogue의
native 관찰, packaged full journey나 외부 release gate를 뜻하지 않는다. push, PR, merge는 수행하지 않았다.

### UX-R2.28 — native world 강·교량·전신주·건물 합성 보완

native screenshot에서 재현된 직선형 강, 떠 있거나 막대처럼 보이는 교량, 지면 중심에서 시작하는 도체,
강한 직사각형 building plate를 기존 `RealtimePlaceholderMap` draw authority 안에서 보완했다. authoritative
water polygon을 바꾸지 않고 양쪽 강둑에 공통 굴곡과 독립적인 가장자리 변화를 합성해 water surface와
흐름선이 같은 S자 실루엣을 따른다. 두 교량은 이 시각 강둑을 직접 샘플링해 양안 너머까지 착지하고,
차도 texture, 차선, 교대, 측면 두께·보강재를 한 구조로 그린다. 큰 중앙 반사 sprite는 낮은 대비의 작은
반사로 줄였다.

pole class는 실제 표시 sprite 높이에서 상단 attachment를 계산하며 세 가닥 도체가 pole top 사이에서
처짐을 유지한다. node equipment→conductor→overlay 순서로 구조물, 선로와 선택 ring의 층을 분리했다.
building terrain은 불투명한 사각 plate 대신 0.18 이하 parcel fill과 약한 outline을 사용한다. Core terrain,
construction/hit geometry, gameplay·save schema와 tracked G3 자산 목록은 바꾸지 않았다.

renderer smoke가 bank deviation, 두 measured bridge의 양안 착지, building parcel alpha와 모든 pole endpoint의
raised attachment를 고정한다. Debug build, 전체 `./dev check`의 Core 8,289 assertions, save/settings failure
matrix, G3 50-file draw union, FHD/QHD/UHD 100–200% offscreen UI harness와 두 targeted checkpoint가 PASS했다.
exact final commit의 native full-screen에서 normal world, line-construction mode와 pole-network fixture를
관찰했다. 이는 한 LLM의 formative 시각 확인이며 사람 미감 승인, 공식 UX 점수, package·외부 release gate를
뜻하지 않는다. push, PR, merge는 수행하지 않았다.

## 5. G3 아트와 main 통합

루트 `assets/`의 네 이미지를 시각 방향으로 삼아 회화적 아이소메트릭 도시·설비·UI 자산을 제작했다.
중간의 지도 35개 부분 적용은 최종 작업으로 대체됐다. 현재 기준선은 G3 PNG 57개 전부를 live R2에
연결한다.

- 지도 50개: 지형, 강·제방, 도로, 주거·병원·산업 시설, 발전·전신주·변전소 구조물
- UI 7개: panel, HUD metric, inspector, tool slot과 버튼 chrome
- clear·heat·rain·storm draw, UI scale, hit/focus와 기존 상태 표현 회귀 검사
- `RealtimeSliceMain`을 저장소 기본 Godot 장면으로 전환
- 작업 이력을 local `main` 한 branch로 정리

이 완료는 runtime 연결과 자동검사의 증거다. 사람 미감·사용성, 전체 상태별 production polish,
R2 save/package 또는 출시 승인을 뜻하지 않는다.

## 6. 문서 기준선 정리

현재 사실, 남은 일, 제품 기준과 완료 이력을 분리했다. 완료된 scope와 체크리스트는 현재 문서
트리에서 제거하고 이 파일에 압축했다. README에는 실행 가능한 현재 사실만, `NEXT_TASKS.md`에는
미완료 항목만 남겼으며 SHA·커밋 영수증은 현재-facing 기획 문서에서 제거했다.

후속 재감사에서는 fresh-install 후보와 공식 평가의 순서, audio/settings와 score-bearing 도구 gate,
UX-R0 context 보존, 설비 catalog·용어·역사 링크와 source 주석을 바로잡았다. 이는 문서와 동결 입력의
정합성 보완이며 새로운 gameplay, package 또는 UX 점수 완료를 뜻하지 않는다.

## 7. R2 개발 구조 단순화

current R2를 새 gameplay 없이 더 적은 권위·분기·파일 fan-out으로 변경할 수 있게 정리했다.

- root `Gridworks.sln`과 `./dev`를 current Core/Game/check의 단일 개발 진입점으로 만들고 historical
  Product/V1/check graph와 동결 V2 `ExportRelease`를 분리했다.
- release route와 native 4장 cap을 `RealtimeNativeRouteCatalog` 하나로 묶고, strict loader가 V2 base와
  V3 overlay를 합성한 뒤 generic story flow가 Core transition에서 modal timing/request를 만들게 했다.
- Godot main에서 plain C# `RealtimeSession`을 분리해 main을 441줄 scene/input/publication adapter로
  줄였다. raw input은 `RealtimeInputRouter`가 typed request로 바꾸고 Main이 명시적 capability로
  검증·routing한다.
- 한 full projection은 하나의 `RealtimePresentationSource`에서 조립한다. modal의 이중 projection을 제거하고,
  1,968줄 presenter를 158줄 facade와 world/timeline/context/construction/shell/modal component로 나눴다.
  component끼리는 호출하지 않고 facade만 최종 immutable presentation을 조립한다.
- stable ID, 문구, timeline 정책과 target resolution을 leaf authority로 분리하고 full ID 형식을 독립
  assertion으로 고정했다.
- [개발 구조](../ARCHITECTURE.md)에 규칙·application·presentation·Godot ownership과 chapter/mechanic/
  presentation 변경 경로를 기록했다.

Debug/Release build, current check suites, Python 회귀, 두 named checkpoint의 기존 canonical hash와 전체
Godot UI harness를 유지했다. 이 완료는 compile 시간 개선, 새 chapter/title/save/package 또는 사람 UX
품질을 주장하지 않는다.

## 8. Agent 온보딩 문서 정리

새 agent가 제품 상태, 변경 권한과 작업 절차를 한 문서에서 추측하지 않도록 문서 역할을 분리했다.
루트 README→active scope→agent guide→질문별 문서 지도의 첫 읽기 순서를 `AGENTS.md`와 current 문서에서
통일하고, `AGENT_GUIDE.md` 하나가 scope 작성, 최소 검증, commit·review·handoff와 금지된 추론을
소유하게 했다. 문서 지도에는 각 문서가 갱신될 때와 두지 않을 내용을 명시했다.

상대 링크와 문서 권위·용어 충돌을 다시 감사하고 internal qualification과 공개 출시 자격을 혼동할 수
있던 실행 안내 문구를 바로잡았다. 이 정리는 제품 코드, current R2 capability, 외부 gate 또는 공개
배포 상태를 바꾸지 않았다.

## 9. 이 문서가 소유하지 않는 항목

현재 질문의 소유 문서는 [문서 지도](../README.md)가 지정한다. 제품 구현 상태는
[루트 README](../../README.md), current 개발 구조는 [개발 구조](../ARCHITECTURE.md), repository backlog는
[남은 구현 작업](../NEXT_TASKS.md), device·사람·평가·출시 증거와 승인은
[외부 출시 gate](../RELEASE_GATES.md)가 소유한다. 이 완료 이력의 경계 문장을 current 문서로 복사해
갱신하지 않는다.
