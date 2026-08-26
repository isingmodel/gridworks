# Gridworks 실시간 상용 UX LLM-as-a-judge 프로토콜

> 고정 judge: `gpt-5.6-sol`, reasoning effort `ultra`
>
> 현재 상태: text-plan 형성평가만 완료. 공식 native 점수 없음. 이 프로토콜은
> [외부 출시 gate](../RELEASE_GATES.md)의 실행 owner·선행 증거가 준비되고, 현재 사용자 지시와
> 새 `ACTIVE_SCOPE.md`가 명시적으로 열 때만 실행한다.

## 1. 목적

이 프로토콜은 Gridworks가 규칙 시연을 넘어 실제 판매를 검토할 수 있는 하나의 게임 경험인지
점수화한다. 평가 대상은 turn 방식이 아니라 pause·1×·2×·4× 속도, 계속 흐르는 공사와 사건,
열 노출·보호정지·복귀를 가진 실시간 게임이다.

주요 질문은 다음과 같다.

- 빈 user-data에서 title의 `새 게임`으로 첫 행동에 진입하고, 유효한 저장에서는 같은 title의
  `이어하기`로 정확한 상태를 복구하는가?
- 첫 3장이 시계·속도·건설·forecast·경로·열 규칙을 행동으로 가르치는가?
- 본편 5장이 같은 망에서 새로운 위기와 trade-off를 확장하는가?
- 결과, 누적 약속, finale와 epilogue가 플레이를 회수하는가?
- 실패·pause·save/resume 뒤 현재 시각과 다음 행동을 되찾는가?
- world, HUD, 한 줄 사건 지평선(future-event bar), context와 audio가 같은 Core 상태를 말하는가?
- title/settings/modal/HUD/tool/timeline·겹친 후보 선택의 keyboard 경로, UI scale,
  Reduce Motion과 색 외 cue에서 필수 정보가 유지되는가? 연속 좌표 map pan/zoom/
  free placement는 pointer-owned spatial interaction으로 평가한다.

LLM 점수는 내부 product-risk proxy다. 사람의 재미·미감·사용성, 한국어·전력설비 전문 검토,
OS 호환성이나 출시 승인을 대신하지 않는다.

## 2. 평가 권위

| 질문 | 데이터·코드 권위 |
|---|---|
| 작성된 8장 story·결과·epilogue | `data/release-campaign-v2.json` |
| 실시간 장 일정·사건·결정 기한 | `data/release-campaign-v3.json` |
| 실시간 규칙 | `src/Gridworks.Core/Release/V3/` |
| native UX | `game/realtime/r2/`, `game/realtime/ui/` |
| 제품 lifecycle seam | combined 2B record v2가 고정한 exact package의 bounded title·Continue·reset·settings·generated-audio runtime |
| 평가 후보 | 2B-qualified exact package를 첫 capture 전 versioned evaluation-session gate에 등록하고 candidate identity로 결속한 후보; full journey capture·evidence는 후속 평가 산출물 |
| score-bearing model identity·freshness | 같은 session에 결속된 검증 가능한 platform/API raw receipt |
| 사건 지평선(future-event bar) | `game/realtime/ui/RealtimeEventRail.cs`와 typed presentation |
| 점수 label·weight·수치 reduction | `tools/commercial-ux/rubric.json` |
| 공식 native hard gate | 이 문서 4·7절; 실행 전 versioned oracle policy로 결속해야 함 |
| story part topology | `tools/commercial-ux/realtime_text_contract.py` |

현재 기본 장면의 인자 없는 실행은 session 없는 제품 title을 열고, 저장 파일이 없을 때 `새 게임`은
canonical `FIRST_LIGHT`→`LONGEST_NIGHT` 누적 8장 `ProductCampaign`의 briefing으로 진입한다. product-owned
session의 모든 장 stable 진행, story-idle active event·duty와 exact-minute active in-chapter story는
current v3로 저장되며, zero-command exact initial c0/c1과 bounded non-final result→next briefing handoff도
같은 cursor로 복원한다. story-idle과 prior v1은 paused·normal speed·no-modal로, supported active v2/v3
story는 같은 authored modal로 복원된다.
non-saveable 정상 종료는 직전 valid save bytes를 보존하고 이후 safe 정상 종료만 primary를 갱신한다.
in-progress와 raw bytes를 읽을 수 있는 blocked save는 기존 `새 게임`의 확인→sibling backup→canonical
bootstrap 경로를 공유하며, I/O 실패와 backup 실패는 fail-closed한다.
명시적 개발 route는 product save를 읽거나 쓰지 않는다. 이 결정론적 title/journal-restorable-save wiring,
작성·구현된 8장과 누적 4장 production-input 직접 관찰을 같은 coverage로 세지 않는다.
과거 V2 title/save/settings/audio, editor project tree와 UX-R1 candidate는 current R2 평가 권위가 아니다.

