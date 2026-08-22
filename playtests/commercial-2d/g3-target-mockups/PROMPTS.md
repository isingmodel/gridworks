# G.3 target mockup prompts

All three images used the Codex built-in `image_gen` tool. The four files under `assets/` were style,
camera, composition and state references. Generated target pixels are planning evidence only and are not
runtime assets.

## PROMPT-SITING-V3

Inputs were `assets/04-plant-siting.png` first and `plant-siting-v2-source.png` second.

```text
Create a newly rendered revision of IMAGE 2 while preserving its current exact isometric camera, crop, river path, site positions, city and industrial mass positions, plant and pylon scale, orange distance bands, route geometry, left rail, right inspector, and top bar. Apply only these parity corrections from IMAGE 1: lift the terrain and concrete into softer gray-brown midtones; add IMAGE 1's restrained atmospheric haze and softer distance falloff; reduce crushed blacks and over-sharp engraved ground contrast; make the river slightly lighter muted blue-gray with softer reflection and less sharply cut banks; reduce conductor and tower contrast/glow to IMAGE 1's subtler cyan and amber; match IMAGE 1's exact top-bar segment widths and breathing room. The required four-stage event timeline must remain visible, but make it a compact semi-opaque overlay no larger than the normal target strip, positioned over the extreme bottom-center and revealing the lower grid context around it; use short icon-led Korean labels. Do not enlarge it and do not change any underlying world geometry. Keep the right comparison cards and actions exactly proportioned. Newly render all pixels, no direct pasted source pixels or exact source text. No watermark, no extra panels, no large labels, no mobile/720p layout, no flat vectors, no perspective horizon.
```

## PROMPT-HEAT-V2

Inputs were `assets/02-heatwave-outage.png` first and `normal-first-light-v4-source.png` second.

```text
Create a completely newly rendered Korean GRIDWORKS extreme-heat/protective-outage screenshot at 1672x941. IMAGE 1 is the strict spatial and state template: preserve its exact low orthographic isometric camera, crop, district positions and occupied/open balance, diagonal nearly dry river course and exposed banks, huge upper-right amber sun, warm dust haze, all facility and pylon relative sizes, the red/orange failed route and unavailable marks, the surviving cyan route, broad service fields, and exact top/left/right HUD footprints. Reproduce IMAGE 1's dark weathered industrial materials, brown-gray heat-baked terrain, sparse amber lights, haze, and readable midtones with newly rendered pixels; do not paste exact pixels or text. IMAGE 2 may inform only the compact Korean UI typography and four-stage event-strip language. Replace IMAGE 1's bottom playback strip inside the exact same width/height/x-position footprint with four tiny event-stage cells: briefing, heat phase active, outage result, next; use the same heavy blackened-metal chrome and cyan/amber/red accents, with short icon-led Korean labels and no large numerals. Keep the inspector compact and make the outage visually unmistakable without adding panels. Do not change geography, camera, landmark scale, river state, route geometry, overlay proportions, or light direction from IMAGE 1. No watermark, no mobile layout, no 720p, no flat vector style, no large text, no perspective horizon.
```

## PROMPT-SITING-V2

Inputs were `assets/04-plant-siting.png` first and `normal-first-light-v4-source.png` second.

```text
Create a completely newly rendered Korean GRIDWORKS plant-siting decision screenshot at 1672x941. IMAGE 1 is the strict spatial template: preserve its exact low orthographic isometric camera, crop, river-valley course and bank depth, top-left and top-right large plant sites, lower-right foreground substation/industrial mass, pylon positions, orange source-to-site measurement lines, blue service fields, occupied/open balance, weathered graphite-and-soil materials, cyan/amber lighting, and exact top/left/right HUD footprints. Do not reinterpret the scene layout or make plants into small icons. Newly render all pixels and short Korean interface text; do not paste exact source pixels or exact text. IMAGE 2 may inform only compact Korean typography and the four-stage event-strip language. Replace IMAGE 1's bottom playback strip within the exact same width/height/x-position footprint with four tiny event cells: briefing, site selection active, approval result, next. Use the same heavy blackened-metal chrome, icon scale, spacing, brass/cyan accents, and very short labels. Keep the right siting inspector compact, with concise site facts and two actions in the same footprint as IMAGE 1. Preserve camera, geography, plant scale, route/measurement geometry, HUD overlay ratios, and lighting direction exactly. No watermark, no extra panels, no large text, no mobile or 720p layout, no flat vector art, no perspective horizon.
```

