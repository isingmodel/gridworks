# Gridworks GPT-5.6-sol ultra 상용 UX 평가 프로토콜

> 상태: **FROZEN v1 — 2026-08-22 첫 게임 후보 수정 전 동결**  
> actor·judge·verifier: **`gpt-5.6-sol`**, reasoning effort **`ultra`**  
> 목표: 공식 `CommercialUXProxy >= 87.0`

이 프로토콜은 Gridworks의 튜토리얼부터 에필로그까지가 실제 판매 후보로 느껴질 만큼 일관되고
이해 가능한 게임 경험인지 점수화한다. LLM은 자유 총점을 만들지 않고 미리 정한 checkpoint cell에
범주형 label과 관찰 근거만 반환한다. 숫자 변환, cell/category 합산, 불일치 penalty와 cap은
결정론적 도구가 수행한다.

LLM은 사람 취향, 전문 한국어 교정, 외부 기기 호환성 또는 공개 출시 승인이 아니다. build,
상태전이, story wiring/coverage, save 동일성, crash와 clipping처럼 기계가 증명할 수 있는 사실도
주관 점수에 섞지 않고 hard gate로 둔다.

## 1. 역할 분리

공식 run은 다음 역할을 같은 호출에서 겸하지 않는다.

1. 세 **cold actor**가 서로 다른 fresh user-data에서 native 앱을 직접 플레이하고 원본 trace를 만든다.
2. 세 **blind judge**가 actor ID를 익명화한 동일한 hash-pinned trace 세트를 각각 평가한다.
3. 별도 **blind verifier**가 label·점수·threshold 없이 관찰 근거의 존재만 확인한다.
4. 결정론적 oracle과 집계기가 typed 사실, hard gate, 숫자와 최종 verdict를 계산한다.

actor는 점수를 내거나 개선안을 쓰지 않는다. judge는 게임을 조작하지 않고 고정 evidence만 본다.
세 actor와 세 judge는 모두 `gpt-5.6-sol` ultra지만 이는 process 독립성일 뿐 model-family 다양성의
증거가 아니다. actor와 judge를 분리하지 못하거나 한 actor 기록을 세 번 재판정한 결과는
`FORMATIVE_REPLAY_ONLY`이며 공식 점수가 아니다.

## 2. 평가 레인

### 2.1 TEXT-PLAN — 구현 전 형성 평가

campaign 권위에서 자동 추출한 다음 입력만 세 fresh blind judge에 제공한다.

- 한 문단의 제품 premise와 플레이어 역할
- 장 순서와 각 장의 학습·위기·선택 의도
- 정상 도달하는 26개 briefing/window/result/epilogue selector와 authored text
- chapter/window/branch 같은 사실 ID와 native 노출 여부

source, test, 개발자의 해설, 이전 점수, 목표 87과 변경 희망사항은 숨긴다. 이 레인은 텍스트로
판정 가능한 journey, tutorial plan, causality, agency, pacing/payoff와 Korean만 형성 평가한다.
결과 이름은 `TextPlanProxy`이며 **공식 CommercialUXProxy에 합산하지 않는다**.

text-plan도 자유 category 인상을 쓰지 않는다. 실제 matrix에서 텍스트로 판정 가능한 다음 cell만
같은 비중으로 재사용한다.

| text category | cell과 category 내부 비중 |
|---|---|
| journey(12) | `TP-J1 chapter arc(50)`, `TP-J2 results→epilogue→replay closure(50)` |
| tutorial(13) | `TP-T1 First Light plan(40)`, `TP-T2 Second Heart plan(35)`, `TP-T3 Second Source/withdrawal plan(25)` |
| causality(13) | `TP-C1 first path(20)`, `TP-C2 corridor(20)`, `TP-C3 source/capacity(20)`, `TP-C4 thermal/flood/maintenance(20)`, `TP-C5 finale result(20)` |
| agency(10) | `TP-A1 route/cost/time(30)`, `TP-A2 North Bank(25)`, `TP-A3 Whose Margin(25)`, `TP-A4 finale promise(20)` |
| pacing(8) | `TP-P1 tutorial withdrawal(25)`, `TP-P2 chapters 4–7 escalation(35)`, `TP-P3 finale/epilogue payoff(40)` |
| Korean(4) | `TP-K1 tutorial/control(35)`, `TP-K2 operation/error/result(35)`, `TP-K3 story/epilogue(30)` |

