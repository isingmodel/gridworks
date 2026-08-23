# Gridworks — 실시간 상용 UX 87 활성 계약

> 상태: **UX-R2.3 source/fix review 완료 · user-requested native observation 보류 · 공식 평가 작업 중단**
>
> 제품 방향 권위: [에셋 스타일 실시간 게임 계약](ASSET_STYLE_REALTIME_GAME.md)
>
> 목표: 고정 `gpt-5.6-sol` + reasoning effort `ultra`의 공식
> `CommercialUXProxy >= 87`

이 문서는 고정 judge·UX 평가 계약을 보존한다. historical A1-G3 partial port 뒤의
[realtime G3 canonicalization/main 통합 scope](REALTIME_G3_MAIN_CONSOLIDATION.md)는 완료됐으며 현재 추가 구현
권한은 없다. 제품 방향은 turn 단위 진행이 아니라
pause·1×·2×·4× 속도, 계속 흐르는 시계, 미리 보이는 사건과 시간에 따른 공사·열 노출·정지·회복을
가진 실시간 전력망 게임으로 고정한다. `origin/main`의 Release.V3/R2 기반을 이 방향의 권위로
사용하며, 과거 `codex/commercial-ux-87`의 V2 runtime 구현을 합치지 않는다.

UX-R0는 source-bound 텍스트 artifact, 스토리 파트 단독 실행과 세 fresh text judge 기준점을 닫았다.
UX-R1은 V3/R2 candidate bytes, replay, session claim, evaluation-chain parent와 blocked artifact
provenance, local controlled transcript authority를 fail-closed로 포팅하고 전체 종료 검토까지 완료했다.
사용자의 “87점 이상까지 계속 개선”과 직접 플레이 지시에 따라 UX-R2를 작은 순차 단위로 열었다.
실제 release `FIRST_LIGHT` 장(`FIRST_LIGHT_SUPPLY` phase/event)의 briefing→live→authored result,
한 줄 chronological future-event rail과 사람이 조작하는 Debug checkpoint host를 허용한 UX-R2.1은
source revision `e385707071e4ccfb34d5200e3401897db7f164ad`, 두 독립 review P0 0/P1 0과 세 actual-input
PASS record로 완료했다. UX-R2.2는 같은 망·현금·시계를 이어받는 `FIRST_LIGHT`→`SECOND_HEART`
→`SECOND_SOURCE` 누적 tutorial prefix와 그 장 전환·접속 조건·예고 범람 표시를 source `659709d`와
technical-route fix `40ed3fa`에서 구현했다. 전체 결정론 회귀, 두 bounded source review P0 0/P1 0과
fresh-process 세 장 actual-input record로 비점수 완료했다. A1 runtime art, 5–8장, persistence, 기본
장면과 score-bearing capture는 열지 않았다. 현재 UX-R2.3은 누적 상태를 보존한
`NORTH_BANK_PROMISE` 한 장, 명시적 6개월 calendar transition, 한 줄 rail의 최초 promise deadline과
Keep/Defer branch만 열었다. gate-opening 독립 검토 `PASS_FOR_UX_R2_3_GATE_OPENING`, P0 0/P1 0을
통과해 exact source allowlist 구현만 승인됐다. 이후 source `aee4932`와 fix `d85bb3f`가 구현·검증됐고
두 bounded source/fix review는 P0 0/P1 0이다. 최신 사용자 지시는 이 시점에서 native direct play와
87점 반복을 멈추고 G3 visual application만 끝낸 뒤 중단하는 것이었다. 이후 사용자는 full G3가
기본 제품 화면에서 보이도록 R2 default entry와 local main 통합을 명시했다.

## 1. 플레이어 결과

최종 후보는 처음 접한 플레이어가 저장소나 캠페인 데이터를 보지 않고 다음 여정을 이해하고 끝낼 수
있어야 한다.

1. title에서 새 게임을 시작하고 현재 시각, 첫 목표, 다음 행동과 다음 사건을 찾는다.
2. 첫 세 장에서 pause·속도·future-event status bar·건설 완료 시각·실제 공급 경로·회랑 독립성·용량을
   행동으로 배운다.
3. 본편 다섯 장에서 폭염·범람·계획정지·보호정지·도시 약속이 이전 망을 시간에 따라 어떻게 시험하는지
   예측하고 선택한다.
4. 결과, 다음 장의 현재 상태, 누적 약속을 혼동하지 않고 자신의 실제 경로·병목·선택 결과를 읽는다.
5. 진행 중 save와 process 재시작 뒤 시각·공사·사건·다음 행동을 되찾는다.
6. 여덟 장, 마지막 결과, 세 epilogue card, 약속별 결말과 장 선택까지 하나의 완결된 게임으로 경험한다.

## 2. 현재 사실과 증거 상한

- 작성 콘텐츠 권위는 `data/release-campaign-v2.json`의 8장이다.
- 실시간 일정 권위는 `data/release-campaign-v3.json`의 8장·16 event다.
- 실시간 규칙 권위는 `src/Gridworks.Core/Release/V3/`다.
- 현재 제품 기본 장면과 native 평가 대상은 `RealtimeSliceMain`이다. source-bound historical candidate의
  비기본/`CommercialMain` 설명은 그 옛 evidence revision에만 적용된다.
- R2 presentation은 `FIRST_LIGHT`→`SECOND_HEART`→`SECOND_SOURCE` tutorial prefix의 deterministic
  controller/UI와 actual macOS mouse/keyboard authored positive-result 경로까지 확인됐다.
- `A1_NORMAL_READY`, `A1_CONSTRUCTION_DUE_1M` 두 checkpoint만 구현됐다.
- source revision `379e980`의 evaluator가 39-file Debug/editor candidate와 두 성공·세 인자 거부 probe를 exact
  bytes로 결속하고 독립 verifier에서 다시 실행한다. 이는 score-bearing native capture가 아니다.
- R2 `RealtimeEventRail`은 한 줄 chronological track, compact marker, custom hover 상세 정보와 AX
  selector 계약을 full UI matrix에서 통과했고 actual tutorial prefix에서 단일 track과 marker 클릭·선택
  연동을 관찰했다. CUA는 hover-only popup의 네이티브 출현 자체를 별도 사람 증거로 승격하지 않는다.
- 나머지 5장, full campaign transition, save/resume, finale·epilogue native E2E는
  `NOT_IMPLEMENTED`다.
- 동결 V2 `CommercialMain`이나 Core-only replay로 이 누락을 채우지 않는다.
- 현재 `CommercialUXProxy = null`이며 score-bearing capture는 허용되지 않았다.
- 같은 source-bound 입력의 첫 INITIAL panel은 `TP-A1` 불안정으로 점수가 성립하지 않았고 보존했다.
- 별도 세 fresh run의 두 번째 INITIAL panel은 `SCORED_FORMATIVE`, `TextPlanProxy = 83.4475`로
  안정 집계됐다. 플랫폼 서명 execution receipt는 저장소에 내보내지 못했다.
- UX-R1 local controlled transcript는 별도 fresh `gpt-5.6-sol`/`ultra` semantic-echo rollout을
  source-bound parent에 결속했다. 이는 platform attestation, judge 실행 또는 점수 증거가 아니다.
- UX-R2.1 product source는 `e385707071e4ccfb34d5200e3401897db7f164ad`다. Debug build 0 warnings,
  Realtime 23 suites/673 assertions, Commercial 31 suites/7084 assertions, 34 story atom, full UI scale
  matrix와 두 automated checkpoint hash가 PASS했고 독립 source review는 P0 0/P1 0이다.
- `FORMATIVE_DIRECT_PLAY_PASS:FIRST_LIGHT`와 두 interactive checkpoint actual-input record를 실제
  production mouse/keyboard 경로로 생성했다. headless runner PASS와 actual record를 서로 대신하지 않는다.
- UX-R2.2 product source는 `659709d`, technical checkpoint-route 보존 fix는 `40ed3fa`다. Debug build
  0 warnings, Realtime 24 suites/778 assertions, Commercial 31 suites/7084 assertions, text tools 34
  parts/16 mutations, 34-part manifest, full UI scale matrix와 기존 checkpoint hash가 PASS했다. source와
  fix bounded review는 각각 P0 0/P1 0이다.
