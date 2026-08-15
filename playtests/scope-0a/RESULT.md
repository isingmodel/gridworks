# Scope 0A LLM proxy result

> Current state: `LLM-PROXY-R1 = PROXY-REVISE`
>
> Workflow: `Scope0State = ACTIVE`
>
> Human evidence: `HumanValidationStatus = NOT_COLLECTED`

## R1 frozen run

- Source commit: `c781f9885a1fd4b2664d6217d74c7ef898178df8`
- Card and prompt: `S0A-CARD-v1`, `S0A-PROXY-v1`
- Allocation: `L01 AB`, `L02 BA`, `L03 AB`, `L04 BA`, `L05 AB`
- Model configuration: `gpt-5.6-sol`, medium reasoning, no fork or memory
- Provider build metadata: `NOT_EXPOSED`
- Technically valid sessions: `5 / 5`
- Verbatim transcript SHA-256: `452296cd98ae070504536309540b236e6d1a88d1bb1006fc1d872bd30061cc32`
- Scored CSV SHA-256: `613bc5b0bfb93a5742a3661012b73898448f78391815b0fc80b209c8a0f6cdf6`
- Raw files: local-only under `playtests/scope-0a/private/`; not committed

The parent runner exposes neither provider build IDs nor per-session service timestamps/tool telemetry. The record therefore preserves the public model configuration, one continuous round completion timestamp, and each session's verbatim tool-report line. This limits reproducibility and is not treated as proof of an identical hidden build.

## R1 aggregate

| Measure | Result |
|---|---:|
| Coverage pass | `0 / 5` |
| Risk causality pass | `5 / 5` |
| Utility/internal boundary pass | `5 / 5` |
| Trade-off pass | `5 / 5` |
| Without facilitator help | `5 / 5` |
| Integrated causal pass | `0 / 5` |

All five sessions correctly said that the town was not supplied and that the disconnected upstream feeder had to be connected. None explicitly explained the other required half of the frozen rubric: the visible service area is only a connectable area and does not itself prove supply. This was every failed session's sole scored deficit.

All four event cells, electrical-versus-spatial causality, hospital-owned internal power versus utility delivery, and the 4 M route trade-off were explained correctly in all five sessions. All five chose the northern detour; choice rate is diagnostic and did not affect the decision. Reveal comments most often noted that the town still loses supply during the river event even when the hospital remains utility-supplied.

## R1 decision

`PROXY-REVISE`

The run had five technically valid sessions and did not reach 4/5 integrated passes. Every failed session had the same single scored deficit attributable to one Card 1 question/information-focus problem, so the active contract's mutually exclusive decision order selects `PROXY-REVISE`, not `PROXY-FAIL`.

The one allowed revision may change only the Card 1 question so it directly asks whether service-area inclusion is sufficient as well as what upstream connection is missing. Fixture values, topology, oracle, rubric, layout, Cards 2–4, and all economic numbers remain frozen. The revised card and prompt require new versions, a fresh preflight, and five new cold sessions; R1 results will not be pooled with R2.

This result is LLM proxy evidence only. It neither establishes novice human understanding nor authorizes Scope 0B implementation.
