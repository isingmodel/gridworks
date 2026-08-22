# Commercial G.3 individual runtime assets

G.3 keeps every object and tile as a separately generated/runtime-bound file. No reference screenshot,
target mockup, whole-map plate, atlas crop, or screenshot pixel is shipped as scenery. This ledger records
the exact ImageGen source, extraction, and final runtime hash for each added asset.

Current package authority is stricter than the historical entries below: exactly 29 PNGs remain under
`game/art/commercial/g3`, and every one is scene-bound. The 23 superseded composite/unbound outputs were
moved to `playtests/commercial-2d/g3-rejected-composites/`; their entries are provenance only and any old
`g3/...` path in those historical sections is not a current runtime path. The accepted Step 1 city kit is
the 12-row atomic table below plus six atomic road tiles and explicit placement records.

## `g3/tiles/ground-rubble-relief-c.png`

- built-in ImageGen source run: `exec-d9df6212-3e34-41bb-8e04-6de02979e81c`
- preserved source: `playtests/commercial-2d/g3-runtime-sources/ground-rubble-relief-c-source.png`
- inputs: `assets/01-grid-construction.png`
- source SHA-256: `26ccba0019d2702f454d78085cfd95366ce1d7bfb625bf7ae7f253795e56248c`
- runtime processing: deterministic 1254x1254 to 1024x1024 downscale; fully opaque material tile.
- runtime class: one repeated ground material tile, never a map plate, scene, or district.
- final SHA-256: `9dbd83fdc9300077db9643c4671623e8464b8ad7b47c9698d24e79247b11aaf1`

```text
Create exactly one production-ready seamless GROUND MATERIAL TILE for the Gridworks industrial isometric strategy map, using the attached reference as the strict visual target. This is a straight-on square material source that will be mapped into 2:1 isometric ground diamonds by the game, not a scene and not an isometric object. Match the reference ground: embossed soot-black compacted earth, fractured charcoal rock, weathered dark concrete fragments, subtle tire and cable scars, restrained warm brown/bronze midtones, sparse tiny amber mineral and work-light flecks, crisp painterly relief with strong local depth but no photographic noise. The texture must remain readable when downscaled and must avoid large isolated boulders or obvious repeated motifs. Seamless and tileable on all four edges, full-bleed opaque surface, uniform lighting from the upper-right implied only through micro-relief. Exactly one tile only. No buildings, no houses, no plant, no pylon, no substation, no road lanes, no river, no water, no bridge, no UI, no text, no labels, no border, no transparent padding, no checkerboard, no map composition, no complete district. Square high-resolution PNG, 1024x1024 or larger.
```

## `g3/objects/continuous-worker-city-parcel-h.png`

- built-in ImageGen initial source run: `exec-6e17388b-8ac2-4c79-8e50-e174f17a304a`
- preserved initial source: `playtests/commercial-2d/g3-runtime-sources/continuous-worker-city-parcel-h-source.png`,
  SHA-256 `926a5d4c65c3b73fa64c8f2055a5a730c47160717fcdd7b61a8204485498664f`
- built-in ImageGen edge-removal revision run: `exec-3d73a023-91a6-4070-a7ff-e405610445a5`
- preserved revision source: `playtests/commercial-2d/g3-runtime-sources/continuous-worker-city-parcel-h-revision-source.png`
- built-in ImageGen dark-edge revision run: `exec-2899fa1a-40f7-499c-af04-242e03b8976d`
- preserved dark-edge source: `playtests/commercial-2d/g3-runtime-sources/continuous-worker-city-parcel-h-dark-edge-source.png`
- inputs: `assets/01-grid-construction.png`
- edge-removal source SHA-256: `94d89ecd24cc4e18771f498075431fb2d8944d619573b3a1769e6e61413cc6dc`
- dark-edge source SHA-256: `963c95ebe4011c177cfb581dc237db93b7810bfe0288ff5038cc03def4f16c36`
- deterministic background-connected neutral-matte extraction (`--connected-matte-floor 110`)
  proves 45.61% fully transparent runtime pixels and alpha-zero corners while preserving disconnected
  facility highlights and removing the light mipmap fringe.
- disposition: **REJECTED / NOT RUNTIME-BOUND**. It contains many buildings and roads in one raster and
  violates G.3 Step 1's one composition unit per PNG rule. Preserved only as failed provenance.
- final SHA-256: `7017240bb759763814e5f5a1823e5a9c5ed6858ed5baf9040df76aa565ad4d68`

```text
Create exactly ONE production-ready isolated isometric runtime object: a large continuous eastern worker-city parcel H for the Gridworks power-grid strategy map. Match the attached reference extremely closely in classic 2:1 orthographic isometric projection, yaw 45 degrees, pitch 35.264 degrees, upper-right dim key light, soot-black roofs, graphite masonry, weathered dark concrete, embossed painterly relief, readable blue-gray midtones, deep contact shadows, and many restrained tiny amber windows. Composition: one irregular organic district mass, approximately 2.5:1 footprint, containing 38–48 individually readable small worker houses and shopfronts on a CONTINUOUS network of narrow diagonal streets, two small plazas, sheds, retaining walls, drains, cable reels, rubble seams, and a few low utility poles with NO overhead conductors. The streets and houses must flow continuously across the parcel rather than appearing as repeated square cells or separated stamps. The outer silhouette must be stepped and asymmetrical with several notches; absolutely no rectangular, diamond, green, cyan, or gray foundation plate and no perimeter outline. Houses remain much smaller than a substation or transmission pylon. One independently placeable object only, not a whole city map, not a background, not a tile, not an atlas. Place the centered object on a perfectly uniform pure white background with generous clearance for deterministic background removal. No river, water, bank, bridge, hospital, plant, substation, lattice tower, overhead conductor, cyan/amber route overlay, selection/service radius, HUD, UI, text, labels, border, watermark, people, trees, or vivid vegetation. High-resolution square PNG.
```

Revision prompt:

```text
EDIT the FIRST attached image as the target runtime object. Preserve its continuous network of small soot-dark worker houses, diagonal streets, density, classic 2:1 orthographic isometric camera, and amber windows, while matching the SECOND attached Gridworks reference more closely. Remove the entire raised slab/platform/perimeter curb and every continuous rectangular or diamond outer edge. The revised district must NOT sit on a single foundation plate. Rebuild the outside 15–20 percent as a highly irregular feathered city edge: staggered house rows, short road stubs, broken alleys, isolated sheds, rubble fans, cable reels, drains, scattered dark stones, and exposed soil fingers ending at different depths, with at least eight deep asymmetrical notches. No straight boundary may continue for more than one small-house width. Keep streets continuous through the center and make the occupied mass more densely interlocked, but ensure the silhouette organically dissolves into the outside background rather than forming a bounded parcel. Exactly ONE independently placeable city object, not a map, not a background, not a tile, not an atlas. Center it on a perfectly uniform pure white background with generous clearance for deterministic background removal. Preserve upper-right dim lighting, graphite roofs, blackened masonry, blue-gray midtones, painterly embossed relief, and tiny restrained amber windows. No green/cyan/gray plate, no perimeter outline, no river, water, bank, bridge, plant, substation, transmission pylon, overhead conductors, route overlay, selection radius, HUD, UI, text, labels, people, vegetation, checkerboard, border, or watermark. High-resolution square PNG.
```

Dark-edge revision prompt:

```text
EDIT the FIRST attached city object as the target. Preserve its classic 2:1 orthographic isometric camera, continuous diagonal street network, dark worker houses, irregular notched silhouette, and transparent-isolation composition. Correct one production defect: the outer rubble/soil fingers and every exposed edge fragment must be SOOT-BLACK, charcoal, dark graphite, or muted dark brown exactly like the SECOND reference's terrain. Remove every pale gray, white, silver, light concrete, snow-like, chalk-like, or glowing perimeter fragment. No bright outline, no light halo, no rim lighting around the outer silhouette, and no raised slab edge. The brightest outer-edge pixel should remain a restrained dark blue-gray midtone; amber is allowed only in tiny windows or lamps well inside the silhouette. Keep the outer edge deeply irregular with at least eight notches, short road stubs, staggered buildings, near-black rubble fans, and dark soil fingers ending at varied depths. Increase the central occupied density slightly by adding small attached sheds and row-house infill, without creating repeated square cells. Exactly ONE independent runtime city object, never a map/background/tile/atlas. Center it on a perfectly uniform pure white background with generous clearance for deterministic background removal; the white must stop cleanly at the dark antialiased silhouette and must not reflect into it. Preserve upper-right dim light, embossed painterly relief, blackened roofs, graphite masonry, blue-gray midtones, and restrained tiny amber windows. No plate, perimeter curb, river, water, bridge, plant, substation, pylon, conductors, route overlay, selection radius, HUD, UI, text, labels, people, vegetation, checkerboard, border, watermark, or scene shadow outside the footprint. High-resolution square PNG.
```

## `g3/objects/tall-thermal-power-station-b.png`

- built-in ImageGen source run: `exec-a2f2f54b-a7e6-4c3d-9a92-e1bc0a78a6ca`
- preserved source: `playtests/commercial-2d/g3-runtime-sources/tall-thermal-power-station-b-source.png`
- inputs: `assets/01-grid-construction.png`
- source SHA-256: `dc27e57351374c218449e00f555f1cf7d813a150b3283d11c778a37a609f89e0`
- deterministic neutral-matte extraction proves 58.97% fully transparent runtime pixels and alpha-zero corners.
- runtime class: one individually placed source-landmark object.
- final SHA-256: `5c2a68b5c396e46e2cca12fc8fe3bb63a7f2106711c2ddcbfc2aaab0db191023`

```text
Create exactly ONE production-ready isolated isometric runtime object: a tall soot-dark thermal power station B for the Gridworks power-grid strategy map. Use the attached reference as the strict target for camera, scale language, material, lighting, and detail. Exact classic 2:1 orthographic isometric projection, yaw 45 degrees, pitch 35.264 degrees, no perspective convergence, upper-right dim key light, deep graphite shadows, weathered charcoal concrete, blackened steel, readable blue-gray midtones, crisp embossed hand-painted relief, and restrained tiny amber work lights. Composition: a compact stepped industrial footprint about 1.6:1 wide with one dense turbine hall, boiler house, pipe racks, small tanks, transformers, cable reels, coal-dark service apron, and TWO prominent slender smokestacks that rise roughly 1.2 times the visible base height. The twin stacks must be the dominant vertical silhouette, separated and readable, with muted rust/cream safety bands and only a very faint short soot plume contained close to each stack. Add layered roofs and vertical depth so the facility feels tall and substantial rather than shallow or miniature. One independently placeable object only, not a whole industrial district, not a background, not a tile, not an atlas. Place the centered complete object on a perfectly uniform pure white background with generous clearance for deterministic background removal. No river, water, bank, bridge, houses, hospital, substation, transmission pylon, overhead conductor, cyan or amber route overlay, selection/service radius, HUD, UI, text, labels, border, watermark, people, trees, vivid vegetation, checkerboard, or scene shadow outside the footprint. High-resolution square PNG.
```

## `g3/objects/chunky-switching-substation-b.png`