- fresh process에서 production mouse/keyboard로 세 authored positive result를 닫아
  `FORMATIVE_DIRECT_PLAY_PASS:FIRST_LIGHT`, `:SECOND_HEART`, `:SECOND_SOURCE`와 마지막
  `FULL_FLOW_E2E_PASS:TUTORIAL_THROUGH_SECOND_SOURCE`를 생성했다. keyboard candidate 선택, marker
  클릭·선택, active flood 적색 solid fill은 관찰했고 custom hover-only popup 출현은 관찰하지 않았다.

따라서 text judge의 결과는 개선 우선순위를 정하는 `TextPlanProxy`일 뿐 공식 87점이 아니다.

## 3. 단일 권위

| 질문 | 권위 |
|---|---|
| 제품 방향·시각 경계 | `docs/scopes/ASSET_STYLE_REALTIME_GAME.md` |
| 평가 범위·gate | 이 문서 |
| 평가 절차·점수 계약 | `docs/product/COMMERCIAL_UX_EVALUATION_PROTOCOL_KO.md` |
| 작성 story·결과·epilogue | `data/release-campaign-v2.json` |
| 실시간 장 일정·event | `data/release-campaign-v3.json` |
| 실시간 상태·전이·forecast | `src/Gridworks.Core/Release/V3/` |
| 현재 native UX 기반 | `game/realtime/r2/`, `game/realtime/ui/` |
| 결정론적 검사 | `tools/Gridworks.RealtimeChecks`, `tools/Gridworks.CommercialChecks` |
| judge artifact·집계 | `tools/commercial-ux/` |

Game과 평가 도구는 story, 일정과 결과 사실을 새로 만들지 않는다. base authored content와 V3 schedule을
검증해 조합한다. `authoredReachable=true`는 콘텐츠가 권위에 존재한다는 뜻일 뿐 R2 화면에서 실제로
도달했다는 뜻이 아니다.

## 4. Future-event status bar 계약

future-event status bar는 실시간 게임의 핵심 planning surface다. 현재 R2의 수평
`RealtimeEventRail`은 수요·기한·기상 정지·공사·보호를 lane별 장문 행으로 나누지 않고 한 줄
chronological track에 compact cue로 놓으며, pointer hover 상세 정보와 전체 AX selector를 제공한다.
다음 결과를 만족해야 한다.

- gameplay 중 현재 시각과 다음 사건까지 남은 시간을 확대 없이 찾을 수 있다.
- 사건의 예고 시각, 시작·종료 구간과 active/completed 상태가 한 시간축에서 구분된다.
- 공사 완료 예정, promise decision deadline, 열 노출 소진·보호정지·복귀가 사건과 같은 축에서
  충돌 여부를 비교할 수 있다.
- actual forecast와 아직 발주하지 않은 draft forecast가 형태·문장·상태로 구분된다.
- marker를 선택하면 지도와 상세 패널이 같은 typed 대상을 가리키고, 선택을 닫아도 현재 시각의 맥락을
  잃지 않는다.
- pause·1×·2×·4×에서 countdown과 marker 상태가 Core minute와 일치한다.
- keyboard, UI 125%, Reduce Motion과 색 외 cue에서도 사건 순서·심각도·상태·선택을 읽고 이동할 수
  있다.

TEXT-PLAN은 이 정보 구조와 각 장 schedule을 평가한다. NATIVE는 실제 화면의 지속 가시성, 정보 밀도,
clipping, focus, synchronized transition과 플레이어 이해를 평가한다. rail이 코드에 존재한다는 사실만으로
해당 cell을 PASS 처리하지 않는다.

## 5. 스토리 파트 단독 실행

전체 캠페인을 재생하지 않고 34개 narrative atom 중 하나를 exact selector로 검사할 수 있어야 한다.

```text
<chapterId>/briefing
<chapterId>/window/<windowId>
<chapterId>/result/standard
<chapterId>/result/keep
<chapterId>/result/defer
campaign/epilogue/card/<city-report|medical-witness|closing>
campaign/epilogue/promise/<chapterId>/<keep|defer>
```

현재 topology는 briefing 8, window story 6, result card 11, epilogue card 3, promise branch line 6이다.
promise result와 epilogue line은 `NORTH_BANK_PROMISE`, `WHOSE_MARGIN`, `BEFORE_WATER_RISE`에만 있다.

- selector는 대소문자와 구분자를 포함한 canonical ID 하나만 허용한다.
- 출력은 base campaign과 V3 schedule을 결속한 stable JSON이다.
- 각 schedule은 event ID뿐 아니라 priority, 시작 offset, duration, forecast lead를 포함한다.
- unknown, malformed, 해당 장에서 불가능한 branch는 typed JSON 오류와 exit 2로 실패한다.
- manifest는 모든 atom을 정확히 한 번 포함하고 반복 실행에서 byte-identical이어야 한다.
- part 단독 실행은 unit/content test다. native UI reachability나 전체 장 완료를 주장하지 않는다.

표준 명령은 다음과 같다.

```sh
dotnet run --project tools/Gridworks.CommercialChecks -c Release -- --story-manifest
dotnet run --project tools/Gridworks.CommercialChecks -c Release -- \
  --story-part FIRST_LIGHT/briefing
dotnet run --project tools/Gridworks.CommercialChecks -c Release -- \
  --story-part campaign/epilogue/promise/NORTH_BANK_PROMISE/keep
```

## 6. 평가 lane

### TEXT-PLAN

세 fresh blinded judge가 premise, player role, 8장 learning/crisis/choice intent, 16 event schedule,
future-event status bar 계약, 34개 narrative atom만 읽는다. model은 모두 `gpt-5.6-sol`, effort는
`ultra`다. 출력은 strict categorical JSON이고 집계기는 median과 고정 spread penalty를 적용한다.

TEXT-PLAN은 계획의 명료성·완결성만 평가하며 `officialCommercialUX=false`다.

### NATIVE

실제 macOS candidate를 처음부터 조작하는 cold journey 3개와 고정 checkpoint·alternate branch를
수행하는 coverage journey를 같은 candidate/evidence session에 결속한다. 세 blind judge, evidence
verifier와 deterministic oracle이 모두 같은 bytes와 trace를 확인한 결과만 `CommercialUXProxy` 후보가
된다.

공식 native coverage는 tutorial 3장, 본편 5장, result branch, finale·epilogue, 진행 중/완료
save-resume, invalid action recovery, future-event status bar, keyboard, UI 125%, Reduce Motion, 색 외 cue,
audio/video를 포함한다.

## 7. 순차 gate

한 번에 아래 gate 하나만 연다. 현재 사용자 지시의 “87점 이상까지 개선”은 순서대로 계속할 권한이지만,
각 gate의 종료 증거와 현재 상태 문서를 갱신하기 전 다음 gate 파일을 미리 만들지 않는다.

### UX-R0 — 실시간 텍스트 기준선 — 완료

- protocol/rubric/model identity를 후보 수정 전에 고정한다.
- base campaign과 V3 full event schedule에서 34-part manifest와 source-bound artifact를 만든다.
- story part 단독 실행과 mutation coverage를 결정론적으로 닫는다.
- 세 fresh `gpt-5.6-sol` `ultra` judge로 첫 `TextPlanProxy`를 만든다.
- 결과는 formative이며 native 구현이나 official 87을 주장하지 않는다.

종료: exact build/suite, text tooling test, 3-judge aggregate, 독립 P0/P1 review와 문서 상태가 모두
일치한다.

종료 증거는 커밋 `746c0aa`와
`playtests/commercial-ux-87-realtime/text-plan-r0/`에 있다. 첫 INITIAL panel은 불안정으로 보존했고,
두 번째 INITIAL panel은 `TextPlanProxy = 83.4475`, `officialCommercialUX=false`다. 독립 종료 검토는
P0 0/P1 0으로 PASS했다.

### UX-R1 — native evaluator authority port — 완료

- V3/R2 candidate bytes, replay authority, session claim과 evidence provenance를 새 권위에 맞게 닫는다.
- targeted checkpoint와 full-flow 예외의 actor recipe를 분리한다.
- score-bearing capture 전 독립 fail-closed review를 통과한다.