같은 text artifact를 세 fresh judge가 각각 판정한다. cell별 세 label의 numeric median을 쓰고
`TextRawSpread`는 cell→category→전체 순서로 최고-최저 label 점수 차이를 같은 weight로 합산한다.

```text
TextCategoryScore = Sum(textCellWeight * median(three judge scores)) / 100
TextRaw            = Sum(applicableCategoryWeight * TextCategoryScore) / 60
TextRawSpread      = Sum(applicableCategoryWeight * weightedTextCellSpread) / 60
TextPlanProxy      = TextRaw - min(8, TextRawSpread * 0.20)
```

text cell의 ordinal range가 두 단계 이상이면 세 판정을 한 번 전부 교체 재실행하고 다시 불안정하면
`TextPlanProxy=null`, `BLOCKED_JUDGE_INSTABILITY`로 기록한다.

### 2.2 COLD-JOURNEY — 실제 초회 플레이

actor에게 fresh user-data, 기본 1920×1080 UI 100%의 native 앱과 “에필로그까지 플레이하세요”라는
목표만 준다. README, source, campaign JSON, test fixture, save 내부, log, terminal, web과 개발 대화는
볼 수 없다. 게임 안의 도움말, 설정, 취소, 되돌리기, 재시작과 저장은 정상 기능이므로 사용할 수 있다.

고정 흐름은 `새 게임 → 여덟 장 → 에필로그 → 장 선택`이다. 4장 진행 중 미리 정한 decision-window에서
저장·정상 종료하고 새 process에서 `계속`으로 재개한다. actor는 first-use probe마다 짧게 다음만
기록한다.

```text
current goal / expected visible consequence / cited visible in-game source
```

이는 숨은 추론이 아니라 게임 화면이 개념을 실제 노출했는지 확인하는 관찰이다. 모델의 사전 전력망
지식을 지울 수 없으므로 “초보 학습성을 증명했다”고 주장하지 않고, 외부 검색 없이 화면 근거를
인용해 진행했는지만 기록한다.

같은 checkpoint에서 진행 상태가 바뀌지 않은 채 서로 다른 합리적 in-product 행동 12개를 시도하면
`PLAYER_STALLED`로 끝낸다. inference/tool 대기시간은 세지 않는다. 입력 전달 실패는
`HARNESS_BLOCKED`로 분리한다.

### 2.3 COVERAGE-JOURNEY — 고정 누락 분기·접근성 관찰

cold actor가 고르지 않은 promise branch, 대표 실패→회복, UI 125%, keyboard-only, Reduce Motion,
완료 저장→재개를 고정 actual-input recipe로 실행한다. recipe, journal-derived save prefix, branch 순서,
capture point, audio 설정, retry 규칙과 모든 실패 시도는 **첫 게임 후보 수정 전에** manifest와 SHA로
동결한다. handcrafted save나 candidate마다 유리한 route/capture 선택은 금지한다.

coverage는 누락을 발견하고 cold 근거를 보완하지만 cold 실패를 올리거나 지울 수 없다. 같은 cell에
cold와 coverage가 모두 있으면 더 낮은 점수를 쓴다.

## 3. 고정 episode와 checkpoint

coverage recipe는 다음 episode를 모두 포함한다.

