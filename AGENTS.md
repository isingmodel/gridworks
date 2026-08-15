# Repository instructions

1. Read `README.md`, then the active scope it names, before changing plans, code, or data.
2. Only the active scope is authorized. Candidate documents are not a backlog; do not prebuild future systems.
3. Use the question ownership map in `docs/README.md`; current user instruction and the active scope always win.
4. Use the active scope's single data authority and completion checks; do not duplicate authoritative values.
5. After every major unit, review the evidence and update `README.md` plus the affected scope before starting
   an explicitly approved next gate.
6. Do not use LLM or policy agents for open-ended tuning or target choice ratios. Keep experiments bounded by
   the parameter policy in `README.md` and `docs/development/BALANCING_STATIC_SIM.md`.
7. After a major unit, commit it, run one bounded independent subagent review when available, fix only
   scope-valid findings, rerun checks, and commit the reviewed result.
8. At the same checkpoint, audit all documentation for current behavior and decisions; update or remove any
   legacy, stale, contradictory, or superseded material before proceeding.
9. Push, open a PR, or merge only when the current user task explicitly authorizes that repository write.
