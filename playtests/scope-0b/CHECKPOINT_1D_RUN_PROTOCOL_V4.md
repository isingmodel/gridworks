# Scope 0B v3 protocol result and v4 reset checkpoint

> Status: **REVIEWED — official v4 sessions authorized**
>
> Superseded: v4 and v5 later closed `PROXY-RUN-BLOCKED`; current state and any future authorization are owned by
> [checkpoint 1F](CHECKPOINT_1F_RUN_PROTOCOL_V6.md). This historical banner no longer authorizes a launch.
>
> `RoundStatus = PROXY-RUN-BLOCKED` for `S0B-RUN-v3`
>
> `SubGateDecision = PENDING`
>
> `Scope0State = 0B_ACTIVE`
>
> `HumanValidationStatus = NOT_COLLECTED`

## 1. Why v3 stops without a game decision

`S0B-RUN-v3` used five launches. All five reached the native `FINAL` state, but prompt identity is part of
TechnicalValid and is locked before any gameplay field is scored.

- L01 and L02 received the frozen participant message and are technically valid.
- L03–L05 received an added absolute skill path and different wrapping. The path was setup information, not
  a game hint, but each actual task-message hash differs from the frozen hash recorded in its manifest.
- Three launches are therefore invalid. Only two replacements remain, so the highest attainable valid count
  is `4/5`. Continuing cannot reach the five-slot gate.

```text
EvidenceRound = S0B-RUN-v3
OfficialLaunches = 5/7
CompletedNativeUI = 5/5
TechnicalValid = 2/5
OfficialGameplayScore = NOT_COMPUTED
RoundStatus = PROXY-RUN-BLOCKED
BlockerClass = RUN_PROTOCOL
SubGateDecision = PENDING
RevisionBudgetRemaining = 1
```

This is not `GO`, `REVISE` or `NO-GO`, and it does not spend the gameplay revision budget. v3 responses are
not scored, reinterpreted under v4 or combined with another round.

## 2. Frozen v3 evidence anchors

Raw evidence remains ignored under `playtests/scope-0b/private/`. The actual task-message hash is computed from
the fenced task turn in each transcript. The frozen hash is the v3 checkpoint value.

| Evidence ID | Frozen prompt | Actual prompt | Tool trace | Runner manifest | Transcript | App diagnostic |
|---|---|---|---|---|---|---|
| `S0B-V3-L01-launch1` | `69581e0052b589c027bcd24d512e9c13445221b884910ee76d27fb1c5695894f` | same | `df77abf6c7f9e7cb6483cd915a3555505d681b95482615670e72da7142a94a41` | `12a82dc734b0ef475090ba1c720c862a9289295e3da06ef9dd1cb58a390c1579` | `8c3fcd5869d506035005fc64bc4190046ef9e8a705f9a4acff258478df79be87` | `29a2e68d99d157f39bfeb63fce774c7cab902054f1f01128d030ece4f8d5e1d1` |
| `S0B-V3-L02-launch1` | `ad500409ac941f93f2d6b650870387816b6a665649976aa6996c7dfe24d68336` | same | `9a18f0462eb9d556c64ac94e47a2a6c79c069b0da7860746ca32fd2efb0bf7eb` | `4168e641cbd6e67a58fb9e68e05b7080ac1ca2a0b82acb6d0791bcb1ad6d84f4` | `15d42cf05210820622f6679be4d1f8576723e2259a98ec2edd93cdebbbec247e` | `bcca417a332670baab5269f21957ed7eb3e1feb5a7e67f34cc94d5f7a9756971` |
| `S0B-V3-L03-launch1` | `9c147521442ff983b9bc00e0fec99538291b9ecf6f9177a829279fb746e5a30f` | `747162e5793440325a1c2883d8b2e7c010f9e2984bc5f5bbb1a8662f914ec069` | `73c92db9c706c81d907d3fd48063ded0342634c29460a10af6ea1c0af27a018f` | `804dcee6ecad19d0c91d718c2997651a1cf6c66143fa4c829843c526eede6bc9` | `7e77b9b26154e1ae78ed6ccc312c9450031dba15d0063320098aa3640f0feafd` | `19f547ef1b88c4b06b8d62d1fd8ba84478b32952c912b0ffe0ab0838ccb001a4` |
| `S0B-V3-L04-launch1` | `db0879f57e0326e1e311cca8c0ff8d5c21348cc35d1403f8fc7a5f1029d33aec` | `18db41d2d63c4d69bda8aa7756d922163dcca86840db16f06618aeb527ebf125` | `a7b9695d72de1ba7ceae9ed569481942e0bc024ffd32d96db3f09094e7dd3397` | `0ddfbb1c2ae45aa86e7c18686482940c03db7e652a4e1b019a047bef428b5ec2` | `28cc7560305eebe9698f56ae971c358a2ef717417178cee5c72bc663ad8a0da1` | `d8403a30443a3eec105814b612e5965260ba3ac7ffbefb55935938145439924a` |
| `S0B-V3-L05-launch1` | `5e25f93cf46f49c27ca8c5cbe9e0c71b14b9f2abb6d037e35bac32ddec166e7e` | `c908bd3e3fab18d4013c2eb3936ec19188938b65c7b9f733884a146c51bdb0f7` | `0bfe42f9a410317e80c9e3d0d30e71d9b2254308c5b70b21aa89227264b2c0e7` | `f8c465deea548607bc1d594958f1f69f55ce53e6dc1e3c5a60e9431cfe158639` | `b6eac087cbe7b05634866733e258c32a3269fb719e227fe2deb7e5b8a924bb5f` | `9f781718e2242fae3151ef1bf268efd57c67d417a48d18edd36a2056dd40bd16` |

