# Gridworks repository rules

## Read first

Before changing implementation or balance data, read:

1. `SCOPE_1_0.md`
2. the relevant rules in `GAME_DESIGN_KO.md`
3. `BALANCING_STATIC_SIM.md` for any numeric change
4. `POST_1_0.md` before proposing scope expansion

## Scope

- Implement only `SCOPE_1_0.md` unless the user explicitly opens a later milestone.
- Do not pre-build post-1.0 systems as speculative infrastructure.
- Treat concept PNGs as visual references, never as numeric or rules authority.
- Nuclear projects, data centers, cooling-water distance, and water/air cooling are post-1.0 only. Do not
  add their contracts, schemas, commands, UI, or placeholder fields before that milestone is explicitly opened.
- If that milestone opens, preserve one `CoolingWaterZone`, one linear distance-loss rule, one hard cutoff,
  and two data-center cooling states. Water networks and data-center internal management remain out of scope
  unless a later adaptive planning checkpoint explicitly changes the boundary.

## Architecture

- Keep the authoritative simulation in pure C#/.NET with no Godot reference.
- Initial projects are `Gridworks.Core`, `Gridworks.Application`, `Gridworks.Persistence`,
  `Gridworks.Godot`, and `Gridworks.Headless`. Use folders/namespaces for Core subdomains.
- Dependencies flow `Application -> Core` and `Persistence/Godot/Headless -> Application + Core`.
- Godot sends application commands and renders read models; it never mutates authoritative state directly.
- Authoritative time advances only through one-minute ticks. Fast-forward repeats ticks without rendering.

## Data and determinism

- From the first code commit, versioned `data/` plus schemas are the sole authority for catalog and scenario
  numbers. Do not duplicate balance constants in C# or Godot scenes.
- Use stable IDs, ID-sorted iteration, integer game minutes, integer money, and `long kW-minute` energy.
- The 1.0 authority has no random stream. Scenario weather, demand, warnings, and outages are fixed data.
- Quantize power-flow outputs before protection, metering, economics, events, saves, and hashes.
- A failed or non-convergent solve must fail closed; never reuse stale flows as if safe.
- Cosmetic randomness must not enter saves, replays, or authoritative hashes.

## Balance changes

- Do not ask an LLM to autonomously tune until a scalar score improves.
- Classify parameters and sweep only bounded `BalanceKnob` values. Never sweep structural or derived values.
- Keep exactly three active balance knobs in the first milestone and never exceed six later. Opening a new
  knob requires closing another unless the adaptive planning checkpoint explicitly justifies a larger set.
- Use Static Balance Lab to screen, authoritative Headless runs to verify shortlisted timelines, and human
  playtests to approve comprehension and fun.
- Commit each balance decision with hypothesis, parameter family, tested bounds, result, available human
  evidence, and remaining risk.

## Verification

- Add or update deterministic unit, scenario, save/replay, and schema tests with every rules change.
- Run formatting, build, all relevant tests, Headless golden scenarios, and Godot smoke tests before claiming
  completion. Record reference hardware for performance gates.
- Do not enter the next implementation gate until the current gate's criteria are met.

## Adaptive planning checkpoint

There is no complete plan that should be executed unchanged from beginning to end. After completing every
major development unit or implementation gate, pause before starting the next one and:

1. summarize the implementation, automated checks, performance measurements, playtest evidence, surprises,
   and remaining risks from the unit just completed;
2. check which assumptions in the current plan were confirmed or disproved;
3. review the next development unit's scope, ordering, dependencies, parameter budget, tests, and definition
   of done;
4. update the active plan and the relevant scope/design/decision documents when evidence calls for a change;
5. explicitly record work that was deferred, removed, or newly justified.

Keep only the next major unit detailed; later units stay coarse. Passing a gate authorizes this review, not
automatic execution of the old next step. Do not expand scope merely because a future system was mentioned
in `POST_1_0.md`.
