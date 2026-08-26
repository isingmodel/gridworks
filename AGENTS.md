# Repository instructions

1. Read `README.md`, `docs/ACTIVE_SCOPE.md`, then `docs/AGENT_GUIDE.md`. Use the question ownership map in `docs/README.md` instead of searching every document.
2. Current user instruction wins, followed by the active scope and the question owner's current document. History and readiness never authorize work.
3. Read-only explanation or diagnosis may inspect relevant evidence. Before changing task files, record the authorized change in one active scope with a single result, authority, out-of-scope list, and completion checks.
4. Prove rules, state transitions, builds, and wiring with the smallest deterministic check first. Treat headless/package evidence and device/human evidence as different claims.
5. Use an LLM playtest only when the active scope requires an observation automation cannot provide. Keep one fixed sample, do not tune from it, and never call it human evidence.
6. Commit major units, request one bounded independent review when available, fix only scope-valid findings, rerun checks, update only the documents that own changed facts, and close the scope.
7. Push, open a PR, merge, run an external release gate, or publish only when the current user task explicitly authorizes that action.
