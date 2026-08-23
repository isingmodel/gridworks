# Gridworks 실시간 상용 UX LLM-as-a-judge 프로토콜

> 상태: **실시간 텍스트 프로토콜 v2 포팅 완료 · native lane 미포팅 · 공식 점수 없음**
>
> 고정 judge: `gpt-5.6-sol`, reasoning effort `ultra`

## 1. 목적

이 프로토콜은 Gridworks가 “규칙은 동작하지만 제품으로는 미완성인 prototype”을 넘어, 실제 판매를
검토할 수 있는 하나의 게임 경험인지 내부적으로 점수화한다. 현재 제품 방향은 turn 단위가 아니라
**pause·1×·2×·4× 속도와 계속 흐르는 시계, 예고된 사건, 시간에 따른 공사·열 노출·정지·회복**을
가진 실시간 전력망 전략 게임이다.

평가 질문은 다음과 같다.

- 처음 보는 플레이어가 도움 없이 title에서 첫 행동으로 진입하는가?
- 첫 세 장이 clock·pause·speed·건설·forecast·경로·열 규칙을 행동으로 가르치는가?
- 본편 다섯 장이 같은 규칙을 새 사건과 trade-off로 확장하는가?
- 장 결과, 누적 약속, 마지막 장과 epilogue가 실제 플레이를 회수하는가?
- 실패·pause·저장·fresh-process 재개 뒤 현재 시각과 다음 행동을 되찾는가?
- 지도·HUD·event horizon·context dock·sound가 같은 typed Core 상태를 말하는가?
- future-event status bar에서 현재 시각, 다음 사건 countdown, 시작·종료, 공사 완료, 결정 기한과
  열 보호 경계가 한 시간축으로 읽히는가?
- keyboard, UI 100/125, Reduce Motion과 색 외 cue에서도 같은 필수 정보와 행동을 제공하는가?

LLM 점수는 내부 product-risk proxy다. 사람 사용성, 재미, 미감, 한국어·전력설비 전문 검토,
실제 OS 호환성이나 공개 출시 승인을 대신하지 않는다.

## 2. 평가 권위

| 질문 | 권위 |
|---|---|
| 작성된 8장 콘텐츠·story·결과·epilogue | `data/release-campaign-v2.json` |
| 실시간 장 일정·사건·결정 기한 | `data/release-campaign-v3.json` |
| 실시간 규칙 | `src/Gridworks.Core/Release/V3/` |
| 실시간 UX 기반 | `game/realtime/r2/`, `game/realtime/ui/` |
| 현재 targeted checkpoint | `A1_NORMAL_READY`, `A1_CONSTRUCTION_DUE_1M` |
| future-event status bar | `game/realtime/ui/RealtimeEventRail.cs`와 typed R2 presentation |
| rubric·집계 상수 | `tools/commercial-ux/rubric.json` |
| narrative atom topology | `tools/commercial-ux/realtime_text_contract.py` |

현재 `CommercialMain`은 동결 V2 기본 장면이고, `RealtimeSliceMain`은 FIRST_LIGHT R2 slice다. 따라서
작성된 8장과 Release.V3 Core 일정이 존재해도 전체 8장 native presentation이 있다고 간주하지 않는다.
이 구분은 text artifact에 그대로 포함한다.

`RealtimeEventRail`이 존재한다는 사실은 native 품질 증거가 아니다. 실제 gameplay에서 다음 사건과
공사·결정·열 경계를 지속적으로 찾고 비교할 수 있는지를 cold/coverage evidence로 별도 판정한다.

UX-R2.1 source `e385707071e4ccfb34d5200e3401897db7f164ad`는 actual release FIRST_LIGHT loader,
controller story, 한 줄 typed future-event rail과 interactive checkpoint host의 deterministic 검사를
통과했고 first-light source와 single-rail 독립 review는 각각 P0 0/P1 0이다. 실제 macOS production
mouse/keyboard로 한 장과 두 checkpoint의 정확한 non-score PASS record도 생성했다. 이는 native judge
lane evidence나 사람 미감 증거가 아니다.

## 3. 두 평가 lane

### 3.1 TEXT-PLAN

코드나 화면을 보지 않는 fresh judge가 다음만 평가한다.