- built-in ImageGen source run: `exec-f779a95f-8aab-48e8-bcbf-0158dd2f93f5`
- preserved source: `playtests/commercial-2d/g3-runtime-sources/chunky-switching-substation-b-source.png`
- inputs: `assets/01-grid-construction.png`, `assets/03-route-comparison.png`
- source SHA-256: `65455c398fa19d21a515d7497090af971c8d7f66552c763f0237f28bf2c49486`
- deterministic neutral-matte extraction proves 60.09% fully transparent runtime pixels and alpha-zero corners.
- runtime class: one individually placed/drafted substation node-class object.
- final SHA-256: `05efe18d597276c1bb218e7b4ff087f1a9420f0ddd421b4741884dfe28e3245f`

```text
Create exactly ONE production-ready isolated isometric runtime object: a chunky high-voltage switching substation B for the Gridworks power-grid strategy map. Match the attached references extremely closely in classic 2:1 orthographic isometric projection, yaw 45 degrees, pitch 35.264 degrees, no perspective convergence, upper-right dim key light, soot-black steel, graphite concrete, weathered porcelain, readable cool blue-gray midtones, fine embossed painterly relief, deep contact shadows, and only a few restrained amber status lights. Composition: one compact but substantial 1.7:1 stepped diamond footprint with two large transformers, six tall bus gantries, visibly thick busbars, breakers, ceramic insulator banks, control hut, cable trenches, oil containment curb, grounding hardware, and FOUR clear conductor attachment portals at the outer corners. The upper gantries and insulators must rise prominently so the object has vertical depth; it must read larger and chunkier than a house but smaller than the tall power station. Preserve a clean dark concrete/rock footprint only directly beneath equipment, with an irregular stepped silhouette and no broad green or cyan plate. One independently placeable object only, not a district, not a background, not a tile, not an atlas. Place the centered complete object on a perfectly uniform pure white background with generous clearance for deterministic background removal. No power plant, smokestack, houses, hospital, river, water, bank, bridge, transmission tower, overhead conductors extending outside the equipment, cyan/amber route overlay, selection/service radius, HUD, UI, text, labels, border, watermark, people, trees, vegetation, checkerboard, or scene shadow outside the footprint. High-resolution square PNG.
```

## `g3/objects/river-current-reflection-a.png`

- built-in ImageGen source run: `exec-f9f60f8e-d2a3-4028-9e7b-40369049c2df`
- preserved source: `playtests/commercial-2d/g3-runtime-sources/river-current-reflection-a-source.png`
- inputs: `assets/01-grid-construction.png`, `assets/04-plant-siting.png`
- source SHA-256: `3a61c7636681863b3a2fdc563970a1c46ef21e08c1eb44eb2ebe0b61d099a62c`
- deterministic neutral-matte extraction proves 92.92% fully transparent runtime pixels and alpha-zero corners.
- runtime class: one individually placed surface-current overlay; code/data continue to own river geometry.
- final SHA-256: `966add7347d8a7196db9495dffc84581ad198d4dc08bd652a5b066c28e87274b`

```text
Create exactly ONE production-ready isolated 2D runtime overlay object: a narrow river current-and-reflection streak cluster A for the Gridworks industrial isometric strategy map. Use the attached references as the strict target for their deep blue-black river, sparse slate/silver-blue reflection, understated flow, embossed painterly detail, and dark industrial atmosphere. This is NOT a river shape, not a water tile, and not scenery: it is one independently placeable transparent overlay cluster that the game will place on top of its code-defined river channel. Exact classic 2:1 isometric screen-plane appearance consistent with yaw 45 degrees and pitch 35.264 degrees. Composition: a long slightly curved irregular cluster approximately 4.2:1 wide, made of 14–22 separated medium-dark silver-blue broken current strokes, two soft cool reflection patches, three short foam flecks, and several tiny dim glints. Keep large gaps of empty space between marks; no continuous outer silhouette, no border, no rectangle, no diamond, no bank, no soil, no rocks. The strokes must be visible after downscaling but restrained, with softened painterly edges and varied length; avoid neon cyan, bright white, metallic chrome, ocean waves, concentric ripples, or a flat stripe. Place the complete centered overlay on a perfectly uniform pure white background with generous clearance for deterministic background removal. No complete river, no water background, no bridge, no building, no plant, no substation, no pylon, no conductor, no route overlay, no selection radius, no HUD, no UI, no text, no labels, no watermark, no people, no checkerboard, no scene shadow. High-resolution square PNG.
```

## `g3/objects/river-bank-rock-segment-a.png`

- accepted built-in ImageGen revision run: `exec-e9f4465a-4bfc-45ef-ae58-a0fed72d3822`
- preserved source: `playtests/commercial-2d/g3-runtime-sources/river-bank-rock-segment-a-v27-source.png`
- inputs: the previous atomic `river-bank-rock-segment-a.png` as object/camera target and
  `assets/04-plant-siting.png` as style-only reference.
- source and runtime SHA-256: `68e47a2a142f150b90c4d91474b08c3d430f98fd11c1d401ed82375e49feef9a`
- direct RGBA inspection proves 76.10% fully transparent pixels, alpha range 0–255, and
  alpha-zero corners; no matte extraction or scene crop was used.
- runtime class: one short individually placed bank-rock segment; repeated placement follows the
  authoritative river banks and never supplies the river geometry or a whole-map background.
- rejected predecessor retained in git history: `exec-f67eadea-97a1-4583-8122-2c735dad1979`;
  it needed checkerboard extraction and had a flatter rectangular slab silhouette.

```text
Create one new atomic game object variant: a single short wet rocky river-bank ledge segment on a true transparent background. IMAGE 1 is the exact object, camera, footprint, and isolation target: preserve its steep 3/4 isometric angle and long narrow placement footprint, but do not copy its rectangular slab or checkerboard. IMAGE 2 is style guidance only: match its charcoal-brown painterly industrial valley, softly blended soil, recessed river-bank depth, muted sepia midtones, and restrained wet highlights. The object must contain only one continuous bank ledge segment: broken dark basalt stones at the outer side, a low eroded soil shelf tapering naturally on all ends, subtle moisture at the inner river edge. No water surface, no river, no bridge, no city, no road, no power infrastructure, no UI, no labels, no scenery, no base tile, no shadow rectangle. Orthographic steep isometric 3/4 view, directional light from upper left, softened painterly texture, readable at about 48 pixels. Real alpha transparency all around irregular silhouette; no white, gray, checkerboard, or colored background. Centered with generous transparent padding, 1:1 square PNG.
```

## `g3/objects/irregular-residential-parcel-c.png`

- built-in ImageGen source run: `exec-3286fc30-4dcd-42f6-8503-57ea7d33e426`
- preserved source: `playtests/commercial-2d/g3-runtime-sources/irregular-residential-parcel-c-source.png`,
  SHA-256 `e0d19f6d32624966e68840d0fab015c0f62ea2b2656884818af2b0fd47c9f79e`
- inputs: `assets/01-grid-construction.png`
- runtime processing: deterministic 1774×887 to 1024×512 downscale; source transparency preserved.
- runtime class: one individually placed irregular residential/workshop parcel, never a map plate.
- runtime SHA-256: `366c43e573b4d8e4424d1af594ccd33453f759c50c86d39eae73a934baef96b0`

```text
Create one isolated game-production sprite asset: an irregular dense workers' residential parcel for a dark industrial power-grid strategy game, visually matching assets/01-grid-construction.png as closely as possible. Fixed 2:1 isometric orthographic view, camera yaw 45 degrees, steep pitch about 35 degrees, no perspective convergence. The parcel contains 9 to 13 distinct compact soot-dark row houses and workshops of varied footprint and roof direction, two narrow diagonal-compatible paved lanes, tiny utility sheds, rubble piles, cables and warm amber window pinlights. Cohesive blackened iron, charcoal slate, worn concrete, brown soil, restrained blue-gray midtones, crisp hand-painted high-detail game art, heavy but readable silhouettes. Irregular outer footprint, not a square repeated grid, no large base plate. Transparent background with clean alpha around every silhouette; no scenery outside the parcel, no UI, no border, no text, no labels, no power pylons, no substations, no river, no people. Centered object, generous transparent margin, lighting from upper left consistent with the reference.
```

## `g3/tiles/ground-rubble-mix-b.png`

- built-in ImageGen source run: `exec-8cd425fe-9341-4ccb-9e4b-350b367bb72b`
- preserved source: `playtests/commercial-2d/g3-runtime-sources/ground-rubble-mix-b-source.png`,
  SHA-256 `115bba8592cc063d739610f43ab3183db9ceb8234dfe100ed46f6e403d4fdd51`
- inputs: `assets/01-grid-construction.png`
- runtime processing: deterministic downscale to 1024×1024; fully opaque material tile.
- runtime class: one seamless repeated ground material tile, never a map plate or parcel scene.
- runtime SHA-256: `436fde50506ccb7758bb70abd6d2a8301bd4ba9ff5e0db2a0075e3b88cf80ee3`

```text
Create one production-ready seamless square ground MATERIAL TILE for a dark industrial isometric power-grid strategy game, closely matching the ground in assets/01-grid-construction.png. This is not a map, scene, parcel, or plate: it is one edge-to-edge repeatable material texture viewed straight-on for runtime tiling. Dense irregular mixture of soot-black compacted soil, graphite rubble, broken weathered concrete fragments, shallow service-road scars, buried cable grooves, tiny drain grates, scattered dark stones, rare muted rust flecks and sparse tiny amber glints. Strong cohesive sculpted midtone relief but no large recognizable object, no repeating square blocks, no regular brick grid, no dominant straight seam, no diamond border. Seamless on every edge with features crossing edges naturally; macro variation distributed evenly so repetition is hard to detect. Charcoal/blackened metal/weathered concrete palette, restrained blue-gray fill, upper-left relief lighting consistent with the reference. Fully opaque, no transparency, no chroma key, no buildings, no roads forming a map, no river, no power equipment, no UI, no text, no symbols, no border, no watermark. High-resolution square PNG.
```

## `g3/objects/river-bank-inner-bend-a.png`

- built-in ImageGen source run: `exec-c9f2a9d6-e69c-4f15-b85b-50d24b96ba54`
- preserved source: `playtests/commercial-2d/g3-runtime-sources/river-bank-inner-bend-a-source.png`
- inputs: `assets/01-grid-construction.png`
- source SHA-256: `970674f72e3d90927e7f58f32b533b60ffbca40de79db7f2df60dd8962d3b24d`
- deterministic checkerboard extraction: 69.45% fully transparent pixels, alpha-zero corners.
- final SHA-256: `df5ea949041197a3e1cbcec0c18ad043bed68e06170be66e89a397788a0da587`

```text
Using the attached Gridworks screenshot only as the strict camera, material, and lighting reference, create ONE isolated reusable INNER-BEND RIVERBANK object for a 2D isometric strategy game. Match the reference's exact classic 2:1 orthographic isometric angle, yaw 45 degrees, pitch 35.264 degrees, upper-right key light, soot-black graphite rock, wet dark soil, broken concrete, subtle cool reflected light, sparse muted amber mineral glints, embossed painterly relief, and dense premium strategy-game detail. Geometry: a compact concave 70-degree inner river bend, like an L-shaped rocky bank corner with a smooth inside arc; land occupies the outside of the bend, while the empty river channel wraps around the concave inner edge. Show a clear dark vertical bank drop on the water-facing concave edge and a rough soil transition on the land-facing edge. It must connect visually to straight bank segments at both ends. No water surface, no complete river, no bridge, no buildings, no pylons, no cables, no roads, no UI, no text, no icons, no checkerboard, no green screen, no colored matte, no scene background. Output exactly one centered object with generous padding and a genuinely transparent RGBA background to every canvas edge, clean antialiased silhouette, no white/green halo, no baked scene shadow outside the footprint.
```

