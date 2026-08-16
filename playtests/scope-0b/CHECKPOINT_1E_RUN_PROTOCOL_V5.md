# Scope 0B v4 protocol result and v5 reset checkpoint

> Status: **v4 `PROXY-RUN-BLOCKED`; v5 DRAFT — official sessions closed pending independent review**
>
> `SubGateDecision = PENDING`
>
> `Scope0State = 0B_ACTIVE`
>
> `HumanValidationStatus = NOT_COLLECTED`

## 1. Why v4 stops without a game decision

`S0B-RUN-v4` used five launches. All five reached native `FINAL`, but evidence-format compliance is part of
`TechnicalValid` and is locked before any gameplay field is scored.

- L01 and L03 preserve the full pre-screen `ALL_TOOLS` metadata response required by the frozen export.
- L02, L04 and L05 preserve the exact query, tool names, used signature, identifying text and error status but
  omit the remainder of the returned metadata descriptions.
- The facilitator sheet required the metadata/error `exact text`, while the active contract required a complete
  tool trace. The result review rejected a post-run interpretation that excerpts were sufficient.
- Three launches are therefore technically invalid. Only two replacements remain, so the highest attainable
  valid count is `4/5`. Continuing cannot reach the five-slot gate.

```text
EvidenceRound = S0B-RUN-v4
OfficialLaunches = 5/7
CompletedNativeUI = 5/5
TechnicalValid = 2/5
OfficialGameplayScore = NOT_COMPUTED
RoundStatus = PROXY-RUN-BLOCKED
BlockerClass = EVIDENCE_FORMAT
SubGateDecision = PENDING
RevisionBudgetRemaining = 1
```

This is not `GO`, `REVISE` or `NO-GO`, and it does not spend the gameplay revision budget. The five gameplay
answers are not scored, reinterpreted under v5 or combined with another round.

## 2. Frozen v4 evidence anchors

Raw evidence remains ignored under `playtests/scope-0b/private/`.

| Session | Prompt | Transcript | Tool trace | App diagnostic | Runner manifest | TechnicalValid |
|---|---|---|---|---|---|---|
| `L01` | `9bfc43ba6dfc76d32e8fd90cc291f0ea6dd8e691e716436ef52b9968c9c85e8d` | `2f5e0d261d26e7ded381a80437fa90d96f77c645fae552e7432f990aff36687d` | `dd5f9ce4447a5cd04a6d8082d5f1ee1c38ffeab6ff4f208a212a51388f851546` | `d6580608a0322c39faf9e73d85825cde6bb1b9df6d69b2aa888c644b1f35b5c5` | `77e40a77a7dc588ba354db25b7e692bd3a7ee61cdd748b1916ee97ad9e87c2b3` | `TRUE` |
| `L02` | `6916776a6b9d0fd376b52873e0388f2400f5f0349af9c0f55910f6b001d78b98` | `8b2cea6a4cbb00d1dcc3993f39a33e10b59f468e6751322c6088b61bcf022877` | `d5cea522419ede402660314a31ed47eb11154496bfff680cbb6001e59548a608` | `c9aedeb1ef8dd74e5959f9d9a6e670833a5fb1ac7a89e5563b17f16d16f335e8` | `a9a817e05f2502d97d9d51f5bbbdda21f8c4bf2230253f430ced2687b8141b07` | `FALSE` |
| `L03` | `60de2219aba2a8499483acfdb428d9d6afc25e46cb07f1edccd27191b3f3699e` | `b6e85e208bb4f257033c176edbbc9d646f0afbbc218a55258bc1b7b734e87aa8` | `cd38a1828c3d21941ca64ea70fac3d478eb259d672ba9f2c613314f3da4ef729` | `d57ec3c59f15993e04be3481ba96d8bf385829b7be434e1e560f8669d30c8d07` | `3a6dd2813490e635c13c37606f551dceef4a7d8027ae035e57a29080286cd72e` | `TRUE` |
| `L04` | `a90e90bbe69de2249a32e0a6c38055f5083bdb3be226c35921c74b2f25abde5b` | `bc125cbcad5085b20d969eba989a267874fbf33e4872fc4b9f651d247bf36888` | `111323885ca76a5209160b089e931dabcc9372c22bdd2fbb0aec5bdc6f39be53` | `238da01b7c1f554fd89e1f748d6cef53e38a0dbcdc0f5252a2322922185a37fa` | `8a25539ce2797d0e2c356071f1fb90a0691e0dccdd7d7e4d14ccb3bdcad19c8e` | `FALSE` |
| `L05` | `77f1585b2fe54f486035c5664c6b786ee90e4e6d1fde4934640a679db1fa2da5` | `126815aa1940dd7458a476ffd47bc263450476066363968a99d4bb424ac243ee` | `93d2cbf2fbdd93b36f1593157a555c48985c8f266f386571d3134d34f3a9ddb2` | `d636cb4e227bcbf742aeac225792d83701adf17007b43c4a035e2bf90689b00b` | `05c1d7f41b1737b595846b66ce8535260712bff65d75ea00c33c2b49af7d2981` | `FALSE` |

