# Scope 0A R2 checkpoint 1 — materials freeze

> Checkpoint: R2 card, prompt and decision rule frozen before scored LLM proxy sessions
>
> `SubGateDecision = PENDING`
>
> `Scope0State = 0A_R2_ACTIVE`
>
> `HumanValidationStatus = NOT_COLLECTED`

## Frozen evidence

- `CardSetVersion = S0A-CARD-v2`
- `PromptVersion = S0A-PROXY-v2`
- `DecisionRuleVersion = S0A-GATE-v2`
- four logical cards, ten SVG source frames and ten exact `1600×900` RGB PNG inputs
- PNG authority: [`CARD_HASHES.sha256`](CARD_HASHES.sha256)
- fixed prompt, field rubrics and allocation: [`FACILITATOR_SHEET.md`](FACILITATOR_SHEET.md)
- allocation: `R2-L01 AB`, `R2-L02 BA`, `R2-L03 AB`, `R2-L04 BA`, `R2-L05 AB`
- raw inputs and responses remain local-only under `playtests/scope-0a-r2/private/`

R1 remains immutable. Card 2, both Card 3 variants and both settlement-reveal sources and PNGs are
byte-identical to R1. Only Card 1 and the prediction/causal axis copy changed.

## One material change family

`InformationStructure: rubric-aligned contrast elicitation` is the only material family.

- Card 1 separately elicits current supply, service-area capability and the energized upstream path.
- Card 4 separately elicits electrical switching-circuit and spatial-corridor relationships.
- No fixture, topology, result, cost, schedule, settlement value, field rubric or choice incentive changed.
- No service-area definition, relationship answer, recommended plan or scored result is revealed.

Before any R2 response was collected, the user's concern about an overly strong binary gate was resolved by
freezing `S0A-GATE-v2`: every field must reach 4/5 and the same-response four-field AND must reach 3/5. This
still blocks a repeated misconception and requires a fully integrated majority, while two distinct one-off
expression slips do not stop an exploratory, reversible playable gate. Five same-model executions are not
treated as a statistical population sample. R1 is not rescored and remains failed at Coverage 0/5.

## Checks

- `ruby playtests/scope-0a-r2/verify_scope0a_r2.rb`: PASS for R1 oracle regression, links, exact frame set,
  unchanged-frame equality, dimensions, colorspace, metadata, hashes, structured prompts, order variants and
  phase leaks
- `ruby playtests/scope-0a/verify_scope0a.rb`: PASS; historical R1 artifacts remain unchanged and valid
- `git diff --check`: PASS
- manual render QA: PASS for Korean text fit, margins, Card 1 two-line prompt, Card 4 header/reason-label fit,
  AB/BA symmetry, neutral choices and causal-before-settlement reveal
- non-scored cold dry run: `PENDING`

## Limits

- This is LLM proxy evidence preparation, not novice-human usability evidence.
- The new questions measure prompted contrast application, not spontaneous discovery.
- The decision rule is a bounded consistency gate for entering Scope 0B, not a confidence interval or release
  certification.
- No object capability changed, so the object catalog does not change.

## Repository checkpoint

- Initial materials commit: `PENDING`
- Independent bounded review: `PENDING`
- Reviewed commit: `PENDING`
- Push/PR: not authorized by the current task
