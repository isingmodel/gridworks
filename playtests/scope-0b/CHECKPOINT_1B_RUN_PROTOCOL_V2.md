# Scope 0B run-protocol reset checkpoint

> Status: **REVIEWED — official v2 sessions authorized**
>
> Superseded: current state and any future authorization are owned by
> [checkpoint 1F](CHECKPOINT_1F_RUN_PROTOCOL_V6.md). This historical banner no longer authorizes a launch.
>
> `RoundStatus = PROXY-RUN-BLOCKED` for `S0B-RUN-v1`
>
> `SubGateDecision = PENDING`
>
> `Scope0State = 0B_ACTIVE`
>
> `HumanValidationStatus = NOT_COLLECTED`

## 1. Why v1 is not a game result

The frozen v1 facilitator allowed exactly one Computer Use skill read before measurement. Every completed
official participant also queried the environment tool catalog to discover the deferred Node REPL callable.
That query exposed no game, fixture, oracle, rubric or earlier-session content, and all five participants then
reached `FINAL` through the real native UI. It nevertheless violated the exact bootstrap contract and made the
runner manifests' `skill-read-then-node_repl+@oai/sky-only` assertion false.

The first L01 launch was an independently evidenced `runner_error:runner`; its replacement and L02–L05 made
six official launches. The five completed sessions are all technically invalid under the frozen v1 rule, so
one remaining launch cannot produce five valid slots. The active gate therefore requires:

```text
EvidenceRound = S0B-RUN-v1
RoundStatus = PROXY-RUN-BLOCKED
BlockerClass = RUN_PROTOCOL
TechnicalValid = 0/5
SubGateDecision = PENDING
RevisionBudgetRemaining = 1
```

This is not `GO`, `REVISE` or `NO-GO`. The positive answers cannot be used as a gate aggregate, the v1
manifest flags are not rewritten after the fact, and v1 is never combined with v2.

## 2. Frozen v1 evidence anchors

Raw evidence remains ignored under `playtests/scope-0b/private/`. The aggregate file SHA-256 is
`301f775789991dad12e75a20d19a28222f55be307e11c21317ad64acf23bde5a`; its five `TechnicalValid=TRUE`
values are superseded by the strict evidence audit above and must not be used.

| Launch | Tool trace SHA-256 | Runner manifest SHA-256 | Transcript SHA-256 | App diagnostic SHA-256 |
|---|---|---|---|---|
| L01 first, runner replacement | `83582e43ea6180fbe53e9046eab599fbdce07933e4d3fc254f6a236275a28098` | `1485ee1b5b4e1136e958aebde3ea9fdc0b1cd96bccc10ffda1b4fa434eee3a81` | `e707188fa950bf8d141d01c848fcf9bdd247dd135633fea076708825e7598648` | `c9e694e115e80c93cf3dc76969f1a494352a1b0828add797700eaf6be7c4585d` |
| `S0B-L01` | `905dfa0a643db5235ac570418174512ea3b08c3fb67b6e2f13e71684d6321b2a` | `c2a8a51e75c7964fa93885ffb68097eb922f9cfc829120a901d9293d09f2be77` | `bcc98249d9e212548eebca91230edbe69a40cfafeb913217d225b90f9c81fe0a` | `57272284bcbb619731f944c478d2e8de69b2dd3f12e37f2f3ded4d842ee4c41a` |
| `S0B-L02` | `3539ce27046d594da36a8577e58e6db6bfdc192338a9b52090ef40d86ead13a3` | `b4e767a1bc769028802caa325cf0635388e84c78fb6a1a173b985329312e472b` | `552af38331c3e924a540a2b1d3b1095cf149c5630647ab2be06697c535105fae` | `3c4107015c595cb2944ce09d9bb796143814b5ecc67688c2c448e7e5cbcf3bbb` |
| `S0B-L03` | `eedeb148243b6bd7fccbee91ff9a1c5bbdad609cc1966d4bfce8d59637af9a13` | `b78189e3a8612ad368d82397e0fa60e29d9b9de8d26e6713d7de08bed0cdc226` | `78ceaff2e4e9b6f3f382f90d11ba6515fbb6011ccee3f16f266fb84f1488dca0` | `0652c1652b59dc1db348dbb0a8fdbf731fa36f59a6921074483445a526d961cb` |
| `S0B-L04` | `22dc29a4f4f4128955e3918e406eb979001c7f4b0a4ab6f72b703a05794a4318` | `1c8a1251f64844fad09bf48e3410a8d8b4f72dba92c0603fdb45d4caaca161e6` | `954456d36fe1baba0aeda97ff17562851aa0936aeb173e8eaeeb1c7689953d00` | `b9e3a4ad3fa5ce564c7030918ca151ddafc7213f8b29c3de41591a6ac51e38dc` |
| `S0B-L05` | `2b1129707ea1109a6221f4beab508233a48b6868a5cf1051ca0a32965e6f0f30` | `db5cada9efe2203844164c1b11aef12ee0ae8720b3fa44ed449aeca8790e6e57` | `3e1d56ff32ae84e00d73f7154bcc42e95f02d75da8bd3a9c39d8447da04a6bc0` | `413c08347ae1eea644085e461f50f7875acba9ddc1b5936616fe1f4ef18532db` |