| ID | 고정 episode |
|---|---|
| `E00-TITLE` | title, settings, keyboard-only UI 125%, Reduce Motion |
| `E01-FIRST-LIGHT` | 물 위 불법 배치→유효 배치, draft 수정, 완공, 계획 amber→통전 cyan |
| `E02-SECOND-HEART` | 같은 위험 회랑 실패→독립 회랑 회복 |
| `E03-SECOND-SOURCE` | 서부 전원 정지, 전체 경로 용량, 튜토리얼 안내 감소 |
| `E04-NORTH-BANK` | 약속 keep와 defer 두 결과 |
| `E05-WHOSE-MARGIN` | 연속/비상 열 사용과 다음 국면 보호정지, 두 약속 결과 |
| `E06-FLOOD` | 기한, 위험구역, 거부→회복, 범람 우회 |
| `E07-MAINTENANCE` | 열 reset, 계획정지, 두 약속 결과 |
| `E08-FINALE` | 폭염→폭풍, 두 약속 결과, epilogue |
| `E09-MID-RESUME` | 4장 decision 중간 저장→프로세스 종료→계속 |
| `E10-COMPLETE-RESUME` | 완료 저장→계속→epilogue/장 선택 |
| `E11-AUTHORED-TEXT` | 정상 도달 26 selector와 native 표시 checkpoint 대응 |

gold state는 검증된 accepted command journal prefix를 fresh run에 replay해 만들며 결과 snapshot hash를
manifest에 둔다. 각 runtime capture는 지정 checkpoint를 처음 도달했을 때 한 번만 얻는다. audio를
평가하는 episode는 화면·input ledger와 동기화된 원본 audio를 함께 보존한다. audio가 없으면
`BLOCKED_MISSING_EVIDENCE`이며 audiovisual label을 추측하지 않는다.

## 4. label anchor

judge는 각 cell에 다음 label 하나만 선택한다. 숫자나 전체 PASS/FAIL을 직접 만들지 않는다.

| label | 점수 | 공통 관찰 anchor |
|---|---:|---|
| `EXCELLENT` | 100 | 배정된 필수 probe를 모두 화면 근거로 정확히 예측·완료했고 중요한 마찰이 없음 |
| `STRONG` | 85 | 외부 도움 없이 완료했고 작고 국소적인 망설임을 스스로 바로잡음 |
| `SERVICEABLE` | 70 | 완료했지만 반복되는 회복 가능한 불확실성·오독·우회가 있음 |
| `WEAK` | 40 | 외부 힌트/재시작이 필요하거나 되돌리기 어려운 행동 전까지 핵심 mental model이 크게 틀림 |
| `BROKEN` | 0 | 필요한 affordance·설명·경험이 관찰상 사실상 없거나 사용할 수 없음 |

`MISSING`, `INVALID`, `NOT_OBSERVED`는 0점 label이 아니라 판정 차단 상태다. 단, 아래 정의처럼 두
actor 이상에서 검증된 제품 `PLAYER_STALLED`로 이후 cell에 도달하지 못한 경우는
`NOT_REACHED_BY_PRODUCT`이며 결정론적으로 0점을 부여하고 cap 49를 적용한다. `EXCELLENT`는 결함을
못 찾았다는 뜻이 아니며 정확한 예측/완료와 구체적인 강점 근거가 모두 있어야 한다. 근거가 부족하면
최대 `STRONG`이다.

## 5. rubric과 category weight

포괄성 자체는 story·chapter·state coverage hard gate가 소유한다. 주관 rubric은 완결된 여정의 체감과
전달 품질을 평가한다.

| category | 가중치 | category anchor |
|---|---:|---|
| journey cohesion | 12 | title→학습→본편→결과→epilogue/replay가 현재 맥락을 잃지 않는 한 여정인가 |
| tutorial/learnability | 13 | 첫 세 장에서 목표·조작·규칙·실패 회복을 행동 속에서 배우고 안내가 줄어드는가 |
| state hierarchy | 13 | 현재 장·목표·actual/projection·의무/약속·경고·결과 경계가 구분되는가 |
| interaction feedback | 12 | 선택·배치·연결·취소·되돌리기·승인의 affordance와 피드백이 일관적인가 |
| causal legibility | 13 | 공급·실패·열·정지·비용 결과를 실제 경로·병목으로 설명하고 다음 판단에 쓰는가 |
| agency/trade-off legibility | 10 | route·비용·시간·여유·약속이 읽히는 trade-off와 다른 결과를 만드는가 |
| pacing/payoff | 8 | 장마다 질문이 확장되고 결과·마지막 장·epilogue가 선택의 축적을 회수하는가 |
| audiovisual integration | 6 | 지도·HUD·상태 cue·motion·sound가 같은 상태와 행동을 일관되게 강화하는가 |
| recovery orientation | 5 | 오류·재시작·저장·재개 뒤 현재 맥락과 다음 행동을 되찾는가 |
| accessible legibility | 4 | UI 100/125, keyboard, Reduce Motion과 색 외 cue에서 같은 필수 정보/행동을 읽는가 |
| Korean clarity/consistency | 4 | 목표·조작·경고·결과·story의 한국어가 자연스럽고 동일 개념을 일관되게 부르는가 |

