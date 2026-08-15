# Scope 0A R2 facilitator sheet — S0A-CARD-v2

> Hidden from participants. Use these three messages exactly; do not explain electricity or paraphrase after the session starts.

## Frozen runner

- `PromptVersion = S0A-PROXY-v2`
- `DecisionRuleVersion = S0A-GATE-v2`
- allocation: `R2-L01 AB`, `R2-L02 BA`, `R2-L03 AB`, `R2-L04 BA`, `R2-L05 AB`
- model identifier: `gpt-5.6-sol`
- reasoning effort: `medium`
- context: `fork_turns = none`
- build metadata: exposed value or `NOT_EXPOSED`
- only allowed tool: `view_image` on exact PNG paths named in the current message

Substitute only `{SESSION_ID}`, `{VARIANT}` (`ab|ba`) and `{PATH_PREFIX}` ending in `playtests/scope-0a-r2`. Preserve all substituted inputs, original responses and reported tool lines in `private/LLM-PROXY-R2-transcripts.md`; keep scored rows in `private/LLM-PROXY-R2-responses.csv`.

## Message 1/3

```text
[Scope 0A R2 LLM proxy {SESSION_ID} — 1/3]

네 장의 게임 카드가 전달하는 정보만 사용해 답해주세요. 정답을 맞히려 검색하거나 저장소의 다른 파일을 읽지 말고, 처음 보는 전략 게임 화면처럼 판단해주세요. 잘 모르겠으면 추측하지 말고 무엇이 모호한지 적어주세요.

허용 도구는 view_image뿐입니다. 다음 한 파일만 여십시오.
{PATH_PREFIX}/cards/png/card-01.png
다른 파일, 도구, 검색, 저장소, 기억 또는 다른 대화를 사용하면 이 세션은 무효입니다.

① 이 마을은 지금 공급 중인가?
② 마을이 서비스 권역 안에 있다는 것은 무엇을 가능하게 하는가?
③ 실제 공급 여부는 무엇으로 판단하며, 공급되려면 무엇이 더 필요한가?

1~3번에 각각 결론과 카드에서 읽은 이유를 짧게 답하고, 마지막 줄에 `도구 기록: view_image — <실제로 연 파일>`을 적으세요.
```

Record the answer before continuing.

## Message 2/3

```text
[Scope 0A R2 LLM proxy {SESSION_ID} — 2/3]

하루가 지나 고정된 상위 접속공사가 발주·완료됐고, 마을은 이제 통전됐습니다. 지금은 DAY 1 08:00입니다.

허용 도구는 view_image뿐입니다. 아래 세 파일을 적힌 순서대로 여십시오.
{PATH_PREFIX}/cards/png/card-02.png
{PATH_PREFIX}/cards/png/card-03-{VARIANT}.png
{PATH_PREFIX}/cards/png/card-04-{VARIANT}-prediction.png
다른 파일, 특히 causal-reveal 또는 settlement-reveal 파일을 열면 이 세션은 무효입니다.

카드 4 표의 위 행부터 아래 행까지, 각 행에서는 왼쪽 사건 다음 오른쪽 사건 순서로 네 칸에 답하세요. 각 칸에 카드에 보이는 계획명과 사건명을 적고 `남음` 또는 `끊김`을 고르세요. 왼쪽 `전기회로 사고` 열은 각 계획선과 병원 주 회로 E1의 차단 회로 관계를, 오른쪽 `공간 통로 사고` 열은 각 계획선과 강변 통로의 공간 관계를 근거로 명시하세요.

그 뒤 다음 네 질문에 순서대로 답하세요.

1. 강변 계획에서 강변 통로 전체가 사용불가가 되면 병원 생명안전 부하는 무엇으로 유지되는가?
2. 그 시간에 전력회사는 병원에 전기를 공급·판매한 것인가?
3. 두 계획은 각각 무엇을 사고 무엇을 포기하는가?
4. 지역 전력회사 책임자라면 어느 계획을 고르며, 반대안을 고를 조건은 무엇인가?

마지막 줄에 `도구 기록: view_image — <실제로 연 파일들을 순서대로>`를 적으세요.
```

Record the complete response and choice before revealing anything.

## Message 3/3

```text
[Scope 0A R2 LLM proxy {SESSION_ID} — 3/3]

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

| Field | Required reason |
|---|---|
| `CoveragePass` | Service area means the geographic area connectable to that substation; actual supply requires a closed energized path to an online generator. |
| `RiskCausalityPass` | All four cells are correct; both E1 survivals are attributed to different switched circuits, while river failure/north survival are attributed to spatial corridor relationships. |
| `UtilityInternalPass` | Hospital-owned internal supply maintains 8 MW P0 during the river event, but utility hospital delivery and sale are zero. |
| `TradeOffPass` | River saves 4 M and survives E1 but loses utility hospital service in the river event; north costs 4 M more, survives E1 and keeps utility hospital service in the river event. |

`IntegratedCausalPass = CoveragePass && RiskCausalityPass && UtilityInternalPass && TradeOffPass && !FacilitatorHelp`.

R2 is `PROXY-PASS` only when Coverage, RiskCausality, UtilityInternal and TradeOff are each at least 4/5 and Integrated is at least 3/5 among the same five technically valid sessions. These five same-model runs are a bounded consistency probe, not a population estimate. Choice and response speed are diagnostic. Do not relax the field rubrics or tune costs/choice ratios after seeing results.

Below PASS, use `PROXY-REVISE` only when every required conclusion is correct in at least 4/5 and all remaining reason omissions or axis ambiguities fit one InformationStructure family without changing the fixture, field rubric, values or answers. Use `PROXY-FAIL` when a core conclusion or event outcome is wrong in at least two sessions, two or more change families are required, passing would require answer disclosure or a fixture, answer-meaning or game-rule change, or the run meets neither PASS nor REVISE. FAIL applies to this card version, not to the whole game idea.
