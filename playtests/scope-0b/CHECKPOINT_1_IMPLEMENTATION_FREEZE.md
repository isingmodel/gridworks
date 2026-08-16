# Scope 0B implementation-freeze checkpoint

> Status: **REVIEWED IMPLEMENTATION — runtime unchanged; current execution copy is v3**
>
> `SubGateDecision = PENDING`
>
> `Scope0State = 0B_ACTIVE`
>
> `HumanValidationStatus = NOT_COLLECTED`

## Frozen build authority

The build evidence in this file remains current. v1 and v2 were later blocked by bootstrap provenance rules;
their frozen evidence remains in [`checkpoint 1B`](CHECKPOINT_1B_RUN_PROTOCOL_V2.md) and
[`checkpoint 1C`](CHECKPOINT_1C_RUN_PROTOCOL_V3.md). Only the current execution copy is superseded by v3.
Runtime, fixture and gate did not change.

- active contract: [`docs/scopes/SCOPE_0B_PLAYABLE.md`](../../docs/scopes/SCOPE_0B_PLAYABLE.md)
- machine fixture: [`data/scope-0b-v1.json`](../../data/scope-0b-v1.json)
- implementation verifier: [`verify_implementation.rb`](verify_implementation.rb)
- facilitator copy: [`FACILITATOR_SHEET.md`](FACILITATOR_SHEET.md)
- record schema: [`record-template.csv`](record-template.csv)
- `BuildVersion = S0B-BUILD-v1`
- reviewed build commit: `c14750ee34955236d482e048d769438c10032584`
- source-manifest build SHA-256:
  `69b658715a84b4099677b36c7d4fb458d65add59fcff8474865d95bf418e03bd`
- fixture SHA-256: `e617f7b9163294ca0e72f89bf3cb3a3be634c0de21f1d2736549863f53617e57`
- prompt-template SHA-256: `4a07e8fdf61cbd2475ba27613e9a89d4fcb254cc54c6d19d5f6a740ca64f2111`
- facilitator-sheet SHA-256: `0ae5f2379add1fc92418cf3f7446fc2694361bdbcd6d39bcf552fc200fed2b46`
- record-template SHA-256: `7d9e96313f3a2ba6189ef09267798890b2abd749a1bdb6373afe5d4c955104e1`

The runtime build hash is a deterministic manifest of the runtime source inputs in `game/`,
`src/Gridworks.Core/`, `Directory.Build.props` and `global.json`. The fixture has its own hash. This is not a
claim that compiler DLL bytes are checkout-path-independent: portable PDB/source paths made equal commits in
different directories produce different DLL hashes. Official sessions therefore freeze the reviewed commit,
source manifest, fixture and exact toolchain together and reuse the same imported build.

## Toolchain evidence

- host: macOS 26.6, Apple Silicon `osx-arm64`
- .NET SDK: `8.0.129`; runtime: `8.0.29`; `global.json` roll-forward disabled
- Godot: `4.7.1.stable.mono.official.a13da4feb`
- Godot archive:
  `.tools/Godot_v4.7.1-stable_mono_macos.universal.zip`
- archive SHA-256: `92cac516baa8ddc7756eeaa38a6d007778a968bfbf188db7c5d6e6ec21c5d52c`
- Godot binary and project paths are frozen in the facilitator sheet.

## Implementation and review evidence

- initial implementation commit: `44b505e4ce2f11afd57dd433e0a1bd67b7f1c8f0`
- bounded independent reviewers: `scope0b_core_review`, `scope0b_ui_review`, `scope0b_core_impl`
  (documentation/scope audit)
- initial review result: `P0=0`; one UI truth-label `P1`; bounded `P2` strict-loader, presentation,
  diagnostic and documentation findings
- fixes in reviewed build:
  - North final now says the hospital supply was maintained rather than recovered.
  - LostSales is neutral and explicitly cash-excluded; counterfactual results use player-facing language.
  - AB/BA must contain the two authored corridor projects; explicit-null fixture input is normalized.
  - READY build hash covers Core/Game source, scene and project settings; diagnostic logs fail on reuse.
  - risk caption contrast, fixture-owned window title and stale checkpoint/product wording were corrected.
- reviewed build commit: `c14750ee34955236d482e048d769438c10032584`
- reviewed result after fixes: `P0=0, P1=0`; all reported scope-valid findings resolved
- scope audit: no BFS, free placement, save/replay, scheduler, future schema, oracle access from Game or
  unsupported future systems found

## Automatic and native evidence

All of the following passed on the reviewed commit:

```text
ruby playtests/scope-0b/verify_contract.rb
ruby playtests/scope-0a-r2/verify_scope0a_r2.rb
dotnet format src/Gridworks.Core/Gridworks.Core.csproj --verify-no-changes --no-restore
dotnet format tools/Gridworks.Checks/Gridworks.Checks.csproj --verify-no-changes --no-restore
dotnet format game/Gridworks.Game.csproj --verify-no-changes --no-restore
dotnet build src/Gridworks.Core/Gridworks.Core.csproj -c Release
dotnet run --project tools/Gridworks.Checks/Gridworks.Checks.csproj -c Release -- data/scope-0b-v1.json
dotnet build game/Gridworks.Game.csproj -c Release
/Users/fred/dev/electric_simulator/.tools/godot-4.7.1/Godot_mono.app/Contents/MacOS/Godot --headless --editor --path /Users/fred/dev/electric_simulator/game --quit --log-file /private/tmp/s0b-freeze-c147-import-godot.log
/Users/fred/dev/electric_simulator/.tools/godot-4.7.1/Godot_mono.app/Contents/MacOS/Godot --headless --path /Users/fred/dev/electric_simulator/game --log-file /private/tmp/s0b-freeze-c147-ab-godot.log -- --session-id S0B-FREEZE-AB --variant ab --diagnostic-log /private/tmp/s0b-freeze-c147-ab-app.jsonl --smoke
/Users/fred/dev/electric_simulator/.tools/godot-4.7.1/Godot_mono.app/Contents/MacOS/Godot --headless --path /Users/fred/dev/electric_simulator/game --log-file /private/tmp/s0b-freeze-c147-ba-godot.log -- --session-id S0B-FREEZE-BA --variant ba --diagnostic-log /private/tmp/s0b-freeze-c147-ba-app.jsonl --smoke
```