deterministic keyboard trap/clipping/glyph/색 단독 cue는 hard gate다. accessible label은 그 사실을
통과한 경험의 가독성과 부담을, Korean label은 strict 문구 wiring을 통과한 의미 명료성을 평가한다.

## 6. category × checkpoint cell matrix

judge는 전체 인상을 한 번에 label하지 않는다. 아래 cell을 먼저 판정하고 category 안에서 표시된
고정 비중으로 합산한다.

| category | cell(weight within category) | evidence lane |
|---|---|---|
| journey | `J1 title→tutorial(20)`, `J2 result→next boundaries(30)`, `J3 mid-resume continuity(20)`, `J4 finale→epilogue→select(30)` | cold; J2/J4 coverage 보조 |
| tutorial | `T1 First Light goal/action(40)`, `T2 Second Heart corridor model(35)`, `T3 Second Source capacity/withdrawal(25)` | cold, coverage |
| hierarchy | `H1 objective/build state(25)`, `H2 actual/projection + must/promise(25)`, `H3 result boundary(25)`, `H4 heat/flood warning(25)` | cold, coverage |
| feedback | `I1 placement/draft correction(35)`, `I2 connection/energization(30)`, `I3 rejection/recovery/approval(35)` | cold, coverage |
| causality | `C1 First Light path(20)`, `C2 corridor independence(20)`, `C3 source/capacity(20)`, `C4 thermal/flood/maintenance(20)`, `C5 finale result(20)` | cold, coverage |
| agency | `A1 route/cost/time(30)`, `A2 North Bank branches(25)`, `A3 Whose Margin branches(25)`, `A4 finale promise(20)` | cold prediction + blinded coverage alternate |
| pacing | `P1 tutorial withdrawal(25)`, `P2 chapters 4–7 escalation(35)`, `P3 finale/epilogue payoff(40)` | cold chronological only |
| audiovisual | `V1 normal interaction(25)`, `V2 heat(25)`, `V3 flood(25)`, `V4 finale(25)` | synchronized coverage video+audio |
| recovery | `R1 invalid action→correction(30)`, `R2 mid-decision resume(45)`, `R3 completed resume/select(25)` | cold R2, fixed coverage |
| accessibility | `L1 keyboard-only(40)`, `L2 UI125 hierarchy(40)`, `L3 Reduce Motion/non-color cue(20)` | fixed coverage |
| Korean | `K1 tutorial/control(35)`, `K2 operation/error/result(35)`, `K3 authored story/epilogue(30)` | cold+coverage; K3 text manifest 보조 |

한 evidence가 여러 cell에서 사실상 같은 결함을 보일 때 가장 직접적인 cell 한 곳에서만 감점한다.
그 결함이 별도 행동 결과를 만든 경우에만 다른 cell에 그 결과를 기록한다. required cell 하나라도
`MISSING/INVALID/NOT_OBSERVED`이면 공식 점수를 만들지 않는다.

## 7. actor trace와 structured judgment

actor trace는 장/checkpoint마다 다음만 기록한다.

```text
actorId / episode / checkpoint / app-active action index
current goal / expected visible consequence / cited visible in-game source
input event / visible and audible feedback / progress-state hash
prediction immediately before approval
observed result and short causal account after approval
confusion or recovery incident key
terminal state: COMPLETED / PLAYER_STALLED / HARNESS_BLOCKED
```

