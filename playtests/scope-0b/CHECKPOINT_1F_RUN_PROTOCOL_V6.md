# Scope 0B v5 protocol result and v6 reset checkpoint

> Status: **DRAFT — official v6 sessions closed**
>
> `SubGateDecision = PENDING`
>
> `Scope0State = 0B_ACTIVE`
>
> `HumanValidationStatus = NOT_COLLECTED`

## 1. Why v5 stops without a game decision

`S0B-RUN-v5` stopped after six actual launches. L01 used both permitted replacements before its third launch.
The third L01 launch and L02 launch reached native `FINAL`, but the coordinator had not created their runner
manifests or sampled the required monotonic launch/READY/end times while they ran. Filesystem timestamps and
boot-time reconstruction are not the frozen monotonic evidence and must not be substituted after the fact.

The v5 contract makes that runner artifact part of `TechnicalValid`. L01 therefore cannot supply a valid slot,
and at most the remaining four logical sessions could be valid. Five valid slots are mathematically impossible,
so L05 was not launched and gameplay was not scored.

```text
EvidenceRound = S0B-RUN-v5
ActualLaunches = 6/7
CompletedNativeUI = 4
TechnicalValidSlots = NOT_ESTABLISHED
MaximumAttainableValidSlots = 4/5
OfficialGameplayScore = NOT_COMPUTED
RoundStatus = PROXY-RUN-BLOCKED
BlockerClass = RUNNER_EVIDENCE
SubGateDecision = PENDING
RevisionBudgetRemaining = 1
```

This is not `GO`, `REVISE` or `NO-GO`, spends no gameplay revision and is not combined with v6. Raw v5 app
and engine logs remain private. No runner manifest is backfilled with reconstructed times.

## 2. Small v6 delta

The repeated blockers are caused by the measurement wrapper, not the game. v6 therefore removes the wrapper
instead of adding another evidence schema.

1. Build, fixture, scene, UI, prompt text, model, rubric, gate and AB/BA allocation are unchanged.
2. Before participant dispatch, the coordinator confirms a fresh single process, exact `READY` identity and
   readable target title. Setup failure blocks the round before any participant observation.
3. After dispatch, each of the five cold sessions is scored and never replaced. A participant/app failure is a
   completion failure rather than selected-out evidence.
4. The platform-owned session JSONL and app diagnostic JSONL are the only required original evidence.
5. Runner manifests, copied transcripts, participant provenance exports, custom monotonic timestamps,
   end-reason/fault tables and seven-launch replacement accounting are removed.
6. The platform log audits exact prompt, model/task identity, content sources, UI sequence and final report.
   The app log audits frozen identity, locked predictions, accepted commands and final state.
7. The 15-minute limit remains operational but is not an experimental validity field.

- `ContractVersion = S0B-CONTRACT-v6`
- `PromptVersion = S0B-PROXY-v6`
- `RunProtocolVersion = S0B-RUN-v6`
- unchanged: `S0B-BUILD-v1`, `S0B-FIXTURE-v1`, `S0B-GATE-v1`
- sessions: `S0B-V6-L01`–`L05`, variants `AB/BA/AB/BA/AB`

## 3. Frozen hashes

- source-manifest build SHA-256:
  `69b658715a84b4099677b36c7d4fb458d65add59fcff8474865d95bf418e03bd`
- fixture SHA-256: `e617f7b9163294ca0e72f89bf3cb3a3be634c0de21f1d2736549863f53617e57`
- task-message template SHA-256: `1625d1a8d2fcb918855d9d9e28bf536bd1a8c0cb9f6d93f25c0c2269831b4869`
- facilitator-sheet SHA-256: `cf67adbccb6bd808e0bf4747c62892372286fc99fda8fe15b9b85c5634e66fca`

| Session | Variant | Task-message SHA-256 |
|---|---|---|
| `S0B-V6-L01` | `ab` | `1c5f7309434683d56350d07671628a192f3667d2d2ca07d7894e41e680152453` |
| `S0B-V6-L02` | `ba` | `ce8d8722c83f7f793c22bc5eb00d6ede1c2cb7d8a08f798a2e817e3d6a41fdca` |
| `S0B-V6-L03` | `ab` | `fb0b769a3e3dce788126dcdc0bf72af6619aae9458b9c789af86a1808de52e99` |
| `S0B-V6-L04` | `ba` | `21d6c8d0628e9e7aeb965deac130d8c7d61ef587f933fa20ddc3f03fb2893425` |
| `S0B-V6-L05` | `ab` | `45d1b39a4e738535b19157f927b8566548d259933068e5afe5a151728ecc0e92` |

## 4. Review checkpoint

- initial v6 protocol commit: `PENDING`
- bounded independent v6 reviewers: `PENDING`
- review standard: skeptic; simple structure is the default
- runtime, fixture, UI, gameplay rubric or gate change: `NONE`
- final review: `PENDING`
- reviewed v6 protocol commit: `PENDING`

Official v6 sessions remain closed until this checkpoint and the facilitator sheet are reviewed, hashes are
exact and both verification scripts pass. Scope 1 remains unopened.