## `g3/objects/river-bank-outer-bend-a.png`

- built-in ImageGen source run: `exec-f4e1cbc5-eeef-4f8e-95ac-168bad8e197c`
- preserved source: `playtests/commercial-2d/g3-runtime-sources/river-bank-outer-bend-a-source.png`
- inputs: `assets/01-grid-construction.png`
- source SHA-256: `811cb8be0bad53f53a7a87e838fb2f8d09924458699f8874cf638946d7449fe1`
- deterministic checkerboard extraction: 62.88% fully transparent pixels, alpha-zero corners.
- final SHA-256: `0e100f4e057af9627261d64ce61c2b256faa42d172ff99cfe52b7b7f894fc01a`

```text
Using the attached Gridworks screenshot only as the strict camera, material, and lighting reference, create ONE isolated reusable OUTER-BEND RIVERBANK object for a 2D isometric strategy game. Match the reference's exact classic 2:1 orthographic isometric angle, yaw 45 degrees, pitch 35.264 degrees, upper-right key light, soot-black graphite rock, wet dark soil, broken concrete, subtle cool reflected light, sparse muted amber mineral glints, embossed painterly relief, and dense premium strategy-game detail. Geometry: a compact convex 70-degree outer river bend, a broad curved rocky shoulder whose water-facing edge forms a smooth convex arc; the land mass sits inside the wide shoulder and the empty river channel follows the outside of the curve. Show a clear dark vertical bank drop along the convex water-facing edge and a feathered rough-soil transition on the land-facing edge. It must connect visually to straight bank segments at both ends and must be visibly different from an inner-bend piece. No water surface, no complete river, no bridge, no buildings, no pylons, no cables, no roads, no UI, no text, no icons, no checkerboard, no green screen, no colored matte, no scene background. Output exactly one centered object with generous padding and a genuinely transparent RGBA background to every canvas edge, clean antialiased silhouette, no white/green halo, no baked scene shadow outside the footprint.
```

## `g3/objects/residential-cluster-a.png`

- built-in ImageGen source run: `exec-a8be7451-70ec-4b09-ad40-e105f5202ab9`
- preserved source: `/Users/fred/.codex/generated_images/01a02155-55e1-7ef1-a3a4-a96b0ee32134/exec-a8be7451-70ec-4b09-ad40-e105f5202ab9.png`
- inputs: `assets/01-grid-construction.png`, `game/art/commercial/objects/facility-residential-v1.png`
- source was an opaque baked checkerboard and was not accepted directly.
- deterministic extraction: `tools/reference-parity/extract-checkerboard-alpha.py`; only the connected
  light neutral matte class is removed and the output must prove alpha-zero corners and at least 35%
  fully transparent pixels.
- final SHA-256: `3a83543a925cef4c08c5a8d32e05e78315c4d6cf037a14c9c8bde843c078bfe0`

```text
Create one production-ready transparent-background 2D game object sprite: a compact Korean riverside residential cluster for a dark industrial power-grid strategy game. Match the attached reference screenshot's exact low orthographic 2:1 isometric camera (same yaw and pitch), relative house proportions, dense soot-dark graphite roofs, weathered charcoal masonry, narrow concrete alleys, tiny warm amber windows and sparse utility clutter. The cluster must contain 5 to 7 attached and detached low houses with a stepped diamond footprint, subtle pipes, sheds and poles, reading as one individually placeable world object. Match the attached existing sprite's transparent isolation and ground-anchor convention, but make the building arrangement and silhouette distinctly different. Lighting from upper right, restrained cool blue-gray fill, readable midtones, no cyan selection glow. Fully transparent RGBA background to the canvas edges; no ground rectangle, no scenery, no UI, no text, no labels, no border, no watermark, no shadow clipped by canvas. Center the complete object with generous transparent padding, square high-resolution output.
```

## `g3/objects/central-rubble-service-corridor-a.png`

- built-in ImageGen source run: `exec-5890cc41-19b5-4f24-b29a-86e98a185953`
- preserved source: `/Users/fred/.codex/generated_images/01a02155-55e1-7ef1-a3a4-a96b0ee32134/exec-5890cc41-19b5-4f24-b29a-86e98a185953.png`
- inputs: `assets/01-grid-construction.png`
- source was an opaque baked checkerboard and was not accepted directly.
- deterministic extraction: `tools/reference-parity/extract-checkerboard-alpha.py`; output proves
  49.03% fully transparent pixels and alpha-zero corners.
- runtime class: one individually placed central rubble/service-corridor object; it contains no
  water, grid, HUD, or whole-map background.
- final SHA-256: `406eed22427bc70c0a56848bedd5cd54995561414a2898229ace643d54c320b5`

```text
Create ONE isolated transparent-background 2D game object asset for Gridworks, using the attached reference only as the strict visual-style, camera, material, and lighting guide. Asset class: CENTRAL RUBBLE AND SERVICE CORRIDOR, an irregular wide 2:1 isometric diamond-shaped cluster of rough charcoal soil, broken dark rocks, gravel berms, weathered concrete service lanes, tiny utility sheds, cable drums, scrap piles, drainage culverts, and sparse warm amber work lamps. Match the reference's low three-quarter 2:1 isometric angle exactly, blackened iron / graphite / weathered concrete material language, granular high-relief ground detail, tiny amber pin lights, sharp hand-painted premium strategy-game rendering, and the same object scale as the reference's central open corridor. The cluster must visually fill empty terrain while remaining clearly an individual placeable object. Transparent alpha outside the irregular object silhouette; clean fully transparent corners; no rectangular plate, no background, no horizon, no sky, no river or water, no buildings larger than a small shed, no power plants, no substations, no transmission towers, no conductors or route lines, no service-radius overlays, no HUD, no labels, no text, no icons, no border, no selection ring. Center the entire object with generous transparent padding. Native high-resolution PNG suitable for downscaling in Godot.
```

## `g3/tiles/river-water-surface-a.png`

- built-in ImageGen source run: `exec-95e0b79f-89e6-4a8a-b948-843b27dc8f8d`
- preserved source: `/Users/fred/.codex/generated_images/01a02155-55e1-7ef1-a3a4-a96b0ee32134/exec-95e0b79f-89e6-4a8a-b948-843b27dc8f8d.png`
- inputs: `assets/01-grid-construction.png`, `assets/04-plant-siting.png`
- runtime class: individual edge-to-edge water material tile, bound only inside the authoritative
  `CHEONGRYU_RIVER` polygon; bank and channel geometry remain code/data-owned.
- final SHA-256: `42f6bdf046a1c859e89b7b4530d56c45504d46f219495bac64dde7e63fb3c98b`

```text
Create ONE production-ready seamless river WATER SURFACE TILE for a dark industrial isometric strategy game, using the attached references as the strict style and water-quality target. Extract the visual language of the references' narrow winding river only: deep blue-black water with visible depth, soft slate-blue reflections, sparse broken silver-blue glints, subtle slow current streaks following one consistent diagonal flow, slight soot and sediment tint, restrained contrast, no ocean waves and no crumpled-metal or rocky texture. The output is an individual square seamless/tileable texture viewed straight-on as a material source for polygon UV mapping; it must NOT include perspective banks or a complete river shape. Edge-to-edge water surface only, seamless on all four edges, no transparent padding, no checkerboard, no green screen, no rocks, no soil, no banks, no bridge, no buildings, no plants, no pylons, no conductors, no selection glow, no HUD, no text, no labels, no border, no watermark. Match the references' graphite/blue-gray palette and sparse reflective depth under dim upper-right light. High-resolution square PNG.
```

## `g3/ui/panel-frame.png`

- built-in ImageGen source run: `exec-f0157e38-28c3-4029-b7ad-b4ec917f1349`
- preserved source: `playtests/commercial-2d/g3-runtime-sources/panel-frame-source.png`, SHA-256
  `2e2e39eb481d8da7a3381f06ed83bc56f38de899584808c168f6ced0a91e45a4`
- inputs: `assets/01-grid-construction.png`, `assets/03-route-comparison.png`,
  `assets/04-plant-siting.png`
- runtime processing: deterministic 128×128 Lanczos downscale for a 15 px 9-slice margin.
- runtime SHA-256: `2b946bbc6de12d77f98af2482b60e413a30c953771fe23d550a50885787fbdb8`

```text
Create one production-ready square UI 9-slice source texture for a dark industrial power-grid strategy game. Match the attached references' exact heavy blackened steel HUD chrome: a deep empty charcoal center, thick layered beveled gunmetal frame, weathered scratches and soot, restrained aged brass inner rule, square reinforced corners with small round rivets, subtle upper-right metallic highlight and lower-left shadow. Perfectly front-facing orthographic UI plate, symmetric edges suitable for 9-slice stretching, all important corners and border detail confined to the outer 14 percent, center 55 percent visually uniform and empty for arbitrary content. No text, numbers, icons, buttons, controls, cyan glow, amber glow, logo, watermark, perspective, scenery, map, buildings, checkerboard, or transparency requirement. Square 1024-style high-resolution output; frame touches all four canvas edges without being cropped.
```

## `g3/objects/industrial-warehouse-a.png`

- built-in ImageGen source run: `exec-4190c72f-45d6-41e1-abe3-e854d2c46edf`
- preserved source: `/Users/fred/.codex/generated_images/01a02155-55e1-7ef1-a3a4-a96b0ee32134/exec-4190c72f-45d6-41e1-abe3-e854d2c46edf.png`
- inputs: `assets/01-grid-construction.png`, `game/art/commercial/objects/facility-industry-v1.png`
- extraction: `tools/reference-parity/extract-chroma-alpha.py`, with validated alpha-zero corners
  and 64.02% fully transparent pixels.
- final SHA-256: `91389f01e322ecfffbb63a978d92b19a4dd3b0906aebccb4298367093f3d17fe`

```text
Create one production-ready isolated 2D game object sprite: a dense soot-dark industrial warehouse and utility-yard cluster for a Korean riverside power-grid strategy game. Match the attached reference screenshot's low orthographic 2:1 isometric camera exactly (same yaw/pitch and upper-right key light), object scale language, blackened steel, weathered concrete, pipes, tanks, roof vents, narrow service lanes, scrap piles and sparse tiny amber work lights. Arrange 4 to 6 connected low warehouse/maintenance structures on a stepped diamond footprint, with one modest chimney and visible rooftop ducts, distinct from the attached existing factory sprite. No power plant-scale stacks and no transmission equipment. Center one complete individually placeable object with generous padding. Use a uniform pure bright green #00FF00 chroma-key background from silhouette to all four edges, with no checkerboard, gradient, shadow, scenery, ground beyond the attached object footprint, UI, text, labels, logo, cyan glow, watermark, or border. High-resolution square output.
```

## `g3/tiles/dense-city-parcel-a.png`