숨은 chain-of-thought는 요청·저장하지 않는다. judge는 익명 actor trace, 원본 frame/video/audio와 고정
cell anchor만 받아 strict JSON을 반환한다.

```json
{
  "judgeRunId": "opaque-id",
  "actorArtifactId": "opaque-hash",
  "cells": [
    {
      "cellId": "T1",
      "label": "STRONG",
      "confidence": "HIGH",
      "strengthEvidence": [{"checkpoint":"E01","artifact":"frame-id","observation":"..."}],
      "gapEvidence": [],
      "incidentKeys": [],
      "recommendedChange":"one bounded product change or null"
    }
  ]
}
```

evidence 없는 label, 중복/missing cell, `LOW` confidence인 score-bearing cell은 invalid다. schema를 두
번 재시도해도 실패하면 `BLOCKED_JUDGE_SCHEMA`다. 개선안은 점수에 들어가지 않으며 cell당 하나다.

## 8. qualification과 provenance

candidate를 보기 전에 같은 prompt/schema로 만든 최소 12개 candidate-independent anchor trace를
판정한다. 명백히 완결/국소 망설임/반복 혼란/외부 힌트 필요/부재인 fixed trace가 각 label band를
대표한다. exact expected band 95% 이상, `EXCELLENT`와 `BROKEN` anchor 전부 일치, schema 100%가
필요하다. 한 번 전체 재실행 뒤에도 실패하면 `BLOCKED_JUDGE_QUALIFICATION`이다.

다음을 candidate manifest에 기록한다.

```text
resolved model ID/snapshot, reasoning effort, CLI/transport version
sampling/seed support and values, prompt/rubric/schema SHA
source/package/world/campaign SHA, OS/architecture
viewport/UI scale/input mode/Reduce Motion/audio settings
user-data/save/journal/recording SHA, actor/judge/verifier run IDs
retry, invalidation, harness failure and terminal state ledger
```

model snapshot, effort, prompt/rubric/schema, recipe 또는 candidate commit이 바뀌면 기존 점수를 이어
붙이지 않는다.

## 9. blind evidence verifier와 deterministic oracle

fresh verifier는 원본 artifact와 익명 관찰만 보고 `SUPPORTED`, `PARTIAL`, `UNSUPPORTED`를 반환한다.
원 label, 숫자, threshold, previous build를 보지 않으며 label을 올리거나 counterfactual 학습성을
판정하지 않는다. score/cap-bearing 관찰은 `SUPPORTED`여야 한다. 두 번 실패하면
`BLOCKED_EVIDENCE_VERIFICATION`이다.

source/path/bottleneck/thermal/cash/result/save 사실은 LLM verifier가 아니라 Core snapshot·typed result와
trace의 deterministic oracle 비교가 소유한다.

## 10. 결정론적 집계

각 `(actor artifact × cell)`에서 세 blind judge label의 numeric median을 구한다. 그다음 세 cold actor의
cell median을 `ColdCellScore`로 쓴다. coverage cell은 같은 세 judge median을 `CoverageCellScore`로
만든다. 둘 다 있는 cell은 더 낮은 값을 사용한다.

```text
JudgeCell(actor, cell) = median(three blind judge fixed label scores)
ColdCell(cell)          = median(three cold actor JudgeCell scores)
FinalCell(cell)         = min(ColdCell, CoverageCell) when both exist; otherwise assigned lane score
CategoryScore           = Sum(cellWeightWithinCategory * FinalCell) / 100
RawCommercialUX         = Sum(categoryWeight * CategoryScore) / 100
```

judge spread와 actor spread는 다음 순서로 축약한다.

```text
JudgeSpread(actor, cell) = max(three judge scores) - min(three judge scores)
ColdJudgeSpread(cell)     = median(three actor JudgeSpread values)
ActorSpread(cell)         = max(three JudgeCell values) - min(three JudgeCell values)
ColdCellSpread(cell)      = (ColdJudgeSpread + ActorSpread) / 2
CoverageCellSpread(cell)  = max(three coverage judge scores) - min(three coverage judge scores)
FinalCellSpread(cell)     = max(ColdCellSpread, CoverageCellSpread) when both lanes own the cell;
                            otherwise the assigned lane spread
RawSpread                 = cell→category→global weighted mean of FinalCellSpread
DisagreementPenalty = min(8, RawSpread * 0.20)
PreCapCommercialUX = RawCommercialUX - DisagreementPenalty
CommercialUXProxy  = min(PreCapCommercialUX, ActiveCap)
```

