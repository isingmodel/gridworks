# Scope 0B v2 protocol result and v3 reset checkpoint

> Status: **FROZEN DRAFT — official v3 sessions closed pending independent review**
>
> `RoundStatus = PROXY-RUN-BLOCKED` for `S0B-RUN-v2`
>
> `SubGateDecision = PENDING`
>
> `Scope0State = 0B_ACTIVE`
>
> `HumanValidationStatus = NOT_COLLECTED`

## 1. Why v2 stops without a game decision

`S0B-RUN-v2` used two launches for logical slot `S0B-V2-L01` and then stopped.

- launch 1 reached app `READY`, but the first target-state read returned the exact host-lock error. The
  retained trace supports `runner_error:runner`, so this launch was the one allowed replacement.
- launch 2 completed the real native UI through `FINAL`, locked the four expected predictions and chose
  North. Before the first direct wrapper, however, it made one extra outer import attempt. That attempt failed
  with `unsupported import in exec` and exposed no game, repository, oracle, web or other-session content.
  It still violated the frozen v2 rule that only the designated skill read could precede the first direct
  wrapper, so `TechnicalValid = false` under v2.

Launch 2 is a participant protocol mismatch, not an independently evidenced runner error, and is therefore
not replaceable. Once logical L01 has no valid slot, L02–L05 can supply at most four valid slots. Continuing
would spend launches without making the five-slot gate reachable, so the round closes immediately:

```text
EvidenceRound = S0B-RUN-v2
OfficialLaunches = 2/7
RoundStatus = PROXY-RUN-BLOCKED
BlockerClass = RUN_PROTOCOL
TechnicalValid = 0/5
SubGateDecision = PENDING
RevisionBudgetRemaining = 1
```

This is not `GO`, `REVISE` or `NO-GO`. It does not spend the gameplay revision budget. v2 responses are not
scored, combined with v1 or reinterpreted under v3. The completed launch is only an unscored technical
observation that the unchanged native path can reach `FINAL`.

## 2. Frozen v2 evidence anchors

Raw evidence remains ignored under `playtests/scope-0b/private/`. The retained Markdown traces are
coordinator-normalized summaries, not a claim that v2's exact post-run export requirement was satisfied; the
bootstrap violation and app diagnostic are nevertheless directly preserved.

| Evidence ID | Role | Tool trace | Runner manifest | Transcript | App diagnostic |
|---|---|---|---|---|---|
| `S0B-V2-L01-launch1` | evidenced runner replacement | `aeae76d5d9db7915e3dc7807526269c7f20064c4fd74f85d148148f4d1f3b53e` | `f162c9b73fe337286027ba066e1e945ffd854b610df107735a87d1780fdd2edc` | `56c37cdf17760bbf45840d8c3efa327ac113fb9950ccd18b5788c47edcdb470d` | `8f964d8df3998a0d5aacc2b97a0713d0fdc84c08311c441984d7161e10e487e4` |
| `S0B-V2-L01-launch2` | completed but v2-invalid | `87c191df96b73791a1b2f76070407d38aa5b2de16651682051f70ca4508d9873` | `403e84fb6b0fe2ea2e584659e91c977ae6131da2ad588be985008ad030d98415` | `c1a69378030a8dff666f51c1fbf990d5dc22df4f3e55bdd773b99b1bb590ae35` | `52ab0f929df485712bebb9399f97ef16aae0b9253c1bd1e5dcf6093ff6155975` |

Both launches used engine-log SHA-256
`678be1a5c713f54beb463daf16a33bc57ef6b439cdf48326e1db931eb7842dc0`. Launch 2's app diagnostic has the
exact ten-event sequence and North final snapshot
`d8d6ac9edf2dd05e45be72dd1d0f2d01d849a8e051d2d59115b9ba9a7880792d`.

## 3. Small v3 rule

The repeated v1/v2 failures were caused by harmless runner-shell details, not game-context contamination.
v3 therefore removes the literal bootstrap-call sequence from TechnicalValid and keeps only three checks:

