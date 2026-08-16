# Scope 0B evidence package

This directory holds the public verification and checkpoint artifacts for the active
[Scope 0B contract](../../docs/scopes/SCOPE_0B_PLAYABLE.md).

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
as protocol `PROXY-RUN-BLOCKED` with `TechnicalValid = 2/5` and no gameplay score. The smaller v5 evidence
delta completed independent review with `P0/P1/P2 = 0`; official v5 sessions may run serially.

Future raw transcripts, native diagnostic logs and screenshots belong in `private/` and stay out of Git.
Only their hashes, anonymous aggregate and reviewed decision may be published here.