- built-in ImageGen source run: `exec-c14f772a-896c-47af-8eb9-d816200111ec`
- preserved source: `/Users/fred/.codex/generated_images/01a02155-55e1-7ef1-a3a4-a96b0ee32134/exec-c14f772a-896c-47af-8eb9-d816200111ec.png`
- inputs: `assets/01-grid-construction.png`, `assets/02-heatwave-outage.png`,
  `game/art/commercial/tiles/residential-block-v1.png`
- extraction: `tools/reference-parity/extract-chroma-alpha.py`, with validated alpha-zero corners
  and 60.95% fully transparent pixels.
- final SHA-256: `3c14ee648637d38e2417b5d50da46283a9155063518caf8496cf76798993f24e`

```text
Create one production-ready isolated 2D world parcel tile for a dark Korean industrial power-grid strategy game: a dense residential and civic street block on a clean 2:1 isometric diamond footprint. Match the attached reference screenshot's exact low orthographic camera, yaw, pitch, small-building scale, continuous urban density, diagonal road axes, blackened graphite roofs, weathered charcoal concrete, narrow alleys, utility clutter, subtle soot and tiny warm amber windows. The diamond parcel must contain 14 to 20 varied low houses and workshops, two intersecting narrow roads, small courtyards and edge connectors that can overlap adjacent parcel tiles; no single landmark dominates. The full parcel, including its road/ground diamond, is one individually placeable tile centered with generous padding. Upper-right light, cool blue-gray fill, readable midtones. Use a uniform pure bright green #00FF00 chroma-key background outside the exact diamond silhouette to all four canvas edges. No checkerboard, gradient, scenery beyond the parcel, river, power lines, pylons, facility selection glow, UI, text, labels, logo, watermark, border, or perspective horizon. High-resolution square output.
```

## `g3/objects/industrial-service-yard-b.png`

- built-in ImageGen source run: `exec-9b79fe53-8869-4c89-805d-d073874b9fb7`
- preserved source: `playtests/commercial-2d/g3-runtime-sources/industrial-service-yard-b-source.png`,
  SHA-256 `29be8e5c28028807905903575d28e66f486e16cafca7ef6c97f60948fdf97561`
- inputs: `assets/01-grid-construction.png`
- extraction: `tools/reference-parity/extract-chroma-alpha.py`, with validated alpha-zero corners
  and 70.39% fully transparent pixels.
- final SHA-256: `c7b014a5528964162e4707de7b7208adbd32237d6b6567692d865094611dcb68`

```text
Create one single isolated isometric industrial service-yard cluster asset for a dark near-future electrical grid strategy game, visually matching the supplied reference screenshot as closely as possible. Exact camera: classic 2:1 orthographic isometric, yaw 45 degrees, pitch 35.264 degrees, no perspective convergence. Asset content: a compact cluster of two low soot-dark metal maintenance sheds, one small transformer pad, cable reels, stacked dark pipes, scattered rubble, cracked concrete apron, and 6 to 10 tiny warm amber work lights. The cluster must be moderately dense but low-profile, designed to fill an industrial city foreground without resembling a major power plant. Materials: graphite steel, weathered dark concrete, coal-black soil, rusty bronze edges, sculpted midtones and fine embossed detail. Lighting: upper-right cool key light, deep but readable shadows, small amber practical lights. Composition: centered object, all visible faces use the same isometric angle, footprint about 2.2:1 wide, generous empty margin on every side. Background must be plain flat vivid chroma green #00FF00 with no gradient, no shadow beyond the object's footprint, no UI, no text, no labels, no roads extending to the canvas edge, no cyan or amber power-line overlay, no border, no scene, no whole-map background. Render as a production-ready individual runtime sprite at 1024x1024, crisp edge detail, no checkerboard.
```

## `g3/objects/residential-cluster-b.png`

- built-in ImageGen revision run: `exec-26e67dbe-a8a3-449d-9147-c561f572bd28`
- preserved initial source/run: `playtests/commercial-2d/g3-runtime-sources/residential-cluster-b-source.png`,
  `exec-cc7ffbfc-9bb6-4634-a6ae-408d39d5ec5c`, SHA-256
  `0e2afc6aeb368926d2d15e169ad0e3966f14cd93a5bafa565075d6e724eec911`
- preserved accepted revision source:
  `playtests/commercial-2d/g3-runtime-sources/residential-cluster-b-revision-source.png`, SHA-256
  `83e4315dc1437cc4d8640fa94531814a4274a383b4354a56b2ba077287688b8b`
- inputs: `assets/01-grid-construction.png`, `game/art/commercial/g3/objects/residential-cluster-a.png`
- extraction: `tools/reference-parity/extract-chroma-alpha.py`, with validated alpha-zero corners
  and 59.65% fully transparent pixels.
- final SHA-256: `28ec3b84e5a59f33772b2388b551b1a004e21fb953db102a3ab2b724a27e1cc3`

```text
Revise the supplied isolated residential cluster sprite for production use at small runtime scale while preserving its exact single-object composition, pure #00FF00 chroma background, classic 2:1 orthographic isometric camera, upper-right lighting, and dark Gridworks visual style. Match the reference screenshot's readable sculpted midtones at 170 to 220 display pixels. Raise wall and roof midtones by about 20 percent, separate adjacent roofs with restrained slate-gray edge light, simplify micro-noise that collapses when downscaled, enlarge the warm amber windows slightly, and make alleys and building silhouettes remain distinct at thumbnail size. Keep soot-dark graphite materials and deep shadows, but do not let houses merge into a black speckled mass. Preserve 9 to 12 houses, the irregular individual footprint, generous green margin, and coherent upper-right key light. Background must remain perfectly uniform vivid chroma green #00FF00 to all edges. No transparency/checkerboard, no UI, no text, no labels, no power lines, no cyan glow, no map, no border, no added scene. Output one complete 1024x1024 revised isolated asset.
```
## `g3/objects/dense-residential-neighborhood-d.png`

- built-in ImageGen source run: `exec-8ca48001-391e-4673-9cc4-5b00e180a6c0`
- preserved source: `playtests/commercial-2d/g3-runtime-sources/dense-residential-neighborhood-d-source.png`,
  SHA-256 `f21cbfd39796ce378f03639379777b277f5aad5dbea6e00f7c52a93ad30b5d26`
- inputs: `assets/01-grid-construction.png`
- extraction: `tools/reference-parity/extract-checkerboard-alpha.py`, with validated alpha-zero
  corners and 51.01% fully transparent pixels; deterministic 1024×512 Lanczos downscale.
- runtime class: one individually placed dense residential parcel; never a map background plate.
- runtime SHA-256: `de9f4d9a777a9d89418d669399c29c95537f2d7f92a14aa51bfad5af4010281d`

```text
Create exactly ONE production-ready individual isometric game object asset: a dense residential neighborhood parcel D for the Gridworks industrial power-grid strategy map. Use the attached reference image as the strict target for camera, 2:1 isometric angle, blackened steel-and-concrete material language, soot-dark charcoal terrain, crisp granular micro-contrast, and sparse warm amber window lights. The object must be a compact irregular diamond-shaped parcel containing roughly 28–36 individually readable small worker houses, narrow diagonal roads, utility sheds, a few poles WITHOUT overhead conductors, rubble seams and tiny retaining walls. It must look like one dense district object that can be placed repeatedly but is not a whole map or background. Match the reference’s right-side housing density and house-to-substation scale: houses must remain much smaller than a substation or transmission pylon. Upper-right dim light, deep contact shadows, restrained amber, no green vegetation, no bright daylight. Transparent background, clean alpha, no checkerboard, no rectangular ground plate beyond the irregular parcel footprint, no water, no river, no bridge, no power plant, no substation, no transmission tower, no cyan or amber route lines, no HUD, no UI, no text, no labels, no watermark. Center the single parcel with generous transparent padding. High-resolution PNG, approximately 2:1 isometric footprint.
```
## `g3/objects/industrial-rubble-service-yard-c.png`

- built-in ImageGen source run: `exec-b8ce85e7-daac-4e60-8a38-70c301a87165`
- preserved source: `playtests/commercial-2d/g3-runtime-sources/industrial-rubble-service-yard-c-source.png`,
  SHA-256 `9b1c257dc7d6ba4ea9b8959fef5e2254da567e58401c4c18c8c619c1a315b47a`
- inputs: `assets/01-grid-construction.png`
- extraction: `tools/reference-parity/extract-checkerboard-alpha.py`, with validated alpha-zero
  corners and 54.29% fully transparent pixels; deterministic 1024×512 Lanczos downscale.
- runtime class: one individually placed industrial yard; never a map background plate.
- runtime SHA-256: `41dc1aece082d8785d7aa431848c433b044b5769fc19485df8ce359565ca74b4`

```text
Create exactly ONE production-ready individual isometric game object asset: an industrial rubble-and-service yard C for the Gridworks power-grid strategy map. Use the attached reference image as the strict target for its 2:1 orthographic isometric camera, upper-right lighting, soot-dark industrial material, crisp granular ground relief, and restrained amber practical lights. Content: a dense irregular 2.2:1 footprint with 5–7 low blackened-steel workshops and utility sheds, one modest cylindrical tank, cable drums, stacked pipes, broken concrete slabs, coal-black rubble berms, narrow diagonal service lanes, scattered crates, two small roof vents, and 10–16 tiny warm amber lamps. It must fill an otherwise empty industrial center/foreground while remaining much smaller and lower than a power plant, substation, or transmission pylon. It is one individually placeable runtime object, not a map plate or background. Keep buildings individually readable at 260–340 display pixels with crisp silhouette separation and slightly lifted slate-gray midtones matching the reference. Transparent background with clean alpha, no checkerboard, no rectangular base outside the irregular yard footprint, no water, no river, no bridge, no houses, no power plant, no substation, no transmission towers, no conductors or route lines, no cyan glow, no HUD, no UI, no text, no labels, no watermark. Center the single yard with generous transparent padding. High-resolution PNG, approximately 2:1 isometric footprint.
```

## `g3/objects/irregular-riverside-neighborhood-e.png`

- built-in ImageGen source run: `exec-13729f25-b47b-459e-ac2c-1cc79f9d231b`
- preserved source: `playtests/commercial-2d/g3-runtime-sources/irregular-riverside-neighborhood-e-source.png`,
  SHA-256 `be2ea708ca93d0250d0cb3d6f674565a8910c5691ae7308a5bf6c330e0e211d2`
- inputs: `assets/01-grid-construction.png`
- extraction: `tools/reference-parity/extract-checkerboard-alpha.py`, with validated alpha-zero
  corners and 53.06% fully transparent pixels; deterministic 1024×512 Lanczos downscale.
- runtime class: one individually placed asymmetric riverside neighborhood; never a map plate.
- runtime SHA-256: `22cda7165a9e8ed682a9fd427aac84815cce5b7a69df92824734041622e37a87`