## 3. 두 평가 lane

### TEXT-PLAN

코드와 화면을 보지 않는 fresh judge가 premise, 8장 learning/crisis/choice intent, 16개 사건 일정,
평가 입력에 기록된 native coverage 상한과 34개 narrative atom을 평가한다. 결과는 `TextPlanProxy`이며
언제나 형성평가다.
계획의 내부 완결성은 볼 수 있지만 구현·조작성·가독성·재미를 증명하지 않는다.

현재 source의 text-plan context는 UX-R0 형성평가 당시의
`FIRST_LIGHT_TARGETED_R2_SLICE_ONLY` coverage 상한을 의도적으로 보존하지만, 이후 바뀐 기본 장면 사실은
반영한다. 따라서 UX-R0 panel에 결속된 정확한 context bytes는
`playtests/commercial-ux-87-realtime/text-plan-r0/text-plan-context.json`이 소유한다. 기존 83.4475 panel을
current context로 소급 재집계하지 않는다. 다음 평가를 열 때 새 version의 context와 candidate를 현재
native coverage에 맞춰 만든다.

### NATIVE

2B-qualified exact package를 versioned evaluation session에 결속한 뒤 처음부터 조작하는 cold journey와
고정 checkpoint/alternate branch를 다루는 coverage journey를 같은 evidence set으로 평가한다. 이 전체
결속을 통과한 결과만 `CommercialUXProxy` 후보가 될 수 있다.

- cold actor 3명: 서로의 trace를 보지 않는 fresh actor
- coverage actor: branch, 오류 회복, 접근성, save/resume와 audiovisual coverage
- blind judge 3명: 개발 의도와 이전 점수를 보지 않고 같은 evidence 평가
- evidence verifier: 인용한 관찰이 artifact에 실제 존재하는지 검증
- deterministic oracle: build, boot, state transition, save/restore와 hard gate 판정

actor는 judge가 아니며 judge는 게임을 대신 조작하지 않는다. actor prompt에는 정답 행동이나 숨은
규칙을 넣지 않는다.

## 4. 게임 완결성 coverage

공식 native session은 finalized combined 2B record가 고정한 exact package를 소비하되, full native journey
capture·evidence와 versioned evaluation-session authority·oracle이 준비된 뒤에만 시작한다. bounded 2B2
lifecycle record 자체를 evaluation-ready evidence로 승격하지 않는다.

공식 native session은 다음을 모두 포함해야 한다.

1. 빈 user-data의 title→`새 게임`→첫 briefing→`FIRST_LIGHT`; 이때 `이어하기`는 비활성
2. tutorial: `FIRST_LIGHT`, `SECOND_HEART`, `SECOND_SOURCE`
3. 본편: `NORTH_BANK_PROMISE`, `WHOSE_MARGIN`, `BEFORE_WATER_RISE`,
   `SWITCH_OFF_TO_PROTECT`, `LONGEST_NIGHT`
4. 각 장의 준비시간, forecast, 사건 시작/종료, 공사 완공과 결과 전환
5. 한 줄 사건 지평선의 현재 시각, countdown, event interval, 공사 완료, promise deadline,
   열 노출·정지·복귀와 actual/draft 구분
6. 세 약속의 Keep/Defer 결과와 epilogue 반영
7. finale→세 epilogue card→누적 약속→terminal save
8. 진행 중 save→프로세스 종료→같은 candidate 재실행→title의 `이어하기`→정확한 상태 복구
9. 완료 save→같은 candidate 재실행→title의 `이어하기` terminal 복구와 `새 게임` 재시작
10. invalid action, pause/speed, non-spatial journey의 keyboard-only, pointer-owned spatial interaction,
    title/pause settings, UI 125%, Reduce Motion과 색 외 cue;
    settings 변경→같은 candidate의 fresh process 재실행→값 복원
11. 정상·공사·폭염·범람·보호정지·회복·finale의 동기화된 화면과 audio; volume/mute와 capture settings
    기록

native 경로에 없는 항목은 작성 데이터가 있어도 `NOT_IMPLEMENTED` 또는 `NOT_OBSERVED`다. V2 화면이나
Core-only replay로 R2 coverage를 대신하지 않는다.

## 5. unit, checkpoint와 E2E

결함 재현은 가장 가까운 deterministic 단위에서 시작한다.

- 문장·결과 하나: `./dev story <selector>`
- 특정 시각의 Core/UI 상태: `./dev checkpoint <CHECKPOINT_ID>`
- 장 누적 전환: catalog가 명시적으로 지원하는 `./dev play through <CHAPTER_ID>`
- onboarding, default boot, save/restore, 전체 campaign: fresh-process E2E

