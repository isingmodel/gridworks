# Gridworks — 실시간 상용 UX 87 활성 계약

> 상태: **활성 — UX-R0 실시간 텍스트 기준선**
>
> 제품 방향 권위: [에셋 스타일 실시간 게임 계약](ASSET_STYLE_REALTIME_GAME.md)
>
> 목표: 고정 `gpt-5.6-sol` + reasoning effort `ultra`의 공식
> `CommercialUXProxy >= 87`

이 문서는 현재 사용자 지시가 연 **단일 작업 scope**다. 제품 방향은 turn 단위 진행이 아니라
pause·1×·2×·4× 속도, 계속 흐르는 시계, 미리 보이는 사건과 시간에 따른 공사·열 노출·정지·회복을
가진 실시간 전력망 게임으로 고정한다. `origin/main`의 Release.V3/R2 기반을 이 방향의 권위로
사용하며, 과거 `codex/commercial-ux-87`의 V2 runtime 구현을 합치지 않는다.

현재 gate는 평가 계약, 작성 콘텐츠와 실시간 일정의 결속, 스토리 파트 단독 실행, 세 fresh text
judge의 기준점 생성까지만 허용한다. A1 runtime art gate는 열지 않는다. 다음 gate의 native
packager·capture·전체 캠페인 presentation을 미리 만들지 않는다.

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
- R2에는 `RealtimeEventRail`이 있지만 실제 플레이에서 future-event status bar로 충분히 읽히는지는 아직
  관찰하지 않았다.
- 나머지 7장, full campaign transition, save/resume, finale·epilogue native E2E는
  `NOT_IMPLEMENTED`다.
- 동결 V2 기본 장면 `CommercialMain`이나 Core-only replay로 이 누락을 채우지 않는다.
- 현재 `CommercialUXProxy = null`이며 score-bearing capture는 허용되지 않았다.

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

### UX-R0 — 실시간 텍스트 기준선 — 활성

- protocol/rubric/model identity를 후보 수정 전에 고정한다.
- base campaign과 V3 schedule에서 34-part manifest와 hash-bound artifact를 만든다.
- story part 단독 실행과 mutation coverage를 결정론적으로 닫는다.
- 세 fresh `gpt-5.6-sol` `ultra` judge로 첫 `TextPlanProxy`를 만든다.
- 결과는 formative이며 native 구현이나 official 87을 주장하지 않는다.

종료: exact build/suite, text tooling test, 3-judge aggregate, 독립 P0/P1 review와 문서 상태가 모두
일치한다.

### UX-R1 — native evaluator authority port — 미개방

- V3/R2 candidate bytes, replay authority, session claim과 evidence provenance를 새 권위에 맞게 닫는다.
- targeted checkpoint와 full-flow 예외의 actor recipe를 분리한다.
- score-bearing capture 전 독립 fail-closed review를 통과한다.

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
ActiveEvaluationGate = UX_R0_REALTIME_TEXT_BASELINE
ProductDirection = ASSET_STYLE_REALTIME_GAME
ProductArtGate = NONE_A1_NOT_OPENED
RealtimeAuthority = RELEASE_V3_PLUS_R2
FutureEventStatusBar = REQUIRED_R2_EVENT_RAIL_PRESENT_NATIVE_QUALITY_NOT_OBSERVED
StoryPartManifest = 34_AUTHORED_ATOMS_DETERMINISTIC_PASS
TextPlanProxy = NOT_YET_JUDGED_ON_REALTIME_TEXT_PROTOCOL_V2
CommercialUXProxy = null
FullCampaignNativeE2E = NOT_IMPLEMENTED
ScoreBearingCaptureAllowed = false
NativeCapture = BLOCKED_MAC_LOCKED
PublicReleaseStatus = NOT_AUTHORIZED
```