첫 candidate·route 단위는 source revision `379e9800c81ca315976ab4c28d511664df6ab7ed`에서 완료했다. 제품
source 170개와 evaluator producer 4개
Git blob, Godot regular-file tree 153개, Debug package 39개, checkpoint 성공 2개와 selector 거부 3개를
결속한다. semantic verifier는 caller가 준 receipt를 신뢰하지 않고 다섯 probe를 fresh process로 다시
실행한다. 두 fresh manifest는 byte-identical이며 candidate hash는
`sha256:373785e45a4485dfeded43466a5bff0f66de4a0c106c972262686e7a432cbdd6`다. 구조 schema만 통과하거나
checkpoint PASS 문자열만 존재하는 것으로 권위를 대신할 수 없다.

Git provenance는 `/usr/bin/git` selector가 아니라 실제 Command Line Tools Git 본체를 직접 실행하고,
그 bytes·version·SHA-1 object format을 결속한다. 모든 authority read는 explicit per-worktree Git dir과
work tree, fresh 환경, `--no-replace-objects`·`--no-lazy-fetch`의 CLI+환경 이중 차단을 사용한다. commit과
blob replace, ambient `GIT_*`, 서로 다른 HEAD를 가진 linked worktree 회귀가 PASS했다. system dylib
transitive closure는 결속하지 않았으며 score-bearing capture는 계속 금지다.

root 전체 suite는 16/16 PASS했고 두 fresh manifest의 raw SHA-256은 모두
`ca7826d38cae6e8a28e142e10e522e9c1425ba6abcec938182d9819ab0b2a816`이었다. 독립 재실행도
16/16 PASS, 같은 두 manifest/hash, AJV 8.20.0 Draft 2020-12 strict PASS를 재현했고 최종 review는
P0 0/P1 0이다. schema-only 검증, .NET·Git transitive runtime closure, future-event bar의 native 품질,
full flow와 공식 점수는 이 종료 증거에 포함되지 않는다.

두 번째 session/attempt 단위는 source revision
`5a31ff35a6e2d293c2f1800e4297945ecf3a5584`에서 완료했다. 고정 candidate와 story bytes를 session 내부
`inputs/`에 먼저 exclusive-write+fsync하고 finalized claim을 마지막 commit marker로 쓴다. session
evaluator 7개 Git blob과 running bytes aggregate는
`sha256:faea99acbc3a09334ccc8fbb9140a894f99fbfb0340ce224e9f12cba9b54b3e9`다. candidate source와 session
authority source는 별도 필드이며 package tree·candidate producer hash도 claim에 직접 결속한다.

route는 두 targeted checkpoint, authored-only `STORY_PART_UNIT` 34개, 실행 불가
`FULL_FLOW_EXCEPTION`만 허용한다. story part는 `nativeReachabilityClaim=false`이고 full-flow는 attempt,
producer output, producer start가 모두 0이다. executable route는 최대 세 append-only attempt를 가지며
`PRODUCER_NO_OUTPUT`·`TRANSPORT_FAILURE`만 다음 attempt를 허용한다. 유효하지만 다른 JSON은
nonretryable `INTEGRITY_FAILURE`이고 세 번째 retryable outcome도 네 번째 attempt를 열지 않는다. terminal
path는 output을 읽기 전에 zero-byte로 예약되며 caller는 outcome을 공급할 수 없다.

root session suite는 12/12 PASS(74.211초), candidate 회귀는 16/16 PASS(220.046초), 세 schema는 AJV
8.20.0 Draft 2020-12 strict compile PASS다. 독립 재실행은 session 12/12 PASS(82.335초), P0 0/P1 0으로
`PASS_FOR_UX_R1_SESSION_ATTEMPT_MAJOR_UNIT_ONLY`를 판정했다. 두 대표 `--story-part` 실행도 exact JSON과
exit 0을 재현했다. future-event status bar는 여섯 signal과 두 checkpoint headless wiring만 결속하며
native 품질은 계속 `NOT_OBSERVED`다.

세 번째 evaluation-chain parent 단위는 source revision
`74ba7256766f41c1398fba98f59c1c942a4cb96e`에서 완료했다. finalized session을 수정하지 않고
`<session-root>.evaluation-chain-v1` sibling에 session claim, candidate/story와 성공까지의 모든
start/output/terminal prefix를 exact snapshot한다. executable route는 마지막 유일 `SUCCESS`만 선택하며
앞선 retry를 생략할 수 없다. full-flow는 nested unavailable terminal 하나와 attempt 0을 유지한다.

chain claim은 evidence/actor/judge/verifier/oracle/aggregate의 일곱 future path만 고정하고 실제 파일이나
placeholder를 만들지 않는다. targeted boundary는 future-event 여섯 signal, 두 checkpoint headless wiring
PASS와 native `NOT_OBSERVED`를 함께 보존한다. 5개 evaluator Git blob aggregate는
`sha256:d87e605449e558d5debd2652f3cf0282f851da45eb19b85e1b0d811af18d218f`, policy raw는
`sha256:accef28faf6583f844e082e0a4c22f2087810111897cfb4c7bbf8c287a37e6d0`다. root suite와 독립
재실행은 각각 14/14 PASS, AJV 8.20.0 Draft 2020-12 strict PASS였고 독립 verdict는 P0 0/P1 0이다.
model execution authority는 `UNAVAILABLE`, `CommercialUXProxy = null`이다.

네 번째 current-route artifact 단위는 source revision
`a270339a778e49ce0458c61cef383fc96283a596`에서 완료했다. parent가 고정한 순서대로 evidence index,
actor terminal, judge input, judge terminal, evidence verifier result, product oracle ledger와 aggregate를
하나의 append-only prefix로 쓴다. aggregate는 마지막 `O_EXCL + fsync` commit marker이며 각 artifact는
parent claim·전체 선행 artifact raw/self hash와 exact canonical path에 결속된다.

이 chain은 현재 route를 정직하게 `FINALIZED_BLOCKED_NON_SCORE`로 닫을 뿐 실행을 사칭하지 않는다.
bound native evidence와 이 authority의 capture/model/evidence verifier/product oracle/score aggregation은
모두 0회 또는 미실행이고 hard gate는 `NOT_EVALUATED`다. targeted route는 future-event 여섯 signal,
headless wiring PASS와 native `NOT_OBSERVED`를 그대로 보존하며 story part는 native reachability를
주장하지 않고 full-flow는 0-attempt unavailable terminal을 유지한다. `officialCommercialUX=false`,
`ScoreBearingCaptureAllowed=false`, `CommercialUXProxy=null`이다.

11개 evaluator Git blob과 running bytes aggregate는
`sha256:225696ad11902e33213693e75e9576368a091b1a16ba32a3c0a449e6179dea1d`, artifact policy raw는
`sha256:f27c3c49c00d547ee55ab5b0719fda1729ee13322dff6caccc48b2fea6297960`다. root source-bound suite는
11/11 PASS(353.480초), 28개 genuine route artifact의 AJV 8.20.0 Draft 2020-12 strict instance 검증과
write/fsync interruption, race, hardlink, cross-chain, downstream rehash와 false-score mutation을 통과했다.
독립 재실행도 11/11 PASS(374.887초), duplicate-key JSON·7 schema AJV strict·AST PASS를 재현했고 최종
review는 P0 0/P1 0이다.

다섯 번째 local controlled transcript 단위는 source revision
`2b0b6ee355790b73cc47eb17c17bd737bdcf8d9a`에서 완료했다. finalized blocked aggregate에서 canonical
projection을 재구성하고 그 hash를 exact하게 되돌리는 고정 semantic echo prompt만 native Codex CLI에
전달한다. blocked `judge-input.json`은 executable이 아니며 이 probe에서 실행하지 않는다. 시작 전후
rollout inventory의 정확히 한 항목 추가, raw JSONL, 요청·보고 model/effort, native executable
identity와 exact output을 append-only receipt에 결속한다.

