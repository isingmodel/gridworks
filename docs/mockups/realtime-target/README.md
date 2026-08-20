# Realtime game-screen design targets

This directory contains a standalone, original HTML/CSS/SVG design target for
the realtime grid-operations experience. It depicts the intended visual and UX
direction; it is not wired to the Godot runtime.

## Artifact boundary

```text
ArtifactPurpose = DESIGN_TARGET
RenderSource = HTML_CSS_SVG
RuntimeAsset = NO
GodotSceneEvidence = NO
R2CompletionEvidence = NO
R3ArtCompletionEvidence = NO
HumanReviewEvidence = NO
DefaultProductPath = NO
```

The target uses no production raster assets or critic/reference imagery. Its
city, equipment, conductors, UI, and state cues are authored directly in HTML,
CSS, and inline SVG. Do not add these PNGs to the runtime asset manifest or cite
them as implementation, playtest, professional-review, or release evidence.

## Screens

- `normal`: calm realtime operation with a physical distribution corridor,
  selected substation, bounded inspector, and a horizontal event horizon.
- `emergency`: first critical thermal incident, auto-pause reason, local red
  equipment cues, exact operating limits, trip/recovery timing, and one clear
  action.
- `construction`: a single active planning flow with an amber physical route
  ghost, equipment trade-offs, Core-shaped quote copy, and comparison-forecast
  markers.

Open the source with `?state=normal`, `?state=emergency`, or
`?state=construction`:

```text
gridworks-realtime-target.html?state=normal
gridworks-realtime-target.html?state=emergency
gridworks-realtime-target.html?state=construction
```

## Rendered targets

| State | FHD target | UHD target |
|---|---|---|
| Normal operation | `renders/normal-fhd.png` | `renders/normal-uhd.png` |
| Thermal emergency | `renders/emergency-fhd.png` | `renders/emergency-uhd.png` |
| Construction planning | `renders/construction-fhd.png` | `renders/construction-uhd.png` |

FHD renders are 1920×1080. UHD renders are the same composition and information
at 3840×2160, rendered at 2× density rather than expanded with more panels.

### Render SHA-256

```text
construction-fhd.png  27d7fa310e93e8135d10ce12e078a71fc6b46a03cde82507048575b950413042
construction-uhd.png  cdd8aa0c0aed795e4ac9a1e76d4baccc6a241ef0c5b9552558d4199dd8d44fb9
emergency-fhd.png     d0e345c73f92bc220b7683a5d42363b499b8b5ef736493cea15d1221a1d9f01b
emergency-uhd.png     05890147d30485f309226636ae730d0f6f8a0ad55610d4fd2ef243fc6d31d765
normal-fhd.png        6e248b5c4c9987c6f429d93584a18a6c2a47fcfa2892d3cdf0966ccda53aaa78
normal-uhd.png        80350029862d9e76baec34f54bd4256761c8ffc64cd2f5257688f3b7cef245d6
```

## Visual vocabulary

- Material colors describe normal equipment: damp asphalt, concrete,
  galvanized steel, gravel, and dark three-phase conductors.
- Cyan means selection or energized analysis.
- Amber means planning or comparison.
- Red means a localized real emergency or protective failure.
- Shape, line treatment, icons, and Korean text duplicate every status color.
- The map remains primary; inspector, tools, and modal surfaces are conditional.