- 게임 premise와 플레이어 역할
- 8개 장의 learning/crisis/choice intent
- 8장·16 event의 priority, 시작 offset, duration, forecast lead를 포함한 실시간 schedule
- 현재 native coverage 상한
- authority에서 직접 만든 34개 narrative atom

34개 atom은 briefing 8, window story 6, result card 11, epilogue card 3, epilogue promise branch
line 6이다. 결과는 `TextPlanProxy`이고 항상 `officialCommercialUX=false`다. 계획의 내부 완결성을
확인할 뿐 구현·조작성·가독성·재미를 증명하지 않는다.

### 3.2 NATIVE

실제 macOS 후보를 처음부터 조작한 cold journey와, 고정 checkpoint/alternate branch를 수행하는
coverage journey를 같은 evidence set으로 평가한다. 결과만 `CommercialUXProxy` 후보가 될 수 있다.

- cold actor 3명: 서로의 trace를 보지 않는 fresh actor
- coverage actor: 정해진 branch, 오류 회복, accessibility, save/resume와 audio/video coverage
- blind judge 3명: actor identity·개발 의도·이전 점수를 보지 않고 동일 evidence set 평가
- evidence verifier: 인용한 화면·trace가 관찰을 실제로 지지하는지 별도 판정
- deterministic oracle: build, boot, state identity, command result, save hash와 hard gate 판정

actor는 judge가 아니며 judge는 게임을 대신 조작하지 않는다. 개발자는 actor prompt에 정답 행동이나
숨은 규칙을 넣지 않는다.

## 4. 게임 완결성 coverage

공식 native session은 다음 경계를 모두 포함해야 한다.

1. 새 user-data에서 title→첫 입력→FIRST_LIGHT
2. tutorial 3장: FIRST_LIGHT, SECOND_HEART, SECOND_SOURCE
3. 본편 5장: NORTH_BANK_PROMISE, WHOSE_MARGIN, BEFORE_WATER_RISE,
   SWITCH_OFF_TO_PROTECT, LONGEST_NIGHT
4. 각 장의 준비시간, forecast, 사건 시작/종료, 공사 완공과 결과 전환
5. future-event status bar의 현재 시각, 다음 사건 countdown, event interval, 공사 완료,
   promise deadline, 열 노출·보호정지·복귀와 actual/draft forecast 구분
6. keep/defer가 있는 세 약속의 서로 다른 result와 epilogue line
7. finale→세 epilogue card→누적 약속 기록→chapter/replay 선택
8. 진행 중 save→process 종료→fresh process resume
9. 완료 save→fresh process resume→결과/선택 복구
10. invalid action, pause/speed 전환, keyboard-only, UI 125%, Reduce Motion/색 외 cue
11. 정상·폭염·범람·보호정지·회복의 synchronized 화면과 audio

한 항목이라도 native 경로에 없으면 text authority가 존재해도 그 항목은 `NOT_IMPLEMENTED` 또는
`NOT_OBSERVED`다. 미구현 장을 V2 기본 장면이나 Core-only replay로 대신 채우지 않는다.

## 5. 구간 테스트와 전체 E2E

결함 재현과 단위 검증은 가장 가까운 named deterministic checkpoint에서 시작한다. 이는 이전 상태를
짧게 만들 뿐, 진입 뒤 production controller·clock·reducer·presentation·input·render 경로를
우회하지 않는다.

다음은 시작 경로 자체가 평가 대상이므로 반드시 처음부터 수행한다.

- onboarding과 최초 mental model
- default scene/package boot
- save 생성과 fresh-process restore
- 장 누적 선택과 full campaign completion
- finale, epilogue와 completed resume

작성 콘텐츠는 전체 플레이 없이 selector 하나로 단독 검사할 수 있어야 한다. selector output은
authored content와 V3 schedule을 결속하지만 native UI reachability를 주장하지 않는다.

## 6. Rubric

native category weight 합은 100이다.

