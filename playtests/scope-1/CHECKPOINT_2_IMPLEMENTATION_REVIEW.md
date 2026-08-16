# Scope 1 checkpoint 2 — implementation review

> `CheckpointStatus = REVIEW_IN_PROGRESS`
>
> `ImplementationStatus = INITIAL_IMPLEMENTATION_COMPLETE`
>
> `NativeComputerUsePreflight = NOT_RUN`
>
> `OfficialProxyAuthorization = NOT_GRANTED`

## Implemented vertical slice

- scope-local strict loader, definitions, placement session and deterministic view hash
- independent Scope 1 checker with ten suites
- separate Godot scene and custom map input; standard Undo, order and completion buttons
- generic smoke-only coordinate arguments that traverse the real map inverse-snap/input handler
- scope-local diagnostic JSONL; no checker witness coordinate is stored in product fixture, Game source or log
- default scene changed to Scope 1 while completed Scope 0B remains runnable by explicit scene path

The implementation does not add graph search, terminal selection, types, economics, terrain, route suggestion,
coordinate-entry UI or future lifecycle abstractions.

## Frozen identities before independent review

- activation baseline: `be1b3c275a1212c89fab47b87bbe3d5e1e591724`
- fixture SHA-256: `8c1cd63efe1e6a6d3745db96c4071fd3a264ace07e715883581163f1c98e6a2b`
- initial Scope 1 snapshot: `928a92efde792d1c40a6452424785f181a060bbce6a12cf02010a47c754ab34d`
- completed Scope 1 snapshot: `f088b365ec59ec127a2215cf6f65bd09550598303d2b2d33c2b5bb6a00989555`
- initial build hash: `f8af82bf9e6ecc824f811b6a1b7309ee2d78a29eda2718e06075269602dc6ab2`
- Scope 0B BA regression snapshot: `d8d6ac9edf2dd05e45be72dd1d0f2d01d849a8e051d2d59115b9ba9a7880792d`

These implementation hashes may change only for scope-valid review fixes. The reviewed checkpoint must record the
final values and rerun [`verify_implementation.rb`](verify_implementation.rb).

## Reproducible checks

```text
dotnet build src/Gridworks.Core/Gridworks.Core.csproj -c Release --no-restore
dotnet build game/Gridworks.Game.csproj -c Debug --no-restore -t:Rebuild
dotnet run --project tools/Gridworks.Scope1Checks/Gridworks.Scope1Checks.csproj --no-restore
dotnet run --project tools/Gridworks.Checks/Gridworks.Checks.csproj --no-restore
ruby playtests/scope-1/verify_contract.rb
ruby playtests/scope-1/verify_implementation.rb
```

The implementation verifier imports Godot 4.7.1, runs Scope 1 smoke with externally supplied checker coordinates,
requires `READY → SUPPORT_ADDED ×2 → ORDERED(target off) → COMPLETED(target on) → FINAL`, and runs the completed
Scope 0B BA scene smoke separately.

Current independent evidence:

- Core Release and Game Debug: zero warnings/errors
- Scope 1: `10/10` suites, `274` assertions
- Scope 0B: `7` suites, `3,098` assertions
- headless Scope 1 and explicit Scope 0B scene smoke: PASS

## Review closure

- initial implementation commit: `PENDING`
- bounded independent reviewers: `PENDING`
- final review: `PENDING`
- reviewed implementation commit: `PENDING`

Source review and native visual/Computer Use preflight must close before any official participant session.
