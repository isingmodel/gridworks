# Commercial UX text-plan tools

This directory implements the formative `TEXT-PLAN` lane only. `TextPlanProxy` never
becomes `CommercialUXProxy`.

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
