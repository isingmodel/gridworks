# Scope 1 evidence package

This directory holds activation, implementation, proxy and result evidence for the active
[Scope 1 manual-line contract](../../docs/scopes/SCOPE_1_INTERACTION.md).

Current state: the machine-fixture handoff in
[`CHECKPOINT_1_CONTRACT_FREEZE.md`](CHECKPOINT_1_CONTRACT_FREEZE.md) is independently reviewed and source
implementation is open. Official proxy sessions remain closed until a later reviewed implementation/evidence
checkpoint. [`CHECKPOINT_0_CONTRACT_PREPARATION.md`](CHECKPOINT_0_CONTRACT_PREPARATION.md) is the historical
pre-authorization preparation record and does not govern the active state.

The initial vertical slice and headless regressions are recorded in
[`CHECKPOINT_2_IMPLEMENTATION_REVIEW.md`](CHECKPOINT_2_IMPLEMENTATION_REVIEW.md). Its source review and native
Computer Use preflight are not yet closed, so it grants no official-session authority.

Private app logs created by this repository belong under `private/` and remain ignored. Platform-owned session
JSONL is not copied; a result may record its immutable path and SHA-256 after the round.