| category | weight | 핵심 질문 |
|---|---:|---|
| journey cohesion | 12 | title→live tutorial→본편→결과→epilogue/replay가 한 여정인가 |
| tutorial/learnability | 13 | clock·pause·speed·forecast·경로·용량을 첫 세 장에서 배우는가 |
| state hierarchy | 13 | future-event bar의 시각·countdown·event/공사/열 경계와 actual/forecast·의무·결과가 구분되는가 |
| interaction feedback | 12 | pause·선택·배치·발주·시간 경과·완공·회복이 일관적인가 |
| causal legibility | 13 | 경로·병목·event·thermal duty·trip/recovery 원인을 설명하는가 |
| agency/trade-off | 10 | 비용·완공 시각·사건 전 여유·약속이 실제 다른 결과를 만드는가 |
| pacing/payoff | 8 | 대기·속도·압박이 지루하거나 기습적이지 않고 finale가 회수하는가 |
| audiovisual integration | 6 | world·HUD·motion·sound가 같은 상태를 강화하는가 |
| recovery orientation | 5 | 오류·pause·save/resume 뒤 시각·사건·다음 행동을 되찾는가 |
| accessible legibility | 4 | keyboard/UI125/Reduce Motion/색 외 cue가 동등한가 |
| Korean clarity | 4 | 조작·시간·경고·결과·story 용어가 자연스럽고 일관적인가 |

label은 `EXCELLENT=100`, `STRONG=85`, `SERVICEABLE=70`, `WEAK=40`, `BROKEN=0`이다. judge는
숫자를 직접 출력하지 않고 label과 artifact/evidence에 존재하는 짧은 근거만 반환한다.

## 7. 집계와 87 gate

cell마다 세 judge label을 숫자로 바꿔 median을 사용한다. cold와 coverage가 모두 소유하는 cell은 두
lane 결과의 최솟값을 사용한다. judge 간 spread에는 rubric의 고정 penalty를 적용한다.

공식 성공 조건은 모두 필요하다.

- `CommercialUXProxy >= 87.0`
- required cell minimum `>=70`
- journey, tutorial, hierarchy, feedback, causality category `>=85`
- recovery, accessibility, Korean category `>=85`
- future-event status bar 필수 signal이 cold와 coverage에서 모두 관찰되고 Core minute와 일치
- crash, soft-lock, state corruption, save/restore mismatch, 누락 장/결과/epilogue 등 hard gate 0
- evidence verifier의 material unsupported claim 0
- candidate/source/evidence/judge provenance가 한 session으로 결속됨

text score, developer smoke, Core-only replay, targeted checkpoint PASS는 이 공식을 통과한 official 점수로
승격할 수 없다.

## 8. 고정 model과 독립성

모든 score-bearing actor/judge/verifier call은 다음 identity를 raw receipt로 남긴다.

```text
model = gpt-5.6-sol
reasoningEffort = ultra
slot = SOL-ULTRA
```

세 judge는 fresh context를 사용하고 서로의 답, 이전 점수와 개선 diff를 보지 않는다. replacement
입력은 현재 fail-closed로 비활성화한다. 불안정 panel은 덮어쓰지 않고 보존하며, 별도 이름의 세 fresh
run으로 새 INITIAL panel을 만든다. qualification anchor나 holdout을 제품 수정의 정답지로 사용하지
않는다.

UX-R0 text baseline의 model/effort는 orchestration task에서 정확히 고정하지만, repository artifact에
platform 서명 execution receipt를 내보내는 권위는 아직 없다. 따라서 text 결과는 이 제한을 명시한
formative proxy다. UX-R1은 공식 native capture 전에 platform/API receipt를 대신 주장하지 않는 local
controlled transcript authority를 session-bound aggregate에 결속했다. 이 영수증은 요청·local rollout의
`gpt-5.6-sol`/`ultra` 일치와 freshness를 검증하지만 platform attestation, judge 실행 또는 점수 증거는
아니다.

## 9. 개선 반복

한 반복은 다음 순서를 지킨다.

```text
deterministic failure / blinded observation
→ 최소 재현 checkpoint 또는 full-flow 경계 확인
→ scope 안의 한 UX 원인 수정
→ 관련 단위·build·checkpoint 회귀
→ 새 candidate bytes와 fresh evidence
→ fresh actor/judge session
```

