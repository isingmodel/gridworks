# Scope 0A checkpoint 2 — R1 proxy decision

> `SubGateDecision = PROXY-FAIL`
>
> `Scope0State = STOPPED`
>
> `HumanValidationStatus = NOT_COLLECTED`

## Evidence

- Frozen source commit: `c781f9885a1fd4b2664d6217d74c7ef898178df8`
- Five technically valid cold sessions under the available runner evidence, with the pre-registered AB/BA allocation
- Deterministic preflight: PASS before the round
- Independent reviewed scoring: coverage `0/5`; risk causality `4/5`; utility/internal boundary and trade-off each `5/5`; integrated `0/5`
- Verbatim transcript SHA-256: `452296cd98ae070504536309540b236e6d1a88d1bb1006fc1d872bd30061cc32`
- Scored CSV SHA-256: `ce5171df855829d323957deca16369bb5fa6df8a78be4e0eb01cfc9dd8adf948`
- Public aggregate: [`RESULT.md`](RESULT.md)

## Decision and bounded next action

All five sessions omitted the service-area meaning required by `CoveragePass`. L02 also attributed North×E1 survival to a separate route/corridor rather than unambiguously to a different switched circuit. The other four sessions made that electrical-versus-spatial distinction explicit.

Because not every failed session has the same single scored deficit, the active decision order requires `PROXY-FAIL`. No revision is authorized. Scope 0 is stopped and Scope 0B remains unopened.

## Limits and observations

- This is correlated same-model proxy evidence, not human evidence.
- Provider build IDs, individual service timestamps, and independent tool telemetry were not exposed; `TechnicalValid` is therefore qualified as valid under the available runner evidence, which consists of the public runner configuration, continuous round timestamp, and verbatim self-reported tool lines.
- All five route choices favored the northern detour. The choice rate is not a target and caused no tuning.

## Repository checkpoint

- Initial R1 result commit: `5086419`
- Independent bounded review: `r1_result_review` found the L02 electrical/spatial attribution error; blind `l02_risk_adjudicator` independently confirmed `RiskCausalityPass=false`
- Reviewed result commit: `e732079`
- Push/PR: not authorized by the current task
