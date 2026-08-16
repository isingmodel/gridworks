# Scope 0B evidence package

This directory holds the public verification and checkpoint artifacts for the completed
[Scope 0B contract](../../docs/scopes/SCOPE_0B_PLAYABLE.md).

Official v6 closed with five evidence-valid `COMPLETED` rows and a strict `GO`. See
[`RESULT.md`](RESULT.md) and [`CHECKPOINT_2_DECISION.md`](CHECKPOINT_2_DECISION.md). Scope 1 is not opened by
that result.

Current state: the executable build, independent code review and native [L00 result](L00_RESULT.md) are
complete. Official run v1 then used six launches, including one evidenced runner replacement. Five
participants reached `FINAL`, but every completed run used a pre-measurement tool-catalog lookup that the
frozen manifest policy did not allow. The strict result is therefore protocol `PROXY-RUN-BLOCKED`, not a game
decision. The combined [v1 result and v2 protocol-reset checkpoint](CHECKPOINT_1B_RUN_PROTOCOL_V2.md) keeps
the build, fixture, UI, rubric and gate unchanged while supplying the direct Computer Use callable in the
exact v2 task message. v2 then used one evidenced runner replacement and one native full run, but the full
run had an extra harmless import attempt that the literal frozen bootstrap did not allow. It cannot be
replaced under v2, so five valid slots became unreachable and
[`S0B-RUN-v2` also closed as protocol `PROXY-RUN-BLOCKED`](CHECKPOINT_1C_RUN_PROTOCOL_V3.md).

v3 then completed five native sessions, but the coordinator added a skill cache path to three frozen task
messages. Those three actual prompt hashes differ from their manifests, so only `2/5` slots are technically
valid and the round is protocol `PROXY-RUN-BLOCKED`; gameplay fields were not scored.
[`Checkpoint 1D`](CHECKPOINT_1D_RUN_PROTOCOL_V4.md) preserves that evidence without a game decision and freezes
a smaller v4 delivery rule: one canonical prompt source, whitespace-only normalization and transcript checking.
Runtime, fixture, UI, rubric and gate remain unchanged. Independent v4 review closed with `P0/P1/P2 = 0`;
five v4 launches then reached native `FINAL`, but three traces did not preserve the complete metadata `exact text`
required by that frozen evidence format. [Checkpoint 1E](CHECKPOINT_1E_RUN_PROTOCOL_V5.md) therefore closes v4
as protocol `PROXY-RUN-BLOCKED` with `TechnicalValid = 2/5` and no gameplay score. v5 then stopped after six
launches because required runner manifests and contemporaneous monotonic samples were not recorded for the
first completed slots; reconstructed filesystem times are not substituted. [Checkpoint 1F](CHECKPOINT_1F_RUN_PROTOCOL_V6.md)
closes v5 without a gameplay score and defines v6 around fixed rows plus coordinator, participant and app originals.
Whether official v6 sessions may start is owned only by checkpoint 1F.

[`record-template.csv`](record-template.csv) is the frozen historical v1–v5 schema. v6 does not use a runner
manifest or that CSV; its five fixed rows are reported only after the reviewed round.

App diagnostics and optional engine logs belong in `private/` and stay out of Git. Platform coordinator and
participant session JSONL remain in their platform-owned location and are not copied; only paths, SHA-256,
anonymous aggregate and the reviewed decision may be published here.

`verify_implementation.rb` is the pre-run authorization guard frozen at commit
`23be035e856e052091c529c14c8552aecc129327`. A later result commit is expected to fail its HEAD guard; do not
weaken that historical check. Closure uses the contract verifier, build/checks and the independent evidence and
strict-score reviews linked from the result.
