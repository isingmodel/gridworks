# Scope 0A facilitator sheet — S0A-CARD-v1

> Frozen historical R1 protocol. `LLM-PROXY-R1 = PROXY-FAIL`; there is no active round. Do not execute, revise, or reuse this sheet without a newly authorized gate. During R1 this sheet was hidden from participants, and each cold-context LLM proxy could inspect only the named participant card files.

## Frozen R1 runner and message contract

- `PromptVersion = S0A-PROXY-v1`
- model identifier: `gpt-5.6-sol`
- reasoning effort: `medium`
- context: `fork_turns = none`, no memory or prior messages
- provider build metadata: record the exposed value; otherwise record `NOT_EXPOSED`
- allowed tool: `view_image` on the exact PNG paths named in the current message

The three templates below are the complete participant input. Substitute only `{SESSION_ID}`, `{VARIANT}` (`ab` or `ba`), and `{PATH_PREFIX}` (the absolute path ending in `playtests/scope-0a`). Do not change spacing-independent wording, add hints, or answer follow-up questions. Preserve every substituted input, original response, and reported tool line verbatim in `private/LLM-PROXY-R1-transcripts.md`; keep the scored row in `private/LLM-PROXY-R1-responses.csv`.

## Message 1 — start and Card 1

```text
[Scope 0A LLM proxy {SESSION_ID} — 1/3]

네 장의 게임 카드가 전달하는 정보만 사용해 답해주세요. 정답을 맞히려 검색하거나 저장소의 다른 파일을 읽지 말고, 처음 보는 전략 게임 화면처럼 판단해주세요. 잘 모르겠으면 추측하지 말고 무엇이 모호한지 적어주세요.

허용 도구는 view_image뿐입니다. 다음 한 파일만 여십시오.
{PATH_PREFIX}/cards/png/card-01.png
다른 파일, 도구, 검색, 저장소, 기억 또는 다른 대화를 사용하면 이 세션은 무효입니다.

이 마을은 지금 공급 중인가? 공급되려면 무엇이 더 필요한가?

결론과 카드에서 읽은 이유를 짧게 답하고, 마지막 줄에 `도구 기록: view_image — <실제로 연 파일>`을 적으세요.
```

Record the answer before continuing.

## Message 2 — fixed transition, comparison, and prediction

Use the assigned `{VARIANT}` for every variant path.

```text
[Scope 0A LLM proxy {SESSION_ID} — 2/3]

하루가 지나 고정된 상위 접속공사가 발주·완료됐고, 마을은 이제 통전됐습니다. 지금은 DAY 1 08:00입니다.

허용 도구는 view_image뿐입니다. 아래 세 파일을 적힌 순서대로 여십시오.
{PATH_PREFIX}/cards/png/card-02.png
{PATH_PREFIX}/cards/png/card-03-{VARIANT}.png
{PATH_PREFIX}/cards/png/card-04-{VARIANT}-prediction.png
다른 파일, 특히 causal-reveal 또는 settlement-reveal 파일을 열면 이 세션은 무효입니다.

카드 4 표의 위 행부터 아래 행까지, 각 행에서는 왼쪽 사건 다음 오른쪽 사건 순서로 네 칸에 답하세요. 각 답에 카드에 보이는 계획명과 사건명을 적고, `남음` 또는 `끊김`을 고른 뒤 카드에서 읽은 이유를 붙이세요.

그 뒤 다음 네 질문에 순서대로 답하세요.

1. 강변 계획에서 강변 통로 전체가 사용불가가 되면 병원 생명안전 부하는 무엇으로 유지되는가?
2. 그 시간에 전력회사는 병원에 전기를 공급·판매한 것인가?
3. 두 계획은 각각 무엇을 사고 무엇을 포기하는가?
4. 지역 전력회사 책임자라면 어느 계획을 고르며, 반대안을 고를 조건은 무엇인가?

마지막 줄에 `도구 기록: view_image — <실제로 연 파일들을 순서대로>`를 적으세요.
```

Record the complete response and choice before revealing anything.

## Message 3 — staged reveal

```text
[Scope 0A LLM proxy {SESSION_ID} — 3/3]

앞선 답은 이미 고정됐습니다. 수정하거나 다시 채점하지 마세요.

허용 도구는 view_image뿐입니다. 먼저 아래 인과 공개 파일만 열어 확인한 뒤, 다음 정산 공개 파일을 여십시오.
{PATH_PREFIX}/cards/png/card-04-{VARIANT}-causal-reveal.png
{PATH_PREFIX}/cards/png/card-04-{VARIANT}-settlement-reveal.png
다른 파일이나 도구는 사용하지 마세요.

앞선 답을 다시 풀지 말고, 두 화면이 정상적으로 읽혔는지와 예상 밖 관찰만 한두 문장으로 적으세요.
마지막 줄에 `도구 기록: view_image — <실제로 연 파일들을 순서대로>`를 적으세요.
```

Do not change any score after this message. Record only unexpected comments.

## Pre-registered scoring

Set each pass field to true only when the original answer contains every required reason.

| Field | Required reason |
|---|---|
| `CoveragePass` | The service area is only a connectable area; a closed upstream path to the generator is still required. |
| `RiskCausalityPass` | Both backup routes survive loss of hospital E1 because they are different switched circuits; the river route fails with E1 in the shared river corridor, while the north route remains because it is spatially separate. All four prediction cells must match. |
| `UtilityInternalPass` | During the river-corridor event the hospital-owned internal supply keeps the 8 MW life-safety load continuous, but utility delivery and sale to the hospital are zero. |
| `TradeOffPass` | River saves 4 M and survives E1 but loses utility hospital service in the river event; north costs 4 M more, survives E1, and retains utility hospital service in the river event. |

`IntegratedCausalPass = CoveragePass && RiskCausalityPass && UtilityInternalPass && TradeOffPass && !FacilitatorHelp`.

Choice, preference, response speed, and which route is selected were diagnostic only. The frozen R1 decision order was: obtain five technically valid sessions; choose `PROXY-PASS` at 4/5 or better; below 4/5 choose `PROXY-REVISE` only when every failed session has the same single scored deficit attributable to one expression or information structure; otherwise choose `PROXY-FAIL`. Costs and choice ratios were not tuned.
