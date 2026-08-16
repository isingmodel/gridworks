# Scope 0A R2 test kit

> Frozen completed round: `LLM-PROXY-R2 = PROXY-PASS`. Do not rerun or revise these materials.

- `CardSetVersion = S0A-CARD-v2`
- `PromptVersion = S0A-PROXY-v2`
- `DecisionRuleVersion = S0A-GATE-v2`
- allocation: `R2-L01 AB`, `R2-L02 BA`, `R2-L03 AB`, `R2-L04 BA`, `R2-L05 AB`
- frozen historical contract: [`SCOPE_0A_R2_CARD_TEST.md`](../../docs/scopes/SCOPE_0A_R2_CARD_TEST.md)

SVG files are the editable participant source. `cards/png/` contains the exact frozen inputs and [`CARD_HASHES.sha256`](CARD_HASHES.sha256) identifies them. Raw messages and scored rows stay under `private/` and are ignored by Git.

The frozen aggregate and evidence hashes are in [`RESULT.md`](RESULT.md). `ruby verify_scope0a_r2.rb` must
continue to pass, but there is no active participant round.
