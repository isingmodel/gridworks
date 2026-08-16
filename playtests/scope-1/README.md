# Scope 1 evidence package

This directory records the active [Scope 1 manual-line contract](../../docs/scopes/SCOPE_1_INTERACTION.md).

- [`CHECKPOINT_0_CONTRACT_PREPARATION.md`](CHECKPOINT_0_CONTRACT_PREPARATION.md) preserves the historical
  pre-authorization preparation.
- [`CHECKPOINT_1_IMPLEMENTATION_ACTIVATION.md`](CHECKPOINT_1_IMPLEMENTATION_ACTIVATION.md) records the reviewed
  user authorization for implementation.
- [`CHECKPOINT_2_FIXTURE_HANDOFF.md`](CHECKPOINT_2_FIXTURE_HANDOFF.md) reviews the nine-field product fixture
  before Core or Game consumes it. The review is complete and the JSON is the sole machine authority.
- [`CHECKPOINT_3_IMPLEMENTATION_REVIEW.md`](CHECKPOINT_3_IMPLEMENTATION_REVIEW.md) records the isolated Core,
  checks and Godot vertical slice. Its source and native review are complete.
- [`verify_contract.rb`](verify_contract.rb) checks only fixture structure, values and the checker oracle. It
  does not authorize implementation or inspect Git history.

Official proxy sessions remain closed. If they are later authorized, repository-authored private app logs belong
under `private/` and remain ignored; platform-owned originals are not copied.
