# Scope 1 checkpoint 2 — fixture handoff

> `HandoffStatus = REVIEW_IN_PROGRESS`
>
> `MachineAuthority = CONTRACT_SKELETON`
>
> `ImplementationAuthorization = GRANTED_AFTER_REVIEW`
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

- initial fixture commit: `PENDING`
- bounded independent reviewer: `PENDING`
- initial findings: `PENDING`
- final recheck: `PENDING`
- reviewed fixture commit: `PENDING`

No Core, checks project or Game file may begin until this checkpoint records a clean review and
`MachineAuthority = REVIEWED_JSON`.
