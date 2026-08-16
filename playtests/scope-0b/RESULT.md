# Scope 0B native UI proxy result

> Decision: **`S0B-GATE-v1 = GO`**
>
> Workflow: `Scope0State = REVIEWED`
>
> Human evidence: `HumanValidationStatus = NOT_COLLECTED`

## Frozen run

- reviewed authorization commit: `577e10b036bfb06c41c61aa0cb44a9b48593e7f8`
- versions: `S0B-CONTRACT-v4`, `S0B-BUILD-v1`, `S0B-FIXTURE-v1`, `S0B-PROXY-v4`,
  `S0B-RUN-v4`, `S0B-GATE-v1`
- allocation: `L01 AB`, `L02 BA`, `L03 AB`, `L04 BA`, `L05 AB`
- model configuration: `gpt-5.6-sol`, medium reasoning, `fork_turns=none`
- official launches: `5 / 7`; replacements: `0 / 2`
- technically valid sessions: `5 / 5`
- source-manifest build SHA-256:
  `69b658715a84b4099677b36c7d4fb458d65add59fcff8474865d95bf418e03bd`
- fixture SHA-256: `e617f7b9163294ca0e72f89bf3cb3a3be634c0de21f1d2736549863f53617e57`
- frozen record CSV SHA-256: `8fca25f1ad1156431b3a6f664e069813095e72f7904ac76c7e55e77c209309e5`
- raw files: local-only under `playtests/scope-0b/private/`; not committed

The five launches used distinct native processes and non-overlapping monotonic intervals. Every diagnostic has
the frozen ten accepted events from `READY` through `FINAL`, the assigned AB/BA variant and the same reviewed
build and fixture hashes. Every UI action in the retained histories is followed by a fresh target-state read.

## Private evidence anchors

| Session | Prompt | Transcript | Tool trace | App diagnostic | Runner manifest |
|---|---|---|---|---|---|
| `L01` | `9bfc43ba6dfc76d32e8fd90cc291f0ea6dd8e691e716436ef52b9968c9c85e8d` | `2f5e0d261d26e7ded381a80437fa90d96f77c645fae552e7432f990aff36687d` | `dd5f9ce4447a5cd04a6d8082d5f1ee1c38ffeab6ff4f208a212a51388f851546` | `d6580608a0322c39faf9e73d85825cde6bb1b9df6d69b2aa888c644b1f35b5c5` | `77e40a77a7dc588ba354db25b7e692bd3a7ee61cdd748b1916ee97ad9e87c2b3` |
| `L02` | `6916776a6b9d0fd376b52873e0388f2400f5f0349af9c0f55910f6b001d78b98` | `8b2cea6a4cbb00d1dcc3993f39a33e10b59f468e6751322c6088b61bcf022877` | `d5cea522419ede402660314a31ed47eb11154496bfff680cbb6001e59548a608` | `c9aedeb1ef8dd74e5959f9d9a6e670833a5fb1ac7a89e5563b17f16d16f335e8` | `a9a817e05f2502d97d9d51f5bbbdda21f8c4bf2230253f430ced2687b8141b07` |
| `L03` | `60de2219aba2a8499483acfdb428d9d6afc25e46cb07f1edccd27191b3f3699e` | `b6e85e208bb4f257033c176edbbc9d646f0afbbc218a55258bc1b7b734e87aa8` | `cd38a1828c3d21941ca64ea70fac3d478eb259d672ba9f2c613314f3da4ef729` | `d57ec3c59f15993e04be3481ba96d8bf385829b7be434e1e560f8669d30c8d07` | `3a6dd2813490e635c13c37606f551dceef4a7d8027ae035e57a29080286cd72e` |
| `L04` | `a90e90bbe69de2249a32e0a6c38055f5083bdb3be226c35921c74b2f25abde5b` | `bc125cbcad5085b20d969eba989a267874fbf33e4872fc4b9f651d247bf36888` | `111323885ca76a5209160b089e931dabcc9372c22bdd2fbb0aec5bdc6f39be53` | `238da01b7c1f554fd89e1f748d6cef53e38a0dbcdc0f5252a2322922185a37fa` | `8a25539ce2797d0e2c356071f1fb90a0691e0dccdd7d7e4d14ccb3bdcad19c8e` |
| `L05` | `77f1585b2fe54f486035c5664c6b786ee90e4e6d1fde4934640a679db1fa2da5` | `126815aa1940dd7458a476ffd47bc263450476066363968a99d4bb424ac243ee` | `93d2cbf2fbdd93b36f1593157a555c48985c8f266f386571d3134d34f3a9ddb2` | `d636cb4e227bcbf742aeac225792d83701adf17007b43c4a035e2bf90689b00b` | `05c1d7f41b1737b595846b66ce8535260712bff65d75ea00c33c2b49af7d2981` |

