# G.3 formative-v26 step-completion record

This record closes the current implementation/formative step only. It does not claim the final
`ReferenceParity` gate.

## Fixed candidate

- viewport: native `1920×1080`, UI 100%; no 720p run
- runtime captures: `runtime/pair-normal.png`, `pair-heat.png`, `pair-route.png`,
  `pair-siting.png`, `pair-flood-baseline.png`, `pair-flood.png`
- pair boards: deterministically rebuilt by `build-final-pair-boards.py`; recipes preserve source
  hashes, crop/paste rectangles, and no-retouch/no-color-correction declarations
- composition: 55 individually bound runtime art files, including 12 atomic city/prop objects,
  6 road tiles, 8 grid parts and the separately generated transparent bridge B; 338 explicit
  atomic city placements and 641 world instances

## Deterministic and native evidence

- `Gridworks Commercial checks: PASS (22 suites, 2331 assertions)`
- Debug build: 0 warnings, 0 errors
- checkpoint: PASS, missions 4, edges 19, `input=focus-keyboard`, `resolution=1920x1080`
- completion: PASS, missions 8, factual results and epilogue, `input=focus-keyboard`,
  `resolution=1920x1080`
- completed resume: PASS, complete state restored with 7 prior results,
  `input=focus-keyboard`, `resolution=1920x1080`

## SOL Ultra formative

Exactly one blinded process was run for each of the ten fixed pairs, always with
`gpt-5.6-sol`, reasoning effort `ultra`, strict JSON schema and normalized evidence boxes. The
accepted outputs are in `formative-v26/`.

Using the protocol's fixed categorical conversion and category weights, the single-call diagnostic
proxy is `74.375` before four-call spread penalty:

| category | single-call median |
|---|---:|
| camera | 85 |
| density | 65 |
| river | 65 |
| scale | 65 |
| material | 85 |
| grid | 65 |
| HUD | 85 |
| state | 75 |
| timeline | 92.5 |

The result is formative only: it lacks order reversal, the second replicate, jury-stability checks,
and disagreement penalty. Therefore it is neither an official score nor a passing `ReferenceParity`.
The remaining structural bottlenecks are density and river parity; the active G.3 `>80` goal remains
open for a later iteration.