story selector는 authored content와 V3 일정을 결속하지만 native UI 도달성을 주장하지 않는다.
checkpoint는 진입 뒤 production controller·clock·presentation·input·render를 우회해서는 안 된다.

## 6. Rubric

native category weight의 합은 100이다.

| category | weight | 핵심 질문 |
|---|---:|---|
| journey cohesion | 12 | title→tutorial→본편→결과→epilogue/completed New Game이 한 여정인가 |
| tutorial/learnability | 13 | 시계·속도·forecast·경로·용량을 첫 3장에서 배우는가 |
| state hierarchy | 13 | 시간축의 사건/공사/열 경계와 actual/forecast가 구분되는가 |
| interaction feedback | 12 | 선택·배치·발주·시간 경과·완공·복귀가 일관적인가 |
| causal legibility | 13 | 경로·병목·사건·열 노출·정지 원인을 설명하는가 |
| agency/trade-off | 10 | 비용·완공 시각·여유·약속이 다른 결과를 만드는가 |
| pacing/payoff | 8 | 대기와 압박이 납득되고 finale가 회수하는가 |
| audiovisual integration | 6 | world·HUD·motion·sound가 같은 상태를 강화하는가 |
| recovery orientation | 5 | 오류·pause·resume 뒤 현재 상태를 되찾는가 |
| accessible legibility | 4 | non-spatial keyboard, pointer-owned spatial input, UI125/Reduce Motion/색 외 cue가 명확한가 |
| Korean clarity | 4 | 조작·시간·경고·결과 용어가 자연스럽고 일관적인가 |

judge는 각 cell에 `EXCELLENT`, `STRONG`, `SERVICEABLE`, `WEAK`, `BROKEN`과 evidence에 존재하는 짧은
근거를 반환한다. 집계기는 이를 각각 100, 85, 70, 40, 0으로 변환한다.

## 7. 집계와 87 gate

cell마다 세 judge의 median을 사용한다. cold와 coverage가 모두 소유하는 cell은 둘 중 낮은 값을 쓰고,
judge 간 spread에는 고정 penalty를 적용한다.

공식 성공에는 다음이 모두 필요하다.

- `CommercialUXProxy >= 87.0`
- required cell 최솟값 70 이상
- journey, tutorial, hierarchy, feedback, causality category 85 이상
- recovery, accessibility, Korean category 85 이상
- 사건 지평선 필수 signal이 cold와 coverage에서 모두 보이고 Core 시각과 일치
- crash, soft-lock, state corruption, save mismatch, 누락 장/결과/epilogue hard gate 0
- material unsupported evidence claim 0
- candidate, source, evidence와 judge run이 한 session에 결속됨
- 모든 cold/coverage evidence가 동일한 2B-qualified exact package contents에서 생성됨
- title의 `새 게임`·`이어하기`, 필수 settings와 synchronized audio 누락 0
- capture 당시 settings와 audio channel 상태가 evidence에 기록되고 fresh-process settings 복원이 확인됨

첫 score-bearing capture 전에 2B-qualified exact package를 소비하는 versioned evaluation-session authority,
evidence verifier, hard-gate oracle과 aggregator가 `rubric.json`, 이 절의 hard gate, candidate,
evidence와 model identity·receipt를 결속해야 한다. 하나라도 없거나 불일치하면 점수를 내지 않고
fail-closed한다. 현재 repository에는 이 score-bearing execution authority가 없다.

text 점수, developer smoke, Core-only replay 또는 checkpoint PASS는 공식 점수로 승격할 수 없다.

## 8. model과 독립성

모든 model-backed score-bearing actor/judge/verifier run은 실제 model·reasoning effort·freshness와
session binding을 검증 가능한 platform/API raw receipt에 남긴다. 그중 점수를 반환하는 judge는 다음
identity로 고정한다.

```text
model = gpt-5.6-sol
reasoningEffort = ultra
slot = SOL-ULTRA
```

세 judge는 fresh context를 사용하고 서로의 답, 이전 점수와 개선 diff를 보지 않는다. 실패하거나
불안정한 panel을 덮어쓰지 않고 세 fresh run으로 새 panel을 만든다. holdout이나 qualification
anchor를 제품 수정의 정답지로 사용하지 않는다.

repository JSON의 자기선언, local process log나 controlled transcript만으로는 model·effort·freshness와
session binding을 증명할 수 없다. raw receipt가 없거나 검증되지 않으면 해당 session은
`ScoreBearingCaptureAllowed = false`, `officialCommercialUX = false`, `CommercialUXProxy = null`로 닫고
나중에 공식 session으로 승격하지 않는다.

## 9. 개선 반복

