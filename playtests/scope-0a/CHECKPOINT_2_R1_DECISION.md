# Scope 0A checkpoint 2 — R1 proxy decision

> `SubGateDecision = PROXY-REVISE`
>
> `Scope0State = ACTIVE`
>
> `HumanValidationStatus = NOT_COLLECTED`

## Evidence

- Frozen source commit: `c781f9885a1fd4b2664d6217d74c7ef898178df8`
- Five technically valid cold sessions with the pre-registered AB/BA allocation
- Deterministic preflight: PASS before the round
- Independent strict scoring: coverage `0/5`; risk causality, utility/internal boundary, and trade-off each `5/5`; integrated `0/5`
- Verbatim transcript SHA-256: `452296cd98ae070504536309540b236e6d1a88d1bb1006fc1d872bd30061cc32`
- Scored CSV SHA-256: `613bc5b0bfb93a5742a3661012b73898448f78391815b0fc80b209c8a0f6cdf6`
- Public aggregate: [`RESULT.md`](RESULT.md)

## Decision and bounded next action

Every failed session had one and the same deficit: it named the missing upstream feeder but did not state that service-area inclusion alone is insufficient. The active decision order therefore requires `PROXY-REVISE`.

The only authorized revision is the Card 1 question/information focus. It may explicitly ask whether the service area is sufficient and what upstream connection is needed. No fixture value, rubric item, topology, economic number, Cards 2–4 content, layout, or target choice ratio may change. R2 starts from five new cold sessions and does not pool R1.

## Limits and observations

- This is correlated same-model proxy evidence, not human evidence.
- Provider build IDs, individual service timestamps, and independent tool telemetry were not exposed; only the public runner configuration, continuous round timestamp, and verbatim self-reported tool lines could be preserved.
- All five route choices favored the northern detour. The choice rate is not a target and caused no tuning.

## Repository checkpoint

- Initial R1 result commit: `PENDING`
- Independent bounded review: `PENDING`
- Reviewed result commit: `PENDING`
- Push/PR: not authorized by the current task
