# Scope 1 checkpoint 3 — implementation review

> `CheckpointStatus = REVIEW_IN_PROGRESS`
>
> `ImplementationStatus = INITIAL_IMPLEMENTATION_COMPLETE`
>
> `NativeVisualReview = NOT_RUN`
>
> `OfficialProxyAuthorization = NOT_GRANTED`

## Implemented vertical slice

- Three scope-local Core files implement the strict fixture, four commands, two pure previews and deterministic
  view JSON without extending Scope 0B types.
- An independent checks executable owns the checker-only witness and exercises loader rejection, the A/B oracles,
  error precedence, rejected-state invariance, preview parity, boundary arithmetic, atomic completion and view
  determinism.
- `Scope1Main.tscn`, `Scope1Main.cs` and `Scope1PlacementMapView.cs` implement a separate Godot screen. The custom
  map snaps pointer input once and sends integer points to Core; standard buttons own Undo, order and completion.
- Smoke-only coordinates arrive only as command-line arguments, travel through viewport input routing and never
  become product fixture fields, Game defaults, participant inputs or diagnostic coordinates.
- Scope 1 diagnostics are a small private JSONL writer inside `Scope1Main.cs`; no shared telemetry framework or
  Scope 0B launch/log type was reused.

The existing `project.godot`, `Main.tscn`, Scope 0B Core/Game source and Scope 0B fixture are unchanged.

## Initial deterministic evidence

- Scope 1 fixture SHA-256: `f308a739f9e4fcaf9d6f07aacba65af6fdd9ae3600a1e5569254fcb749bb2edc`
- initial view SHA-256: `928a92efde792d1c40a6452424785f181a060bbce6a12cf02010a47c754ab34d`
- completed view SHA-256: `f088b365ec59ec127a2215cf6f65bd09550598303d2b2d33c2b5bb6a00989555`
- initial source build hash: `7417939e9cd0c9bd06c042f0a88ff7a29455b27c2e17947630c3fd21a33ff403`
- Scope 1 checks: `8` suites, `605` assertions, PASS
- Scope 0B checks: `7` suites, `3,098` assertions, PASS
- Scope 1 diagnostic events: `READY → SUPPORT_ADDED → SUPPORT_ADDED → ORDERED → COMPLETED → FINAL`
- Scope 0B regression finals: AB `9a77be76b9e404331143b1da3c9ef7ac1bf3b0b047c570d329c5b0c7dd34ff5f`,
  BA `d8d6ac9edf2dd05e45be72dd1d0f2d01d849a8e051d2d59115b9ba9a7880792d`

The initial smoke evidence is under `/private/tmp/gridworks-s1-initial.wQ413u/`. Final review will use a fresh
directory and record any changed build or view hashes.

## Reproducible checks

```text
ruby playtests/scope-1/verify_contract.rb
dotnet build src/Gridworks.Core/Gridworks.Core.csproj -c Release
dotnet run --project tools/Gridworks.Scope1Checks/Gridworks.Scope1Checks.csproj -c Release -- data/scope-1-v1.json
ruby playtests/scope-0b/verify_contract.rb
dotnet run --project tools/Gridworks.Checks/Gridworks.Checks.csproj -c Release
dotnet build game/Gridworks.Game.csproj -c Debug -t:Rebuild
```

Godot import and smoke use the repository's pinned Godot 4.7.1 binary. Scope 1 runs with explicit
`--scene res://Scope1Main.tscn`, exactly two `--smoke-support` values and a fresh diagnostic path. Scope 0B AB/BA
run through the unchanged default `Main.tscn` with fresh logs.

## Review closure

- initial implementation commit: `PENDING`
- bounded independent reviewers: `PENDING`
- initial findings: `PENDING`
- final recheck: `PENDING`
- reviewed implementation commit: `PENDING`

Source review and native 1280×720 clipping/accessibility review must close before implementation evidence is
complete. Official proxy execution remains a separate user decision.