7개 Git blob과 running bytes aggregate는
`sha256:1a9c24ff253374cb05a0b5854aeb7d7379329a0d0481656065f987e0f99c8751`, policy raw는
`sha256:ff77b3f3b95958b4813efb2a2a91ac3533faefd99e208f4d098640d1bc739cf6`다. 13/13 adversarial suite와
start/final/output 세 schema의 AJV 8.20.0 Draft 2020-12 strict compile·instance 검증이 PASS했다. 독립
public verifier가 재구성한 genuine receipt raw SHA-256은
`sha256:f7c17c4a9bcf29bfe1dc77d1d638d755e32517e375f418b90356ebaba456891f`, transcript authority ID는
`sha256:d79bcb78d0f18426f7019f0959dbb3742d719154654b8716c298e0edaa840927`다. 요청과 local rollout은
`gpt-5.6-sol`/`ultra`, originator `codex_exec`, 1 turn·0 tool call로 일치했다. 독립 subunit review는
P0 0/P1 0이다.

이 receipt의 platform model/effort/freshness attestation은 모두 false이고 server-signed receipt는 null이다.
현재 route의 bound judge input 실행 횟수와 judge model call count는 0이다. 성공 전 생성된 별도 poisoned
root의 zero-byte final receipt는 resume·삭제·승격하지 않고 보존한다. 따라서 이 단위가 닫혀도
`officialCommercialUX=false`, `ScoreBearingCaptureAllowed=false`, `CommercialUXProxy=null`이며,
full-flow는 `UNAVAILABLE_NOT_IMPLEMENTED`, native capture는 계속 금지다.

전체 UX-R1 독립 검토는 source boundary `2b0b6ee355790b73cc47eb17c17bd737bdcf8d9a`에서
`PASS_FOR_UX_R1_WHOLE_GATE_CLOSURE`, P0 0/P1 0으로 끝났다. 이 종료는 official judge, native UX evidence,
platform attestation, score-bearing capture 또는 `CommercialUXProxy` 점수를 승인하지 않는다.

수정 allowlist는 `tools/commercial-ux/native/`, 그 디렉터리의 deterministic test, 이 scope와 현재 상태
문서, `tools/commercial-ux/README.md`다. 기존 untracked native 파일과 과거 V2 branch는 설계 참고일 뿐
자동 채택하지 않는다. `game/`, `src/`, `data/`와 제품 art asset은 이 gate에서 수정하지 않는다.

종료 조건은 다음과 같다.

- candidate manifest가 V3/R2의 exact source·project·runtime bytes와 기본/비기본 scene 사실을 결속한다.
- replay/checkpoint authority와 full-flow exception이 서로를 사칭할 수 없다.
- model/effort/freshness를 repository JSON 자기선언만으로 공식화하지 않고 platform/API receipt 또는
  동등한 transcript authority를 session hash에 결속한다.
- evidence item, actor result, judge input과 aggregate가 같은 finalized session과 candidate bytes를
  참조하며 누락·교체·경로 이동·재집계를 mutation test가 거부한다.
- 실제 native capture는 계속 금지하고 독립 P0/P1 review와 문서 상태를 먼저 닫는다.

### UX-R2 — 실시간 게임 완결성 순차 계획 — UX-R2.3 NORTH_BANK scope 활성

- tutorial 3장과 본편 5장의 native presentation·진행·result transition을 구현한다.
- full campaign, promise accumulation, finale·epilogue, save/resume를 실제 R2 경로로 연결한다.
- future-event status bar가 모든 장의 event·construction·thermal 경계를 표현하도록 한다.
- 이 gate를 열 때 제품 A1–A5 경계와 수정 allowlist를 현재 상태 문서에서 명시적으로 재조정한다.

위 네 항목은 비권한 roadmap outcome이다. UX-R2.1과 UX-R2.2 exact allowlist는 완료 이력이며 현재
실행할 수 있는 새 runtime 구현 단위는 아래 UX-R2.3 exact allowlist뿐이다.

#### UX-R2.1 — FIRST_LIGHT release tutorial/rail — 완료

이 단위의 player outcome은 nondefault Debug R2에서 실제 authored `FIRST_LIGHT` 장 briefing을 닫고,
`FIRST_LIGHT_SUPPLY` phase/event 동안 실시간 clock·공사·사건을 조작해 authored standard result까지
도달하면서 현재 시각, 다음 사건 countdown, event start/end와 actual/draft construction completion을
같은 future-event bar에서 읽는 것이다. 기존 기술 fixture campaign ID `FIRST_LIGHT`와 release chapter
ID `FIRST_LIGHT`는 이름만 같으므로 route/source identity로 엄격히 구분한다.

허용 파일은 다음 exact 목록이다.

- 새 `src/Gridworks.Core/Release/V3/RealtimeCampaignOverlayLoader.cs`
- `game/Gridworks.Game.csproj`
- `game/realtime/r2/RealtimeSliceResources.cs`
- `game/realtime/r2/RealtimeSliceMain.cs`
- `game/realtime/r2/RealtimeSlicePresenter.cs`
- `game/realtime/r2/RealtimeSliceCheckpoint.cs`
- `game/realtime/r2/RealtimeSliceCheckpointRunner.cs`
- 새 `game/realtime/r2/RealtimeInteractiveCheckpointHost.cs`
- 새 `game/realtime/r2/RealtimeInteractiveCheckpointHost.cs.uid`
- 새 `game/realtime/r2/RealtimeInteractiveCheckpointHost.tscn`
- `game/realtime/ui/RealtimeUiContracts.cs`
- `game/realtime/ui/RealtimeEventRail.cs`
- `game/realtime/ui/RealtimeEventRail.tscn`
- `game/realtime/ui/RealtimeUiLayoutHarness.cs`
- `game/realtime/r2/RealtimeR2Smoke.cs`
- `tools/Gridworks.RealtimeChecks/Program.cs`
- `tools/Gridworks.CommercialChecks/Program.cs`
- `tools/commercial-ux/README.md`
- `README.md`
- `docs/README.md`
- `docs/ROADMAP_2D.md`
- `docs/ROADMAP_2D_CHECKLIST.md`
- `docs/product/COMMERCIAL_UX_EVALUATION_PROTOCOL_KO.md`
- `docs/scopes/ASSET_STYLE_REALTIME_GAME.md`
- `docs/scopes/COMMERCIAL_UX_87.md`

`data/**`, `game/assets/realtime/**`, `game/realtime/world/**`, `game/project.godot`, export preset/package,
V3 persistence, default scene과 2–8장 runtime presentation은 금지한다. release V2+V3 raw bytes를 shared
strict loader가 in-memory로 조합한 exact first-chapter prefix만 쓴다. A1 art, A2 thermal presentation,
A3 catalog와 A4 campaign/save는 계속 미개방이다.

종료 조건은 다음과 같다.

- shared loader가 release V2+V3를 strict하게 조합하고 exact first chapter/event prefix를 source hash와
  함께 검증한다. test-only private composer는 같은 권위를 재구현하지 않는다.
- briefing close→production reducer/input→clock·construction·event→authored standard result가 실제
  scene에서 끝나며 generic 결과 copy로 대체하지 않는다.
- `FIRST_LIGHT/briefing`과 `FIRST_LIGHT/result/standard`는 기존 exact story-part unit bytes와
  native presentation의 동일 authored card를 비교한다. 나머지 32 atom은 native 도달을 주장하지 않는다.
- future-event bar는 typed minute 하나로 현재 시각, persistent next-event countdown, event start/end,
  actual active/completed construction과 draft completion을 한 줄에 표시한다. compact marker는
  state·severity·source·kind·cluster count를 색 외 cue로 남기고, hover 상세 정보와 AX selector에서
  exact source·timing·title·description을 복구한다.
- Debug interactive host는 exact A1 checkpoint에서 paused로 대기하고 실제 mouse/keyboard production UI만
  받아 한 minute 경계를 진행한다. 자동 HUD press·frame injection이나 화면 속 actor hint를 쓰지 않는다.
- 기존 두 A1 checkpoint의 start/end canonical hash와 headless oracle, story manifest/part, 1×·2×·4×
  chunk invariance, FHD UI 100/125%, keyboard/focus 회귀가 그대로 PASS한다.
- source commit 뒤 full chapter는 exact scene에 `--release-chapter=FIRST_LIGHT` 하나만 주어 실행하고
  authored standard result를 닫을 때 console record `FORMATIVE_DIRECT_PLAY_PASS:FIRST_LIGHT`를 남긴다.
