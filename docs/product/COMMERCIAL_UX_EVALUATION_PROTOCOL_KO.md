# Gridworks 실시간 상용 UX LLM-as-a-judge 프로토콜

> 고정 judge: `gpt-5.6-sol`, reasoning effort `ultra`
>
> 현재 상태: text-plan 형성평가만 완료. 공식 native 점수 없음.

## 1. 목적

이 프로토콜은 Gridworks가 규칙 시연을 넘어 실제 판매를 검토할 수 있는 하나의 게임 경험인지
점수화한다. 평가 대상은 turn 방식이 아니라 pause·1×·2×·4× 속도, 계속 흐르는 공사와 사건,
열 노출·보호정지·복귀를 가진 실시간 게임이다.

주요 질문은 다음과 같다.

- 처음 보는 플레이어가 title에서 첫 행동으로 진입하는가?
- 첫 3장이 시계·속도·건설·forecast·경로·열 규칙을 행동으로 가르치는가?
- 본편 5장이 같은 망에서 새로운 위기와 trade-off를 확장하는가?
- 결과, 누적 약속, finale와 epilogue가 플레이를 회수하는가?
- 실패·pause·save/resume 뒤 현재 시각과 다음 행동을 되찾는가?
- world, HUD, 한 줄 future-event bar, context와 audio가 같은 Core 상태를 말하는가?
- mouse/keyboard, UI scale, Reduce Motion과 색 외 cue에서 필수 정보가 유지되는가?

LLM 점수는 내부 product-risk proxy다. 사람의 재미·미감·사용성, 한국어·전력설비 전문 검토,
OS 호환성이나 출시 승인을 대신하지 않는다.

## 2. 평가 권위

| 질문 | 데이터·코드 권위 |
|---|---|
| 작성된 8장 story·결과·epilogue | `data/release-campaign-v2.json` |
| 실시간 장 일정·사건·결정 기한 | `data/release-campaign-v3.json` |
| 실시간 규칙 | `src/Gridworks.Core/Release/V3/` |
| native UX | `game/realtime/r2/`, `game/realtime/ui/` |
| 한 줄 사건 시간축 | `game/realtime/ui/RealtimeEventRail.cs`와 typed presentation |
| rubric·집계 | `tools/commercial-ux/rubric.json` |
| story part topology | `tools/commercial-ux/realtime_text_contract.py` |

현재 기본 장면은 `RealtimeSliceMain`이지만 인자 없는 실행은 기술 fixture다. 기본 scene, 작성된 8장,
native 4장 구현과 직접 관찰된 3장을 같은 coverage로 세지 않는다.

## 3. 두 평가 lane

### TEXT-PLAN

코드와 화면을 보지 않는 fresh judge가 premise, 8장 learning/crisis/choice intent, 16개 사건 일정,
평가 입력에 기록된 native coverage 상한과 34개 narrative atom을 평가한다. 결과는 `TextPlanProxy`이며
언제나 형성평가다.
계획의 내부 완결성은 볼 수 있지만 구현·조작성·가독성·재미를 증명하지 않는다.

현재 source의 text-plan context는 UX-R0 형성평가 당시의
`FIRST_LIGHT_TARGETED_R2_SLICE_ONLY` coverage를 의도적으로 보존한다. 이후 구현된 4장 상태를 나타내는
현재 문서가 아니며, 기존 83.4475 panel을 소급 변경하지 않는다. 다음 평가를 열 때 새 version의
context와 candidate를 현재 native coverage에 맞춰 만든다.

### NATIVE

실제 설치 후보를 처음부터 조작하는 cold journey와 고정 checkpoint/alternate branch를 다루는 coverage
journey를 같은 evidence set으로 평가한다. 이 결과만 `CommercialUXProxy` 후보가 될 수 있다.

- cold actor 3명: 서로의 trace를 보지 않는 fresh actor
- coverage actor: branch, 오류 회복, 접근성, save/resume와 audiovisual coverage
- blind judge 3명: 개발 의도와 이전 점수를 보지 않고 같은 evidence 평가
- evidence verifier: 인용한 관찰이 artifact에 실제 존재하는지 검증
- deterministic oracle: build, boot, state transition, save/restore와 hard gate 판정

actor는 judge가 아니며 judge는 게임을 대신 조작하지 않는다. actor prompt에는 정답 행동이나 숨은
규칙을 넣지 않는다.

## 4. 게임 완결성 coverage

공식 native session은 다음을 모두 포함해야 한다.

