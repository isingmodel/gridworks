# G.3 Step 1 atomic city hard-gate evidence

Status: **PASS — 2026-08-22**. This directory records only the supported 1920×1080 UI 100%/125%
evidence. No 720p capture or claim exists.

## Implemented boundary

- 12 separately generated atomic city/building/prop PNGs, one ImageGen run per PNG
- 6 separately generated atomic road/yard tiles
- 80 explicit city object instances and 40 explicit road instances
- projected-Y/X/kind stable depth order; no district/parcel/cluster/neighborhood/hamlet raster binding
- 29 G.3 files remain package-eligible and every one is scene-bound; 23 superseded composites were moved to
  `playtests/commercial-2d/g3-rejected-composites/`
- retained legacy residential/hospital block rasters are scene-unbound and export-excluded

## Fixed evidence

| artifact | SHA-256 |
|---|---|
| `1920x1080-ui100-discrete-art-path.png` | `aecc39767d24b6a208b8ffa1d4b7947ba4acf6cbd2238620c3d592e3ff33918e` |
| `1920x1080-ui125-path-reduce-motion.png` | `398a747dc5e163c60bd1b5b335e48d42c589ccd1835dfa0aa12ec0ae138f491b` |
| `pair-kit-city-atomic-board.png` | `b152ab46e987d0fc7e9996cdad61462f4b1980481389ba1eaba2da70afd93f3b` |
| `pair-kit-city-atomic-board.recipe.json` | `8286e6d74c1848203e5df47e18a77d012e4a88208b5784ae572582214aaed19d` |
| `atomic-city-audit-sol-ultra.json` | `4de3eec8aaeb1958424a6eb02cae520fd49de07fc2b5b301ba9bb64a05bd8120` |
| `atomic-city-audit-sol-ultra.execution.json` | `f630bb6d49abe0981a9cf9ecb1c17c5b898da05c94b37fdbb61c9a97c5150eab` |

The execution record fixes `codex-cli 0.149.0`, model `gpt-5.6-sol`, mode `ultra`, and the exact board,
map, and recipe hashes. The structured result is `PASS`: all 12 cells have
`singleCompositionUnit=true` and `visibleSolidCount=1`; `largeBakedCityRasterPresent=false`; critical
failure count is zero. This is a hard-gate boolean audit, not a ReferenceParity score and not human review.

## Deterministic/native result

- CommercialChecks: PASS, 22 suites / 2,153 assertions
- Game Debug and Release builds: PASS, zero warnings/errors
- actual-input presentation smoke: PASS at 1920×1080 UI 100% and 125%, 14 tiles / 23 objects
- actual-input placement smoke: PASS at 1920×1080 UI 125%, Home `전체 보기`, Q/E candidate cycling

Step 1 is closed. Step 2 is the atomic river/water/bank kit and authoritative state rendering; the final
goal remains `ReferenceParity >85` after the 2026-08-22 owner threshold adjustment; 85.0 fails.
