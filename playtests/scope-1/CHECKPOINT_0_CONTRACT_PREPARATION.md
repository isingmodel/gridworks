# Scope 1 checkpoint 0 — contract preparation

> 역사 기록: 이 checkpoint의 `NOT_GRANTED`는 준비 당시 상태다. 2026-08-16 구현 승인은
> [`CHECKPOINT_1_IMPLEMENTATION_ACTIVATION.md`](CHECKPOINT_1_IMPLEMENTATION_ACTIVATION.md)가 소유한다.

> `PreparationStatus = REVIEWED`
>
> `ImplementationAuthorization = NOT_GRANTED`
>
> `HumanValidationStatus = NOT_COLLECTED`

## Adaptive decision

Scope 0B official v6 ended `GO` with all frozen fields and integrated `5/5`. It established authored native-UI
causal transfer but did not test direct line construction. Manual support placement plus one `MaxSpan` is the
smallest untested 1.0 product promise that can be isolated without siting, demolition, economy or events.

The selected contract is [`SCOPE_1_INTERACTION.md`](../../docs/scopes/SCOPE_1_INTERACTION.md). This checkpoint
prepares documentation only. It authorizes no source, game, tool, data fixture or official-session change.

## Scope reduction

Removed from the earlier candidate:

- terminal selection, quote/back/cancel and cash
- existing crossing lines and junction semantics
- generic reachability, branching and future graph abstractions
- path optimization, minimum pole count and parameter tuning
- human-test steps that conflicted with the temporary LLM-proxy instruction

The remaining fixture has two fixed terminals, one support type, one line type, integer squared distance,
one `MaxSpan`, `Undo`, atomic completion and one integrated proxy criterion.

## Validation policy refinement

Later development lessons narrowed the proxy to the root README policy as instantiated by Scope 1 §9, without
opening implementation or execution or changing the fixture, rules, authorization or human-evidence status.

- initial policy refinement commit: `5ba5142312cab78e7f48d0d6716eff3c13765a2a`
- bounded independent reviewer: `development_lessons_audit`
- initial review: `P0=0, P1=2, P2=3`; all findings addressed without new files or execution machinery
- final recheck: `P0=0, P1=0, P2=0`; blocker 없음
- reviewed policy content commit: `41a2a65e251c1c39fdd69473f53cf461117ca285`

## Current-code plan refresh

The Scope 1 plan now isolates a minimal vertical slice from the completed Scope 0B files, keeps checker-only witness
coordinates out of the future product fixture, fixes the fixture → Core → Game → optional observation order and
defines the exact Scope 0B regressions that must remain intact. It still authorizes no implementation, fixture file or
official proxy execution.

- initial plan refresh commit: `9fc4dfe2da2af226588ced76b12ae80c7d71c629`
- bounded independent reviewers: `architecture_spec_audit`, `development_lessons_audit`,
  `game_state_narrative_audit`
- initial reviews: architecture `P0=0, P1=4, P2=2`; validation `P0=0, P1=2, P2=1`;
  authority/currentness `P0=0, P1=0, P2=0`
- final recheck: `P0=0, P1=0, P2=0`; blocker 없음
- reviewed plan content commit: `445e2ad6b11cb770cbfc2989420f873b09c021cf`

## Repository checkpoint

- initial preparation commit: `aa0669bc54a3a4847ee6b29aa61a7551a6dcfac2`
- bounded independent reviewers: `scope1_contract_skeptical_review`, `scope1_docs_authority_review`,
  `scope1_gate_parameter_review`
- final review: `P0=0, P1=0, P2=0`; blocker 없음
- reviewed preparation content commit: `2dd0c732e57df76172eca76fbd06a166eff02ee4`
- implementation: closed pending explicit user approval
- push/PR: not authorized by the current task
