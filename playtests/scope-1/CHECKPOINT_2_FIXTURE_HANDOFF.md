# Scope 1 checkpoint 2 — fixture handoff

> `HandoffStatus = REVIEWED`
>
> `MachineAuthority = REVIEWED_JSON`
>
> `CoreGameStart = OPEN`
>
> `OfficialProxyAuthorization = NOT_GRANTED`

## Handoff boundary

The active Scope 1 contract's nine-field JSON skeleton has been copied without added product fields to
`data/scope-1-v1.json`. The fixture contains no presentation object, hidden witness, checker oracle, route
recommendation, economy, graph, load or future lifecycle state.

Until this checkpoint is reviewed, the contract skeleton remains authoritative and Core or Game work stays
closed. After review, the JSON becomes the sole machine authority. The checker-only witness remains only in
`verify_contract.rb` and the later independent Core checks.

## Deterministic evidence

- fixture SHA-256: `f308a739f9e4fcaf9d6f07aacba65af6fdd9ae3600a1e5569254fcb749bb2edc`
- exact nine-field root and nested shapes: `PASS`
- direct span failure, boundary witness and completion minute: `PASS`
- Core/Game dependency: none

## Review closure

- initial fixture commit: `17d76099d1c16974b62d3fa59233e338b5df4a5d`
- bounded independent reviewer: `development_lessons_audit`
- initial findings: `P0=0, P1=1, P2=2`; authority wording and redundant checker cases simplified
- final recheck: `P0=0, P1=0, P2=0`; blockers none
- reviewed fixture commit: `f1eafadf512ee71138034e3639776973ad09ab39`

The reviewed JSON is now the sole machine authority. Core and checker work may start; official proxy rows
remain closed.