```text
Create exactly ONE production-ready individual isometric game object asset: an irregular riverside workers' neighborhood E for the Gridworks industrial power-grid strategy map. Use the attached reference image as the strict target for its classic 2:1 orthographic isometric camera, upper-right dim light, soot-black graphite roofs, weathered concrete, sculpted rocky relief, fine granular contrast, and sparse warm amber windows. Make the silhouette visibly asymmetric and organic rather than a rectangular repeated stamp: 20–28 individually readable low houses and workshops step around two crooked diagonal lanes, a small open rubble court, retaining walls, utility sheds, drain channels, cable spools, and broken concrete edges. One side must have a concave riverside-facing edge but contain no water or riverbank. Keep houses much smaller than a substation or pylon and readable at 330–430 display pixels. This is one individually placeable runtime object, not a map, district plate, or background. Transparent background with clean alpha and generous padding; no checkerboard, rectangular base, river, water, bridge, plant, substation, transmission tower, overhead conductors, cyan/amber route overlays, HUD, UI, text, labels, border, or watermark. High-resolution PNG with an approximately 2:1 footprint.
```

## `g3/objects/industrial-salvage-boiler-yard-d.png`

- built-in ImageGen source run: `exec-9446f837-cff7-45b0-8862-a483d93b3497`
- preserved source: `playtests/commercial-2d/g3-runtime-sources/industrial-salvage-boiler-yard-d-source.png`,
  SHA-256 `83aa3570195f75955f0f2b58b5caf4f4e5b13a20892bbc2a9c8423c586882157`
- inputs: `assets/01-grid-construction.png`
- extraction: `tools/reference-parity/extract-checkerboard-alpha.py`, with validated alpha-zero
  corners and 62.47% fully transparent pixels; deterministic 1024×512 Lanczos downscale.
- runtime class: one individually placed salvage and boiler service yard; never a map plate.
- runtime SHA-256: `9b8d0e08ddaa6435329f0438fe71b898b1672cb2f4284e5c925f7fa8b1a65075`

```text
Create exactly ONE production-ready isolated isometric game object: industrial salvage-and-boiler service yard D for the Gridworks power-grid strategy map. Match the attached reference's 2:1 orthographic isometric camera, upper-right light, blackened iron, coal-dark rubble, weathered concrete, readable slate-gray midtones, crisp hand-painted relief, and restrained tiny amber work lamps. Use an irregular 2.3:1 footprint containing three distinct low boiler/maintenance sheds, one small horizontal tank, short dark exhaust stacks, pipe racks, transformer parts, cable drums, scrap-metal rows, broken slab piles, narrow diagonal service tracks, and a rough central loading court. Give it a different silhouette and internal organization from other yards; it must remain far smaller and lower than a plant, substation, or transmission tower. This is one individually placeable runtime object, not a whole scene, district, map plate, or background. Genuinely transparent RGBA background to all edges, clean antialiasing and generous padding; no checkerboard, rectangular ground base, water, river, bridge, houses, plant-scale stack, substation, pylon, conductor, cyan/amber overlay, HUD, UI, text, label, border, or watermark. High-resolution PNG, approximately 2:1 isometric footprint.
```

## `g3/objects/rubble-utility-corridor-b.png`

- built-in ImageGen source run: `exec-2e0ef399-174d-43e9-845c-e75990a0ee89`
- preserved source: `playtests/commercial-2d/g3-runtime-sources/rubble-utility-corridor-b-source.png`,
  SHA-256 `fa7e89ccdbecf36317ad84d81adb99dc154d00de51de7a0d6d90f2bcac046373`
- inputs: `assets/01-grid-construction.png`
- extraction: `tools/reference-parity/extract-checkerboard-alpha.py`, with validated alpha-zero
  corners and 70.13% fully transparent pixels; deterministic max-side 1024 px downscale.
- runtime class: one individually placed bent rubble/utility corridor; never a map plate.
- runtime SHA-256: `c12e4801529127217d6ef9e6f77f0a3d5a005f778ebb57dbe3b61424c52265e7`

```text
Create exactly ONE production-ready isolated isometric 2D game object asset named rubble utility corridor B for the Gridworks power-grid strategy map. Match the attached reference image as strictly as possible: classic 2:1 orthographic isometric projection (yaw 45 degrees, pitch 35.264 degrees), upper-right dim key light, soot-black graphite rock, tangled dead roots and cable scrap, broken weathered concrete, dark wet soil, sparse black utility poles without overhead conductors, tiny drain fragments, muted bronze edges and only 5–9 tiny amber work-light pinpoints. Composition: one long irregular rocky utility corridor, approximately 2.7:1 in its isometric footprint, with a bent/asymmetric outline and no straight rectangular boundary. It should visually connect industrial and residential areas at 360–520 display pixels and provide the dense continuous rubble relief seen through the reference center without becoming a district or map. No large buildings; at most two tiny low maintenance sheds that remain secondary. This is one individually placeable runtime object, not a scene, map plate, background, tile, or complete road. Genuinely transparent RGBA background to every canvas edge with clean alpha and generous padding. No checkerboard, colored matte, rectangular base, river, water, bridge, power plant, substation, transmission tower, conductor lines, cyan/amber route overlays, service radius, HUD, UI, text, labels, icons, border, watermark, people, or vegetation. High-resolution PNG; consistent object scale and lighting with the reference.
```

## `g3/objects/compact-utility-hamlet-f.png`

- built-in ImageGen source run: `exec-4fae8930-4a79-461e-bb9f-b79250eb56af`
- preserved source: `playtests/commercial-2d/g3-runtime-sources/compact-utility-hamlet-f-source.png`,
  SHA-256 `f88e7aed5f91d12179fb63ab386baa9484c4d606bcbbaa64922f3d5c64c0036a`
- inputs: `assets/01-grid-construction.png`
- deterministic checkerboard-alpha extraction: 64.91% fully transparent pixels; max-side 1024 px.
- runtime class: one individually placed utility hamlet, never a map plate.
- runtime SHA-256: `c78ad2a6ba5b56d692ab2e99c23ae66a530d49b439a0bf3a95340176a91e6ec8`

```text
Create exactly ONE production-ready isolated isometric runtime object: compact utility hamlet F for the Gridworks industrial power-grid strategy map. Match the attached reference as strictly as possible in classic 2:1 orthographic isometric projection, yaw 45 degrees, pitch 35.264 degrees, upper-right dim key light, soot-black graphite roofs, weathered dark concrete, charcoal rubble, readable slate-gray midtones, tight contact shadows, and sparse tiny amber window lights. Content: an irregular compact cluster of 11–15 individually readable low worker houses and electrical maintenance workshops, two narrow intersecting diagonal lanes, one tiny transformer shed without a substation silhouette, cable drums, scrap fencing, drain channels, retaining rubble, and a few utility poles with NO overhead conductors. The footprint must be asymmetric and organically stepped, approximately 2.0:1, clearly different from a square city stamp, and useful for joining the reference's central grid corridor without dominating a plant or substation. One individually placeable object only; not a district plate, whole map, scene, background, or tile. Genuinely transparent RGBA background to all canvas edges, generous padding, crisp alpha. No checkerboard or colored matte, no rectangular base, no river/water/bank/bridge, no plant, no substation, no lattice transmission tower, no conductors, no cyan/amber overlays, no selection radius, no HUD/UI/text/labels/icons/border/watermark, no people or bright vegetation. High-resolution PNG, same small-house scale and lighting as the reference.
```

## `g3/objects/dense-roadside-residential-g.png`

- built-in ImageGen source run: `exec-d24d2edc-14c3-40c4-9cbe-0c8f9f6d9d9b`
- preserved source: `playtests/commercial-2d/g3-runtime-sources/dense-roadside-residential-g-source.png`,
  SHA-256 `a3b796703254a40d2208c04aeaba534b76c1279bda1ca67315604fbd96492312`
- inputs: `assets/01-grid-construction.png`
- deterministic checkerboard-alpha extraction: 60.95% fully transparent pixels; max-side 1024 px.
- runtime class: one individually placed asymmetric roadside neighborhood, never a map plate.
- runtime SHA-256: `870b5b33f58681be9154768806588aa52d7633d0444d4cb8730c32b06cf2d5b2`

```text
Create exactly ONE production-ready isolated isometric runtime object: dense roadside residential cluster G for the Gridworks industrial power-grid strategy map. Strictly match the attached reference's classic 2:1 orthographic isometric camera (yaw 45 degrees, pitch 35.264 degrees), object scale, upper-right low industrial lighting, blackened slate roofs, weathered charcoal masonry, soot-dark concrete lanes, crisp painterly relief, controlled blue-gray midtones, and many sparse warm amber windows. Composition: 18–24 small individually readable worker houses and shopfronts arranged along one bent diagonal main lane and two short side alleys, with irregular stepped outer silhouette, small courtyards, sheds, retaining walls, cable reels, drains, and rubble seams. Make it a long asymmetrical neighborhood wedge approximately 2.4:1, visibly distinct from every rectangular or diamond parcel stamp, designed to continue dense urban fabric beside a power route. Houses remain much smaller than a substation, pylon, or plant. One independently placeable runtime object only, never a whole city, map plate, scene, background, atlas, or material tile. Genuinely transparent RGBA background with clean alpha to all four edges and generous padding. No checkerboard or colored matte, no rectangular ground base, no river/water/bank/bridge, no hospital landmark, no plant, no substation, no transmission tower, no overhead conductor, no cyan/amber overlay, no service radius, no HUD/UI/text/labels/icons/border/watermark, no people or vivid green vegetation. High-resolution PNG, cohesive with the reference.
```

## `g3/objects/scrap-industrial-micro-block-e.png`

- built-in ImageGen source run: `exec-1a9d2cf2-70d0-4fea-8b3c-33ae31dd6990`
- preserved source: `playtests/commercial-2d/g3-runtime-sources/scrap-industrial-micro-block-e-source.png`,
  SHA-256 `f45cd50c798a89ce09d9742289f079be0cfc7d5eb3e4702fbe5c6ecb2882a4c1`
- inputs: `assets/01-grid-construction.png`
- deterministic checkerboard-alpha extraction: 61.84% fully transparent pixels; max-side 1024 px.
- runtime class: one individually placed industrial micro-block, never a map plate.
- runtime SHA-256: `c1c9e16034cf4498c4df7276e7730ee04c4da5022e1a1173a8a645d7bdfd139c`

```text
Create exactly ONE production-ready isolated isometric runtime object: scrap industrial micro-block E for the Gridworks power-grid strategy map. Match the attached reference as strictly as possible: classic 2:1 orthographic isometric projection, yaw 45 degrees, pitch 35.264 degrees, upper-right dim key light, blackened iron, soot-dark concrete, graphite rubble, restrained steel-blue midtones, crisp embossed mechanical detail, deep readable shadows, and 8–12 tiny warm amber work lights. Content: an irregular compact block of four low but distinct maintenance/fabrication buildings, one narrow sawtooth-roof shed, one small cylindrical tank, pipe racks, cable drums, stacked transformer scrap, broken concrete, dark service lanes, fences, and drain trenches. Footprint approximately 2.1:1 with an asymmetric notched outline; lower and much smaller than a power plant, substation, or lattice pylon. It must join a central industrial corridor without reading as a repeated rectangular stamp. Exactly one individually placeable runtime object; not a map, district plate, background, scene, atlas, or tile. Genuinely transparent RGBA background to every edge with generous padding and clean alpha. No checkerboard or colored matte, no rectangular ground base, no houses, no river/water/bank/bridge, no plant-scale chimney, no substation, no transmission tower or conductor, no cyan/amber overlay, no service radius, no HUD/UI/text/labels/icons/border/watermark, no people or bright vegetation. High-resolution PNG, same scale and lighting language as the reference.
```

## `g3/objects/continuous-worker-city-parcel-i.png`