1. **Frozen identity and complete evidence** — build, fixture, prompt, variant, fresh process, trace,
   transcript and runner manifest agree.
2. **Game-context isolation** — from task start, the participant never reads repository/source/data, web,
   static cards, oracle/rubric, prior sessions or another app's contents.
3. **Actual target interaction** — after the first readable Gridworks state, all UI reads/actions use the
   frozen target through Computer Use, and the app diagnostic is the command/result authority.

Across the whole task, allowed game-information sources are only the exact message, designated skill and
current frozen Gridworks UI. Generic tool name/signature or availability, import/transport errors and
exact-target metadata are non-game diagnostics. Repository/source/data/logs, web, stored screenshots/static
cards, oracle/rubric, prior sessions, app inventory and other-app contents are forbidden. The first readable
Gridworks state starts scored interaction; no UI action may precede it, and every Gridworks read/action uses
the frozen target through Computer Use. A failed import, wrapper spelling error or generic tool-catalog lookup
does not by itself invalidate a slot.

This is a provenance-filter repair, not a looser game gate. The four field thresholds remain `4/5`, integrated
remains `3/5`, and `S0B-GATE-v1`, gameplay revision budget, app, fixture, UI and rubric are unchanged. v1 and
v2 remain immutable under their own stricter rules.

- `ContractVersion = S0B-CONTRACT-v3`
- `PromptVersion = S0B-PROXY-v3`
- `RunProtocolVersion = S0B-RUN-v3`
- unchanged: `S0B-BUILD-v1`, `S0B-FIXTURE-v1`, `S0B-GATE-v1`, runtime, data, scene, rubric, thresholds,
  revision budget and record columns
- sessions: `S0B-V3-L01`–`L05`, variants `AB/BA/AB/BA/AB`, five cold sessions, maximum two replacements for
  any `TechnicalValid=false` launch and seven official launches; working-app stop/timeout and app failure are
  TechnicalValid scored failures and are not replaced
- prior reviewed L00 remains applicable because build, AX target and action path did not change

## 4. v3 frozen hashes

- source-manifest build SHA-256:
  `69b658715a84b4099677b36c7d4fb458d65add59fcff8474865d95bf418e03bd`
- fixture SHA-256: `e617f7b9163294ca0e72f89bf3cb3a3be634c0de21f1d2736549863f53617e57`
- task-message template SHA-256: `34cd25113a33c8aeb3f1ce4e009582f5214db03df6a9d11b2ce8c526505b5391`
- facilitator-sheet SHA-256: `6e45b8b84faa44b23228e8b6967994b8ac200cbb893152ea51549d62b8ecc411`
- record-template SHA-256: `7d9e96313f3a2ba6189ef09267798890b2abd749a1bdb6373afe5d4c955104e1`

| Session | Variant | Task-message SHA-256 |
|---|---|---|
| `S0B-V3-L01` | `ab` | `69581e0052b589c027bcd24d512e9c13445221b884910ee76d27fb1c5695894f` |
| `S0B-V3-L02` | `ba` | `ad500409ac941f93f2d6b650870387816b6a665649976aa6996c7dfe24d68336` |
| `S0B-V3-L03` | `ab` | `9c147521442ff983b9bc00e0fec99538291b9ecf6f9177a829279fb746e5a30f` |
| `S0B-V3-L04` | `ba` | `db0879f57e0326e1e311cca8c0ff8d5c21348cc35d1403f8fc7a5f1029d33aec` |
| `S0B-V3-L05` | `ab` | `5e25f93cf46f49c27ca8c5cbe9e0c71b14b9f2abb6d037e35bac32ddec166e7e` |

## 5. Review checkpoint

- initial v3 protocol commit: `PENDING`
- bounded independent reviewer: `PENDING`
- review standard: skeptic; treat simple structure as the default and reject new runner frameworks
- official v3 sessions remain closed until review findings are fixed, all verifiers pass and a reviewed
  protocol commit is recorded here