- checkpoint는 interactive host에 `--checkpoint=A1_NORMAL_READY` 또는
  `--checkpoint=A1_CONSTRUCTION_DUE_1M` 하나만 주고 실제 HUD 1× 입력으로 한 minute 경계를 넘은 뒤 각각
  `TARGETED_LIVE_CHECKPOINT_PASS:A1_NORMAL_READY`,
  `TARGETED_LIVE_CHECKPOINT_PASS:A1_CONSTRUCTION_DUE_1M`을 남긴다.
- 이 세 record는 비점수 개발 관찰이며 candidate/evidence/judge/score artifact를 만들지 않고
  `CommercialUXProxy`에 들어가지 않는다.
- bounded 독립 review P0 0/P1 0과 현재 상태 문서·commit을 먼저 닫는다.

고정 직접 실행 명령은 다음 세 개뿐이다.

```sh
./.tools/godot-4.7.1/Godot_mono.app/Contents/MacOS/Godot \
  --path game --scene res://realtime/r2/RealtimeSliceMain.tscn \
  --windowed --resolution 1280x720 \
  -- --release-chapter=FIRST_LIGHT

./.tools/godot-4.7.1/Godot_mono.app/Contents/MacOS/Godot \
  --path game --scene res://realtime/r2/RealtimeInteractiveCheckpointHost.tscn \
  --windowed --resolution 1280x720 \
  -- --checkpoint=A1_NORMAL_READY

./.tools/godot-4.7.1/Godot_mono.app/Contents/MacOS/Godot \
  --path game --scene res://realtime/r2/RealtimeInteractiveCheckpointHost.tscn \
  --windowed --resolution 1280x720 \
  -- --checkpoint=A1_CONSTRUCTION_DUE_1M
```

실제 입력 관찰은 다음 exact non-score record로 닫혔다.

- full chapter: `FORMATIVE_DIRECT_PLAY_PASS:FIRST_LIGHT`; 17:00 변전소 발주, 19:01 완공,
  20:40 서부 전원선 완공, 20:51 서비스 선로 완공, 21:00–22:00 사건 뒤 authored positive result
  `동부 첫 구간을 인계받았습니다`, 최종 cash 7,030,000
- `A1_NORMAL_READY`: start `7094f631c89fe072800858a205d08358be07a6e0e7341b83026ff619fc03f9a3`,
  replay `4f4d3748681585f49eeb4291262db3c99676baba10913450c94d5e1eda9e1611`, end
  `d61217a830053e59f9c75a69eef110da2604892baf9b52ea74cb04d406ad6fec`, minute 1020→1021
- `A1_CONSTRUCTION_DUE_1M`: start
  `3a00c6c937d130cc7574e3971403445cb036a26aecba6671e300e1398d4b9989`, replay
  `9bd7c3226fd36396d9d9f7a8d81da25379cedb8e0e54441601bb7c89e947c65c`, end
  `304b96410d7652db9928613fe77443d8d50e29efcb273ff8061c064f876f37f9`, minute 1259→1260

현재 source authority는 위 `e385707071e4ccfb34d5200e3401897db7f164ad`이고 shared loader source identity는 V2
`078df95f9f0c833be7e1a299088b4ab6e0de4ddf13426ce5b96a1abbeee70b7a`, V3 overlay
`ef962a272683bfd6761fbf10a0ca14cb6c8bf90cdfde810b468ad451088f2258`, full composed
`7bd151399040934cfcb9f7c96d2879aef6354cda79ced2af184641eb33a02f09`, FIRST_LIGHT prefix
`94379c0e8e4dae54b760a55df8c1143c975eaa12f11079e675b2e67ba57df88e`, release world V3
`a0a837717bbd6d35f655d8094dfa6daac182d47b2d03f24b18c4883c04feecdf`다. 이는 package가 아닌
logic/presentation carve-out이므로 manifest aggregate는 `N/A — non-package carve-out`이다. 첫 source
review가 찾은 무공사 positive-result 위조 P1을 수정한 뒤 first-light 재검토는
`PASS_FOR_UX_R2_1_SOURCE_MAJOR_UNIT`, 단일 rail 재검토는 `PASS_FOR_SINGLE_RAIL_MAJOR_UNIT`이며 모두
P0 0/P1 0이다. 세 actual-input record와 현재 상태 문서를 함께 보존해 UX-R2.1을 완료했다.

#### UX-R2.2 — tutorial prefix through SECOND_SOURCE — 완료

이 단위의 player outcome은 새 nondefault Debug route에서 `FIRST_LIGHT`의 망·현금·시계를 잃지 않고
`SECOND_HEART`, `SECOND_SOURCE`까지 세 tutorial 장을 연속 완료하는 것이다. 각 장은 이전 authored
result→다음 authored briefing 순서로 같은 Core minute에서 정지 전환하고, story modal을 읽는 동안
시각이 흐르지 않으며 닫은 뒤 기존 속도를 복구한다. 개별 2·3장 시작 route는 누적 인과를 위조하므로
만들지 않는다.

학습 보조는 장마다 의도적으로 줄인다.

- `SECOND_HEART`: HUD의 Core-owned 접속 조건 `n/2`, announced flood marker를 선택했을 때의 호박색
  forecast 위험구역, active 시 적색 채움만 맥락형으로 남긴다. 단계별 건설 정답은 주지 않는다.
- `SECOND_SOURCE`: authored briefing, 한 줄 rail, 일반/보강 선로의 typed 비용·공기·용량과 existing
  actual/draft forecast만 남긴다. 추가 단계 prompt나 정답 경로는 만들지 않는다.

구현은 기존 두 결함을 닫았다. 새 Core evaluator가 commissioned incident edge를 단일 권위로 계산하고,
V2 승인 의미에 맞춰 첫 authored 시험 시작 minute의 requirement fact를 동결해 장의
`ObjectiveSatisfied`를 판정한다. R2 controller는 same-minute `ChapterCompleted`→`ChapterStarted`의
result→briefing을 FIFO modal로 보존한다. requirement가 없는 기존 slice는 optional fact를 직렬화하지
않아 동결 checkpoint canonical bytes를 보존한다. Game은 회선 수를 다시 계산하지 않는다.

허용 파일은 다음 exact 목록이다.

- 새 `src/Gridworks.Core/Release/V3/RealtimeConnectionRequirementEvaluator.cs`
- `src/Gridworks.Core/Release/V3/RealtimeRunContracts.cs`
- `src/Gridworks.Core/Release/V3/RealtimeCampaignRun.cs`
- `game/realtime/r2/RealtimeSliceResources.cs`
- `game/realtime/r2/RealtimeSliceMain.cs`
- `game/realtime/r2/RealtimeSliceMain.Smoke.cs`
- 새 `game/realtime/r2/RealtimeTutorialChapterFlow.cs`
- 새 `game/realtime/r2/RealtimeTutorialChapterFlow.cs.uid`
- `game/realtime/r2/RealtimeSlicePresenter.cs`
- `game/realtime/r2/RealtimeWorldView.cs`
- `game/realtime/r2/RealtimePlaceholderMap.cs`
- `game/realtime/r2/RealtimePlaceholderMap.Smoke.cs`
- `game/realtime/r2/RealtimeR2Smoke.cs`
- `game/realtime/ui/RealtimeUiLayoutHarness.cs`
- `tools/Gridworks.RealtimeChecks/Program.cs`
- `tools/commercial-ux/README.md`
- `README.md`
- `docs/README.md`
- `docs/ROADMAP_2D.md`
- `docs/ROADMAP_2D_CHECKLIST.md`
- `docs/product/COMMERCIAL_UX_EVALUATION_PROTOCOL_KO.md`
- `docs/scopes/ASSET_STYLE_REALTIME_GAME.md`
- `docs/scopes/COMMERCIAL_UX_87.md`

`RealtimeCampaignOverlayLoader.LoadPrefix(..., 3)`와 embedded release V2/V3 data는 이미 strict authority를
제공하므로 loader, `game/Gridworks.Game.csproj`와 `data/**`는 수정하지 않는다. event rail 구현도
UX-R2.1 source를 그대로 보존한다. `game/assets/realtime/**`, `game/realtime/world/**`, persistence,
default scene, export/package, 4–8장과 promise/finale/epilogue presentation은 금지한다. A1–A5 제품 art
gate와 score-bearing capture도 계속 미개방이다.

