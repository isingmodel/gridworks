# Scope 0B official v6 result

> `SubGateDecision = GO`
>
> `Scope0State = REVIEWED`
>
> `HumanValidationStatus = NOT_COLLECTED`
>
> `NextGate = NOT_SELECTED`

## 1. Frozen run

- authorization/source commit: `23be035e856e052091c529c14c8552aecc129327`
- versions: `S0B-BUILD-v1`, `S0B-FIXTURE-v1`, `S0B-RUN-v6`, `S0B-GATE-v1`
- allocation: `L01 AB`, `L02 BA`, `L03 AB`, `L04 BA`, `L05 AB`
- participant tasks: model `gpt-5.6-sol`, reasoning `medium`, `fork_turns=none`
- fixed rows: `5`; `COMPLETED = 5`, setup/participant/evidence failure `= 0`
- replacement, deleted row and coordinator-sent facilitator follow-up: `0`
- global native preflight and exact Debug rebuild: `PASS`

The five rows were fixed immediately after the single global preflight. No result was inspected to decide
whether a row should be retained. App diagnostics remain under the Git-ignored private directory; platform session
originals remain in their platform-owned location and are not copied into this repository.

## 2. Evidence anchors

- coordinator platform session ID: `01a009f9-40d4-7590-96e9-b2fc82c44c0c`
- coordinator platform SHA-256:
  `9e8b95b2126c599dedad88f6244cf1e76284c2d4650f953072b6f06e436a874c`
- preflight app SHA-256:
  `ba284a084301564be2a63a6c66f9c099a707f36a041d026a5e8bb59e5a374843`
- common final snapshot SHA-256:
  `d8d6ac9edf2dd05e45be72dd1d0f2d01d849a8e051d2d59115b9ba9a7880792d`

| Row | Participant platform ID | Participant SHA-256 | App diagnostic SHA-256 |
|---|---|---|---|
| L01 | `01a009fb-cc43-7c42-85db-53868d31cb6b` | `94ba87c177b52bb0e5ff9b8a7105f05f48a440bc28a0eb964429df8f173bd5f2` | `a447def105fcab4ca71c71a93d55863de5a8e9c9d3f4743e333391bd197fd88b` |
| L02 | `01a009fe-bbf4-7a41-89c2-0b80441b9829` | `2b4d75500bdf52072ada9230cb66cdeabb7f6adfe379dbb9d1bae73e2f9507b0` | `4e87362114574523664db0064fae55b1a036cfc970fc949c19ba2133b2691bb2` |
| L03 | `01a00a01-a2a7-7b30-8327-7cab34b4bb0a` | `dc9476ba307cc749d738408a2904ef3628ca04cd3e1a73dca248e69e5929603f` | `c86ce4adb563cdce11740a490b0538e32094a0fbdf10f96c0f5eed187ad08ee9` |
| L04 | `01a00a04-84b7-7920-9227-6baafad59c4f` | `d2605f3240df1ac7fc7ad19bc73b22da5220eb5a0c09dd07cbd30a855ff3a813` | `0fcbd70e4d872b9f8f8ecdb76a6d698c085bb8d158fe0780829640cd1a415e39` |
| L05 | `01a00a07-a073-75f2-b4b6-74859a96827f` | `538ca7317fe63bae8bb207ed34490c49211aa26498b9522b851ba42aadca04e7` | `146880d42590c36380561de9c1989f4e55387e0ec2aeb48533daeb80e756a1cb` |

The exact original paths at the post-round audit were:

