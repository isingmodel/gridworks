# Commercial UX evaluation tools

This directory keeps the formative `TEXT-PLAN` lane separate from the score-bearing native
lane. `TextPlanProxy` never becomes `CommercialUXProxy`; only a complete, provenance-valid
native evaluation may produce the latter.

## Build the blinded input

First emit the strict 26-part story manifest from the campaign harness, then bind it to the
same campaign authority while building the blinded artifact:

```sh
dotnet run --project tools/Gridworks.CommercialChecks/Gridworks.CommercialChecks.csproj \
  -c Release -- --story-manifest > /tmp/gridworks-story-manifest.json
python3 tools/commercial-ux/build-text-plan-input.py \
  --story-manifest /tmp/gridworks-story-manifest.json \
  --campaign data/release-campaign-v2.json \
  --output /tmp/gridworks-text-plan.json
```

The builder rejects any campaign ID, chapter order, selector metadata, or
`speaker`/`title`/`body` value that differs from campaign authority. The output SHA covers
canonical parsed artifact JSON.

## Run and aggregate the judges

Render `text-plan-judge-prompt.template.txt` separately for three fresh
`gpt-5.6-sol`/`ultra` calls, replacing all placeholders and supplying the built artifact.
Use `text-plan-judge.schema.json` as the strict output schema. The two contract hashes are
SHA-256 of the exact raw checked-in prompt-template and schema bytes; the template is
hashed while its hash placeholders are still present.

Aggregate exactly those three judgments:

```sh
python3 tools/commercial-ux/aggregate-text-plan.py \
  /tmp/judge-01.json /tmp/judge-02.json /tmp/judge-03.json \
  --text-plan /tmp/gridworks-text-plan.json \
  --output /tmp/text-plan-initial.json
```

If and only if the initial status is `RERUN_REQUIRED_JUDGE_INSTABILITY`, replace the full
panel with three fresh, disjoint judge run IDs and link the initial aggregate:

```sh
python3 tools/commercial-ux/aggregate-text-plan.py \
  /tmp/judge-rerun-01.json /tmp/judge-rerun-02.json /tmp/judge-rerun-03.json \
  --text-plan /tmp/gridworks-text-plan.json \
  --replacement-for /tmp/text-plan-initial.json \
  --output /tmp/text-plan-replacement.json
```

The replacement must match the initial text-plan, rubric, prompt, and schema hashes. A
second unstable panel becomes `BLOCKED_JUDGE_INSTABILITY`; a stable replacement is scored
and records the exact initial `panelInputSha256` it replaced.

Replacement is single-use and fail-closed. Before writing replacement output, the
initial aggregate embeds one absolute, canonical, content-addressed receipt path. Its
parent is the resolved parent of the original initial output and its filename is derived
from the exact initial `panelInputSha256`:

```text
<original-output-parent>/.gridworks-commercial-ux-replacement-<panel-sha256-hex>.receipt.json
```

Every unchanged copy or rename of that initial carries the same embedded path, so all
replacement attempts contend on one receipt even across directories. The receipt includes
its own canonical path and the exact initial path used for the successful claim, and binds
the initial/replacement panel hashes, three fresh run IDs, and text-plan/rubric/prompt/schema
hashes. `replacementReceiptSha256` is null for an initial aggregate and is the SHA-256 of
the exact compact receipt bytes (including its trailing newline) for a replacement. An
existing or concurrently claimed receipt rejects reuse.

Every aggregate output must be a fresh path and is created with exclusive-create
semantics, so rerunning into an existing initial or replacement output cannot overwrite
prior evidence. A replacement additionally rejects existing paths and inode aliases of
the initial before claiming the receipt. Any output race or I/O failure after that claim
deliberately leaves the receipt consumed. The initial aggregate is never modified.

Run deterministic checks with:

```sh
python3 tools/commercial-ux/test-text-plan-tools.py
```

## Verify the cited evidence blind

Before using any formative conclusion, strip the three judgments down to deduplicated,
label-blind observations:

```sh
python3 tools/commercial-ux/prepare-text-plan-evidence.py \
  /tmp/judge-01.json /tmp/judge-02.json /tmp/judge-03.json \
  --text-plan /tmp/gridworks-text-plan.json \
  --aggregate /tmp/text-plan-initial.json \
  --output /tmp/text-plan-evidence-input.json
```

The aggregate must be the exact `SCORED_FORMATIVE` output for those same three judgment
files in the same argument order. If instability required a replacement, pass the three
replacement judgments and the scored replacement aggregate instead. The preparer
calls the aggregator's side-effect-free shared verifier, which reruns the production
label-to-cell-to-category calculation and exact-compares the panel hash, status, every cell
and category score, raw score, spread, penalty, proxy, linkage, and receipt provenance. It
never claims a receipt. The preparer then exposes only the opaque
`judgePanelInputSha256` to the verifier.

Render `text-plan-evidence-verifier-prompt.template.txt` once for a fresh
`gpt-5.6-sol`/`ultra` verifier and require
`text-plan-evidence-verifier.schema.json`. The verifier sees the artifact and anonymous
observations, but no label, cell, polarity, score, threshold, or change request. Validate
its JSON with:

```sh
python3 tools/commercial-ux/aggregate-text-plan-evidence.py \
  --input /tmp/text-plan-evidence-input.json \
  --verification /tmp/text-plan-evidence-verifier.json \
  --output /tmp/text-plan-evidence-result.json
```

Only `VERIFIED_SUPPORTED_ONLY` permits the panel's observations to support formative
conclusions. Any missing, partial, unsupported, duplicated, or provenance-invalid row
returns `BLOCKED_EVIDENCE_VERIFICATION` instead of silently dropping adverse evidence.

```sh
python3 tools/commercial-ux/test-text-plan-evidence-verifier.py
```

