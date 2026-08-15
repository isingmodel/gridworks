# Gridworks — 비주얼 제작 명세

이 문서는 게임의 화면 언어와 네 개의 기준 콘셉트 스케치를 재현하기 위한 제작 명세다.
게임 규칙과 숫자의 권위 기준은 `GAME_DESIGN_KO.md`이며, 아래 프롬프트는 각 화면을 처음부터
생성할 수 있는 독립 프롬프트다.

## 1. 공통 아트 방향

- 장르 인상: 혹독한 환경에서 도시의 생명선을 관리하는 산업 생존 전략
- 화면: 읽기 쉬운 아이소메트릭 3D 지도와 견고한 2D 운영 UI의 결합
- 재질: 풍화된 철, 리벳, 그을린 콘크리트, 산화 갈색 지면, 더러운 상아색 하늘
- 상태 색: 통전 청백색, 계획 호박색, 주의 주황색, 실제 고장만 적색
- UI: 숯검정 철제 프레임, 절제된 황동 체결부, 큰 숫자와 단순한 용량 막대
- 감정: 기반시설이 무너지면 도시도 멈춘다는 무게감
- 금지: 원형 도시와 중앙 거대 발전기, 특정 게임의 건물·아이콘·글꼴·문구 복제, 증기기관
  판타지, 외계 에너지, 과도한 네온, 로고와 워터마크

## 2. 기준 산출물

| 화면 | 파일 | 목적 |
|---|---|---|
| 핵심 건설 | `assets/01-grid-construction.png` | 서비스 필드와 실제 송전 연결 설명 |
| 폭염 위기 | `assets/02-heatwave-outage.png` | 노후 회선 사용불가와 임시 우회 설명 |
| 경로 비교 | `assets/03-route-comparison.png` | 비용·공기·N-1 선택 설명 |
| 발전소 입지 | `assets/04-plant-siting.png` | 님비 할증·금융·필수 송출망 설명 |

모든 기준 이미지는 1672×941 RGB PNG, 16:9 가로형이다.

## 3. 핵심 전력망 건설 화면

```text
Use case: ui-mockup
Asset type: original PC strategy game gameplay concept sketch, 16:9 landscape, polished pre-production UX image.
Primary request: show an isometric power-grid construction game in which the player connects a gas power plant to transmission lines, a regional substation, distribution substations, a town, hospital, and factory while controlling cost and spare capacity.
Scene: a severe semi-rural industrial valley with a river, low hills, roads, one gas plant at the map edge, a factory district, a compact town, and a recognizable hospital.
Electrical rules: solid cyan-white transmission lines must connect the generator to a regional primary substation and then to local distribution substations. The regional primary substation has no service field. Only the town and industrial distribution substations project translucent cyan polygonal service fields. Buildings inside an energized field are softly lit; unserved outskirts remain dim.
Construction state: show one ghosted amber planned transmission route with planned tower nodes and clearly separate it from solid energized lines.
Composition: wide isometric map fills most of the canvas; narrow top resource bar; left construction palette for power plant, transmission line, primary substation, and local substation; right inspector for the selected 40 MW town substation; bottom time controls.
Exact readable UI tokens: top "120.0 M", top and inspector "32 MW / 40 MW", "D-12", "PAUSED". Add no paragraphs or other numbers.
Art direction: bleak original industrial survival-management atmosphere, weathered charcoal steel UI, restrained brass, soot-stained structures, sparse vegetation, dusty overcast light, cyan-white current, amber construction. Modern electrical infrastructure, not steampunk.
Constraints: practical shippable strategy-game UI; towers rather than underground cables; service fields visibly different from transmission routes; no characters, logos, trademarks, or watermark; no copied franchise layout, type, icon, or architecture.
Avoid: cheerful pastoral mood, fantasy crystals, glossy sci-fi, central circular city, excessive darkness, unreadable type.
```

## 4. 폭염과 노후 송전선 사용불가 화면

```text
Use case: ui-mockup
Asset type: original PC strategy game emergency-event gameplay concept sketch, 16:9 landscape.
Primary request: show a severe heatwave combined with an aged 154 kV transmission-line outage, successful emergency rerouting, and a grid that is still close to its limit.
Scene: the same type of semi-rural industrial valley with power plant, town, hospital, factory, substations, river, and two transmission corridors. Brutal late-afternoon heat, washed-out copper sky, white sun glare through dust, cracked ground, and visible heat shimmer. No snow.
Grid state: the old upper transmission corridor is unavailable. Its conductors are dark with no electrical glow, one tower carries a maintenance-lockout and aged-equipment warning, and a localized red-orange diagnostic outline marks the corridor without fire or explosion. A lower temporary bypass is complete and carries bright cyan-white current toward the town and hospital; its bottleneck pulses amber near capacity. A local gas and battery path contributes 24 MW. The hospital stays powered, outer town cells fade amber, and factory lighting is partially curtailed.
Composition: top status bar, left operating tools, right unavailable-line inspector, bottom time controls, wide readable isometric map.
Exact readable UI tokens: top "54 MW / 58 MW", "HEAT +8°C", "RESERVE 6%", "D-0", "PAUSED"; right inspector "UNAVAILABLE", "CONDITION 6%", "0 MW / 180 MW"; compact tag beside the lower bypass bottleneck "34 MW / 35 MW".
Meaning: 54 MW is demand, 58 MW is live supply from the 34 MW bypass plus 24 MW local generation and storage. The failed line carries 0 MW against a 180 MW nameplate rating.
Art direction: original bleak industrial survival-management mood, charcoal steel UI, dirty ivory glare, oxidized brown, cyan-white live current, localized amber-red danger only.
Constraints: communicate both heatwave stress and aged-line unavailability; preserve route readability; no entire-screen red tint, snow, fire, characters, logos, trademarks, watermark, or copied franchise assets.
Avoid: fantasy effects, cheerful mood, tiny text, darkness that hides the network.
```

