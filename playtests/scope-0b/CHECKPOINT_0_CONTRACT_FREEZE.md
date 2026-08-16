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
- frozen fixture SHA-256: `7809b9d3e7d5764e3a991604d8fbd5fa06e15840e79bfdf01df6201d686cabcd`

## Repository checkpoint

- initial contract commit: `PENDING`
- independent bounded reviewer: `PENDING`
- scope-valid findings and fixes: `PENDING`
- reviewed contract commit: `PENDING`
- documentation freshness audit: `PENDING`
- implementation may start: `NO`
