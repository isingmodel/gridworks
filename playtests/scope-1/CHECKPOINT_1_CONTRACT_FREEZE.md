# Scope 1 checkpoint 1 — activation and fixture handoff

> `CheckpointStatus = REVIEW_IN_PROGRESS`
>
> `ImplementationAuthorization = GRANTED`
>
> `OfficialProxyAuthorization = NOT_GRANTED`
>
> `FixtureAuthorityStatus = HANDOFF_REVIEW_IN_PROGRESS`

## Authority

The user's persistent goal explicitly authorizes Scope 1 implementation and bounded iterations until Coverage
and Integrated placement pass. Root [`README.md`](../../README.md) now names
[`SCOPE_1_INTERACTION.md`](../../docs/scopes/SCOPE_1_INTERACTION.md) as the active implementation scope.

This checkpoint opens only the manual-support/one-`MaxSpan` vertical slice. It does not open asset siting,
economy, demolition, general graphs, terrain, route optimization or a later scope. Official LLM proxy sessions
remain closed until implementation, native preflight and the evidence protocol receive their own reviewed
checkpoint.

## Single fixture handoff

The proposed machine authority is [`data/scope-1-v1.json`](../../data/scope-1-v1.json):

- schema `gridworks.scope1.fixture.v1`, fixture `S1-FIXTURE-v1`
- exact root fields only; unknown, missing, null or duplicate properties are invalid
- integer `GridUnit` coordinates and `GameMinute` time
- one fixed source, one fixed target, one `MaxSpan`, one build duration
- `verificationOnly.witnessSupportPositions` is checker-only and must never reach Game or participant copy

The reviewed pre-code table in the active scope is the human-readable handoff mirror. Once this checkpoint is
reviewed, JSON becomes the sole machine authority and code must not duplicate its numeric values.

Fixture SHA-256: `31ab8ab0390fd625469f64b463a305880ccab8d87a87d75da2bcb650660fc8d8`

## Isolation contract

- Add scope-local `Scope1*` Core types, loader and placement session.
- Do not generalize or modify Scope 0B `FixtureLoader`, `GridworksSession`, enums or snapshot model.
- Add a separate Scope 1 checks executable and a separate authored Godot scene/map view.
- Keep the completed Scope 0B scene and automated checks runnable as regression evidence.
- Do not add future IDs, type registries, graph interfaces, save fields or route recommendation.

## Required review evidence

- contract verifier checks exact fixture shape, values, integer-distance oracle and document links
- bounded independent reviewers challenge schema necessity, Core isolation and authorization freshness
- Scope 0B contract, 3,098 assertions and Scope 0A R2 regression remain green
- initial activation content commit: `PENDING`
- reviewers: `PENDING`
- final review: `PENDING`
- reviewed activation content commit: `PENDING`

Until all pending fields are closed and `FixtureAuthorityStatus = REVIEWED_MACHINE_AUTHORITY`, implementation
source changes remain closed.
