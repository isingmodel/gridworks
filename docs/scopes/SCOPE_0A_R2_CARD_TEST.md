# Gridworks — Scope 0A R2 구조화 카드 테스트

> 상태: **종료 판정 검토 중 — `LLM-PROXY-R2 = PROXY-PASS`**
>
> 사용자 승인: 2026-08-16, coverage와 통합 통과가 될 때까지 증거 기반 iteration을 계속하고
> Scope 1 실행 준비상태까지 진행하라는 현재 목표
>
> 사람 증거: `HumanValidationStatus = NOT_COLLECTED`
>
> 결과: [`playtests/scope-0a-r2/RESULT.md`](../../playtests/scope-0a-r2/RESULT.md)

이 문서는 종료된 [R1 계약](SCOPE_0A_CARD_TEST.md)과 [R1 결과](../../playtests/scope-0a/RESULT.md)를 바꾸지 않고 새 다섯 cold LLM session으로 동일 인과를 다시 검사한 완전한 R2 실행 계약이다. R1과 R2 결과는 합산하지 않는다. R2 실행 증거에는 이 동결 계약이 우선한다.

## 1. 가설과 변경 경계

> 카드가 답의 결론을 알려주지 않으면서 설명해야 할 대비축을 명시하면, 격리 LLM proxy가
> 서비스 권역·실제 공급과 전기회로·공간 통로를 빠뜨리거나 섞지 않고 적용할 수 있는가?

R1의 결론값은 대부분 맞았지만 자유응답이 두 문제를 만들었다.

1. 다섯 세션 모두 미연결 피더는 찾았으나 서비스 권역의 의미를 말하지 않았다.
2. L02는 북부선의 E1 생존을 다른 차단 회로가 아니라 별도 공간 통로에 귀속했다.

R2의 유일한 변경 family는 `InformationStructure: rubric-aligned contrast elicitation`이다.

- Card 1은 현재 공급, 서비스 권역의 기능, 실제 공급 판단근거와 필요한 연결을 별도 문항으로 묻는다.
- Card 4는 왼쪽을 `전기회로 사고`, 오른쪽을 `공간 통로 사고`로 표시하고 각 칸에 해당 관계를 근거로 쓰게 한다.
- fixture, topology, 일정, 비용, 정산, 카드 2·3, 결과값과 field별 rubric 의미는 바꾸지 않는다.
- R2 응답을 보기 전에 현재 사용자 우려를 반영해 gate 집계만 `S0A-GATE-v2`로 재균형한다.
- Card 3가 R1부터 premise로 보여준 `E1과 다른 차단 회로`, `같은 강변 통로`, `떨어진 별도
  통로`는 그대로 둔다. R2가 새로 주지 않는 것은 계획별 `남음/끊김` 결론, 채워진 선택표시,
  서비스 권역 정의문, 추천·승자와 총점이다.

이는 두 기능을 추가한 것이 아니라 같은 자유응답 문법을 구조화한 한 표현 family 변경이다. `ActiveKnob = 0`; 비용·선택률·성공률 수치 튜닝은 없다.

## 2. 권위와 동결본