- built-in ImageGen source run: `exec-7a8c4a8d-bb15-454b-9652-2e95a4c97c20`
- preserved source: `playtests/commercial-2d/g3-runtime-sources/continuous-worker-city-parcel-i-source.png`
- inputs: `assets/01-grid-construction.png`
- source SHA-256: `ddb623e2ce3d36b794d078b83eb103eca7005a3a582662d60c42ffb0493d188f`
- deterministic background-connected neutral-matte extraction (`--connected-matte-floor 110`)
  proves 48.80% fully transparent runtime pixels and alpha-zero corners.
- disposition: **REJECTED / NOT RUNTIME-BOUND**. It contains many buildings and a road network in one
  raster and violates G.3 Step 1's one composition unit per PNG rule. Preserved only as failed provenance.
- final SHA-256: `c3834c72ec2b227326f84b34618f68cd273d86180d8e6753625c99b165040e3d`

```text
Create exactly ONE production-ready isolated isometric runtime object: continuous eastern worker-city parcel I for Gridworks, deliberately DIFFERENT in layout and silhouette from a regular orthogonal neighborhood. Match the attached reference as strictly as possible in classic 2:1 orthographic isometric projection, yaw 45 degrees, pitch 35.264 degrees, no perspective convergence, upper-right dim key light, soot-black graphite roofs, weathered charcoal concrete, readable cool blue-gray midtones, deep contact shadows, crisp embossed painterly relief, and many restrained tiny amber windows. Composition: one dense asymmetric district mass with 42–54 individually readable small worker houses, attached row shops, sheds and tiny workshops. Use a branching continuous street network: one broad diagonal spine that bends twice, three narrow diagonal cross streets, two offset T-junction plazas, cramped alleys, retaining walls, drains, cable reels, carts, rubble, and low utility poles with NO overhead conductors. Avoid a regular rectangular grid and vary house size, roof direction, and setback. The outer silhouette must organically dissolve into soot-dark rubble and soil: at least ten deep uneven notches, staggered house tips, short road stubs, isolated sheds, and near-black rubble fingers. Absolutely no raised slab, perimeter curb, continuous straight boundary, diamond/rectangular foundation plate, pale edge, white/silver rubble, rim light, or halo. All edge fragments are charcoal, soot-black, muted dark brown, or restrained dark blue-gray. One independently placeable object only, not a whole map, background, tile, atlas, or complete city. Center it on a perfectly uniform pure white background with generous clearance for deterministic background removal; white stops cleanly at the dark silhouette. No river, water, bank, bridge, hospital landmark, power plant, substation, transmission pylon, overhead conductor, cyan/amber route overlay, selection/service radius, HUD, UI, text, labels, people, vegetation, checkerboard, border, watermark, or shadow outside the footprint. High-resolution square PNG.
```

## `g3/objects/industrial-road-bridge-a.png`

- built-in ImageGen source run: `exec-7a22e32e-ae0a-441d-94ad-cb2f84fec546`
- preserved source: `playtests/commercial-2d/g3-runtime-sources/industrial-road-bridge-a-source.png`,
  SHA-256 `250503da75cb72d027085f7921a91a94b205c79c29d985889a55e10a07a2114f`
- inputs: `assets/01-grid-construction.png`, `assets/04-plant-siting.png`
- deterministic checkerboard-alpha extraction: 71.15% fully transparent source pixels;
  deterministic max-side 1024 px downscale.
- runtime class: one individually placed road bridge deck with attached abutment caps; never a
  river plate, terrain background, or map plate.
- runtime SHA-256: `a15b2ddb6cc93603a72c8b4c2c5c53d278d5a283114c9f1cb836c2dce44a9983`

```text
Create exactly one isolated runtime game object: a short two-lane industrial road bridge deck with two compact concrete-and-blackened-steel abutment caps, viewed in classic orthographic 2:1 isometric projection, yaw 45 degrees and pitch 35.264 degrees, no perspective convergence. Match the supplied Gridworks references as closely as possible in camera, painterly embossed detail, graphite steel, soot-dark weathered concrete, restrained warm amber work lights, and upper-right key light. The bridge should span a narrow river diagonally from southwest to northeast, with a slim dark asphalt deck, low riveted steel side girders, visible concrete feet at both ends, and enough underside depth to read as a bridge when placed over dark teal water. One centered object only, no river, no terrain patch, no vehicles, no power lines, no UI, no labels, no letters, no numbers, no border, no crop, no atlas, no extra variants. Place it on a perfectly uniform pure white background with generous clearance on all sides and a soft contact shadow confined immediately under the object, suitable for deterministic background removal. Square 1024x1024 output.
```

## `g3/objects/industrial-road-bridge-b.png`

- accepted built-in ImageGen source run: `exec-072784e2-c6e9-44a8-a3bf-7db4ecd29a21`
- preserved source: `playtests/commercial-2d/g3-runtime-sources/industrial-road-bridge-b-source.png`,
  SHA-256 `8968c0931876d72a95b3df631be22e5150606bc9e81f89134471d097bb2b2386`
- rejected predecessor: `exec-97308055-0dc8-48dd-959c-7ad4b646098b`; the apparent checkerboard was
  baked into opaque pixels, so it was never copied into the repository or accepted as runtime art.
- inputs: accepted bridge A as the object/style reference; the call altered the long-axis orientation,
  retained exactly one bridge, and prohibited river, terrain, background plates, UI and text.
- runtime processing: real source alpha was preserved; `trim-alpha-padding.py --padding 16` reduced
  1536×1024 to 1005×1006 without reconstruction, compositing, or checkerboard extraction.
- runtime proof: 65.34% fully transparent pixels and four alpha-zero corners.
- runtime class: one individually placed NW-SE road bridge with its own four attached end supports;
  never a river plate, terrain background, atlas, or map plate.
- runtime SHA-256: `b6c7e7899b8411b69bb45f2cbb1687fb7ca5df3ded472ce7cd5aee13ac8e7ff4`

```text
Edit the attached single bridge sprite into exactly ONE isolated, complete bridge object for Gridworks. Preserve its dark graphite asphalt, blackened riveted steel, weathered concrete end supports, restrained amber lamps, premium hand-painted relief, and classic 2:1 orthographic isometric camera. Change the bridge's baked long axis to the steep upper-left-to-lower-right screen diagonal so the finished object crosses the runtime river instead of running along it. Keep the whole deck and all attached supports fully visible and centered. Use a genuinely transparent RGBA background to every canvas edge with alpha-zero corners—no checkerboard pixels, matte, glow plate, river, water, bank, terrain, road extension, second object, map, UI, text, labels, border, or watermark.
```

## G.3 Step 1 — atomic city/building/prop kit

The following assets replace every runtime-bound district, parcel, cluster, neighbourhood, hamlet,
service-yard cluster, and city plate. `compositionUnitCount` is exactly `1` for every row. The first nine
sources contained a generated light checker matte and use the repository's deterministic
`extract-checkerboard-alpha.py`; the final three already contained genuine RGBA alpha and are copied without
pixel edits. Every source remains in `playtests/commercial-2d/g3-runtime-sources/`.

| runtime path | ImageGen run | preserved source / source SHA-256 | final SHA-256 | unit |
|---|---|---|---|---|
| `g3/atomic/worker-house-a.png` | `exec-348f1a4d-b730-4ad3-9d06-e9b780840d67` | `atomic-worker-house-a-source.png` / `793a898cdaedf69566e568dc027d68738ef5f6d71be1a7e56332899334ef051e` | `9159d64fbef14a6d35da1695ef5d3bf74b66cf17bdfa69e5bf7a02f66aef133c` | one worker house |
| `g3/atomic/worker-house-b.png` | `exec-06607a7d-af88-4666-84c1-6615bbf5c16c` | `atomic-worker-house-b-source.png` / `48dafb1e6d006664b576ac4f759bc949daa35b26c60b7128abd802900355c6fe` | `0f1c6d2eb9281cdb99b0291bb76ea39c5e759fb0aec6d670921c07a01882c71c` | one worker house |
| `g3/atomic/worker-house-c.png` | `exec-bfda1dea-09bd-4f19-924b-1c5e244d2ad4` | `atomic-worker-house-c-source.png` / `bf18eeb633b82c0d93c4a9a131ef05bc0a48551e41590123384220ea044f5b20` | `4b5e80d82193abe201c8744adb8c5a36f2f5fa8a121dbb2e38ef7900391edfd5` | one worker house |
| `g3/atomic/row-shop-a.png` | `exec-1c3cda36-0c41-4cc4-b799-9142a49b498f` | `atomic-row-shop-a-source.png` / `e1bb9cda78a9b7bc6a15ac3587fd4d67b9d6d24e4a67f05c14a76f12b51767e9` | `4d75f76c62fe922d07ab90cfe684d7b062b9c997fbaf6e9a80039a7b10afb279` | one shop building |
| `g3/atomic/workshop-a.png` | `exec-254a88ba-cfd8-4a7e-ae7e-a52c2c313309` | `atomic-workshop-a-source.png` / `b9a4477505d60c919b6183f3cbe11dbc62a0da3a9eafb27a766ffc25c2a5dbf1` | `7471e78cdef99430d97d80bfc1eebf25afadf3c194f6dc965a5457d05716b920` | one workshop building |
| `g3/atomic/small-warehouse-a.png` | `exec-002d0386-4823-4e0c-a3f0-fbc39c77785a` | `atomic-small-warehouse-a-source.png` / `a0676fb3568474f31eab0836d402a64e3c54835c7e033b96a33560b83ac02271` | `089017630516b33dcca357cbc7aec08b3d0ff22235079459cb2bbce8beb4d221` | one warehouse building |
| `g3/atomic/hospital-main-a.png` | `exec-279112df-8da1-43c9-9a93-71d6c98d3c52` | `atomic-hospital-main-a-source.png` / `11db2aa2ce345a9fffad99c1ac3dc9ec401fad5263982cc1a8a83077285d3a64` | `e7f50e9e970ec1172f6ca4595660fdb3bb5ce57e3dac332999f60ceda5702444` | one hospital building |
| `g3/atomic/hospital-service-a.png` | `exec-7789efea-c361-4bad-ba20-2906b6035670` | `atomic-hospital-service-a-source.png` / `ca6d474857bb32c6d35b614200f094d53b38581b8aa002a0a80f4ad5b611c5a7` | `97ed4d5f889069f5587fbf70e6b6c4419eb8b154e007e28f4524f2169a08a201` | one hospital service building |
| `g3/atomic/pump-house-a.png` | `exec-5807d9c6-e49b-4924-b695-b6dacfc1d681` | `atomic-pump-house-a-source.png` / `def28611ce09a13ecfcf6bd290f2d018f55eda041442e83b41a4f124e33adedb` | `a1de39d09bb5e7a10511b5fb899b10bdc2f246340ece23eea08c82ec176d131c` | one pump-house building |
| `g3/atomic/water-tank-a.png` | `exec-14bf3ed7-f268-41d7-ac6b-e89ab87ab765` | `atomic-water-tank-a-source.png` / `15bf9ceb387a92c625ad7f9380c476da10b037161d2253a2eef52035114881ac` | `15bf9ceb387a92c625ad7f9380c476da10b037161d2253a2eef52035114881ac` | one water-tank object |
| `g3/atomic/retaining-wall-a.png` | `exec-7ac6b279-f844-40b6-ae3f-9a2422cd8043` | `atomic-retaining-wall-a-source.png` / `54e41d72dec8c8df61bd5e895885364417e0d76120236d994a5dda199eb1ea5c` | `54e41d72dec8c8df61bd5e895885364417e0d76120236d994a5dda199eb1ea5c` | one retaining-wall segment |
| `g3/atomic/street-lamp-a.png` | `exec-a0b090cb-b450-441d-9e15-24394a61ec27` | `atomic-street-lamp-a-source.png` / `3c90099d3c08db87ceede6cc2527b6e38d23634be9b570c0ba5c69511174a0e5` | `3c90099d3c08db87ceede6cc2527b6e38d23634be9b570c0ba5c69511174a0e5` | one street-lamp object |