```text
deterministic failure 또는 blinded observation
→ 가장 작은 올바른 재현 경로 확인
→ 한 UX 원인 수정
→ unit·build·checkpoint 회귀
→ 새 candidate와 fresh evidence
→ fresh actor/judge session
```

점수만 올리는 문구, actor prompt의 행동 힌트, 실패 장면 제거와 이전 evidence 재사용은 개선이 아니다.
확인되지 않은 미감·재미 판단은 LLM 단독 사실처럼 쓰지 않는다.

## 10. 현재 판정

- 8장/16개 사건/34개 story part text authority와 단독 selector: 완료
- `TextPlanProxy`: `83.4475`, 형성평가
- R2 native 구현: `LONGEST_NIGHT`까지 누적 8장
- 실제 직접 플레이: `NORTH_BANK_PROMISE`까지 누적 4장 Keep·명시적 Defer 결과 관찰
- current R2 product title의 저장 파일 없음 `새 게임`→누적 8장 `ProductCampaign`: 구현·결정론적
  production-input smoke 완료
- 모든 장 stable in-progress strict replay, current v3 `closedStoryCount`와 read-only v1/v2 호환:
  deterministic 검사 완료
- authored `FIRST_LIGHT` initial c0 save-create→fresh `이어하기`→same briefing→FLOOD c3 write:
  fresh-process smoke 완료
- 첫 Continue가 쓴 active `SECOND_HEART/FLOOD_ISOLATION_TEST` c3→fresh `이어하기`→같은 authored modal→
  닫은 뒤 paused 복원: fresh-process smoke 완료
- 같은 save path의 active story→`SECOND_HEART` result write→fresh `이어하기`→`SECOND_SOURCE` briefing
  exact-once 및 종료 write: fresh-process smoke 완료
- zero-gap result/briefing과 `SECOND_SOURCE`→`NORTH_BANK_PROMISE` long-gap result→briefing→decision의 exact
  Core/history/FIFO 복원: session harness 완료; 이후 미래나 각 장의 fresh-process, 전체 8장
  production-input 직접 여정의 증거는 아님
- finale→세 epilogue card와 누적 Keep/Defer·남은 자금의 deterministic native presentation: 구현
- full `ProductCampaign` current-v3 terminal save→fresh `이어하기`→exact `Ended` world와 동일 terminal
  종료 write: fresh-process smoke 완료; prior v1/v2 completion은 차단
- completed terminal title의 `새 게임`→canonical initial write→fresh `이어하기`: fresh-process smoke 완료
- transient non-saveable 구간의 직전 safe-save byte-exact 보존→다음 safe-point 갱신과 진행 저장의
  확인→backup 실패 차단→raw sibling backup→canonical reset: focused fresh-process product-entry smoke 완료;
  readable invalid/unsupported는 같은 확인 상태, I/O failure는 차단됨. 전체 packaged-campaign
  production-input E2E와 transient cursor는 이 증거에 포함되지 않음
- title/gameplay 공용 current R2 settings, strict missing/invalid/unsupported/read/write fail-closed,
  create→fresh restore, explicit 개발 route read-only, UI scale·volume/mute·Reduce Motion과 local macOS
  non-headless window-mode projection: deterministic wiring 완료. exact package의 app-owned settings bytes
  2B1과 title의 actual InputEvent Apply→fresh Restore·headless UI/audio-bus projection 2B2 완료; gameplay
  settings journey, 물리 display와 사람 접근성 증거는 아님
- current R2 universal macOS ad-hoc package identity ZIP, strict manifest/verifier와 임시 설치 위치의 no-arg
  headless title marker: 완료. exact-empty app-owned root의 missing/settings/initial/terminal fresh-process
  data stage 2B1과 일곱 bounded lifecycle InputEvent stage의 strict record v2도 2B2 완료. engine `user://`
  전체, full packaged-campaign production input, OS hardware, 사람 UX 또는 evaluation-ready 증거는 아님
- exact package의 generated ambient stream/bus one-start와 quiet SFX wiring은 2B2 완료. 실제 audio playback·
  device·speaker, live cue 상태 coverage·청감 품질과 score-bearing execution authority·oracle/aggregator는 미완료
- `CommercialUXProxy`: 없음
- score-bearing native capture와 87점 반복: 아직 시작하지 않음
- historical editor-native non-score evaluator: current package와 미연결이어서 제거; 새 권위는 없음

평가 실행은 완료된 2B-qualified exact package를 소비하는 full native journey capture와 score-bearing
execution authority가 모두 준비된 뒤에도 [외부 출시 gate](../RELEASE_GATES.md) 상태만으로는
시작하지 않는다. 현재 사용자 지시와 새 `ACTIVE_SCOPE.md`가 명시적으로 승인한 별도 scope를 연다.
