# Scope 0B evidence package

This directory holds the public verification and checkpoint artifacts for the active
[Scope 0B contract](../../docs/scopes/SCOPE_0B_PLAYABLE.md).

Current state: the executable build, independent code review and implementation-freeze checkpoint are
reviewed. The unscored [L00 result](L00_RESULT.md) is `PROXY-RUN-BLOCKED` by the external Computer Use
transport. No official proxy round is active. Retry only after that external state changes, and do not create
official sessions until L00 passes on the same frozen build.

Future raw transcripts, native diagnostic logs and screenshots belong in `private/` and stay out of Git.
Only their hashes, anonymous aggregate and reviewed decision may be published here.
