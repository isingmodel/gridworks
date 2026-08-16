# Scope 0B implementation-freeze checkpoint

> Status: **DRAFT — independent checkpoint review pending; proxy remains closed**
>
> `SubGateDecision = PENDING`
>
> `Scope0State = 0B_ACTIVE`
>
> `HumanValidationStatus = NOT_COLLECTED`

## Frozen build authority

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
- facilitator-sheet SHA-256: `f3449d254016f3158e0bab4282d48b14bdead8f2c01eb3c07f8d09f9038ebd98`
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

## Remaining gate

The native UI was locked at the host level when Computer Use was probed. Therefore:

- `S0B-L00 = PENDING_HOST_UNLOCK`
- actual accessibility-tree interaction has **not** been claimed
- official sessions remain closed
- this is an environment preflight condition, not a game score or `NO-GO`

After this checkpoint receives bounded independent review, L00 may start on this exact build. Official L01–L05
may start only if L00 meets every preflight item in the facilitator sheet.
