# Gridworks — 실시간 상용 UX 87 활성 계약

> 상태: **활성 scope — UX-R1 완료 · UX-R2 미개방**
>
> 제품 방향 권위: [에셋 스타일 실시간 게임 계약](ASSET_STYLE_REALTIME_GAME.md)
>
> 목표: 고정 `gpt-5.6-sol` + reasoning effort `ultra`의 공식
> `CommercialUXProxy >= 87`

이 문서는 현재 사용자 지시가 연 **단일 작업 scope**다. 제품 방향은 turn 단위 진행이 아니라
pause·1×·2×·4× 속도, 계속 흐르는 시계, 미리 보이는 사건과 시간에 따른 공사·열 노출·정지·회복을
가진 실시간 전력망 게임으로 고정한다. `origin/main`의 Release.V3/R2 기반을 이 방향의 권위로
사용하며, 과거 `codex/commercial-ux-87`의 V2 runtime 구현을 합치지 않는다.

UX-R0는 source-bound 텍스트 artifact, 스토리 파트 단독 실행과 세 fresh text judge 기준점을 닫았다.
UX-R1은 V3/R2 candidate bytes, replay, session claim, evaluation-chain parent와 blocked artifact
provenance, local controlled transcript authority를 fail-closed로 포팅하고 전체 종료 검토까지 완료했다.
현재 열린 평가 gate는 없다. UX-R2, A1 runtime art와 전체 캠페인 presentation은 열지 않았으며 실제
score-bearing capture도 허용하지 않는다.

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
- 현재 native 평가 대상은 비기본 `RealtimeSliceMain`이다.
- R2 native presentation은 `FIRST_LIGHT` targeted slice만 확인됐다.
- `A1_NORMAL_READY`, `A1_CONSTRUCTION_DUE_1M` 두 checkpoint만 구현됐다.
- source revision `379e980`의 evaluator가 39-file Debug/editor candidate와 두 성공·세 인자 거부 probe를 exact
  bytes로 결속하고 독립 verifier에서 다시 실행한다. 이는 score-bearing native capture가 아니다.
- R2 `RealtimeEventRail`은 두 checkpoint에서 exact package scene-load wiring을 통과했지만, 실제
  플레이에서 future-event status bar로 충분히 읽히는지는 아직 관찰하지 않았다.
- 나머지 7장, full campaign transition, save/resume, finale·epilogue native E2E는
  `NOT_IMPLEMENTED`다.
- 동결 V2 기본 장면 `CommercialMain`이나 Core-only replay로 이 누락을 채우지 않는다.
- 현재 `CommercialUXProxy = null`이며 score-bearing capture는 허용되지 않았다.
- 같은 source-bound 입력의 첫 INITIAL panel은 `TP-A1` 불안정으로 점수가 성립하지 않았고 보존했다.
- 별도 세 fresh run의 두 번째 INITIAL panel은 `SCORED_FORMATIVE`, `TextPlanProxy = 83.4475`로
  안정 집계됐다. 플랫폼 서명 execution receipt는 저장소에 내보내지 못했다.
- UX-R1 local controlled transcript는 별도 fresh `gpt-5.6-sol`/`ultra` semantic-echo rollout을
  source-bound parent에 결속했다. 이는 platform attestation, judge 실행 또는 점수 증거가 아니다.

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
`RealtimeEventRail`을 출발점으로 사용하되 다음 결과를 만족해야 한다.

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

### UX-R2 — 실시간 게임 완결성 — 미개방

- tutorial 3장과 본편 5장의 native presentation·진행·result transition을 구현한다.
- full campaign, promise accumulation, finale·epilogue, save/resume를 실제 R2 경로로 연결한다.
- future-event status bar가 모든 장의 event·construction·thermal 경계를 표현하도록 한다.
- 이 gate를 열 때 제품 A1–A5 경계와 수정 allowlist를 현재 상태 문서에서 명시적으로 재조정한다.

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
ActiveScope = COMMERCIAL_UX_87_REALTIME
ActiveEvaluationGate = NONE
NextEvaluationGate = UX_R2_REALTIME_GAME_COMPLETENESS_NOT_OPENED
ProductDirection = ASSET_STYLE_REALTIME_GAME
ProductArtGate = NONE_A1_NOT_OPENED
RealtimeAuthority = RELEASE_V3_PLUS_R2
FutureEventStatusBar = REQUIRED_R2_EVENT_RAIL_HEADLESS_WIRING_PASS_NATIVE_QUALITY_NOT_OBSERVED
StoryPartManifest = 34_AUTHORED_ATOMS_DETERMINISTIC_PASS
TextPlanProxy = 83.4475_FORMATIVE
TextJudgeExecutionReceipt = NOT_EXPORTED_FORMATIVE_ONLY
CommercialUXProxy = null
FullCampaignNativeE2E = NOT_IMPLEMENTED
ScoreBearingCaptureAllowed = false
NativeCapturePolicy = FORBIDDEN_UX_R2_A1_NOT_OPENED
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
PublicReleaseStatus = NOT_AUTHORIZED
```