- 숫자·topology·oracle 권위: 종료된 [R1 계약 §5](SCOPE_0A_CARD_TEST.md#5-동결-fixture). R2에서 한 값도 바꾸지 않는다.
- participant 자료와 hash 권위: [`playtests/scope-0a-r2/`](../../playtests/scope-0a-r2/)의 `S0A-CARD-v2` SVG와 exact PNG hash manifest
- 진행 절차·prompt·rubric 권위: R2 [`FACILITATOR_SHEET.md`](../../playtests/scope-0a-r2/FACILITATOR_SHEET.md)
- round: `LLM-PROXY-R2`
- prompt: `S0A-PROXY-v2`
- decision rule: `S0A-GATE-v2`

R2 전체는 네 논리 카드, AB/BA와 Card 4의 `prediction → causal reveal → settlement reveal`을 합친 10개 `1600×900` frame이다. Card 2, Card 3 두 variant와 settlement reveal 두 variant는 R1 source와 byte-identical이어야 한다. Card 1, prediction 두 variant와 causal reveal 두 variant만 같은 정보구조 문법에 맞춰 바꾼다.

## 3. Participant copy 변경

Card 1의 질문은 다음 세 항목뿐이다.

> ① 이 마을은 지금 공급 중인가?
>
> ② 마을이 서비스 권역 안에 있다는 것은 무엇을 가능하게 하는가?
>
> ③ 실제 공급 여부는 무엇으로 판단하며, 공급되려면 무엇이 더 필요한가?

Card 4의 두 열은 다음 근거를 요구한다.

| 열 | 참가자가 써야 할 근거 |
|---|---|
| 전기회로 사고 — 병원 주 회로 E1만 사용불가 | 각 계획선과 병원 주 회로 E1의 차단 회로 관계 |
| 공간 통로 사고 — 강변 통로 전체 사용불가 | 각 계획선과 강변 통로의 공간 관계 |

각 칸의 `남음 / 끊김`은 비어 있고 결과는 prediction 응답이 고정된 뒤에만 공개한다. Causal reveal은 같은 열 문법을 유지하지만 결과 내용은 R1 oracle과 동일하다.

## 4. 세션과 기록

- 신규 세션: `R2-L01 AB`, `R2-L02 BA`, `R2-L03 AB`, `R2-L04 BA`, `R2-L05 AB`
- model identifier: `gpt-5.6-sol`; reasoning effort `medium`; `fork_turns = none`
- provider build metadata가 없으면 `NOT_EXPOSED`로 기록하며 동일 build 증거로 과장하지 않는다.
- 허용 입력은 해당 메시지가 이름 붙인 R2 participant PNG와 동결 prompt뿐이다.
- 이미지 확인용 `view_image` 외 도구, 검색, 저장소·rubric·oracle·다른 세션 접근은 무효다.
- 모든 입력·원답·보고된 도구·실행시각·hash를 `playtests/scope-0a-r2/private/`에 보존하고 Git에서 제외한다.
- reveal 뒤 답은 점수에 사용하지 않으며 선택률·응답속도·선호는 진단값일 뿐이다.

실제 세 메시지는 진행자 시트 원문에서 `{SESSION_ID}`, `{VARIANT}`, `{PATH_PREFIX}`만 치환한다.

## 5. 사전 rubric과 판정

원답이 정확한 결론과 아래 이유를 모두 포함해야 pass다.

| Field | 필수 이유 |
|---|---|
| `CoveragePass` | 권역은 해당 변전소에 접속 가능한 지리 범위이고, 실제 공급은 온라인 발전원까지 닫힌 통전 경로가 있어야 함 |
| `RiskCausalityPass` | 네 칸 결과가 맞고, 두 예비선의 E1 생존은 다른 차단 회로 때문이며, 강변선 실패·북부선 생존 차이는 공간 통로 관계 때문임 |
| `UtilityInternalPass` | 강변 사건에도 병원 소유 내부전원이 P0를 지키지만 전력회사 병원 인도·판매는 0임 |
| `TradeOffPass` | 강변은 4 M 절약·E1 생존·강변 사건 계통공급 상실, 북부는 4 M 추가·E1 생존·강변 사건 계통공급 유지 |

`IntegratedCausalPass = CoveragePass && RiskCausalityPass && UtilityInternalPass && TradeOffPass && !FacilitatorHelp`.

R2는 technically valid 다섯 세션의 같은 원답 집합에서 다음을 모두 만족할 때만 `PROXY-PASS`다.

1. `CoveragePass`, `RiskCausalityPass`, `UtilityInternalPass`, `TradeOffPass`가 **각각 4/5 이상**이다.
2. `IntegratedCausalPass`가 **3/5 이상**이다.

같은 model 설정의 다섯 실행은 사람 모집단의 통계 표본이 아니라 고정 자료의 반복 일관성 probe다.
각 field 4/5는 동일 축의 반복 오해를 막고, integrated 3/5는 최소 과반이 한 응답 안에서 전체
인과를 연결했음을 요구한다. 반면 서로 다른 두 세션의 일회성 표현 누락이 전체 개발을 막지는
않는다. 기존 integrated 4/5는 네 field의 AND를 다시 4/5에 요구해 이 gate의 탐색적·가역적
목적에 비해 false negative 비용이 컸다.

이 변경은 R2 원답을 수집하기 전에 사전등록하며 R1을 소급 재채점하지 않는다. R1은 어차피
Coverage 0/5여서 새 집계로도 통과할 수 없다. threshold 미달 시 응답을 고쳐 세지 않는다.
통과선 미달이어도 현재 공급, 네 event cell, 내부전원/판매 경계와 비용·서비스 trade-off의
**결론 자체가 각각 4/5 이상 정확**하고 남은 결손이 이유의 누락·모호성·축 혼동이며, 이를 답
누출·fixture·field rubric·수치 변경 없이 하나의 bounded 표현/정보구조 family로만 고칠 수 있으면
`PROXY-REVISE`다. 핵심 결론이나 사건 결과 하나라도 두 세션 이상에서 틀리거나, 둘 이상의 변경
family가 필요하거나, 통과시키려면 답을 직접 노출하거나 fixture·정답 의미·게임 규칙을 바꿔야
하면 `PROXY-FAIL`이다. 위 `PASS`와 `REVISE` 어느 쪽에도 해당하지 않는 나머지도 모두
`PROXY-FAIL`로 닫는다. `FAIL`은 게임 아이디어 폐기가 아니라 이 카드 version으로 다음 단계에
갈 증거가 부족하다는 뜻이다.
어느 경우도 R2 자료를 현장에서 고치지 않으며, 현재 목표에 따른 다음 iteration은 새 version·새
사전계약·새 cold 세션으로만 연다. R2 내부 revision budget은 0이다.

`PROXY-PASS`는 구조화된 질문을 받은 동일-model LLM proxy가 인과를 적용했다는 제한된 증거다. 자발적 발견, 사람 이해도, 재미, 접근성 또는 실제 조작을 증명하지 않는다.

## 6. Preflight와 완료조건

세션 전에 다음을 모두 통과하고 materials-freeze checkpoint를 commit·독립 review한다.

- R1 topology·units·energy·cash oracle 회귀
- Markdown link와 anchor, SVG 원격 의존성 0
- 10 SVG·PNG, exact `1600×900`, RGB/RGBA, metadata·hash 검사
- R1에서 바꾸지 않는 5개 SVG의 byte equality
- Card 1 세 질문 fragment, Card 4 두 사고축·두 근거축의 AB/BA 대칭
- prediction answer·settlement 누출 0, AB/BA row order 유지
- 수동 render QA: Card 1 두 줄, Card 4 header·근거 label의 clipping·겹침 없음
- R2 정확한 prompt와 session allocation 동결
- `S0A-GATE-v2`가 세션 전 문서·진행자 시트·검증기에 동일하게 동결됨

세션 뒤에는 원문·CSV hash, 독립 strict rescore, 비식별 오해, aggregate와 판정을 공개 결과 한 페이지에 기록한다. 큰 단위 checkpoint 규칙을 마친 뒤에만 다음 gate로 넘어간다.

R2는 `PROXY-PASS`로 종료됐다. 현재 사용자 목표에 따른 Scope 0B 계약·구현의 조건부 승인은 R2
결과 checkpoint가 완료된 뒤에만 발효한다. 그 전에는 공식 세션을 다시 실행하거나 Scope 0B를
구현하지 않는다.
