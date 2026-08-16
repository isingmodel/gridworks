# Repository instructions

1. Read `README.md`, then the active scope it names. Current user instruction wins, followed by the active scope and the question ownership map in `docs/README.md`.
2. Only the active scope is authorized. Readiness, a detailed candidate, or an ambiguous goal does not authorize implementation or execution outside it; do not prebuild future systems.
3. Use the active scope's single data authority and completion checks. Prove rules, state transitions, builds, and wiring with deterministic checks and smoke tests before considering a playtest.
4. Use an LLM playtest only when the active scope explicitly needs an interaction or comprehension observation that automation cannot provide. Keep one small fixed sample, do not tune from it, do not treat it as human evidence, and do not duplicate evidence already preserved by the platform or app.
5. After a major unit, commit it, run one bounded independent subagent review when available, fix only scope-valid findings, rerun checks, and update current-state documentation before opening another gate.
6. Push, open a PR, or merge only when the current user task explicitly authorizes that repository write.