점수만 올리기 위한 문구 추가, actor prompt에 행동 힌트 주입, 실패 장면 제거, 이전 evidence 재사용은
개선이 아니다. 확인되지 않은 미감·재미 문제는 LLM 단독 사실처럼 쓰지 않는다.

## 10. 현재 판정

현재 text lane의 V3 authority 포팅과 34-part 단독 실행은 deterministic PASS다. 첫 INITIAL panel은
불안정으로 보존했고, 별도 세 fresh run의 두 번째 INITIAL panel은 schema 상태
`SCORED_FORMATIVE`, `TextPlanProxy = 83.4475`로 안정 집계했다. UX-R1의 candidate/replay,
session/attempt, evaluation chain, blocked seven-artifact chain과 local controlled transcript authority는
완료했고 전체 gate review도 P0 0/P1 0으로 통과했다. 그러나 8장 R2 presentation과 score-bearing native
capture는 아직 없다. UX-R2.1은 actual release `FIRST_LIGHT` 장(`FIRST_LIGHT_SUPPLY` phase/event)의
native wiring, 한 줄 future-event rail과 interactive checkpoint host를 source `e385707`에서 구현했고
build·전체 회귀·두 독립 review P0 0/P1 0과 세 non-score actual-input PASS record로 완료했다. automated
runner, headless UI PASS와 actual-input 관찰은 서로 대신하지 않으며 어느 쪽도 judge evidence로 승격하지
않는다. 현재 UX-R2.2는 누적 tutorial 3장 prefix의 connection requirement, result→briefing 전환과
forecast flood 표시만 구현하도록 열렸고 이 gate에서도 official score는 만들지 않는다.

```text
TextPlanProxy = 83.4475_FORMATIVE
CommercialUXProxy = null
OfficialScoreStatus = BLOCKED_UX_R2_COMPLETENESS_IN_PROGRESS
ScoreBearingCaptureAllowed = false
TextJudgeExecutionReceipt = NOT_EXPORTED_FORMATIVE_ONLY
ActiveEvaluationGate = UX_R2_2_TUTORIAL_PREFIX_THROUGH_SECOND_SOURCE
NextEvaluationGate = UX_R2_3_MAIN_CHAPTERS_NOT_OPENED
UserAuthorization = EXPLICIT_CONTINUE_TO_87_AND_DIRECT_PLAY
ControlledCodexTranscriptAuthority = PASS_LOCAL_NON_PLATFORM_SOURCE_2B0B6EE_RECEIPT_SHA256_F7C17C4A
UXR1ClosureReview = PASS_P0_0_P1_0_SOURCE_2B0B6EE
UXR21GateOpeningReview = PASS_P0_0_P1_0
NativeCapturePolicy = ALLOWED_ONE_NON_SCORE_DEBUG_TUTORIAL_FLOW_AFTER_SOURCE_COMMIT_BUILD_AND_REVIEW
NativeCaptureEnvironment = MAC_CONSOLE_UNLOCKED_NOT_PREFLIGHTED
UXR21GateStatus = COMPLETE_NON_SCORE
UXR21ProductSourceAuthority = PASS_SOURCE_REVISION_E385707071E4CCFB34D5200E3401897DB7F164AD
UXR21SourceReview = PASS_FOR_UX_R2_1_SOURCE_MAJOR_UNIT_P0_0_P1_0_SOURCE_EC265999
UXR21SingleRailReview = PASS_FOR_SINGLE_RAIL_MAJOR_UNIT_P0_0_P1_0_SOURCE_E385707
UXR21ClosureReview = PASS_FOR_UX_R2_1_CLOSURE_MAJOR_UNIT_P0_0_P1_0_SOURCE_F2839D1
UXR21ActualInputObservation = PASS_THREE_NON_SCORE_RECORDS
UXR22GateStatus = ACTIVE_IMPLEMENTATION_PENDING
UXR22GateOpeningReview = PASS_P0_0_P1_0
TutorialThreeChapterReachability = NOT_IMPLEMENTED
```

UX-R1 native provenance 계약은 독립 검토로 닫혔다. UX-R2.1과 이후 UX-R2.2 직접 플레이는 formative
개발 관찰이며, 8장 completeness와 UX-R3 evidence gate 전에는 score-bearing capture를 허용할 수 없다.