## PROMPT-SITING-V4

Inputs were `assets/04-plant-siting.png` first and `plant-siting-v3-source.png` second.

```text
Revise IMAGE 2 with a single constrained visual correction: make the entire river and its banks match IMAGE 1's light muted blue-gray water value, soft atmospheric haze, low-contrast reflections, softened edge definition, and gray-brown bank depth exactly. Preserve IMAGE 2's river centerline, width, S-curves, bridge locations, and every other pixel-level design decision as closely as possible. Do not change camera, crop, terrain masses, buildings, plants, pylons, routes, distance bands, material values outside the immediate river/bank corridor, top bar, left rail, right inspector, Korean text, or compact bottom event timeline. Newly render the corrected image rather than pasting exact source pixels. No new objects, panels, labels, or effects. No watermark, no mobile layout, no perspective horizon.
```

## PROMPT-NORMAL-V4

Inputs were `assets/01-grid-construction.png` first and `normal-first-light-v3-source.png` second.

```text
Create a newly rendered revision of IMAGE 2 that preserves its current camera, crop, district positions, landmark scale, network topology, and overall UI geometry, while applying only the following five parity corrections from IMAGE 1. (1) Density/ground: replace the candidate's uniformly embossed black open ground with IMAGE 1's irregular fine rubble, road fragments, scattered utility clutter, soot, and brown-gray soil variation, especially across the central corridor; retain the same occupied/open balance. (2) River: keep the exact course and crossings but make the water slightly broader, more muted slate-blue, with rougher irregular rubble banks, greater visible bank depth, softer reflections, and fewer smooth clean silhouettes like IMAGE 1. (3) Material/grid: lift brown-gray midtones slightly and soften the cyan conductor saturation, edge crispness, glow, and bends to match IMAGE 1; keep cyan and amber routes fully readable. (4) HUD typography: keep all panel footprints exactly, but reduce Korean text density by using icons, numbers, and only very short labels; match IMAGE 1's visual breathing room. (5) Timeline: shrink the centered bottom event strip to exactly the same width, height, x-position, icon scale, and spacing footprint as IMAGE 1's compact bottom strip (approximately one third of the canvas, not the wider IMAGE 2 strip). Keep four event stages but remove large numerals and use tiny icon-led cells with only four very short Korean labels; second cell cyan-active. Do not change the isometric projection, district layout, object proportions, plant positions, pylon positions, or overlay locations. Newly render the result; do not paste exact source pixels or exact text. No watermark, no new panels, no large labels, no mobile layout, no perspective horizon.
```

## PROMPT-NORMAL-V3

Inputs were `assets/01-grid-construction.png` first and `normal-first-light-v2-source.png` second.

```text
Redraw a new production-quality Korean GRIDWORKS game screenshot at the same 1672x941 composition as IMAGE 1. IMAGE 1 is the strict spatial template and must dominate: preserve its exact low orthographic isometric camera, crop, map footprint, diagonal ground axes, relative landmark sizes, left-top plant mass, lower-left industrial mass, central substation, lower-right dense residential mass, upper-right rocky negative space, narrow oblique river course and width, bridge/crossing positions, cyan service fields, pylon counts, cyan and dashed amber route geometry, and the exact proportional footprints of the top bar, left tool rail, right inspector, and compact centered bottom strip. Do not reinterpret it as a different city layout. Newly render every surface and object in the same graphite steel, weathered concrete, soot, mist, muted cyan, sparse amber-light visual language; do not paste exact source pixels or exact text. IMAGE 2 may inform only cleaner Korean labeling and event-stage icon design; do not inherit its vertical river, city geography, or larger UI. Keep the right inspector as compact as IMAGE 1. Inside the same compact bottom-strip footprint from IMAGE 1, replace the playback semantics with four tiny visually restrained event-stage cells—briefing, current construction, result, next—using the same blackened-metal chrome, icon scale, spacing, and cyan selection language; no wider or taller timeline. Preserve IMAGE 1's subtle blue haze and readable midtones. No watermark, no mobile layout, no flat vector art, no large text, no extra UI panels, no perspective horizon. This is a non-runtime visual contract mockup; make it look like the same shipped product screen as IMAGE 1 while being newly rendered.
```

