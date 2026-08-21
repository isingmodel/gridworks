# Commercial city plate v1 generation record

- Generated: 2026-08-21
- Tool: OpenAI built-in ImageGen
- Output: `commercial-city-plate-v1.png` (`1672×941`)
- SHA-256: `151a498dc4e6f6284c045a430f1cf3a90873b9db7ca944a9fcec4490a522846c`
- References: `assets/01-grid-construction.png` through `assets/04-plant-siting.png`, used only for
  style, mood, camera angle, material and city-density guidance.

## Base prompt

```text
Use case: stylized-concept
Asset type: production runtime game environment background plate for a 16:9 power-grid strategy map
Input images: Images 1-4 are style, mood, camera-angle, material, and city-density references only
Primary request: derive an original clean background plate for the fictional Korean regional city Cheongryu, matching the references' polished industrial strategy-game atmosphere while leaving all power-network information to live game overlays
Scene/backdrop: one dense but readable city divided by a winding north-to-south river; older power/industrial district on the west and southwest; compact residential neighborhoods on the east; a recognizable hospital campus at mid-east; water utility and larger industrial blocks toward the southeast; roads, bridges, hills, riverbanks, smaller houses, workshops, warm windows, and sparse streetlights
Style/medium: detailed painterly 2D game environment, slightly oblique top-down view, grounded industrial realism, aged steel-and-stone city surfaces, original design
Composition/framing: wide 16:9 map plate, map fills the frame edge to edge, the river creates a central construction constraint, several visually open land corridors remain available for live line construction, no separate UI margins
Lighting/mood: moody late-afternoon-to-evening operational city, warm amber windows against cool blue-gray terrain, atmospheric but not murky
Color palette: charcoal, oxidized bronze, muted slate blue, deep river teal, warm amber lights; restrained contrast so bright cyan and amber live network overlays remain dominant
Materials/textures: worn asphalt, concrete, river water, dark rooftops, industrial steel, subtle terrain relief
Constraints: no text; no letters; no numbers; no UI panels; no borders; no icons; no logos; no watermark; no power lines; no cables; no pylons; no poles; no substations; no highlighted service circles; no route overlays; no warning zones; no dashed lines; no glowing network; no selection outlines; no readable signage. Keep the city plate visually useful beneath precise code-native gameplay overlays.
```

## Exact-review refinement prompt

```text
Edit this existing runtime game environment plate. Preserve the exact canvas aspect ratio, overall oblique top-down industrial Korean city composition, central north-to-south river, road network, dense neighborhoods, factories, warm evening windows, charcoal/bronze/teal palette, painterly texture, and restrained contrast. Remove every baked semantic facility landmark that could be mistaken for authoritative gameplay data: remove the red-cross hospital and its recognizable medical campus, remove all circular water-treatment tanks/basins and water-utility complex, remove any distinctive civic or utility iconography. Replace those areas seamlessly with generic low-rise residential, municipal, workshop, and warehouse blocks matching their surroundings. Do not add or move power infrastructure. No text, letters, numbers, UI, borders, icons, logos, watermark, power lines, cables, pylons, poles, substations, service circles, route overlays, warning zones, dashed lines, glowing network, selection outlines, or readable signage. The central river remains atmospheric only; live code-native overlays will define exact build-blocking terrain and facilities.
```
