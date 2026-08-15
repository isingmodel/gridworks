# Scope 0A checkpoint 1 — materials freeze

> Checkpoint: Scope 0A card and facilitator materials frozen before LLM proxy test
>
> `SubGateDecision = PENDING`
>
> `Scope0State = ACTIVE`
>
> `HumanValidationStatus = NOT_COLLECTED`

## Frozen evidence

- `CardSetVersion = S0A-CARD-v1`
- `PromptVersion = S0A-PROXY-v1`
- 4 logical cards, 10 SVG source frames, and 10 exact 1600×900 RGB PNG inputs
- PNG authority for this round: [`CARD_HASHES.sha256`](CARD_HASHES.sha256)
- Fixed facilitator script and pre-registered rubric: [`FACILITATOR_SHEET.md`](FACILITATOR_SHEET.md)
- Fixed allocation: `L01 AB`, `L02 BA`, `L03 AB`, `L04 BA`, `L05 AB`
- Raw responses remain local-only under `playtests/scope-0a/private/`

## Checks

- `ruby playtests/scope-0a/verify_scope0a.rb`: PASS for topology, units, internal energy, cash, links, card metadata, hashes, forbidden copy, order variants, and phase leaks
- Manual render QA: PASS for 16:9 framing, Korean text fit, contrast, margins, equal plan weight, color-independent line patterns, timeline order, and Card 4 staged disclosure
- Internal dry run `L00`: completed Card 1, fixed transition, Cards 2–4, causal reveal, and settlement reveal with only the allowed PNG `view_image` calls; found no procedural omission or ambiguity; excluded from the scored round
- Card 1 failure cause remained upstream disconnection only; no hidden stable ID, rubric field, recommendation, answer, winner, or aggregate score appeared before its allowed phase

## Observations and limits

- Direct macOS thumbnail conversion produced inconsistent crops. The frozen PNGs were therefore rendered from the SVG source in a 1600×900 Chrome viewport after a 1.5-second virtual-time budget; the verifier hashes the exact outputs used by sessions.
- The user explicitly substituted isolated LLM sessions for the first nonexpert round. This checkpoint does not claim human usability evidence.
- No object capability changed, so `OBJECT_CATALOG.md` required no status update.
- `VISUAL_PRODUCTION_SPEC.md` was updated only to resolve the prediction subject and logical-card/state distinction observed before production.

## Repository checkpoint

- Initial materials commit: `f861900`
- Independent bounded review: `checkpoint1_review` inspected `f861900` and the fixes read-only; no P0, three resolved P1 findings (exact prompts, exclusive decision order, BA prompt order) and one resolved P2 state-name finding
- Reviewed commit: `PENDING`
- Push/PR: not authorized by the current task
