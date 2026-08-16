# Scope 1 checkpoint 1 — activation and fixture handoff

> `CheckpointStatus = REVIEWED`
>
> `ImplementationAuthorization = GRANTED`
>
> `OfficialProxyAuthorization = NOT_GRANTED`
>
> `FixtureAuthorityStatus = REVIEWED_MACHINE_AUTHORITY`

## Authority

On 2026-08-16 the user-provided persistent objective says: “Coverage, 통합 통과가 완수될 때 까지 수정
iteration을 돌려줘. Goal은 Scope 1을 실행할 수 있는 상태까지 만드는 거야.” This explicitly
authorizes Scope 1 implementation and the contract's one possible bounded UI revision. In this contract,
Coverage is a constituent of `IntegratedPlacementPass`; the sole completion gate is
`IntegratedPlacementPass >= 2/3`, with at most one `REVISE` round. Root [`README.md`](../../README.md) now names
[`SCOPE_1_INTERACTION.md`](../../docs/scopes/SCOPE_1_INTERACTION.md) as the active implementation scope.

This checkpoint opens only the manual-support/one-`MaxSpan` vertical slice. It does not open asset siting,
economy, demolition, general graphs, terrain, route optimization or a later scope. Official LLM proxy sessions
remain closed until implementation, native preflight and the evidence protocol receive their own reviewed
checkpoint.

## Single fixture handoff

The proposed machine authority is [`data/scope-1-v1.json`](../../data/scope-1-v1.json):

- schema `gridworks.scope1.fixture.v1`, fixture `S1-FIXTURE-v1`
- exact nine root fields only; unknown, missing, null or duplicate properties are invalid
- integer `GridUnit` coordinates and `GameMinute` time
- one fixed source, one fixed target, one `MaxSpan`, one build duration

The checker-only witness path is test code, not a product fixture parameter. Game and participant-facing
artifacts do not consume it.

The reviewed pre-code table in the active scope is the human-readable handoff mirror. Once this checkpoint is
reviewed, JSON becomes the sole machine authority and code must not duplicate its numeric values.

Fixture SHA-256: `8c1cd63efe1e6a6d3745db96c4071fd3a264ace07e715883581163f1c98e6a2b`

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
- initial activation content commit: `608156cc75f64b21c17169df86faee68bcaf6f1d`
- reviewers: `scope1_contract_skeptical_review`, `scope1_docs_authority_review`,
  `scope1_gate_parameter_review`
- final review: `P0=0, P1=0, P2=0`; blockers none
- reviewed activation content commit: `9119f93f6b3e6623190041ffeeb0d726b573499e`

The fixture handoff is reviewed. JSON is now the sole machine authority and Scope 1 source implementation is
open. Official proxy sessions remain closed until a later reviewed implementation/evidence checkpoint.
