# Scope 0B contract-freeze checkpoint

> Status: **DRAFT — independent review pending**
>
> `SubGateDecision = PENDING`
>
> `Scope0State = 0B_CONTRACT_AUTHORIZED`

## Frozen candidates

- contract: [`docs/scopes/SCOPE_0B_PLAYABLE.md`](../../docs/scopes/SCOPE_0B_PLAYABLE.md)
- machine fixture: [`data/scope-0b-v1.json`](../../data/scope-0b-v1.json)
- contract verifier: [`verify_contract.rb`](verify_contract.rb)
- `FixtureVersion = S0B-FIXTURE-v1`
- frozen prompt-template SHA-256: `4a07e8fdf61cbd2475ba27613e9a89d4fcb254cc54c6d19d5f6a740ca64f2111`
- `DecisionRuleVersion = S0B-GATE-v1`
- implementation authority: **closed until this checkpoint is reviewed**

## Evidence

- R2 source result: [`playtests/scope-0a-r2/RESULT.md`](../scope-0a-r2/RESULT.md), all four fields and
  integrated `5/5`
- selected residual risk: prompted causal understanding may not transfer to a stateful clickable UI
- exact toolchain source and archive digest are recorded in the contract
- fixture handoff, link and oracle checks: `PASS`
  - strict fixture/root and authored-path checks
  - Scope 0A R2 deterministic regression
  - six removal outcomes, energy and exact integer cash oracle
  - local links and stale candidate references
- frozen fixture SHA-256: `e617f7b9163294ca0e72f89bf3cb3a3be634c0de21f1d2736549863f53617e57`

## Repository checkpoint

- initial contract commit: `f0326030c480f482fcb86013615c5264a876f35f`
- independent bounded reviewer: `PENDING`
- scope-valid findings and fixes: `PENDING`
- reviewed contract commit: `PENDING`
- documentation freshness audit: `PENDING`
- implementation may start: `NO`
