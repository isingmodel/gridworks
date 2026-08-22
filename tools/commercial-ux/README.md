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
  --output /tmp/text-plan-evidence-input.json
```

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