## PROMPT-NORMAL-V2

Inputs were `assets/01-grid-construction.png` first and `normal-first-light-v1-source.png` second.
The first `gpt-5.6-sol` ultra formative call on v1 reported camera/scale/material/grid `CLOSE`,
density/river/HUD `RELATED`, and timeline `WEAK`; v2 responds to those visible gaps without using target
pixels at runtime.

```text
Create a completely redrawn, production-quality 1920x1080 Korean PC strategy-game screenshot for GRIDWORKS. IMAGE 1 is the dominant visual reference: match its low orthographic 2:1 isometric camera, yaw/pitch, overall framing, three separated operational mass clusters, large but purposeful patches of smoky rocky ground, narrow diagonal meandering creek entering near the left/lower edge, object scale ladder, muted blue-gray haze, blackened iron/concrete material, sparse amber windows, cyan energized conductors, amber planned conductors, compact top/left/right HUD proportions. IMAGE 2 is secondary and must be used only for Korean product identity and the idea of a real event-stage timeline. Correct IMAGE 2's failures: do not make a broad vertical river, do not fill the entire map with continuous buildings, do not use an over-tall right inspector, and do not use a full-width oversized timeline. Recompose very close to IMAGE 1: a dominant weathered thermal plant in the upper-left cluster; a mid-sized substation cluster near center; a residential/industry cluster in the lower-right; readable lattice pylons and connected routes crossing open ground; a narrow irregular creek with rocky banks, subtle reflections, and one bridge/foundation; at least two clear cyan energized paths plus a restrained dashed amber planned path. UI: thin segmented black/brass top bar; compact left build palette; compact right inspector occupying roughly the upper 40 percent of the right edge with one facility portrait, capacity strip, and two buttons; leave the lower-right mostly map. Add a compact independent five-step event timeline centered along the bottom, approximately the same footprint and chrome language as IMAGE 1's bottom playback control, with tiny distinct briefing/current construction/energized/result/next nodes and a short progress segment—it must read as event stages but visually integrate with the reference instead of becoming a wide dashboard. Korean labels should be short and clean; prioritize coherent visual hierarchy over exact text. No photorealism, no flat vector style, no giant labels, no watermark. Keep all visual content newly rendered rather than copying exact pixels or exact UI text from either input.
```

## PROMPT-NORMAL-V1

```text
Use case: ui-mockup
Asset type: non-runtime 1920×1080 Gridworks game target mockup, normal first-light construction state
Input images: Images 1-4 are the authoritative visual-style, camera, material, density, river, power-grid, and industrial HUD references. Image 5 is the current implementation and is only a functional/layout inventory reference; do not copy its flat orthographic look, sparse terrain, tiny objects, boxed map, circular footprints, or long labels.
Primary request: redesign the current game as a shippable, highly polished 2:1 isometric industrial power-grid strategy screen that is as visually close as possible to Images 1-4 while preserving the game's first-light construction function.
Scene/backdrop: full-bleed dense nocturnal industrial city map with a strongly winding river and dimensional banks crossing the city, roads, residential blocks, hospital, factories, yards, bridge foundations and power facilities; almost no empty black land.
Subject: large western power plant, realistic substations and tall lattice utility poles connected by dual conductors; energized conductors cyan and planned construction amber; the built network crosses the river through authored foundations and reaches an eastern residential district.
Style/medium: premium rendered 2D game UI, low 3/4 classic 2:1 isometric orthographic view, exactly the same graphite steel, weathered concrete, soot-dark surfaces, tiny amber work lights, cyan electrical glow, realistic scale and dense texture language as Images 1-4.
Composition/framing: native 16:9 1920×1080; map fills the entire canvas behind HUD; compact 80px industrial top metrics bar, narrow left construction tool rail, 340px floating right inspector, clearly independent 128px bottom event timeline bar with several briefing→construction→result steps; city remains visible beneath overlay panels.
Lighting/mood: cool night with readable midtones, upper-right key light, cyan energized glow and small amber construction lights; not crushed black.
Text: Korean interface typography may be representative, but keep it sparse, aligned, and readable; emphasize visual hierarchy rather than paragraphs.
Constraints: generate a wholly new target mockup; no direct pasted reference pixels; no whole-map plate intended for runtime; no visible square grid; no perspective convergence; no circular footprint rings except a subtle selected-object ground ellipse; no floating long map labels; no checkerboard; no logos; no watermark; no mobile or 720p layout.
```