### Atomic city prompt contract

All calls used built-in ImageGen. `assets/01-grid-construction.png` was the authoritative reference for
camera/material/light; `worker-house-a.png` became the accepted atomic style lock for later calls. Each raw
prompt included the exact asset-specific line below and the shared constraints: classic 2:1 orthographic,
yaw 45°, pitch 35.264°, upper-right cool key light, soot-dark graphite and restrained amber light, actual
transparent background, exactly one connected composition unit, no second building/annex/yard/road/terrain/
district/parcel/cluster/neighbourhood/plate/UI/text/watermark/checkerboard/matte/halo.

```text
HOUSE_A: Generate exactly ONE compact two-storey worker house as one isolated object; one connected building mass, dark slate roof, weathered graphite masonry, tiny warm amber windows, one small chimney.
HOUSE_B: exactly one compact single-storey worker cottage, rectangular connected building mass, asymmetrical low slate gable roof, one chimney, three small amber windows and one recessed doorway.
HOUSE_C: exactly one narrow three-storey worker townhouse, one connected masonry building mass, steep dark roof, small roof vent, tall vertical silhouette, four restrained amber windows.
ROW_SHOP_A: exactly one small two-storey corner shop building, one connected building mass, dark shallow roof, graphite shopfront shutters, tiny muted amber awning lights and upper windows; no signage.
WORKSHOP_A: exactly one low industrial repair workshop building, one connected blackened-steel shed mass, sawtooth roof limited to that building, one closed service door, two roof vents, three amber task lights.
SMALL_WAREHOUSE_A: exactly one compact single-storey industrial warehouse building, one connected elongated mass, dark shallow gable roof, one closed rolling freight door, one side door, two roof vents, three lamps.
HOSPITAL_MAIN_A: exactly one four-storey municipal hospital main building, one connected rectangular masonry mass, stepped flat roof integral to it, warm window grid, one muted red medical-cross emblem, no campus.
HOSPITAL_SERVICE_A: exactly one two-storey hospital service building, one connected low rectangular mass, flat equipment roof integral to it, dark ventilation grilles, restrained amber windows.
PUMP_HOUSE_A: exactly one compact municipal waterworks pump-house building, one connected masonry/steel mass, low hipped roof, two wall-integrated pipe-entry collars, one status lamp.
WATER_TANK_A: exactly one freestanding cylindrical municipal water-storage tank on one integrated riveted steel support frame, with one attached ladder; no second tank or surrounding equipment.
RETAINING_WALL_A: exactly one short straight weathered concrete/blackened-stone retaining-wall segment, approximately 3.5:1 long, one integral drain, no rubble pile or branching wall.
STREET_LAMP_A: exactly one slender industrial street lamp with one post, one curved arm, one amber lamp housing and tiny integrated base collar; no second prop.
```

## G.3 Step 1 — atomic road/yard tile kit

Each road source is one separately generated 2:1 diamond connection tile. Runtime placement records build the
network; no source contains a road network or city composition.

| runtime path | ImageGen run | preserved source / source SHA-256 | final SHA-256 | unit |
|---|---|---|---|---|
| `g3/roads/road-straight-nw-se-a.png` | `exec-0d5eddd4-7250-499e-8063-9572ab44b149` | `atomic-road-straight-nw-se-a-source.png` / `eddf6e4d3d2cf24dfd33680a5eb87650792ac56d9c19a78eaef83436126ff822` | `da4b42795e46a9e888d935855d1aa861b54c93f9bb71f2f2f75981603a12b1b9` | one straight connection tile |
| `g3/roads/road-straight-ne-sw-a.png` | `exec-7339f677-be4e-4d01-814d-b4fa56dbb026` | `atomic-road-straight-ne-sw-a-source.png` / `eb50095d49bfdedc6487b31d79b6cbefed24e40ae626a92159d620a48044ea87` | `260e954a098449598ae2281a47b352e83379fce05c8978e0b545fd87ae0a9a1d` | one straight connection tile |
| `g3/roads/road-corner-n-e-a.png` | `exec-431fc6a3-8172-4a37-8ca7-8af23278866f` | `atomic-road-corner-n-e-a-source.png` / `daf9ac59ce22b1940372e15ba8c5717e4b9c80b8617abd1d8242d6bea710b786` | `ac9ac6e508d587ab5a4b08cf21214f8f19f1911992240adfa5d0bb1e48ddca86` | one corner connection tile |
| `g3/roads/road-t-junction-a.png` | `exec-e269ef5f-1a1d-48c4-96fd-ec4560724de2` | `atomic-road-t-junction-a-source.png` / `d4887b33a9f8951266c25620763a31ad85726ea123908d2d844701eb469605ce` | `b24dfc1f246c7eb50db452730194ab0fcdb7d0350ac47f153ca2a26ef7b75b91` | one T-junction tile |
| `g3/roads/road-cross-junction-a.png` | `exec-2f632ddb-d92a-4f75-8e97-70a451b8d53a` | `atomic-road-cross-junction-a-source.png` / `43de6b8ad9453b7ba1e5236feea5ea5a2d30b3e33f8b02ca2f6c1ef490a4ee41` | `2fa8047f7c4833e6c72736a31d641cac58063160d43dc3ddf51f04b826ca5551` | one cross-junction tile |
| `g3/roads/service-yard-tile-a.png` | `exec-64bdce3b-b5ae-4aca-83ed-0c5c65a5c234` | `atomic-service-yard-tile-a-source.png` / `a81ccb0f0d53247c04bea2e9ecdb89b1448e7ff9a3dcec043c973fe753d8c8c7` | `40677a9358d6e52f9630a08d174c6ed94505c551c4d46d081872d0d0232223b4` | one single-throat service-yard tile |

### Atomic road prompt contract

All six calls used `assets/01-grid-construction.png` for the road camera/material and
`g3/tiles/ground-rubble-relief-c.png` for the ground-material lock. Each prompt required one precise 2:1
diamond, identical-width endpoints at specified edge midpoints, actual alpha outside the diamond, charcoal
asphalt and black curbs, and prohibited an atlas, multiple tiles, network, building, prop, vehicle, power
equipment, route glow, UI, text, watermark, checkerboard, matte, or halo.

```text
ROAD_STRAIGHT_NW_SE_A: exactly one straight two-lane service road from north-west edge midpoint to south-east edge midpoint; no branch or bend.
ROAD_STRAIGHT_NE_SW_A: exactly one straight two-lane service road from north-east edge midpoint to south-west edge midpoint; no branch or bend.
ROAD_CORNER_N_E_A: exactly one compact 90-degree corner connecting the north-west and north-east edge midpoints; exactly two endpoints.
ROAD_T_JUNCTION_A: exactly one T-junction connecting north-west, north-east and south-east edge midpoints; exactly three endpoints.
ROAD_CROSS_JUNCTION_A: exactly one compact four-way crossing connecting all four edge midpoints; exactly four endpoints.
SERVICE_YARD_TILE_A: exactly one paved industrial service-yard diamond with one south-west road throat, one loading apron and two integral storm grates; no freestanding prop.
```

## G.3 Step 2 — atomic river water/bank/transition/effect kit

The river remains code/data geometry. These files are only individually bound material tiles or short
objects placed along the authoritative banks. ImageGen was called once per selected asset; no call produced
an atlas, full river, full map, shoreline plate, or baked city. The three water sources were center-cropped
and downscaled deterministically to opaque 1024×512 2:1 material tiles. The original nine bank/effect
sources used `extract-checkerboard-alpha.py --connected-matte-floor 200` and a deterministic 1024 px
max-side resize. The three later bank-environment objects arrived with real alpha and used
`trim-alpha-padding.py` only; no checker pixels were accepted. Every transparent object has alpha-zero
corners and a separately pinned source/output SHA.

