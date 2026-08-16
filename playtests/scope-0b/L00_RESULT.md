# Scope 0B L00 first native preflight result

> Historical result: **PROXY-RUN-BLOCKED — Computer Use transport**
>
> `SubGateDecision = PENDING`
>
> `HumanValidationStatus = NOT_COLLECTED`

This is an unscored tool preflight, not a game `GO`, `REVISE` or `NO-GO`. No official L01–L05 session
started, and no static-card or headless substitute was accepted.

## What was proved

- The reviewed native build launched and emitted `READY` in every attempt.
- Every `READY` row carried build hash
  `69b658715a84b4099677b36c7d4fb458d65add59fcff8474865d95bf418e03bd` and fixture hash
  `e617f7b9163294ca0e72f89bf3cb3a3be634c0de21f1d2736549863f53617e57`.
- After the host was unlocked, `sky.list_apps()` returned and identified the running target as
  `org.godotengine.godot`.
- `sky.get_app_state()` did not return an accessibility tree or screenshot within the frozen 20-second
  limit. Targeting the bundle ID and exact app path, resetting the Node REPL, and a separate cold subagent
  Computer Use session all reproduced the same transport hang.
- A Finder control call also hung, so the evidence does not attribute the failure to Gridworks.

No UI click was accepted, no prediction was locked, no corridor was selected and `FINAL` was not reached.
The app processes were stopped after each bounded attempt.

## Private evidence anchors

| Attempt | Evidence | SHA-256 |
|---|---|---|
| host locked | Godot engine log | `678be1a5c713f54beb463daf16a33bc57ef6b439cdf48326e1db931eb7842dc0` |
| host locked | app diagnostic | `20734852c5a39c5641f93902a6928bf7f212d39e95f5c248983939a13a0962c6` |
| unlocked coordinator retry | Godot engine log | `678be1a5c713f54beb463daf16a33bc57ef6b439cdf48326e1db931eb7842dc0` |
| unlocked coordinator retry | app diagnostic | `92bd602f8a2b2861e01390383aba0bd8c1b81b264554e57b95e17df992b0ae28` |
| unlocked cold-subagent retry | Godot engine log | `678be1a5c713f54beb463daf16a33bc57ef6b439cdf48326e1db931eb7842dc0` |
| unlocked cold-subagent retry | app diagnostic | `2ec095c5d7952f837bf9421f9fccf3ef0234bab6aadbfe0b79b8c76f484eeb7e` |
| coordinator + cold-subagent transport trace | tool-trace extract | `e02f22b4c145fadbfb7e563342a286382fb8afd33af87a776c1cadb7773cf161` |

Each app diagnostic has exactly one `READY` row and no later command. Raw logs remain under the ignored
`playtests/scope-0b/private/` directory.

## Review

- initial result commit: `247850d8be2d47e0cc21dcaf871c29d0254e01e8`
- bounded independent reviewer: `scope0b_core_review`
- initial finding: one `P2` provenance gap because READY logs did not preserve the separate Computer Use
  timeout evidence
- minimal fix: one ignored tool-trace extract and its public SHA-256; no runner framework or schema added
- game result, build, fixture, prompt and gate rule were unchanged

## Resume condition

Retry L00 only after the external Computer Use state changes. The retry must use the same reviewed build and
must obtain two `get_app_state` responses within 20 seconds before the first click. A successful full native
run is still required before official sessions open.

## Retry update

After the user restarted Codex and unlocked the host on 2026-08-16, two bounded `get_app_state` calls returned
the native AX tree. The retry stopped before its first click because the Godot editor build exposed an
engine-owned `(DEBUG)` title suffix that the frozen target string omitted. Runtime, fixture, prompt and gate
were unchanged. The target-only correction then received a clean bounded review with direct native AX
reproduction. L00 remains incomplete and may now continue on the same build.