- coordinator: `/Users/fred/.codex/sessions/2026/08/16/rollout-2026-08-16T18-48-45-01a009f9-40d4-7590-96e9-b2fc82c44c0c.jsonl`
- L01 participant: `/Users/fred/.codex/sessions/2026/08/16/rollout-2026-08-16T18-51-31-01a009fb-cc43-7c42-85db-53868d31cb6b.jsonl`
- L02 participant: `/Users/fred/.codex/sessions/2026/08/16/rollout-2026-08-16T18-54-44-01a009fe-bbf4-7a41-89c2-0b80441b9829.jsonl`
- L03 participant: `/Users/fred/.codex/sessions/2026/08/16/rollout-2026-08-16T18-57-54-01a00a01-a2a7-7b30-8327-7cab34b4bb0a.jsonl`
- L04 participant: `/Users/fred/.codex/sessions/2026/08/16/rollout-2026-08-16T19-01-03-01a00a04-84b7-7920-9227-6baafad59c4f.jsonl`
- L05 participant: `/Users/fred/.codex/sessions/2026/08/16/rollout-2026-08-16T19-04-27-01a00a07-a073-75f2-b4b6-74859a96827f.jsonl`
- app diagnostics: `/Users/fred/dev/electric_simulator/playtests/scope-0b/private/S0B-V6-PREFLIGHT-app.jsonl` and
  `/Users/fred/dev/electric_simulator/playtests/scope-0b/private/S0B-V6-L01-app.jsonl` through
  `/Users/fred/dev/electric_simulator/playtests/scope-0b/private/S0B-V6-L05-app.jsonl`

Independent evidence audit `s0b_v6_evidence_audit` reproduced all hashes and classified every fixed row as
`COMPLETED`. It verified the parent/task mapping, frozen model configuration, zero coordinator help, allowed
content sources, action-to-fresh-state order, exact ten-event app sequence, predictions, selected corridor and
`FINAL`.

Platform originals encrypt the spawn-message body. They prove that coordinator dispatch and participant receipt
ciphertext match, but do not prove the frozen prompt's plaintext bytes after the fact. The prompt hash remains a
reviewed execution procedure, not a stronger provenance claim.

## 3. Strict aggregate

| Measure | Result | Frozen gate |
|---|---:|---:|
| `InteractionCompletionPass` | `5 / 5` | `>= 4 / 5` |
| `CoverageActionPass` | `5 / 5` | `>= 4 / 5` |
| `RiskPredictionPass` | `5 / 5` | `>= 4 / 5` |
| `UtilityBoundaryPass` | `5 / 5` | `>= 4 / 5` |
| `IntegratedInteractionPass` | `5 / 5` | `>= 3 / 5` |
| `CoverageConclusionPass` | `5 / 5` | `>= 4 / 5` for revision safety |
| `RiskConclusionPass` | `5 / 5` | `>= 4 / 5` for revision safety |
| `UtilityConclusionPass` | `5 / 5` | `>= 4 / 5` for revision safety |

Every pre-reveal lock was exactly `River/E1 remains`, `River/old corridor cut`, `North/E1 remains`,
`North/old corridor remains`. Every participant completed the native UI, separated service eligibility from an
energized source path, and separated hospital-owned P0 backup from utility delivery and sale. Independent scorer
`s0b_v6_strict_score` reproduced each row without using reveal prose to repair a prediction.

## 4. Decision and limits

`GO`

The result exceeds every frozen threshold; it does not depend on whether the integrated threshold was `3/5` or
`4/5`. Scope 0 is therefore `REVIEWED`. This decision only supports repeated same-model cold-LLM transfer of the
four authored causal distinctions into this native UI.

It does **not** establish novice-human understanding, fun, accessibility, free-line construction, economic
balance, strategy diversity or general power-grid simulation quality. Five same-model rows are not population
statistics. `HumanValidationStatus` remains `NOT_COLLECTED`.

Two diagnostics must not be converted into post-hoc score failures or parameter tuning:

- all five selected `NORTH_DETOUR`; the run did not observe the built River branch and gives no target choice ratio;
- all five said the mutually exclusive `남음/끊김` choices looked like independent switches or checkboxes. They
  still completed unaided, so this is a presentation observation for any future reuse of that control.

L04 sent one unsolicited completion notice toward the coordinator after native `FINAL`; the coordinator did not
reply or help. Godot also printed shutdown diagnostics only after `FINAL` when the exact PID was terminated. The
frozen rule forbids neither event, and the independent audit found no basis to reclassify a row.

Passing Scope 0 does not authorize Scope 1 implementation. A separate adaptive review must select one next risk,
rewrite its contract to match the evidence, and keep implementation closed until the user explicitly approves it.