- Core checks: `7 suites / 3,098 assertions`, all `PASS`
- Godot headless import/build: `PASS`, engine error/warning `0`
- headless `--smoke`: AB and BA both `PASS`; each diagnostic has the exact ten-event sequence
- final snapshot SHA-256:
  - River/AB: `9a77be76b9e404331143b1da3c9ef7ac1bf3b0b047c570d329c5b0c7dd34ff5f`
  - North/BA: `d8d6ac9edf2dd05e45be72dd1d0f2d01d849a8e051d2d59115b9ba9a7880792d`
- native Compatibility renderer: `1280×720`, Apple M1 OpenGL-over-Metal
- visual QA: initial, decision, building, commissioned, event and final screens plus both route finals were
  rendered; no clipping or overlap remained. Color-independent line/pattern/status text was visible.
- expected technical observation: Godot's dummy headless renderer crashed when combined with
  `--write-movie`; native Compatibility rendering recorded frames correctly. Headless smoke itself is clean.

## Frozen participant messages

| Session | Variant | SHA-256 after replacing `<SESSION_ID>` |
|---|---|---|
| `S0B-L01` | `ab` | `019de561a589b186cc299f2a2319c0891a1ac8e64185aa194b47ba15e3c344d6` |
| `S0B-L02` | `ba` | `74713c9f9474ab8cab1c6c132f4bb7f16f26d244eb40cb6bbb86f686df4efcc1` |
| `S0B-L03` | `ab` | `470695ca6e0b250ec076dfd6372cdfcac977907f95ff7337c3cca641cc83c4be` |
| `S0B-L04` | `ba` | `03e19cbcb1debe1a8d9d1ef44dc50c8a19bfd2f3b78bfa147c31207a88a6c6bb` |
| `S0B-L05` | `ab` | `bb3fb9929e3605d5d2fb78d1b3a7ae22ec7d95e30ead8b428f54181d069eaaf2` |

## Checkpoint review

- initial checkpoint commit: `42a827e59ff05a12c5c65b890f4c9a8d4fe9541b`
- bounded independent reviewer: `scope0b_core_review`
- review rule: challenge both missing evidence and unnecessary structure; prefer the smallest structure that
  makes a gate decision reproducible
- accepted fix: define the existing runner manifest's exact prompt hash, fault evidence and replacement
  boundary in the facilitator sheet; add the exact Godot verification commands
- rejected expansion: no general runner framework, separate schema or duplicate CSV telemetry was added
- final review: `P0=0, P1=0, P2=0`; contract, implementation freeze, Core checks and build all passed

## Gate after freeze

At freeze time the native UI was locked at the host level. The later bounded attempts are recorded in
[`L00_RESULT.md`](L00_RESULT.md): the host unlocked, but Computer Use still failed to return AX or screenshot
state and the preflight became `PROXY-RUN-BLOCKED`.

- `S0B-L00 = PROXY-RUN-BLOCKED`
- actual accessibility-tree interaction has **not** been claimed
- official sessions remain closed
- this is an environment preflight condition, not a game score or `NO-GO`

This checkpoint's bounded independent review remains complete. L00 may be retried on this exact build only
after the external Computer Use state changes. Official L01–L05 may start only if L00 meets every preflight
item in the facilitator sheet.

On the authorized retry, Computer Use returned the native accessibility tree, but the frozen Godot editor
binary exposed the engine-owned suffix `(DEBUG)` in the window title. The run stopped before the first click.
Only the exact target title and facilitator hash were corrected; runtime source, fixture, participant prompt,
gate and prior evidence did not change. Initial correction commit `1f759182937473b3b3bf58b53e28ffcf6d9400b8`
received a bounded independent review from `scope0b_core_review`: direct native AX reproduction confirmed the
exact `(DEBUG)` title and frozen READY hashes; verifiers passed with `P0=0, P1=0, P2=0`.

The next L00 attempt completed the full AX element-index path and exact ten-event diagnostic through `FINAL`.
Its public result and private evidence anchors are in [`L00_RESULT.md`](L00_RESULT.md). Initial result commit
`4a84b55374f2255f36806bc16112cf1f8ebc5fda` received a bounded independent review from
`scope0b_core_review`. Two `P1` record defects—premature authorization wording and one mistyped app-log SHA
digit—were fixed. Independent parsing reproduced the AX trace, all hashes and `final:none`; final review was
`P0=0, P1=0, P2=0`. Official v1 and v2 later became protocol `PROXY-RUN-BLOCKED`; the unchanged build may be
reused only under the reviewed current execution copy linked above.