## PROMPT-HEAT-V1

```text
Use case: ui-mockup
Asset type: non-runtime 1920×1080 Gridworks game target mockup, heatwave and protective-outage state
Input images: Images 1-4 are authoritative style and state references; Image 5 is the newly generated normal-state target and is the required same-city composition, camera, object-design, HUD, and timeline continuity anchor.
Primary request: create the same shippable 2:1 isometric Gridworks screen during extreme heat, visually matching Image 2 especially closely while preserving Image 5's exact city identity and industrial UI system.
Scene/backdrop: same full-bleed dense industrial city and strongly winding river; heat has lowered the water level and exposed dimensional mud, stone, and bank shelves without turning the river into a flat polygon; warm dry ground haze across the same roads, buildings, hospital and facilities.
Subject: same large plant, substations and tall lattice poles; one authored route or tower is unavailable/protectively out with orange-red broken conductor and warning pattern, while the surviving route glows cyan and visibly supplies the hospital/residential district; retain dual conductors and realistic tower attachment.
Style/medium: premium rendered 2D game UI, classic low 3/4 2:1 isometric orthographic view; graphite steel and weathered concrete from Images 1-4 with intense but readable heat lighting.
Composition/framing: native 16:9; map fills the entire screen beneath the same compact top HUD, left tool rail, floating right inspector, and independent 128px bottom event timeline showing briefing→heat phase→protective outage→result.
Lighting/mood: harsh upper-right amber sun, dusty warm haze, small amber lamps, bright cyan surviving grid, orange-red outage; preserve readable midtones and material texture.
Text: sparse representative Korean UI, aligned and readable; no paragraphs baked into the map.
Constraints: wholly new target mockup, no direct pasted reference pixels; preserve Image 5's camera and broad city geography; no visible square grid; no flat straight river; no huge empty terrain; no tiny icon-scale facilities; no circular footprint rings; no long floating map labels; no perspective convergence; no checkerboard; no watermark; no 720p/mobile layout.
```

## PROMPT-ROUTE-SITING-V1

```text
Use case: ui-mockup
Asset type: non-runtime 1920×1080 Gridworks game target mockup, route and source-siting comparison state
Input images: Images 1-4 are authoritative visual references, with Images 3 and 4 dominant for route comparison, plant scale, valley depth, winding river, distance overlays and right inspector. Image 5 is the required normal-state city, camera, material and HUD continuity anchor.
Primary request: create a shippable route-and-plant-siting decision screen in the same Gridworks visual system, as close as possible to Images 3 and 4 while retaining the same city identity as Image 5.
Scene/backdrop: full-bleed dense low-3/4 isometric industrial city across a deep winding river valley; large foreground existing plant and switchyard, hospital and dense eastern district, roads and yards, two credible candidate source sites at different distances.
Subject: two clearly non-overlapping candidate power routes, Route A amber and Route B cyan, each made of tall properly scaled lattice poles and dual conductors with attachment and shallow sag; one route is shorter but risky near the dense district, the other crosses the river/valley through authored foundations and is resilient; large candidate plant silhouettes are visibly comparable to the existing plant rather than tiny icons.
Style/medium: premium rendered 2D industrial strategy UI, classic 2:1 isometric orthographic camera, same graphite steel, weathered concrete, terrain depth, amber lamps, cyan electrical glow and dense city texture as Images 1-4.
Composition/framing: native 16:9; map fills canvas behind compact top metrics, narrow left build rail, a floating right route-comparison inspector with two stacked concise cards, and an independent 128px bottom event timeline with briefing→site selection→route approval→construction→result.
Lighting/mood: cool dusk/night, readable midtones, upper-right key light, cyan and amber routes dominate without hiding terrain.
Text: sparse representative Korean UI; route cards should be visually readable with distance, cost, time, reserve and protection rows but no long paragraphs.
Constraints: wholly new target mockup; no direct pasted reference pixels; preserve Image 5 style and 2:1 camera; no visible square grid; no straight flat river; no empty black map; no tiny facility icons; no circular footprint rings; no floating long labels; no perspective convergence; no checkerboard; no logos; no watermark; no 720p/mobile layout.
```