1. 빈 user-data의 title→첫 입력→`FIRST_LIGHT`
2. tutorial: `FIRST_LIGHT`, `SECOND_HEART`, `SECOND_SOURCE`
3. 본편: `NORTH_BANK_PROMISE`, `WHOSE_MARGIN`, `BEFORE_WATER_RISE`,
   `SWITCH_OFF_TO_PROTECT`, `LONGEST_NIGHT`
4. 각 장의 준비시간, forecast, 사건 시작/종료, 공사 완공과 결과 전환
5. 한 줄 future-event bar의 현재 시각, countdown, event interval, 공사 완료, promise deadline,
   열 노출·정지·복귀와 actual/draft 구분
6. 세 약속의 Keep/Defer 결과와 epilogue 반영
7. finale→세 epilogue card→누적 약속→chapter/replay 선택
8. 진행 중 save→프로세스 종료→fresh process resume
9. 완료 save→fresh process resume→결과와 선택 복구
10. invalid action, pause/speed, keyboard-only, UI 125%, Reduce Motion과 색 외 cue
11. 정상·폭염·범람·보호정지·회복의 동기화된 화면과 audio

native 경로에 없는 항목은 작성 데이터가 있어도 `NOT_IMPLEMENTED` 또는 `NOT_OBSERVED`다. V2 화면이나
Core-only replay로 R2 coverage를 대신하지 않는다.

## 5. unit, checkpoint와 E2E

결함 재현은 가장 가까운 deterministic 단위에서 시작한다.

- 문장·결과 하나: `--story-part <selector>`
- 특정 시각의 Core/UI 상태: named checkpoint
- 장 누적 전환: 해당 장까지의 release route
- onboarding, default boot, save/restore, 전체 campaign: fresh-process E2E

story selector는 authored content와 V3 일정을 결속하지만 native UI 도달성을 주장하지 않는다.
checkpoint는 진입 뒤 production controller·clock·presentation·input·render를 우회해서는 안 된다.

## 6. Rubric

native category weight의 합은 100이다.

| category | weight | 핵심 질문 |
|---|---:|---|
| journey cohesion | 12 | title→tutorial→본편→결과→epilogue/replay가 한 여정인가 |
| tutorial/learnability | 13 | 시계·속도·forecast·경로·용량을 첫 3장에서 배우는가 |
| state hierarchy | 13 | 시간축의 사건/공사/열 경계와 actual/forecast가 구분되는가 |
| interaction feedback | 12 | 선택·배치·발주·시간 경과·완공·복귀가 일관적인가 |
| causal legibility | 13 | 경로·병목·사건·열 노출·정지 원인을 설명하는가 |
| agency/trade-off | 10 | 비용·완공 시각·여유·약속이 다른 결과를 만드는가 |
| pacing/payoff | 8 | 대기와 압박이 납득되고 finale가 회수하는가 |
| audiovisual integration | 6 | world·HUD·motion·sound가 같은 상태를 강화하는가 |
| recovery orientation | 5 | 오류·pause·resume 뒤 현재 상태를 되찾는가 |
| accessible legibility | 4 | keyboard/UI125/Reduce Motion/색 외 cue가 동등한가 |
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
- future-event bar 필수 signal이 cold와 coverage에서 모두 보이고 Core 시각과 일치
- crash, soft-lock, state corruption, save mismatch, 누락 장/결과/epilogue hard gate 0
- material unsupported evidence claim 0
- candidate, source, evidence와 judge run이 한 session에 결속됨

text 점수, developer smoke, Core-only replay 또는 checkpoint PASS는 공식 점수로 승격할 수 없다.

## 8. model과 독립성

모든 score-bearing judge run은 다음 identity를 실행 영수증에 남긴다.

```text
model = gpt-5.6-sol
reasoningEffort = ultra
slot = SOL-ULTRA
```

세 judge는 fresh context를 사용하고 서로의 답, 이전 점수와 개선 diff를 보지 않는다. 실패하거나
불안정한 panel을 덮어쓰지 않고 세 fresh run으로 새 panel을 만든다. holdout이나 qualification
anchor를 제품 수정의 정답지로 사용하지 않는다.

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
- R2 native 구현: `NORTH_BANK_PROMISE`까지 누적 4장
- 실제 직접 플레이: 첫 3장까지
- 4장 직접 플레이, 남은 4장, save/resume, finale/epilogue, current R2 package: 미완료
- `CommercialUXProxy`: 없음
- score-bearing native capture와 87점 반복: 아직 시작하지 않음

평가 실행은 전체 native 여정과 후보가 준비된 뒤 [남은 작업](../NEXT_TASKS.md)의 별도 scope로 연다.