| runtime path | ImageGen run | preserved source / source SHA-256 | final SHA-256 | unit |
|---|---|---|---|---|
| `g3/river/river-water-neutral-b.png` | `exec-e2902ef2-0d4b-40d5-9198-9459848058cd` | `river-water-neutral-b-source.png` / `5df743bfea1f9661d4aaccc024dd2f7217406c669b2742ad819ac413ee4be678` | `9b5d408f7b263ff123280607331d4a20cfdc327555ff17b25ab58721357b3780` | one neutral 2:1 water material tile |
| `g3/river/river-water-heat-a.png` | `exec-0bbd1fe2-6d39-4589-b142-6bd7f0cbb64b` | `river-water-heat-a-source.png` / `8d73d6db870dda8eeb372a3e3fefa74bdb7a8b5229eb67c9c966cc4838102830` | `797627b40d75b87883c1da55793655537af99ccb435d14c93a4a047c21f1f240` | one low-water/heat 2:1 material tile |
| `g3/river/river-water-flood-a.png` | `exec-c0a4f649-0a55-40ca-93a2-01d0be7befbb` | `river-water-flood-a-source.png` / `db2624e358519f927703f4e8ee39800ab0df0db693aa31eb8647e705ab520ba4` | `872e6ffd201fa1fcca98c08a284d42332abd0515033fff76a623175894f8e324` | one flood/rain 2:1 material tile |
| `g3/river/river-bank-left-straight-a.png` | `exec-1a53642a-0344-49ce-8f1f-65e464d8ceb0` | `river-bank-left-straight-a-source.png` / `0bbc7da301def751006ff6a3fd2a354494c6296a8275d536df45637c90892d05` | `100fa39f890229122fceebc26f0280cc876c8501937364b72467c722909ce655` | one left-bank straight edge |
| `g3/river/river-bank-right-straight-a.png` | `exec-85c008ed-521f-42df-9965-345057973ce9` | `river-bank-right-straight-a-source.png` / `6dc9fe06e5cd629dc91645bceb06bac311a752d8e6ebc0b96401dfa62fe556fc` | `58bdc74f54cc65ebece5423c8d86ee319e08a7270acc8e3486e5b833a51c5204` | one right-bank straight edge |
| `g3/river/river-bank-left-inner-a.png` | `exec-8447db00-7d32-494c-8b2f-aac537fb4960` | `river-bank-left-inner-a-source.png` / `a55f06e2ab48d2ca14295e0eed6005da4279190a6c844928e5d0a9ebc576dfff` | `c71abc22419942f85b16939b19a30f3210bc2b4edb640979469a5e6b685c8305` | one left-bank inner bend |
| `g3/river/river-bank-left-outer-a.png` | `exec-d60c0365-8ce9-47c3-b3b4-65372b0cc556` | `river-bank-left-outer-a-source.png` / `3f45537c3ee527171da01deb079d79f8df36e476adf5894eb01a63e34e6122ef` | `dde92ee9a6a8c9d0343cff95add892a2b970a20da6c527f6a0078062ed620291` | one left-bank outer bend |
| `g3/river/river-bank-right-inner-a.png` | `exec-2669c1aa-5bf0-4137-af7d-5d4ebb7bea1a` | `river-bank-right-inner-a-source.png` / `240570a083b72781fa6557dd78b75bebed84de15162927054ff9fdf6c36055ad` | `75920f6117c333cff9b9bc9d6d48d5bf64adc433a1f60985a6f30c86eea179bc` | one right-bank inner bend |
| `g3/river/river-bank-right-outer-a.png` | `exec-d21488ab-a180-4627-be80-bf8b49326b5a` | `river-bank-right-outer-a-source.png` / `7d01224560926ba0fa9661de8baf0308f17f57cb53bb0b7361a21c6e2d67feac` | `1ea5ca9d12fbfd2e18ff82465e7ebcc80b36e1c64c68ad2480b2453f3e4325fb` | one right-bank outer bend |
| `g3/river/river-bridge-abutment-a.png` | `exec-9b9a3087-d3d6-4c95-98d7-6f7c0a1645f5` | `river-bridge-abutment-a-source.png` / `18abd8d627acdfb355b8caacc77fa2342f7255de127c3a38470f7d02e96b5074` | `8d9943ed6441b6ef0ade0a82ffd65e523f11a2f8d6f03010756e6f440dfb3ce1` | one single-bank bridge abutment |
| `g3/river/river-rock-soil-transition-a.png` | `exec-02db16b8-2c39-4648-a702-1e48a8f7c8f4` | `river-rock-soil-transition-a-source.png` / `3be54eb70ef284f8fabc6ca49fa66b48c8ab0951126d76becfce8f615a5ea504` | `a083c1d9549c9a42a0f2c00ad6d78aaa2055ebf98063f0a0606272226c7875bf` | one bank rock-to-soil transition |
| `g3/river/river-flood-ripple-a.png` | `exec-39d06c57-7026-4209-b07a-5fee07e76021` | `river-flood-ripple-a-source.png` / `1e7e4e67c1ae02d758fd31be9517f25ce7d4fc81d37d7b6851c262e16448098f` | `9107d3af98121878c9e331b6f1633fee699f75d17d227876bde87c46eb86979f` | one flood-ripple overlay cluster |
| `g3/river/river-bank-conifer-a.png` | `exec-ad2bc6df-f6ac-4de6-9409-e7ce590348b0` | `river-bank-conifer-a-source.png` / `eedcdcf6d00b012a9187cc22944a3e6884eebe814e7719f83c8758868ae6964f` | `7a00a7776daefa647404a42b176b137654656e9193f2b5aa78edf3c2d9f8af6c` | one windswept conifer object |
| `g3/river/river-bank-scrub-a.png` | `exec-1e6ad975-9e07-45e0-9cf5-902e45e3eac4` | `river-bank-scrub-a-source.png` / `b42691fd7e51ae72d1c1ca45204285a226012698a957d1095fc423061cc35f6c` | `1107e80b4c660a2a93801c3d2588d1512bfaf741fc7811231d6f53fa763d24b5` | one windswept scrub-bush object |
| `g3/river/river-bank-outcrop-a.png` | `exec-c3000101-0616-4fb7-bc3b-773d5d9d5bf6` | `river-bank-outcrop-a-source.png` / `9f0fdf039542b7772eea666721c5c0635ba1ba59450ddea282a88fc16a36c9e7` | `1c4f8431e0fd88a6b9b011d6940055c35a646d36406ad7da966b719ef940c3a4` | one compact basalt-outcrop object |

### Atomic river prompt contract

All selected calls used `assets/01-grid-construction.png` as the normal-state visual authority. The heat
tile additionally used `assets/02-heatwave-outage.png`; the flood tile used `assets/04-plant-siting.png` for
the broader winding-water material language. Bank and effect calls used the corresponding already accepted
atomic river object as a style lock. Every call required soot-dark graphite/blue-gray material, upper-right
dim light, painterly embossed detail, exactly one composition unit, and prohibited a complete river, scene,
map, building, road, power equipment, UI, text, atlas, checkerboard, halo, or second piece.

```text
WATER_NEUTRAL_B: one seamless full-bleed dark blue-black/slate-teal water material, calm broad reflection bands, upper-left to lower-right current.
WATER_HEAT_A: one seamless low-water heat material, muddy slate-brown/blue-gray water, warm bronze reflection, sparse sluggish current.
WATER_FLOOD_A: one seamless flood-rain material, cold slate-blue water, faster current, broad cool reflection, layered ripple and sparse foam.
BANK_LEFT_STRAIGHT_A: one narrow left-bank straight edge; land upper-left, water lower-right, visible water-facing drop.
BANK_RIGHT_STRAIGHT_A: one narrow right-bank straight edge; water upper-left, land lower-right, visible water-facing drop.
BANK_LEFT_INNER_A: one narrow left-bank concave 55-degree bend with two tangent ends and water on the lower-right inside edge.
BANK_LEFT_OUTER_A: one narrow left-bank convex 55-degree bend with two tangent ends and water on the lower-right outside edge.
BANK_RIGHT_INNER_A: one narrow right-bank concave 55-degree bend with two tangent ends and water on the upper-left inside edge.
BANK_RIGHT_OUTER_A: one narrow right-bank convex 55-degree bend with two tangent ends and water on the upper-left outside edge.
BRIDGE_ABUTMENT_A: one compact single-bank concrete/steel bridge seat with wing walls, bearing shelf, anchors, and no deck/opposite bank.
ROCK_SOIL_TRANSITION_A: one tapered bank piece changing from a raised rock edge into wet soil/gravel, with compatible narrow endpoints.
FLOOD_RIPPLE_A: one sparse long cluster of separated cold ripple arcs, broken current streaks and tiny foam flecks on empty matte.
CONIFER_A: exactly one isolated wind-bent riverbank conifer, single ground-contact trunk, soot-dark needles and restrained amber rim light; actual alpha outside the tree; no terrain, water, second plant, scene, or checkerboard.
SCRUB_A: exactly one isolated low windswept riverbank scrub bush, single ground-contact root point, soot-black/desaturated-olive foliage and charcoal dead branches; actual alpha outside the bush; no soil patch, terrain, water, second plant, scene, or checkerboard.
OUTCROP_A: exactly one isolated compact connected riverbank basalt outcrop, one elliptical ground-contact footprint, desaturated olive moss only in cracks and restrained amber rim light; actual alpha outside the object; no soil patch, terrain, water, plant, second cluster, scene, or checkerboard.
```

## G.3 Step 3 — atomic grid/facility kit

The source terminals are no longer one power-station raster. The renderer places a main hall, one stack,
one turbine hall and one breaker bay from explicit relative world coordinates. Substations, both pole
classes and authored crossing foundations each resolve to one additional atomic object. ImageGen was
called once per selected asset and no selected file contains a full plant, switchyard row, route, or map.
All eight source canvases used `extract-checkerboard-alpha.py` and a
deterministic 1024 px max-side resize; every runtime PNG has alpha-zero corners.

| runtime path | ImageGen run | preserved source / source SHA-256 | final SHA-256 | unit |
|---|---|---|---|---|
| `g3/grid/plant-main-hall-a.png` | `exec-ff08804a-3196-4794-9c4a-fbe8fc021601` | `atomic-plant-main-hall-a-source.png` / `48b856d4637507ad455713775572ba625573d4d2191a7035f573d022ea8d80c2` | `06f2d29586db8768cbb768098f26567d5f06ef869e1c0b2689d8d8dbc52b7948` | one main-hall building |
| `g3/grid/plant-smokestack-a.png` | `exec-822fe640-c211-45a2-adc2-1e1f1949c4f5` | `atomic-plant-smokestack-a-source.png` / `ae2fcb13631404eba2d07aa084dcbd9a3f2a1c81c06dba5c05ef94ff09a1f5e1` | `9bb91e05b1814fdf57bddbfe8361925850723a8b4357591280eac4805afb6a28` | one smokestack with integral base |
| `g3/grid/plant-turbine-hall-a.png` | `exec-7e1e7885-b6af-4891-bd70-e9ff3fc92830` | `atomic-plant-turbine-hall-a-source.png` / `eb97c8b8076f411797a5545224142adc6883df74ba72bb1c0af1cd023d16b4dd` | `c0d392c613a9667308755f54aaac782a753a78328b97f66e1e1ea83b1d81cea0` | one turbine-hall building |
| `g3/grid/switchyard-breaker-bay-a.png` | `exec-751318e5-a19c-4fc8-811d-c0490c20e401` | `atomic-switchyard-breaker-bay-a-source.png` / `6d62a81973a60b75bd087addd2da51c1356358512aca60a4a11e136022650684` | `6ae34713dd868cc3c735836e8416ba054e12b4017899a215adfd6c7974066dcd` | one breaker bay |
| `g3/grid/substation-transformer-a.png` | `exec-525b7168-42d9-4c04-a538-4afe281489c6` | `atomic-substation-transformer-a-source.png` / `bc8ab955983669e36b3f2c562e917ddccab6061a21c22387d9101189f8f4fedf` | `60c75dd42c66e55b66249c2a4f93ca006fbbd9afdd4b8875ae50b02ede33cfab` | one transformer machine |
| `g3/grid/pole-standard-a.png` | `exec-f0fe7a22-7c24-402d-9494-ad80b442073a` | `atomic-pole-standard-a-source.png` / `f7cce0b1372b951bfff66fddd1470691c8ed6e607f9633bdc14d07b339010619` | `95211238e68b27a6d3a4256006ecc245af1727b584834537e472cda6ebcb0655` | one standard lattice pole; alpha-bound trim with 16 px padding |
| `g3/grid/pole-reinforced-a.png` | `exec-3db19133-0dbf-42ea-b16b-8f6cf89648d4` | `atomic-pole-reinforced-a-source.png` / `014eb6d8c097f8cb28abf69e992da2169d8381c89c214e352196a6eaae47ba82` | `82321ee3e6792750f875009f660be746a4319bde326e0e8ff3391bb0a6a66379` | one reinforced lattice tower; alpha-bound trim with 16 px padding |
| `g3/grid/bridge-foundation-a.png` | `exec-9775ee23-b362-488f-8d0e-2ab8df147252` | `atomic-bridge-foundation-a-source.png` / `0ea44ff2433a3da7de114241802eee58a7c2ce0d4235615607f8ae4f83217d42` | `56041864e4da3257c9f0e43d37ac2e39d3c0a3fba450af1110ba15ed74fc29f5` | one crossing foundation |

### Atomic grid prompt contract

The plant parts used `assets/01-grid-construction.png` and an accepted atomic workshop for the same
orthographic camera and surface language. Electrical equipment and poles used `assets/01` plus
`assets/03-route-comparison.png`; the crossing foundation used `assets/01` plus
`assets/04-plant-siting.png`. Every prompt required exact 2:1 isometric orthographic projection,
upper-right dim light, graphite steel/weathered concrete, exactly one independently placeable physical
unit, and prohibited a second object, compound, full facility, route, map, atlas, UI, text and watermark.