## 5. 송전 경로 비교 화면

```text
Use case: ui-mockup
Asset type: original PC strategy game transmission-route planning mockup, 16:9 landscape.
Primary request: show two mutually exclusive planned 154 kV routes between the industrial power area and a town substation, with exact cost, schedule, tower count, peak loading, and N-1 comparison.
Scene: severe semi-rural industrial valley with river, road corridor, power plant, factory, town, hospital, substations, and an existing cyan-white energized network. Desaturate terrain slightly while keeping ports and overlays crisp.
Route A: shorter amber dashed river-crossing route, 3.8 km, 12 ghost towers, tight bends, a river warning icon, shared upstream bus and corridor exposure, N-1 failure.
Route B: longer pale-cyan dashed route following a live public road, 5.1 km, 15 ghost towers, a safer-path shield, independent source-side bus, N-1 success. Show draggable waypoint handles. Planned routes must remain different from solid energized lines.
Composition: map occupies about 70 percent; right rugged steel ROUTE COMPARISON panel contains two stacked cards; top bar, left build palette, and bottom time controls remain visible.
Exact right-panel tokens:
"ROUTE A"
"3.8 km"
"12 TOWERS"
"18.4 M"
"D-52"
"PEAK 78%"
"N-1: NO"
"ROUTE B"
"5.1 km"
"15 TOWERS"
"19.6 M"
"D-58"
"PEAK 62%"
"N-1: YES"
Buttons: amber "BUILD A" and cyan "BUILD B".
Top bar tokens: "120.0 M", "32 MW / 40 MW", "D-12", "PAUSED".
Art direction: original bleak industrial survival-management atmosphere, weathered charcoal steel UI, restrained brass, cyan-white live electricity, amber planning language.
Constraints: practical shippable strategy-game UX; exact legible numbers; no extra text, logos, trademarks, watermark, copied UI, or fantasy energy.
Avoid: cinematic-only composition, cheerful palette, unreadable routes, excessive darkness.
```

## 6. 발전소 입지·님비·금융 비교 화면

```text
Use case: ui-mockup
Asset type: original PC strategy game nuclear plant-siting and project-finance mockup, 16:9 landscape.
Primary request: compare a near and remote nuclear plant site against a town, hospital, factory, and existing grid, showing deterministic NIMBY cost, required capital, construction date, and the mandatory export network for a 900 MW station.
Scene: isometric semi-rural industrial valley. A compact town and hospital sit on the right, factory and a 1,200 MW regional hub near the lower center, river and barren hills divide the map, and a remote plateau lies on the left.
Siting overlay: regulatory and community-exposure distance bands measured from residential parcels—muted red inside 1.5 km, amber 1.5–3 km, desaturated yellow 3–6 km, cool gray-green beyond 6 km. These bands are not service fields. Show distance labels and dotted measurements from the nearest town edge.
Candidate A: translucent nuclear station ghost at 2.4 km near town; two closely spaced independent amber 345 kV circuit routes on separate tower rows; one compact route tag "2×345 kV".
Candidate B: translucent nuclear station ghost at 6.8 km on the distant plateau; a visibly longer paired 345 kV export corridor on separate tower rows; one compact route tag "2×345 kV".
Composition: wide map occupies 70 percent; right rugged dark-steel comparison inspector occupies 30 percent; narrow top bar; left plant toolbar with LNG, COAL, NUCLEAR, SOLAR, WIND, with NUCLEAR selected.
Exact SITE A card:
"SITE A — 2.4 km"
"PLANT 420 M"
"SITING +630 M"
"GRID 90 M"
"TOTAL 1,140 M"
"CAPITAL 228 M · D-630"
Exact SITE B card:
"SITE B — 6.8 km"
"PLANT 420 M"
"SITING +0 M"
"GRID 180 M"
"TOTAL 600 M"
"CAPITAL 120 M · D-664"
Top bar tokens: "120.0 M", "NUCLEAR 900 MW", "PAUSED". Do not add a currency symbol before 120.0 M.
Buttons: SITE A uses a disabled dark charcoal button with restrained red-amber outline labeled "INSUFFICIENT"; SITE B uses an active cyan button labeled "BUILD B".
Art direction: original bleak industrial survival-management atmosphere; weathered steel and concrete, oxidized terrain, dirty ivory sky, cyan-white existing current, amber planned construction, localized red only for severe siting cost.
Constraints: show paired circuits rather than a single 600 MW line; distinguish siting bands from service coverage; no protest slogans, people, logos, trademarks, watermark, central circular city, or copied franchise UI.
Avoid: magical rings, fantasy energy, glossy sci-fi, Victorian steampunk, cheerful pastoral palette, illegible text.
```

## 7. 제작 검수 기준

1. 네 이미지의 통화 표기는 모두 기호 없는 `M`이다.
2. 통전, 계획, 주의, 고장의 상태 색이 모든 화면에서 동일하다.
3. 서비스 필드는 배전 변전소에만 붙고 1차 변전소에는 붙지 않는다.
4. 위기 화면의 `54/58`, `34/35`, `0/180`은 서로 다른 계량 대상을 뜻한다.
5. 경로 화면의 비용·공기와 발전소 입지 화면의 총액·자기자본은 `GAME_DESIGN_KO.md`의
   산식과 일치한다.
6. 발전소 입지 화면은 A만 자금 부족이며 B만 즉시 확정 가능하다.
7. 특정 작품의 화면·건물·아이콘을 알아볼 수 있게 복제하지 않는다.
