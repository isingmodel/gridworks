# Scope 0A R2 test kit

> Active round: `LLM-PROXY-R2`

- `CardSetVersion = S0A-CARD-v2`
- `PromptVersion = S0A-PROXY-v2`
- `DecisionRuleVersion = S0A-GATE-v2`
- allocation: `R2-L01 AB`, `R2-L02 BA`, `R2-L03 AB`, `R2-L04 BA`, `R2-L05 AB`
- active contract: [`SCOPE_0A_R2_CARD_TEST.md`](../../docs/scopes/SCOPE_0A_R2_CARD_TEST.md)

SVG files are the editable participant source. `cards/png/` contains the exact frozen inputs and [`CARD_HASHES.sha256`](CARD_HASHES.sha256) identifies them. Raw messages and scored rows stay under `private/` and are ignored by Git.

Run `ruby verify_scope0a_r2.rb` before any session. Do not run the proxy until the materials-freeze checkpoint has an initial commit, bounded independent review, reviewed commit, and passing verifier.
