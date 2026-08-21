# G.3 target mockup prompts

All three images used the Codex built-in `image_gen` tool. The four files under `assets/` were style,
camera, composition and state references. Generated target pixels are planning evidence only and are not
runtime assets.

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