All five engine logs have SHA-256
`678be1a5c713f54beb463daf16a33bc57ef6b439cdf48326e1db931eb7842dc0`.

## Aggregate

| Measure | Result | Gate |
|---|---:|---:|
| Interaction completion | `5 / 5` | `>= 4 / 5` |
| Coverage action and explanation | `5 / 5` | `>= 4 / 5` |
| Pre-reveal risk prediction | `5 / 5` | `>= 4 / 5` |
| Utility/internal-power boundary | `5 / 5` | `>= 4 / 5` |
| Integrated interaction | `5 / 5` | `>= 3 / 5` |
| Coverage conclusion | `5 / 5` | revision diagnostic `>= 4 / 5` |
| Risk conclusion | `5 / 5` | revision diagnostic `>= 4 / 5` |
| Utility conclusion | `5 / 5` | revision diagnostic `>= 4 / 5` |

The diagnostic authority shows that all five locked the exact pre-reveal cells: River/E1 `remains`,
River/OLD `cut`, North/E1 `remains`, North/OLD `remains`. All five then completed feeder order, corridor order,
every milestone and `FINAL` without facilitator help. Every final report distinguished service eligibility from
an energized path to an online generator and hospital-owned P0 backup from utility delivery and sales.

## Evidence-review decision

One skeptical audit read the export phrase “metadata/error exact text” as requiring the entire verbose
`ALL_TOOLS` response byte-for-byte and would have invalidated L02, L04 and L05. A separate bounded adjudication
rejected that interpretation: the frozen sheet asks for every call, source, request or action, result type,
fresh-state boundary and exact metadata/error text, but never says `full response` or `verbatim` for the whole
metadata blob. Those three traces preserve the exact query, all returned tool names, the used callable's exact
signature and identifying text, status, content type and absence of errors. Missing descriptions belong only to
unused generic tools and cannot hide a game-information source. Requiring every metadata byte would add a new
criterion after the run, so the five launches remain technically valid.

This interpretation is limited to the frozen v4 wording. It does not change v1–v3 or repair their invalid
launches, and it is made before gameplay quality is used.

## Decision and limits

`S0B-GATE-v1 = GO`.

The result supports one narrow claim: on this authored fixture and reviewed native build, five cold runs of
the same LLM completed the actual UI and consistently applied the three frozen distinctions. It does not prove
human usability, fun, spontaneous discovery, accessibility, general power-grid understanding or free-line
construction quality. `HumanValidationStatus` remains `NOT_COLLECTED`.

All five selected the northern detour. Selection ratio is diagnostic, not a balance target, and no participant
executed the River branch's UPS-to-diesel chronology. All five also described the prediction pairs as separate
switches rather than a clear one-of-two radio group. The ambiguity did not block completion and is retained as
one bounded presentation observation; the passed build is not patched or rerun for it.

Scope 0 is complete after its result checkpoint is independently reviewed. This `GO` does not authorize Scope
1 implementation or the Static Balance Lab. The next unit must first review the remaining risks and write only
the selected gate's contract boundary.