종료 조건은 다음과 같다.

- exact `--release-through=SECOND_SOURCE` 하나가 canonical 3장/5 event prefix만 열고 기존
  `--release-chapter=FIRST_LIGHT`와 technical checkpoint route를 바꾸지 않는다.
- absolute minute는 FIRST_LIGHT `1020`, event `1260–1320`; SECOND_HEART start `1320`, event
  `1680–1740`, `1800–1860`; SECOND_SOURCE start `1860`, event `2280–2340`, `2400–2460`과 일치한다.
- current/comparison draft 접속 fact는 같은 Core evaluator를 사용한다. 첫 병원 시험 시작 시 1/2이면
  이후 공사로 positive result를 소급 위조하지 않고, 2/2라도 두 회랑이 모두 범람구역에 있으면
  `FLOOD_ISOLATION_TEST` safety failure가 authored positive result를 막는다.
- successful path는 한 안전 회랑이 남아 두 SECOND_HEART event를 통과하고, SECOND_SOURCE의 west/south
  source 시험을 actual path·연속 한계 안에서 통과한다.
- modal 순서는 FIRST_LIGHT result→SECOND_HEART briefing→flood event story→SECOND_HEART result→
  SECOND_SOURCE briefing→south-source event story→final result다. positive authored card는 exact
  `ObjectiveSatisfied`일 때만 보이며 failure는 factual generic result를 사용한다.
- announced forecast risk는 rail 선택+analysis에서 호박색 무채움/패턴 outline, active risk는 적색
  채움/solid outline으로 색 외에도 구분된다. 둘은 같은 authored risk ID를 사용한다.
- 한 줄 chronological rail의 5 event 순서, hover 상세, AX selector, actual/draft construction과 기존
  FIRST_LIGHT 선택 연동은 full UI scale matrix에서 회귀하지 않는다.
- 기존 34-part manifest와 text-plan source는 바꾸지 않는다. 다음 네 canonical selector stdout hash를
  각각 고정하고 단독 실행한다.

```text
SECOND_HEART/briefing         aa7b6c5dbe1bda8af4290c455607bdf6ffa146d20916be4e9f6a95c514f8cf8f
SECOND_HEART/result/standard  8050f89cbbac84fe71bccd71accd12ddc35908c30c7f16891dbacee672135f3f
SECOND_SOURCE/briefing        e91e68d1f17a910ae81ebb904ec4b8430ccbb0d814679cc5ebb462f2540eae37
SECOND_SOURCE/result/standard bf4cee64a62ef23130972fd82d98f25fbd6ff40a6f584f97007500cf104e716c
```

- `FLOOD_ISOLATION_TEST`, `SOUTH_SOURCE_COMMISSIONING_TEST`의 authored phase story는 native event-start
  modal에서 V2 bytes와 직접 비교한다. null인 두 decision-window story를 새 selector로 만들지 않는다.
- V2/V3 raw와 full-composed hash, FIRST_LIGHT product behavior, 기존 두 A1 checkpoint start/replay/end
  hash, 34-part manifest, full Realtime/Commercial/text tooling 회귀가 모두 PASS한다.
- source commit 뒤 production mouse/keyboard만 쓰는 fresh-process 한 경로로 세 authored positive
  result와 `FULL_FLOW_E2E_PASS:TUTORIAL_THROUGH_SECOND_SOURCE`를 남긴다. marker hover와 keyboard
  selection은 각각 한 번 포함하되 이 record는 non-score 개발 관찰이다.
- bounded 독립 review P0 0/P1 0, 수정·재검증과 현재 상태 문서를 먼저 닫는다.

고정 직접 실행 명령은 하나다.

```sh
./.tools/godot-4.7.1/Godot_mono.app/Contents/MacOS/Godot \
  --path game --scene res://realtime/r2/RealtimeSliceMain.tscn \
  --windowed --resolution 1280x720 \
  -- --release-through=SECOND_SOURCE
```

성공한 각 authored result를 실제 production handler로 닫을 때
`FORMATIVE_DIRECT_PLAY_PASS:FIRST_LIGHT`, `FORMATIVE_DIRECT_PLAY_PASS:SECOND_HEART`,
`FORMATIVE_DIRECT_PLAY_PASS:SECOND_SOURCE`를 순서대로 출력하고, 세 장이 모두 성공한 마지막 close만
`FULL_FLOW_E2E_PASS:TUTORIAL_THROUGH_SECOND_SOURCE`를 추가한다. headless smoke나 controller injection은
이 actual-input label을 대신하지 않는다.

완료 근거는 다음과 같다.

- source `659709d`(`feat: extend realtime tutorial through second source`) 뒤 commit-bound 결정론 재검증이
  known checkpoint argument를 nested main이 거부하는 회귀를 발견했고, exact technical checkpoint
  route 보존 fix `40ed3fa`(`fix: preserve exact technical checkpoint routes`) 뒤 전체 검증을 다시 통과했다.
- bounded verdict `PASS_FOR_UX_R2_2_SOURCE_COMMIT`,
  `PASS_FOR_UX_R2_2_SOURCE_FIX_COMMIT`; 각각 P0 0/P1 0
- Debug build 0 warnings/0 errors, Realtime 24 suites/778 assertions, Commercial 31 suites/7084
  assertions, text tools 34 parts/16 mutations, 34-part manifest, full UI scale matrix PASS
- 위 네 story-part SHA-256와 두 A1 checkpoint의 start/replay/end SHA-256 불변
- fresh-process production mouse/keyboard 경로에서 세 `FORMATIVE_DIRECT_PLAY_PASS:*`와
  `FULL_FLOW_E2E_PASS:TUTORIAL_THROUGH_SECOND_SOURCE`를 순서대로 생성
- authored `FLOOD_ISOLATION_TEST`, `SOUTH_SOURCE_COMMISSIONING_TEST` event story와 세 positive result를
  직접 관찰했다. keyboard candidate 선택, active flood 적색 solid fill, rail marker 클릭·선택은
  확인했다. pointer는 marker에 놓였지만 custom hover-only popup 출현은 시각적으로 관찰하지 않았으며,
  그 계약은 full UI scale matrix와 AX 검증 근거로만 보존한다.

따라서 UX-R2.2는 비점수 완료다. 이 evidence 자체는 official capture나 `CommercialUXProxy`가 아니며
UX-R2.3이나 4–8장 runtime을 자동으로 열지 않았다. 아래 UX-R2.3은 별도 exact scope로 한 장만 연다.

#### UX-R2.3 — NORTH_BANK_PROMISE branch/deadline — scope 활성

이 단위의 player outcome은 새 nondefault Debug route에서 앞 세 장의 actual 망·현금·시계를 잃지 않고
첫 본편 장 `NORTH_BANK_PROMISE`를 완료하는 것이다. 단독 4장 route는 누적 인과를 위조하므로 만들지
않는다. `SECOND_SOURCE` 결과에서 player가 명시적으로 6개월 뒤 북안 검토로 이동하고, 정수장 안전
의무를 지키면서 북안 입주 일정을 Keep/Defer 중 선택한다. deadline, 두 operating event와 분기 결과는
기존 한 줄 chronological rail과 authored story에서 같은 Core truth로 읽혀야 한다.

권위 시간은 exact하다.

- `SECOND_SOURCE` 종료 `2460`
- authored gap `262800`; `NORTH_BANK_PROMISE` start `265260`, thermal reset과 2,000,000원 grant는 한 번
- `NORTH_BANK_MOVE_IN_PROMISE` deadline `265680`
- `NORTH_BANK_COMMISSIONING` reveal `265260`, start/end `265740–265830`
- `NEXT_HOT_EVENING_FORECAST` reveal `265470`, start/end `265950–266070`
- cumulative 4장/7 event prefix 완료 `266070`

262800분을 4×로 그대로 기다리면 18.25시간이므로 live streaming을 멈춘 turn으로 바꾸지 않고 explicit
calendar transition을 제공한다. `SECOND_SOURCE` result action은 다음 chapter의 typed
`ChapterStartMinute`를 표시하고, 실제 handler가 `RealtimeCampaignRun.AdvanceTo(...)`로 이동한다. 그
사이에 완료되는 actual construction transition, 망·현금은 보존하고 thermal reset은 chapter start에서만
적용한다. 숨은 minute 대입, bespoke NORTH_BANK save/checkpoint나 frame 장시간 재생은 금지한다.

