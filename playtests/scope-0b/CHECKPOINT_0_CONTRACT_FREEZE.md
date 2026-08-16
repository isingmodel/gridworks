# Scope 0B contract-freeze checkpoint

> Current status: **HISTORICAL CONTRACT CHECKPOINT — Scope 0B completed; no new implementation or run is authorized**
>
> Historical status at review: **REVIEWED — implementation authorized; proxy remains closed**
>
> `SubGateDecision = PENDING`
>
> `Scope0State = 0B_ACTIVE`

## Frozen authority

- contract: [`docs/scopes/SCOPE_0B_PLAYABLE.md`](../../docs/scopes/SCOPE_0B_PLAYABLE.md)
- machine fixture: [`data/scope-0b-v1.json`](../../data/scope-0b-v1.json)
- contract verifier: [`verify_contract.rb`](verify_contract.rb)
- `FixtureVersion = S0B-FIXTURE-v1`
- frozen prompt-template SHA-256: `4a07e8fdf61cbd2475ba27613e9a89d4fcb254cc54c6d19d5f6a740ca64f2111`
- `DecisionRuleVersion = S0B-GATE-v1`
- implementation authority: **open for the exact Scope 0B contract only**

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
- independent bounded reviewers: `scope0b_adversarial`, `scope0b_frozen_review`
- scope-valid findings and fixes: full snapshot/settlement oracle, commissioned-only removal, River-only
  hidden trace, OLD-only display bands, non-terminal crossing rejection, exact participant prompt/AX labels,
  uncensored interaction failures, runner/app fault truth table, and stale-document cleanup
- reviewer result: `P0=0, P1=0, P2=0`
- reviewed contract commit: `01c3c279edfcd3b5b5c743bad5476b1b87ce3dbc`
- documentation freshness audit: `PASS` — README, docs map, product documents, scope index and historical R2
  package agree on current authority
- verification: Scope 0B contract verifier, Scope 0A R2 regression, Scope 0A R1 regression and
  `git diff --check` all `PASS`
- implementation may start: `YES`
