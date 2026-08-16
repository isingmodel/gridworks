# Scope 0B evidence package

This directory holds the public verification and checkpoint artifacts for the completed
[Scope 0B contract](../../docs/scopes/SCOPE_0B_PLAYABLE.md).

Current state: the executable build, independent code review and native [L00 result](L00_RESULT.md) are
complete. v1, v2 and v3 stopped at run-protocol blockers and remain separate immutable evidence in
[checkpoint 1D](CHECKPOINT_1D_RUN_PROTOCOL_V4.md). v4 kept the build, fixture, UI, rubric and gate unchanged,
then completed five technically valid cold sessions without replacement. The [public result](RESULT.md) is
`S0B-GATE-v1 = GO`; all four fields and integrated interaction are `5/5`.

The [decision checkpoint](CHECKPOINT_2_DECISION.md) keeps the claim narrow: this is same-model LLM evidence on
one authored native UI, not human usability, fun, accessibility, balance or free-construction evidence.
`HumanValidationStatus = NOT_COLLECTED`.

Raw transcripts, native diagnostic logs and screenshots belong in `private/` and stay out of Git. Only their
hashes, anonymous aggregate and reviewed decision are published here.