세 actor terminal state가 다르거나 하나만 severe incident를 기록하거나 어느 cold cell의 judge/actor
ordinal range가 두 단계 이상이면 **세 cold actor와 세 blind judge의 전체 panel**을 fresh profile에서
한 번 재실행한다. coverage cell이 불안정하면 전체 coverage recipe와 세 judge를 한 번 재실행한다.
재실행 결과는 원본과 합치거나 유리한 쪽을 고르지 않고 해당 lane의 집계 입력을 전부 교체하며, 원본은
invalidated provenance로 보존한다. 교체 panel도 불안정하면 `BLOCKED_JUDGE_INSTABILITY`다.

두 actor 이상이 같은 verified `PLAYER_STALLED` incident로 끝나면 terminal state disagreement가 아니다.
그 incident 뒤의 required cell은 evidence 누락이 아니라 제품 때문에 도달하지 못한
`NOT_REACHED_BY_PRODUCT`로 기록하고 고정 0점으로 집계하며 cap 49를 활성화한다. 한 actor만 stall이면
위 전체-panel 재실행 규칙을 따르고, 재실행 뒤에도 terminal state가 갈리면 BLOCKED다.

## 11. 결정론적 hard gate

다음 중 하나라도 실패하면 `FAIL_HARD_GATE`이며 LLM label이 상쇄할 수 없다.

- strict world/campaign load와 정확히 8장·3 tutorial·5 main·epilogue
- 정상 도달 story selector 26개, 12개 result branch와 native story/result wiring
- accepted command journal로 모든 필수 checkpoint·분기 도달
- CommercialChecks와 영향 회귀, clean Debug·Release rebuild
- typed source/path/bottleneck/thermal/cash/result와 표시 사실 일치
- 지원 viewport의 startup/input/scene wiring, 필수 text clipping 0
- keyboard trap, 접근 불가 필수 action, missing non-color cue/glyph 0
- audio cue가 지정 typed transition에 존재하고 동기화됨
- save→process restart→resume와 completed save→chapter select snapshot/replay 동일성
- crash, engine softlock/future infeasibility, unhandled exception, save corruption/data loss 0
- 사용자 문구의 machine ID/enum/raw exception 노출 0
- 평가 manifest의 모든 commit/model/recipe/input/output hash 일치

실패가 주관 관찰에서 발견돼도 결정론적으로 재현한 뒤 hard gate에 올린다. 재현하지 못하면 verified
incident로만 남기고 기계 사실처럼 쓰지 않는다.

## 12. 관찰 cap

cap은 stable `chapter/window/screen/incidentType` key로 세 actor 중 둘 이상이 독립 기록하고 verifier가
확인할 때만 적용한다. severe single-run 사건은 1회 전체 재실행 대상으로 삼는다.

| cap | verified UX incident |
|---:|---|
| 49 | 화면 근거/도움 안에서 필수 목표·개념을 찾지 못해 외부 retrieval 또는 개발자 hint가 필요함; 두 actor 이상에서 같은 key로 검증된 12-action `PLAYER_STALLED`/`UX_STALL` |
| 69 | 오류/실패 뒤 서로 다른 합리적 in-product 행동 3개 후에도 회복 경로를 발견하지 못함 |
| 79 | result→next, resume orientation, actual/projection, must/promise 또는 thermal state를 승인 전에 반복 혼동함 |

engine softlock, 상태 모순, 실제 clipping처럼 자동 재현 가능한 사실은 cap이 아니라 hard fail이다.
HARNESS_BLOCKED는 제품 cap이 아니며 `BLOCKED_HARNESS`다. 플레이어가 단순히 덜 좋은 전략을 택한
사실만으로 cap을 적용하지 않는다.

## 13. PASS·FAIL·BLOCKED

