# Scope 0B L00 native preflight result

> Result: **PASS — native Computer Use full run**
>
> `SubGateDecision = PENDING`
>
> `HumanValidationStatus = NOT_COLLECTED`

L00 is an unscored tool rehearsal. It proves that the reviewed native build can be read and operated through
the frozen Computer Use path; it is not a game `GO`, `REVISE` or `NO-GO` and supplies no human evidence.

## Successful run

- session/variant: `S0B-L00 / ab`
- build SHA-256: `69b658715a84b4099677b36c7d4fb458d65add59fcff8474865d95bf418e03bd`
- fixture SHA-256: `e617f7b9163294ca0e72f89bf3cb3a3be634c0de21f1d2736549863f53617e57`
- native target: `Gridworks — 강변 병원 회랑 (DEBUG)`
- input path: element-index actions through `node_repl + @oai/sky`; no coordinate or keyboard fallback
- initial state: two `get_app_state` calls returned within 20 seconds with screenshot/AX state
- completed path: town feeder order → DAY 1 → four predictions → River order/lock → DAY 3 → spatial event
  → DAY 9 20:00 recovery/final
- locked values: River/E1 `remains`, River/OLD `cut`, North/E1 `remains`, North/OLD `remains`
- final snapshot SHA-256: `9a77be76b9e404331143b1da3c9ef7ac1bf3b0b047c570d329c5b0c7dd34ff5f`

Every action was followed by a fresh AX read. The app diagnostic has the exact ten rows
`READY, COMMAND, COMMAND, COMMAND, PREDICTION_LOCKED, REVEAL_OPENED, COMMAND, COMMAND, COMMAND, FINAL`,
all accepted, with monotonic sequence and elapsed time. The final screen reported both utility paths restored,
event cash `−0.717 M`, current cash `5.712 M`, P0 continuity and 60 minutes of internal power remaining.

## Private evidence anchors

| Evidence | SHA-256 |
|---|---|
| app diagnostic JSONL | `3f0c615781ef75b665df7ba7801cf820f250bd2a2c4b0027ecfecb50a260f1d3` |
| Godot engine log | `678be1a5c713f54beb463daf16a33bc57ef6b439cdf48326e1db931eb7842dc0` |
| runner preflight manifest | `2608f298b969f82af0c63bf4b954ae6a8792a8f0f189bdf0ffd7de5e33182c00` |
| successful Computer Use trace | `d703052a9d599bf3c0673d24db4937526374be419907af685a3e35f9c37ce801` |

Raw files remain under ignored `playtests/scope-0b/private/`. A deterministic check independently parsed the
diagnostic order, session/variant, accepted flags, locked values, final hash and runner-to-diagnostic hash.

## Earlier blocked attempts

The first preflight was `PROXY-RUN-BLOCKED`: the reviewed app emitted `READY`, but Computer Use returned no AX
or screenshot state after host unlock. Bundle-ID/path targeting, a Node REPL reset, a cold subagent retry and a
Finder control reproduced the transport hang. That result was reviewed at commits `247850d` and `0e66dd0` and
remains valid historical evidence; it was not a game failure.

After the user restarted Codex, AX returned. The first retry then exposed Godot's engine-owned `(DEBUG)` title
suffix before any click. The exact target-string correction was independently reproduced and reviewed at
commits `1f75918` and `b74769c`; runtime, fixture, prompt and gate never changed.

## Next boundary

L00 satisfies the native preflight. Initial result commit `4a84b55374f2255f36806bc16112cf1f8ebc5fda`
received a bounded independent review from `scope0b_core_review`. It found and closed two `P1` record defects:
the facilitator had authorized official sessions before review completion, and one public app-log SHA digit
was mistyped. Independent parsing then reproduced the AX trace, ten-event diagnostic, runner `final:none`,
four private hashes and unchanged runtime/prompt/gate with `P0=0, P1=0, P2=0`.

Official `S0B-L01`–`S0B-L05` could then run sequentially on the same frozen build. No official session had
started when this review closed. The later v1 block is recorded in
[`checkpoint 1B`](CHECKPOINT_1B_RUN_PROTOCOL_V2.md), the v2 block in
[`checkpoint 1C`](CHECKPOINT_1C_RUN_PROTOCOL_V3.md), and the v3 block plus current v4 draft in
[`checkpoint 1D`](CHECKPOINT_1D_RUN_PROTOCOL_V4.md). This historical L00 result remains native UI evidence
for the unchanged build.