## Native evaluator v1.1 pre-capture contract

The native contract is defined by
[`COMMERCIAL_UX_NATIVE_EVALUATOR_ADDENDUM_KO.md`](../../docs/product/COMMERCIAL_UX_NATIVE_EVALUATOR_ADDENDUM_KO.md)
and the authorities under [`native/`](native/). It fixes `gpt-5.6-sol` with `ultra`
reasoning, 20 qualification anchors, three independent cold actors plus one coverage
envelope, three blind judges receiving the same complete evidence set, a label-blind
verifier, a deterministic oracle, `FORMATIVE-01`, and eight ordered holdouts. The checked-in
hash policy and stage DAG bind prompts, schemas, recipes, receipts, evidence, retries,
replacement, and aggregation without changing the v1 rubric or formulas.

The earlier Gate C native smoke is deterministic developer evidence only. It predates this
score-bearing contract and must not be repackaged as a cold actor, coverage, or judge input.

Validate the candidate-independent contract and current gold-state declarations with:

```sh
python3 tools/commercial-ux/native/validate-contract.py
python3 tools/commercial-ux/native/test-contract.py
python3 tools/commercial-ux/native/test-gold-state.py
python3 tools/commercial-ux/native/test-session-claim.py
python3 tools/commercial-ux/native/validate-gold-state.py --run-story-manifest
python3 tools/commercial-ux/test-native-aggregate.py
python3 -m py_compile \
  tools/commercial-ux/aggregate-native.py \
  tools/commercial-ux/test-native-aggregate.py \
  tools/commercial-ux/native/claim-evaluation-session.py \
  tools/commercial-ux/native/validate-contract.py \
  tools/commercial-ux/native/validate-gold-state.py
```

The current deterministic checks pass 36 schema validations, 53 contract scenarios, 25
gold-state tests, 16 evaluation-session checks, and 78 aggregation tests. `CommercialChecks`
passes 24 suites / 2,910
assertions, and the Debug and Release rebuilds each finish with zero warnings and errors.
Those results prove the pre-capture contract and product regressions, not score readiness or
game quality.

Gold replay builds are source-hermetic at the repository boundary. The contract-bound
[`native/gold-replay-build-inputs.json`](native/gold-replay-build-inputs.json) allowlists the
pinned SDK file, two projects, verifier entrypoint/source, and 19 Commercial Core V2 sources.
Only those 24 exact path/role inputs are copied to a private temporary source tree. Evaluator
authority freezes the four SDK/verifier byte streams; the candidate manifest binds the observed
20 Core project/source byte streams through an ordered canonical projection. Directory build
props/targets/package props, user NuGet sources, ambient MSBuild properties, implicit checkout
sources, and checkout `bin/obj` are excluded before restore and build.
This closes only the gold replay verifier build. Candidate game-build inputs and the complete
runtime resource tree remain a separate `CANDIDATE-MANIFEST-PACKAGER` gate.

Immediately before any score-bearing capture, also run:

```sh
python3 tools/commercial-ux/native/validate-gold-state.py \
  --run-story-manifest \
  --require-score-ready
```

This command is expected to fail closed in the current pre-capture state. The gold-state
manifest still has 52 pending native-replay owners and four unbound E09 witnesses. In
addition, the following 17 deterministic producer stages are not implemented and bound by
their exact raw tool SHA yet:

- `CANDIDATE-MANIFEST-PACKAGER`
- `HOLDOUT-CONSUMPTION-PACKAGER`
- `GOLD-BINDING-PACKAGER`
- `COLD-OBSERVATION-PACKAGER`
- `COLD-PACKAGER`
- `QUALIFICATION-INPUT-PACKAGER`
- `QUALIFICATION-RECEIPT-PACKAGER`
- `COVERAGE-ACTION-LEDGER-PACKAGER`
- `COVERAGE-RUN-PACKAGER`
- `ANONYMIZATION-PACKAGER`
- `EVIDENCE-SET-PACKAGER`
- `CANDIDATE-JUDGE-INPUT-PACKAGER`
- `JUDGE-PANEL-PACKAGER`
- `VERIFICATION-INPUT-PACKAGER`
- `ORACLE-HARD-GATES`
- `EVALUATION-RUN-PACKAGER`
- `AGGREGATION-INPUT-PACKAGER`

Until all of those blockers are closed, the evaluator status is `BLOCKED_PRE_CAPTURE` and
no official cold/native score exists. Do not create placeholder artifacts or invent hashes
to make `--require-score-ready` pass.

Only after score readiness, qualification, capture, judging, verification, and oracle hard
gates have all completed may the exact three-judge panel be aggregated:

```sh
python3 tools/commercial-ux/aggregate-native.py \
  /tmp/native-judge-01.json \
  /tmp/native-judge-02.json \
  /tmp/native-judge-03.json \
  --verifier /tmp/native-verifier.json \
  --oracle-ledger /tmp/oracle-hard-gates.json \
  --candidate-provenance /tmp/native-aggregation-input.json \
  --candidate-manifest /tmp/candidate-manifest.json \
  --qualification-receipt /tmp/qualification-receipt.json \
  --judge-panel /tmp/judge-panel.json \
  --evaluation-run /tmp/evaluation-run-manifest.json \
  --actor-observations \
    /tmp/cold-actor-01.json \
    /tmp/cold-actor-02.json \
    /tmp/cold-actor-03.json \
  --coverage-trace /tmp/coverage-trace.json \
  --evidence-set /tmp/evidence-set.json \
  --output /tmp/native-scorecard.json
```

`FORMATIVE-01` may guide a verified improvement but can never return official PASS. An
official candidate consumes the lowest unused holdout, and a product change advances to the
next unused holdout rather than rerolling the same evidence.