공식 PASS는 다음을 모두 만족한다.

- `CommercialUXProxy >= 87.0`
- journey, tutorial, hierarchy, feedback, causality, recovery, accessibility, Korean 각각 `>=85`
- agency, pacing, audiovisual 각각 `>=70`
- required cell 모두 `>=70`
- active cap, unresolved critical incident와 hard-gate failure 0
- qualification, judge/verifier/schema/manifest 안정성 PASS
- 독립 exact-diff 검토의 열린 P0/P1 0

점수/floor를 못 채우면 `FAIL_UX`, 기계 gate 실패면 `FAIL_HARD_GATE`다. 판정할 수 없을 때는 점수를
억지로 만들지 않는다.

```text
BLOCKED_JUDGE_UNAVAILABLE
BLOCKED_JUDGE_QUALIFICATION
BLOCKED_JUDGE_SCHEMA
BLOCKED_JUDGE_INSTABILITY
BLOCKED_EVIDENCE_VERIFICATION
BLOCKED_MISSING_EVIDENCE
BLOCKED_HARNESS
```

최상위 output은 `verdict`, `commercialUXProxy`, `rawCommercialUX`, `disagreementPenalty`,
`activeCap`, `cellScores`, `categoryScores`, `hardGates`, `criticalIncidents`, `differenceReport`를 가진다.
BLOCKED이면 공식 점수는 `null`이다.

## 14. 형성 평가·holdout·개선 반복

- 첫 candidate 전에 rubric/weight/anchor/cap, 공개 formative recipe와 별도 holdout recipe의 SHA를
  함께 고정한다.
- 공개 formative 한 round에서 verifier가 확인한 가장 큰 P0/P1 단위를 수정하고 deterministic gate를
  다시 닫는다.
- official holdout은 formative 응답을 보지 않은 fresh actor/judge로 exact clean commit에서 실행한다.
- 같은 commit의 낮은 run을 삭제하거나 seed/profile/checkpoint를 바꿔 reroll하지 않는다.
- judge에게 이전 점수나 “87점이 필요하다”는 말을 넣지 않는다.
- final 결과로 product를 바꾸면 그 run은 더 이상 untouched final이 아니다. 다음 개선 전에 이미
  precommitted한 다음 holdout recipe를 열고 새 exact commit을 평가한다.
- 하나의 actor 취향에 규칙·경제를 튜닝하지 않는다. 반복 검증되지 않은 제안은 보고서에만 남긴다.

## 15. 집계 self-test

집계기는 최소 다음을 자동검사한다.

- 모든 cell `STRONG`, spread 0이면 85.0으로 목표 미달
- 일부 검증된 cell이 `EXCELLENT`, 나머지가 `STRONG`이면 산술대로 87 이상 가능
- raw 90이어도 result→next cap이 확정되면 79
- required cell 하나가 70 미만이면 overall 87 이상이어도 FAIL_UX
- judge/actor 두 단계 range가 재실행 후 남으면 점수 null/BLOCKED
- hard gate 하나가 FAIL이면 전부 EXCELLENT여도 FAIL_HARD_GATE
- target 87.0은 PASS, 86.99는 FAIL_UX
- TextPlanProxy 90은 CommercialUXProxy로 승격되지 않음

## 16. 증거 묶음

각 candidate는 `playtests/commercial-ux-87/<candidate-id>/`에 다음을 보존한다.

```text
candidate-manifest.json
story-manifest.json
concept-exposure-manifest.json
coverage-recipe.json + holdout-recipe.json
judge-panel.json + prompt-rubric-schema.sha256
qualification/
text-plan/
cold-actors/actor-01..03/
blind-judges/judge-01..03/
coverage-journey/
evidence-verification/
scorecard.json
DIFFERENCE_REPORT.md
NATIVE_SMOKE.md
```

사람 review가 없으면 `HumanValidationStatus=NOT_COLLECTED`를 유지한다. 개발자가 내용을 알고 실행한
pilot과 replay-only 판정은 결함 발견 자료일 뿐 official cold evidence로 합산하지 않는다.