All five engine logs have SHA-256
`678be1a5c713f54beb463daf16a33bc57ef6b439cdf48326e1db931eb7842dc0`. Each diagnostic has the exact
ten-event completion sequence and final snapshot
`d8d6ac9edf2dd05e45be72dd1d0f2d01d849a8e051d2d59115b9ba9a7880792d`.

## 3. Small v4 delta

v4 changes only prompt delivery and identity checking.

1. The facilitator sheet owns the only canonical participant-message block; the active contract links it.
2. The skill locator is the stable registered name `computer-use:computer-use`, not a cache path.
3. `PromptHash` uses `ascii-whitespace-fold-v1`: valid UTF-8, collapse runs of ASCII space/tab/CR/LF to one
   ASCII space, strip, then SHA-256. ASCII whitespace layout may vary; any changed non-whitespace text fails.
4. The existing implementation verifier renders the message and checks the captured transcript. The
   coordinator sends rendered output without editing and verifies each transcript before locking validity.

No general runner framework or gameplay change is added.

- `ContractVersion = S0B-CONTRACT-v4`
- `PromptVersion = S0B-PROXY-v4`
- `RunProtocolVersion = S0B-RUN-v4`
- unchanged: `S0B-BUILD-v1`, `S0B-FIXTURE-v1`, `S0B-GATE-v1`, runtime, data, scene, field meanings,
  thresholds, revision budget and record columns
- sessions: `S0B-V4-L01`–`L05`, variants `AB/BA/AB/BA/AB`, five new cold sessions, at most two
  technical-invalid replacements and seven launches
- v1, v2 and v3 remain immutable under their own rules and are not combined with v4
- reviewed L00 remains applicable because build, target and action path did not change

## 4. v4 frozen hashes

- source-manifest build SHA-256:
  `69b658715a84b4099677b36c7d4fb458d65add59fcff8474865d95bf418e03bd`
- fixture SHA-256: `e617f7b9163294ca0e72f89bf3cb3a3be634c0de21f1d2736549863f53617e57`
- task-message template SHA-256: `75796ad89829418005a352833c556bd59f8e36f8d442cf2f7735e64fba5cdc74`
- facilitator-sheet SHA-256: `2fa1f53c95c6fab06f6e083857be07333d1f7cfa32bc0b6f4f747931c666beff`
- record-template SHA-256: `7d9e96313f3a2ba6189ef09267798890b2abd749a1bdb6373afe5d4c955104e1`

| Session | Variant | Task-message SHA-256 |
|---|---|---|
| `S0B-V4-L01` | `ab` | `9bfc43ba6dfc76d32e8fd90cc291f0ea6dd8e691e716436ef52b9968c9c85e8d` |
| `S0B-V4-L02` | `ba` | `6916776a6b9d0fd376b52873e0388f2400f5f0349af9c0f55910f6b001d78b98` |
| `S0B-V4-L03` | `ab` | `60de2219aba2a8499483acfdb428d9d6afc25e46cb07f1edccd27191b3f3699e` |
| `S0B-V4-L04` | `ba` | `a90e90bbe69de2249a32e0a6c38055f5083bdb3be226c35921c74b2f25abde5b` |
| `S0B-V4-L05` | `ab` | `77f1585b2fe54f486035c5664c6b786ee90e4e6d1fde4934640a679db1fa2da5` |

## 5. Review checkpoint

- v3 source commit: `82895f458a985b430eb99047c0c5115c0eee0eb6`
- v3 evidence auditor: `s0b_v3_evidence_audit`; strict result `TechnicalValid = 2/5`, `P0=1`
- initial v4 reset commit: `968e7bf81bf7664ef0e539b0424dbaf87312aa3e`
- bounded independent v4 reviewers: `s0b_v4_freeze_review`, `s0b_v4_docs_review`
- review standard: skeptic; simple structure is the default
- accepted fixes: remove every aggregate of technically invalid gameplay answers; describe normalization as
  ASCII-whitespace layout rather than line wrapping; define the captured transcript fence; move authorization
  checking after all invariant checks and bind it to exact top-level status; label implementation-freeze prompt
  hashes as historical v1 evidence
- rejected expansion: no new runner framework, runtime/UI/fixture/rubric/gate change or retrospective v3 score
- final review: `P0=0, P1=0, P2=0`
- reviewed protocol commit: `c3c5f6d401f279e34737835182e9a496119cba38`
