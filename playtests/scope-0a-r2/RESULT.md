# Scope 0A R2 LLM proxy result

> Current state: `LLM-PROXY-R2 = PROXY-PASS`
>
> Workflow: `Scope0State = 0B_CONTRACT_AUTHORIZED`
>
> Human evidence: `HumanValidationStatus = NOT_COLLECTED`

## Frozen run

- source commit: `d55f5a72fef7d3bc4cf34c008aa35901e40e9468`
- materials: `S0A-CARD-v2`, `S0A-PROXY-v2`, `S0A-GATE-v2`
- allocation: `R2-L01 AB`, `R2-L02 BA`, `R2-L03 AB`, `R2-L04 BA`, `R2-L05 AB`
- model configuration: `gpt-5.6-sol`, medium reasoning, no fork or memory
- provider build metadata: `NOT_EXPOSED`
- technically valid sessions under available runner evidence: `5 / 5`
- facilitator SHA-256: `8561848fa873109be2adad6a52d3d43119d9ddd8afd010f9c5ab89e9f14b6f88`
- card-manifest SHA-256: `913e20ec8b1e6394b6545c467cdda338574281244b6f6e98fc258f371f0c9d3b`
- verbatim input/response SHA-256: `891d9a5a91d61b36443efa9d7d6be09660d2cc5195104099d0314f9541f40a80`
- scored CSV SHA-256: `ed41660ce24ca61d9a60013de5055582f53b1daafdd4728c22991bcb3b0135b8`
- raw files: local-only under `playtests/scope-0a-r2/private/`; not committed

The runner does not expose provider build IDs, individual service timestamps or independent tool telemetry.
Technical validity therefore rests on cold-session creation settings, complete input/response records and each
session's reported exact `view_image` paths. This is a provenance limit, not proof of hidden build identity.

## Aggregate

| Measure | Result | Gate |
|---|---:|---:|
| Coverage pass | `5 / 5` | `>= 4 / 5` |
| Risk causality pass | `5 / 5` | `>= 4 / 5` |
| Utility/internal boundary pass | `5 / 5` | `>= 4 / 5` |
| Trade-off pass | `5 / 5` | `>= 4 / 5` |
| Without facilitator help | `5 / 5` | required per integrated pass |
| Integrated causal pass | `5 / 5` | `>= 3 / 5` |

All five sessions gave the exact four event outcomes: river/E1 remains, river/spatial fails, north/E1 remains
and north/spatial remains. Each attributed E1 survival to a different switching circuit, river failure to the
shared river corridor and north survival to the separate northern corridor.

All five separated service-area eligibility from an energized upstream generator path. L05 did not repeat the
phrase “not automatic,” but separately described the geographic service-area condition and the generator–feeder–
voltage requirement for actual supply; strict independent review therefore passed the required meaning rather
than a magic phrase. L03's `내원전` and L05's `내붕전원` were obvious local typos followed immediately by the
correct hospital-owned UPS/diesel, P0 and non-utility-sale explanation, so they were not meaning failures.

## Decision

`PROXY-PASS`

`S0A-GATE-v2` requires every field at 4/5 or better and the same-response four-field AND at 3/5 or better.
The frozen five responses reached 5/5 on every field and integrated measure without reveal-stage corrections.
An independent strict scorer reproduced all five rows and the aggregate from Response 1/3 and 2/3 only.

All five chose the northern detour; this is diagnostic and did not affect the decision. Reveal comments most
often noticed that the town still loses supply during the river event even when the northern route preserves
utility hospital supply, and that the northern event loss is much smaller than the river-route loss.

This result means only that the structured cards elicited the frozen causal distinctions consistently from five
same-model cold LLM runs. It does not establish spontaneous discovery, novice-human understanding, fun,
accessibility or actual UI interaction. `HumanValidationStatus` remains `NOT_COLLECTED`.

The R2 result checkpoint is committed and independently reviewed, so the user's conditional authorization now
opens Scope 0B **contract authoring**. Implementation remains blocked until that contract and its toolchain,
fixture and checks are frozen and independently reviewed. This result does not open Scope 1.