promise UI는 기존 surface를 재사용한다.

- typed `ChapterStartMinute + PromiseDecisionDeadlineOffsetMinutes`를 기존 한 줄 rail의
  `Decision` marker `PROMISE_DEADLINE:NORTH_BANK_MOVE_IN_PROMISE`로 표시한다.
- marker 상세/AX는 마감 시각, Unset의 Core Keep forecast 가정, explicit Keep/Defer, deadline auto-Defer,
  locked/completed 상태를 색 외 text로 복구한다. next summary는 commissioning보다 이 deadline을 먼저
  가리킨다.
- marker 선택 시 기존 ContextDock의 primary/secondary action이 authored keep/defer label을 보여 주고
  production `SetPromiseDecision` command만 보낸다. 선택 자체는 Core state나 minute를 바꾸지 않는다.
- Core가 허용하는 마감 전 변경은 즉시 typed forecast에 반영한다. `265679` 결정은 허용하고 `265680`
  결정은 `PromiseDeadlinePassed`로 무변경 거부한다. unset deadline은 `PromiseDefaulted` 정확히 한 번으로
  Defer가 된다.
- forecast와 completed history는 safety와 promise를 별도 cue로 표시한다. Unset forecast가 Keep을
  가정한다는 사실, Defer 뒤 North load가 의무에서 빠진 사실, Keep의 promise-unserved minute를 숨기지
  않는다.

authored FIFO는 exact하다.

```text
SECOND_SOURCE result
→ explicit 6-month calendar transition
→ NORTH_BANK_PROMISE briefing
→ NORTH_BANK_PLANNING_WINDOW story
→ live promise/construction planning
→ NEXT_HOT_EVENING_FORECAST event story
→ exact kept/deferred result or factual generic failure
```

`NORTH_BANK_COMMISSIONING`의 story는 null이므로 modal을 만들지 않는다. successful explicit Keep은 safety와
promise를 모두 충족할 때 exact kept card, successful explicit Defer는 safety를 충족할 때 exact deferred
card를 사용한다. safety/promise failure는 authored positive branch card를 보이지 않는다. auto-Defer는
deferred result를 사실대로 표시할 수 있지만 explicit-choice formative PASS는 만들지 않는다.

허용 파일은 다음 exact 목록이다.

- `game/realtime/r2/RealtimeSliceResources.cs`
- `game/realtime/r2/RealtimeSliceMain.cs`
- `game/realtime/r2/RealtimeSliceMain.Smoke.cs`
- `game/realtime/r2/RealtimeTutorialChapterFlow.cs`
- `game/realtime/r2/RealtimeSlicePresenter.cs`
- `game/realtime/r2/RealtimeR2Smoke.cs`
- `game/realtime/ui/RealtimeUiLayoutHarness.cs`
- `tools/Gridworks.RealtimeChecks/Program.cs`
- `tools/commercial-ux/README.md`
- `README.md`
- `docs/README.md`
- `docs/ROADMAP_2D.md`
- `docs/ROADMAP_2D_CHECKLIST.md`
- `docs/product/COMMERCIAL_UX_EVALUATION_PROTOCOL_KO.md`
- `docs/scopes/ASSET_STYLE_REALTIME_GAME.md`
- `docs/scopes/COMMERCIAL_UX_87.md`

`RealtimeCampaignOverlayLoader.LoadPrefix(..., 4)`가 기존 strict V2/V3 authority를 제공한다. 따라서
`data/**`, `RealtimeCampaignOverlayLoader.cs`, 다른 `src/Gridworks.Core/**`, `game/Gridworks.Game.csproj`,
event-rail source/scene, runtime assets/world, persistence, default scene와 project/export/package는 수정하지
않는다. 5–8장, campaign promise ledger, save/resume, finale/epilogue, evaluator/candidate와 score-bearing
capture도 미개방이다.

종료 조건은 다음과 같다.

- exact `--release-through=NORTH_BANK_PROMISE` 하나만 canonical 4장/7 event prefix를 열고 기존
  FIRST_LIGHT, SECOND_SOURCE, 두 technical checkpoint와 malformed/unknown/mixed fail-closed route를
  보존한다.
- prefix identity와 위 absolute minute, direct/chunked calendar jump, gap 중 construction completion,
  cash/world continuity, one-time grant/thermal reset을 canonical Core state와 transition으로 검증한다.
- Keep success, Keep promise failure, explicit Defer success, safety failure, unset auto-Defer, 265679/265680
  boundary와 선택 전/후 forecast branch를 결정론적으로 검증한다.
- modal FIFO와 authored bytes, exact kept/deferred/generic result, failed/auto-defaulted evidence-token 차단을
  production controller smoke로 검증한다.
- one-line rail deadline marker의 next ordering, hover 상세, AX, keyboard focus, 두 ContextDock action,
  promise/safety cue를 full FHD/QHD/UHD scale matrix와 actual input path에서 검증한다.
- 기존 34-part manifest와 text-plan source는 바꾸지 않는다. 아래 네 selector stdout hash를 각각 단독
  실행한다.

```text
NORTH_BANK_PROMISE/briefing                          6fcfec395bfd8b68c272547205f71f5068b1c4dd4c531b63528dc59ccf786b38
NORTH_BANK_PROMISE/window/NORTH_BANK_PLANNING_WINDOW c38bb924cd526fa54def0fe9532b3fc15a55ac1cb301144ac86226f6154cdf03
NORTH_BANK_PROMISE/result/keep                       baf730fdac67bd72e1cc3ceb5b1a162b1b3c8414e2f45238183975b6be46f420
NORTH_BANK_PROMISE/result/defer                      f6f8657f3f223724dbeb749ab80a7ef0b74673989323e5f8d7a42b08c1c2547e
```

- full Realtime/Commercial/text tooling, 34-part manifest, 전체 UI scale matrix, 기존 checkpoint hash와
  UX-R2.2 actual behavior가 PASS한다.
- source commit·build·bounded source review P0 0/P1 0 뒤 fresh-process production mouse/keyboard 한
  KEEP path만 비점수 관찰한다. 이전 세 `FORMATIVE_DIRECT_PLAY_PASS:*` 뒤 exact
  `FORMATIVE_DIRECT_PLAY_PASS:NORTH_BANK_PROMISE:KEEP`와
  `FULL_FLOW_E2E_PASS:RELEASE_PREFIX_THROUGH_NORTH_BANK_PROMISE`를 남긴다.
- current-state docs와 bounded closure review P0 0/P1 0을 닫는다.

고정 직접 실행 명령은 source commit·build·review 뒤 다음 하나만 허용한다.

```sh
./.tools/godot-4.7.1/Godot_mono.app/Contents/MacOS/Godot \
  --path game --scene res://realtime/r2/RealtimeSliceMain.tscn \
  --windowed --resolution 1280x720 \
  -- --release-through=NORTH_BANK_PROMISE
```

이 record는 non-score 개발 관찰이며 headless smoke나 controller injection으로 대체하지 않는다.

### UX-R3 — actual E2E와 87 반복 — 미개방

- fresh user-data와 actual input으로 cold 3 + coverage를 실행한다.
- fresh blind judge 3 + verifier + oracle을 집계한다.
- `CommercialUXProxy < 87`이면 blinded evidence가 지지하는 가장 작은 원인을 수정하고 새 candidate와
  새 session으로 반복한다.

## 8. 공식 성공 조건

다음을 모두 만족해야 종료한다.

- `CommercialUXProxy >= 87.0`
- 모든 required cell `>=70`
- journey, tutorial, hierarchy, feedback, causality category `>=85`
- recovery, accessibility, Korean category `>=85`
- future-event status bar의 다음 사건·공사·열 경계가 cold와 coverage 모두에서 검증됨
- crash, soft-lock, state corruption, save mismatch, 누락 장/result/epilogue hard gate 0
- material unsupported evidence claim 0
- candidate/source/evidence/judge provenance가 하나의 finalized session에 결속됨

이 조건은 사람 미감·재미·한국어·전력설비 전문 검토, 실제 외부 hardware, 서명·공증과 공개 출시를
승인하지 않는다.