All five engine logs have SHA-256
`678be1a5c713f54beb463daf16a33bc57ef6b439cdf48326e1db931eb7842dc0`. Each diagnostic has the frozen ten
accepted events and final snapshot `d8d6ac9edf2dd05e45be72dd1d0f2d01d849a8e051d2d59115b9ba9a7880792d`.

## 3. Review record

- reviewed v4 authorization commit: `577e10b036bfb06c41c61aa0cb44a9b48593e7f8`
- initial, incorrect `GO` result commit: `fefdbd8`
- independent evidence review: `s0b_v4_evidence_audit`; `P0=1`
- initial result-commit review: `s0b_result_review`; `P0=1`
- contrary adjudication: `s0b_v4_validity_adjudicator`; rejected because the two independent result reviews
  applied the literal frozen exact-text evidence format
- strict gameplay scorer: `s0b_v4_strict_score`; retained only as unscored diagnostic because valid five slots
  were not established
- documentation/state review: `s0b_result_docs_audit`; terminal-state and stale-document findings resolved
- reviewed v4 closure commit: `e031aa70686d7b24035f971141a8586c5c2ab9de`

## 4. Small v5 delta

v5 changes only the post-run provenance format. It does not add another wrapper choreography.

1. The task message, build, fixture, UI, rubric, gate, model and AB/BA allocation are unchanged except for the
   new session IDs.
2. The post-run export is a compact chronological call/source ledger, not a byte-complete output dump.
3. Successful generic metadata records only returned callable names and the signature actually used.
4. Each metadata/error request and every returned error are exact; each UI action points to its following
   fresh-state read and short stage marker.
5. Unused tool descriptions, wrapper internals, full AX/screenshot text and every target-state response are
   explicitly neither retained nor required for `TechnicalValid`.
6. The report is a bounded participant attestation cross-checked against the prompt, app diagnostic, transcript
   and runner manifest. It is not independent proof that an unreported source was never accessed, so the final
   claim must remain limited to this same-model proxy.

- `ContractVersion = S0B-CONTRACT-v5`
- `PromptVersion = S0B-PROXY-v5`
- `RunProtocolVersion = S0B-RUN-v5`
- unchanged: `S0B-BUILD-v1`, `S0B-FIXTURE-v1`, `S0B-GATE-v1`, runtime, data, scene, field meanings,
  thresholds, replacement cap and revision budget
- sessions: `S0B-V5-L01`–`L05`, variants `AB/BA/AB/BA/AB`, five new cold sessions, at most two
  technical-invalid replacements and seven launches
- v1–v4 remain immutable under their own rules and are not combined with v5

## 5. v5 draft freeze hashes

- source-manifest build SHA-256:
  `69b658715a84b4099677b36c7d4fb458d65add59fcff8474865d95bf418e03bd`
- fixture SHA-256: `e617f7b9163294ca0e72f89bf3cb3a3be634c0de21f1d2736549863f53617e57`
- task-message template SHA-256: `75796ad89829418005a352833c556bd59f8e36f8d442cf2f7735e64fba5cdc74`
- facilitator-sheet SHA-256: `99457431f277e6d639d3874ad6fa90b172223389498a4c1cb9d9a45479ac5ea2`
- record-template SHA-256: `7d9e96313f3a2ba6189ef09267798890b2abd749a1bdb6373afe5d4c955104e1`

| Session | Variant | Task-message SHA-256 |
|---|---|---|
| `S0B-V5-L01` | `ab` | `80fd3418e078469b315376616bd31577cabc189644b6b1bdc4e74b3bb22f2fdd` |
| `S0B-V5-L02` | `ba` | `7dfb1b4ef594bb1ed9afb41ae8b655c3e5078a43cafd39cedb723d32e063bc35` |
| `S0B-V5-L03` | `ab` | `958c01bcb60eae69584356313ac01f71f650dfddd3d9b78142299f2120aeb8e7` |
| `S0B-V5-L04` | `ba` | `f730446480b86c01494bbd5f5cc887cb98c29f9c41ea4f1cb389cc0c62fc6af1` |
| `S0B-V5-L05` | `ab` | `f5690c059407ecf2a3108279775cefe8df341250d54517b607e3f40f48a89afa` |

## 6. v5 review checkpoint

- initial v5 protocol commit: `PENDING`
- bounded independent v5 reviewers: `PENDING`
- review standard: skeptic; simple structure is the default
- runtime, fixture, UI, rubric or gate change: `NONE`
- final review: `PENDING`
- reviewed v5 protocol commit: `PENDING`

Official v5 sessions remain closed until the hashes are exact, this checkpoint and the facilitator sheet both
say `REVIEWED`, a bounded independent review is recorded, and the authorization verifier passes. Scope 1
remains unopened.
