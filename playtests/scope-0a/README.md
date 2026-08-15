# Scope 0A test kit

This directory contains the participant-facing card set and the facilitator materials for the active Scope 0A gate. The rules and values remain authoritative only in [`docs/scopes/SCOPE_0A_CARD_TEST.md`](../../docs/scopes/SCOPE_0A_CARD_TEST.md); these files are derived test artifacts.

## Frozen identifiers

- `CardSetVersion`: `S0A-CARD-v1`
- Current round: `LLM-PROXY-R1`
- Order allocation: `L01 AB`, `L02 BA`, `L03 AB`, `L04 BA`, `L05 AB`

The current user instruction permits five isolated LLM sessions to stand in for the first nonexpert round. This can establish only that the causal story is readable to those proxy sessions. It is not evidence about human usability, play time, enjoyment, or accessibility.

## Files

- [`cards/`](cards/): four logical 16:9 cards. SVG is the editable source; [`cards/png/`](cards/png/) contains the exact RGB inputs used in the proxy round. Cards 3 and 4 have `AB` and `BA` order variants. Card 4 also has facilitator-controlled causal and settlement reveal states.
- [`FACILITATOR_SHEET.md`](FACILITATOR_SHEET.md): fixed neutral script, question order, scoring rubric, and reveal sequence.
- [`record-template.csv`](record-template.csv): blank per-session record schema. Copy it into `private/` before use.
- [`RESULT_TEMPLATE.md`](RESULT_TEMPLATE.md): one-page aggregate decision template.

## Run order

1. Copy `record-template.csv` to `private/LLM-PROXY-R1-responses.csv` and keep all verbatim answers there.
2. Freeze every participant on the same commit and `CardSetVersion`.
3. Give a cold session only Card 1 and collect its answer.
4. Read the fixed transition sentence, then show Card 2, the assigned Card 3 variant, and the matching Card 4 prediction variant.
5. Record all predictions, reasons, internal-power answers, trade-off explanation, choice, and switching condition before any reveal.
6. Show the matching causal reveal. Only after that show the matching settlement reveal.
7. Score with the pre-registered rubric. Do not count a corrected answer after help.
8. Commit only an aggregate, non-identifying result. Do not commit the `private/` file.

Run `ruby verify_scope0a.rb` before freezing a round. The command checks the fixture oracle, units, links, 10 SVG/PNG frames, dimensions, metadata, forbidden participant copy, order variants, and staged information release.
