# Scope 0A LLM proxy result

> Current state: `LLM-PROXY-R1 = PROXY-FAIL`
>
> Workflow: `Scope0State = STOPPED`
>
> Human evidence: `HumanValidationStatus = NOT_COLLECTED`

## R1 frozen run

- Source commit: `c781f9885a1fd4b2664d6217d74c7ef898178df8`
- Card and prompt: `S0A-CARD-v1`, `S0A-PROXY-v1`
- Allocation: `L01 AB`, `L02 BA`, `L03 AB`, `L04 BA`, `L05 AB`
- Model configuration: `gpt-5.6-sol`, medium reasoning, no fork or memory
- Provider build metadata: `NOT_EXPOSED`
- Technically valid sessions under available runner evidence: `5 / 5`
- Verbatim transcript SHA-256: `452296cd98ae070504536309540b236e6d1a88d1bb1006fc1d872bd30061cc32`
- Scored CSV SHA-256: `ce5171df855829d323957deca16369bb5fa6df8a78be4e0eb01cfc9dd8adf948`
- Raw files: local-only under `playtests/scope-0a/private/`; not committed

The parent runner exposes neither provider build IDs nor per-session service timestamps/tool telemetry. The record therefore preserves the public model configuration, one continuous round completion timestamp, and each session's verbatim tool-report line. This limits reproducibility and is not treated as proof of an identical hidden build.

## R1 aggregate

| Measure | Result |
|---|---:|
| Coverage pass | `0 / 5` |
| Risk causality pass | `4 / 5` |
| Utility/internal boundary pass | `5 / 5` |
| Trade-off pass | `5 / 5` |
| Without facilitator help | `5 / 5` |
| Integrated causal pass | `0 / 5` |

All five sessions correctly said that the town was not supplied and that the disconnected upstream feeder had to be connected. None explicitly explained the other required half of the frozen rubric: the visible service area is only a connectable area and does not itself prove supply. Coverage therefore failed in all five sessions.

All four event-cell conclusions were correct. L01 and L03–L05 also attributed both E1 survivals to different switched circuits and the river-event difference to spatial corridors. L02 instead explained North×E1 with “별도 통로” and “경로가 분리,” while naming the different electrical circuit only for the river plan. Under the frozen rubric that is a second scored deficit: it does not unambiguously separate electrical contingency from spatial independence. Utility/internal and trade-off passed in all five sessions.

All five chose the northern detour; choice rate is diagnostic and did not affect the decision. Reveal comments most often noted that the town still loses supply during the river event even when the hospital remains utility-supplied.

## R1 decision

`PROXY-FAIL`

The run had five technically valid sessions under the available runner evidence and did not reach 4/5 integrated passes. Coverage was the sole deficit for four sessions, but L02 had both coverage and risk-causality deficits. Therefore it is false that every failed session had the same single scored deficit. The one-revision condition is not met, so the mutually exclusive decision order selects `PROXY-FAIL`.

No card, prompt, fixture, rubric, topology, number, or implementation is revised under this result. Scope 0 enters `SCOPE_0_STOPPED`; Scope 0B remains unopened. A future attempt would require a new explicit user decision and a newly authorized gate rather than consuming the revision branch that this evidence did not earn.

This result is LLM proxy evidence only. It neither establishes novice human understanding nor authorizes Scope 0B implementation.
