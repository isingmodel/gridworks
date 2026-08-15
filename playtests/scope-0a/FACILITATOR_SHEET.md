# Scope 0A facilitator sheet — S0A-CARD-v1

> Keep this sheet hidden from participants. Do not explain electricity, define a corridor, point to a correct route, or paraphrase a question after the session begins. The current round uses five cold-context LLM proxy sessions; each session may inspect only the named participant card files.

## Start

Say: “네 장의 게임 카드가 전달하는 정보만 사용해 답해주세요. 정답을 맞히려 검색하거나 저장소의 다른 파일을 읽지 말고, 처음 보는 전략 게임 화면처럼 판단해주세요. 잘 모르겠으면 추측하지 말고 무엇이 모호한지 적어주세요.”

Show `cards/png/card-01.png` only. Ask exactly:

> 이 마을은 지금 공급 중인가? 공급되려면 무엇이 더 필요한가?

Record the answer before continuing.

## Fixed Card 1 → Card 2 transition

Say exactly:

> 하루가 지나 고정된 상위 접속공사가 발주·완료됐고, 마을은 이제 통전됐습니다. 지금은 DAY 1 08:00입니다.

Show `cards/png/card-02.png`, then the assigned `cards/png/card-03-ab.png` or `cards/png/card-03-ba.png`, then the matching `cards/png/card-04-*-prediction.png`. Do not show any result file.

Ask the participant to answer all four prediction cells with `남음` or `끊김` and a reason. Then ask exactly, in order:

1. 강변 계획에서 강변 통로 전체가 사용불가가 되면 병원 생명안전 부하는 무엇으로 유지되는가?
2. 그 시간에 전력회사는 병원에 전기를 공급·판매한 것인가?
3. 두 계획은 각각 무엇을 사고 무엇을 포기하는가?
4. 지역 전력회사 책임자라면 어느 계획을 고르며, 반대안을 고를 조건은 무엇인가?

Record the answers and choice before revealing anything.

## Reveal

Show the matching `cards/png/card-04-*-causal-reveal.png`. Do not change any score after the participant sees it. Then show the matching `cards/png/card-04-*-settlement-reveal.png`. Record only unexpected comments; do not ask the participant to repair an earlier answer.

## Pre-registered scoring

Set each pass field to true only when the original answer contains every required reason.

| Field | Required reason |
|---|---|
| `CoveragePass` | The service area is only a connectable area; a closed upstream path to the generator is still required. |
| `RiskCausalityPass` | Both backup routes survive loss of hospital E1 because they are different switched circuits; the river route fails with E1 in the shared river corridor, while the north route remains because it is spatially separate. All four prediction cells must match. |
| `UtilityInternalPass` | During the river-corridor event the hospital-owned internal supply keeps the 8 MW life-safety load continuous, but utility delivery and sale to the hospital are zero. |
| `TradeOffPass` | River saves 4 M and survives E1 but loses utility hospital service in the river event; north costs 4 M more, survives E1, and retains utility hospital service in the river event. |

`IntegratedCausalPass = CoveragePass && RiskCausalityPass && UtilityInternalPass && TradeOffPass && !FacilitatorHelp`.

Choice, preference, response speed, and which route is selected are diagnostic only. The round is `PROXY-PASS` only if at least four of the same five valid sessions have `IntegratedCausalPass=true`. A repeated misunderstanding in at least two sessions attributable to one expression or one information structure permits one `PROXY-REVISE`; do not tune costs or target a choice ratio.