The shared Godot engine-log SHA-256 is
`678be1a5c713f54beb463daf16a33bc57ef6b439cdf48326e1db931eb7842dc0`.

Unscored observations are deliberately narrow: all five completed the app, locked the four expected utility
outcomes, explained the three concepts, and chose North. Four described the two-choice affordance as switch-
like rather than radio-like. These observations do not prove human usability, branch choice balance or a gate
pass, and they do not justify changing the frozen UI before v2.

## 3. Small v2 protocol repair

Only the task-message transport boundary changes.

- `ContractVersion = S0B-CONTRACT-v2`
- `PromptVersion = S0B-PROXY-v2`
- `RunProtocolVersion = S0B-RUN-v2`
- unchanged: `S0B-BUILD-v1`, `S0B-FIXTURE-v1`, `S0B-GATE-v1`, runtime, UI, fixture, rubric, thresholds,
  revision budget and record columns
- direct callable supplied in the exact message: `tools.mcp__node_repl__js`
- direct app target supplied in the exact message: `org.godotengine.godot`
- before measurement: the designated skill read once and nothing else
- measurement: first direct-wrapper call through scored final-report submission; final app-state read is only
  the UI-interaction end, with no catalog/app discovery fallback
- after measurement: participant returns one tool-free retained-history Markdown export; coordinator stores it
  at the launch-specific private path
- logical session ID remains fixed; launch-specific evidence IDs prevent replacement log collisions
- sessions: `S0B-V2-L01`–`L05`, variants `AB/BA/AB/BA/AB`, five cold sessions, maximum two independently
  evidenced runner replacements and seven official launches for v2

The reviewed native L00 remains applicable because the executable build, UI, AX target and action path are
unchanged. Independent review must call the supplied wrapper directly without catalog discovery before v2 is
authorized. Failure of that exact callable closes the attempt as `runner_error:runner`; it does not permit a
fallback search.

## 4. v2 frozen hashes

- source-manifest build SHA-256:
  `69b658715a84b4099677b36c7d4fb458d65add59fcff8474865d95bf418e03bd`
- fixture SHA-256: `e617f7b9163294ca0e72f89bf3cb3a3be634c0de21f1d2736549863f53617e57`
- task-message template SHA-256: `241559a5e16598e9614e2031127751dde0e83ff0fb3ed935835c232188d2ea11`
- facilitator-sheet SHA-256: `ac0e3c9b6e27016b8345e6ef3ee42b6528d9c79bc0a36a0c26a521b1d004c060`
- record-template SHA-256: `7d9e96313f3a2ba6189ef09267798890b2abd749a1bdb6373afe5d4c955104e1`

| Session | Variant | Task-message SHA-256 |
|---|---|---|
| `S0B-V2-L01` | `ab` | `d988b6334c8836178247ff1145eb17525b07dfb9644897e2666dbd4208fd770a` |
| `S0B-V2-L02` | `ba` | `89d024adce0cd501b058e8f60e6316e88923c2ea60931d6f4761ff4e0d71cdae` |
| `S0B-V2-L03` | `ab` | `f0615ce5164361242374df5d8423dff523dc01dfd095cf2cca6c03b618ac9084` |
| `S0B-V2-L04` | `ba` | `c80e559835c83c11892ce5349a0fd0e8ed4407b29b4a74c726232d03b6614ba0` |
| `S0B-V2-L05` | `ab` | `4e6d25b19987bd60748e2f810df2585e54592d16439a3ff9945ef0363d809730` |

## 5. Repository checkpoint

- initial protocol-reset commit: `b45e1f951f6f3dd089ede9f5a08a3d838eecb499`
- bounded independent reviewers: `s0b_v2_freeze_review`, `s0b_v2_execution_adversary`
- accepted fixes: one v1 app-log hash digit; stale active-state wording; fresh-state boundary; launch-specific
  `evidenceId`; exact tool-free retained-history export; tool policy through the scored final report
- rejected expansion: no new runner framework, gameplay/UI change, fixture change or gate change
- direct-wrapper check: `PASS` — direct `tools.mcp__node_repl__js` call returned `NODE_REPL_CALLABLE`
- final review result: `P0=0, P1=0, P2=0`; contract, implementation and R2 regression verifiers plus
  `git diff --check` passed
- reviewed protocol commit: `e724d6e6ad4d99e38adda2bf86c3c5cf7fb7364f`
- official v2 sessions may run sequentially on the unchanged build. No v2 session had started when this
  review closed.