## 9. 현재 상태

```text
ActiveScope = NONE_USER_STOP_AFTER_REALTIME_G3_MAIN_CONSOLIDATION
ActiveEvaluationGate = SUSPENDED_AT_USER_REQUEST_AFTER_UXR23_SOURCE_REVIEW
NextEvaluationGate = NONE_USER_REQUESTED_STOP_AFTER_G3_APPLICATION
NextCandidate = NONE_USER_REQUESTED_STOP
UserAuthorization = EXPLICIT_RESOLVE_REALTIME_G3_SPLIT_AND_CONSOLIDATE_LOCAL_MAIN
ProductDirection = ASSET_STYLE_REALTIME_GAME
ProductArtGate = FULL_G3_R2_DEFAULT_ENTRY_AND_LOCAL_MAIN_CONSOLIDATION_COMPLETE
RuntimeArtAuthority = LOCAL_MAIN_CF5DA56_G3_TREE_57_PNG_MAP50_UI7_APPLIED_TO_LIVE_R2
A1G3ProductSource = COMMIT_1AF2B33_FULL_G3_R2_CANONICALIZATION
A1G3SourceReview = CURRENT_SCOPE_REVIEW_PASS_P0_0_P1_0
DefaultMainScene = RealtimeSliceMain
LocalBranchState = MAIN_ONLY_LOCAL_BRANCH
RealtimeAuthority = RELEASE_V3_PLUS_R2
RealtimeUxAuthority = R2_TUTORIAL_PREFIX_THROUGH_SECOND_SOURCE_PLUS_REVIEWED_UX_R2_3
FutureEventStatusBar = PASS_DETERMINISTIC_SINGLE_CHRONOLOGICAL_TRACK_COMPACT_MARKERS_CUSTOM_HOVER_DETAIL
StoryPartManifest = 34_AUTHORED_ATOMS_DETERMINISTIC_PASS
TextPlanProxy = 83.4475_FORMATIVE
TextJudgeExecutionReceipt = NOT_EXPORTED_FORMATIVE_ONLY
CommercialUXProxy = null
FullCampaignNativeE2E = NOT_IMPLEMENTED_THREE_CHAPTER_PREFIX_ONLY
ScoreBearingCaptureAllowed = false
NativeCapturePolicy = NOT_REQUESTED_USER_STOP_AFTER_G3_APPLICATION
NativeCaptureEnvironment = MAC_CONSOLE_UNLOCKED_NOT_AUTHORIZATION
UXR0ClosureReview = PASS_P0_0_P1_0_COMMIT_746C0AA
NativeCandidateAuthority = PASS_SOURCE_REVISION_379E980_SHA256_373785E4
EvaluatorProducerAuthority = FOUR_GIT_BLOBS_MATCH_CLT_GIT_REPLACE_AND_LAZY_FETCH_DISABLED
TargetedCheckpointAuthority = TWO_POSITIVE_THREE_REJECTION_INDEPENDENT_REPLAY_PASS
UXR1CandidateRouteReview = PASS_P0_0_P1_0_SOURCE_379E980
SessionAttemptAuthority = PASS_SOURCE_REVISION_5A31FF3_PRODUCER_SHA256_FAEA99AC
UXR1SessionAttemptReview = PASS_P0_0_P1_0_SOURCE_5A31FF3
EvaluationChainParentAuthority = PASS_SOURCE_REVISION_74BA725_PRODUCER_SHA256_D87E6054
UXR1ChainParentReview = PASS_P0_0_P1_0_SOURCE_74BA725
CurrentRouteArtifactAuthority = PASS_SOURCE_REVISION_A270339_PRODUCER_SHA256_225696AD
UXR1CurrentRouteArtifactReview = PASS_P0_0_P1_0_SOURCE_A270339
ControlledCodexTranscriptAuthority = PASS_LOCAL_NON_PLATFORM_SOURCE_2B0B6EE_RECEIPT_SHA256_F7C17C4A
UXR1ControlledTranscriptReview = PASS_SUBUNIT_P0_0_P1_0_SOURCE_2B0B6EE
UXR1ClosureReview = PASS_P0_0_P1_0_SOURCE_2B0B6EE
NativeEvaluatorAuthority = COMPLETE_CANDIDATE_ROUTE_SESSION_CHAIN_PARENT_BLOCKED_ARTIFACT_AND_CONTROLLED_TRANSCRIPT
UXR21GateOpeningReview = PASS_P0_0_P1_0
UXR21GateStatus = COMPLETE_NON_SCORE
UXR21ProductSourceAuthority = PASS_SOURCE_REVISION_E385707071E4CCFB34D5200E3401897DB7F164AD
UXR21SourceReview = PASS_FOR_UX_R2_1_SOURCE_MAJOR_UNIT_P0_0_P1_0_SOURCE_EC265999
UXR21SingleRailReview = PASS_FOR_SINGLE_RAIL_MAJOR_UNIT_P0_0_P1_0_SOURCE_E385707
UXR21ClosureReview = PASS_FOR_UX_R2_1_CLOSURE_MAJOR_UNIT_P0_0_P1_0_SOURCE_F2839D1
UXR21ActualInputObservation = PASS_THREE_NON_SCORE_RECORDS
InteractiveCheckpointHost = ACTUAL_INPUT_PASS_A1_NORMAL_READY_AND_A1_CONSTRUCTION_DUE_1M
FirstLightNativeStoryReachability = FORMATIVE_DIRECT_PLAY_PASS_AUTHORED_STANDARD_RESULT
UXR21DeterministicEvidence = BUILD_0_WARNINGS_REALTIME_23_673_COMMERCIAL_31_7084_UI_MATRIX_PASS_STORY_34
UXR22GateStatus = COMPLETE_NON_SCORE
UXR22GateOpeningReview = PASS_FOR_UX_R2_2_GATE_OPENING_P0_0_P1_0
UXR22ProductSourceAuthority = PASS_SOURCE_REVISION_40ED3FAB92A7054D6BC40D609AB6C5D1E1F801CC
UXR22MajorUnitSource = 659709DE2F654908DEE3E5FBC72D4106DF61E6CA
UXR22SourceReview = PASS_FOR_UX_R2_2_SOURCE_COMMIT_P0_0_P1_0_SOURCE_659709D
UXR22SourceFixReview = PASS_FOR_UX_R2_2_SOURCE_FIX_COMMIT_P0_0_P1_0_SOURCE_40ED3FA
UXR22DeterministicEvidence = BUILD_0_WARNINGS_REALTIME_24_778_COMMERCIAL_31_7084_TEXT_TOOLS_34_PARTS_16_MUTATIONS_STORY_34_UI_MATRIX_PASS_CHECKPOINT_HASHES_PRESERVED
UXR22ActualInputObservation = PASS_THREE_CHAPTER_RESULTS_PLUS_FULL_FLOW_NON_SCORE
UXR22MarkerNativeObservation = PASS_CLICK_SELECTION_CUSTOM_HOVER_ONLY_POPUP_NOT_OBSERVED
UXR22KeyboardCandidateObservation = PASS
UXR22ActiveFloodSolidFillObservation = PASS
UXR22ClosureReview = PASS_FOR_UX_R2_2_CLOSURE_MAJOR_UNIT_P0_0_P1_0_SOURCE_CF6398A
TutorialThreeChapterReachability = FORMATIVE_DIRECT_PLAY_PASS_THROUGH_SECOND_SOURCE
UXR23GateStatus = IMPLEMENTED_REVIEWED_NATIVE_OBSERVATION_DEFERRED
UXR23GateOpeningReview = PASS_FOR_UX_R2_3_GATE_OPENING_P0_0_P1_0_SOURCE_B0383D6
UXR23ProductSourceAuthority = PASS_SOURCE_FIX_D85BB3F
UXR23SourceReview = PASS_FOR_UX_R2_3_SOURCE_FIX_COMMIT_P0_0_P1_0
NorthBankPromiseNativeReachability = NOT_OBSERVED_USER_STOP
NorthBankPromiseDeadlineRail = IMPLEMENTED_REVIEWED
InterchapterCalendarTransition = IMPLEMENTED_REVIEWED
PublicReleaseStatus = NOT_AUTHORIZED
```
