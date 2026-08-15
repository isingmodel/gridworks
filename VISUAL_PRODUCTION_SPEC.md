# Gridworks — 비주얼 제작 명세

이 문서는 게임의 화면 언어와 네 개의 기준 콘셉트 스케치를 재현하기 위한 제작 명세다.
게임 규칙과 숫자의 권위 기준은 `GAME_DESIGN_KO.md`이며, 아래 프롬프트는 각 화면을 처음부터
생성할 수 있는 독립 프롬프트다.

1.0의 실제 제작 캡처는 `construction`, `route-comparison`, `hospital-transfer`, `heatwave`다.
현재 원전 입지 이미지는 장기 캠페인용 선행 콘셉트이고, 병원 자동절체 화면은 아래 텍스트
명세를 바탕으로 런타임에서 새로 캡처한다.

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

| 화면 | 파일 | 범위·목적 |
|---|---|---|
| 핵심 건설 | `assets/01-grid-construction.png` | 1.0, 서비스 필드와 실제 송전 연결 설명 |
| 폭염 위기 | `assets/02-heatwave-outage.png` | 1.0, 노후 회선 사용불가와 임시 우회 설명 |
| 경로 비교 | `assets/03-route-comparison.png` | 1.0, 비용·공기·N-1 선택 설명 |
| 병원 자동절체 | 런타임 신규 캡처 | 1.0, E1 상실·UPS·독립 E2 절체 설명 |
| 발전소 입지 | `assets/04-plant-siting.png` | 1.0 이후, 님비·금융·필수 송출망 설명 |

모든 기준 이미지는 1672×941 RGB PNG, 16:9 가로형이다.

## 3. 핵심 전력망 건설 화면

이 화면은 “범위 안에 있으면 자동으로 전기가 생긴다”는 오해를 막아야 한다. 발전소에서
1차 변전소, 배전 변전소로 이어진 실제 통전 경로와 배전 변전소의 서비스 필드를 서로 다른
그래픽 언어로 표현한다. 플레이어가 선택한 마을 변전소는 40 MW 중 32 MW를 공급하고 있으며,
아직 건설되지 않은 노선은 실선이 아니라 호박색 계획 상태로 남는다.

화면을 처음 본 사람은 발전소와 마을이 어디 있는지, 어느 선로가 실제로 살아 있는지,
어느 건물이 서비스 범위 안인지, 선택한 공사가 아직 미완성이라는 사실을 차례대로 읽을 수
있어야 한다. 분위기보다 연결 관계가 우선이며, UI 숫자는 현재 상태와 정격을 혼동시키지
않는다.

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

이 화면은 고장난 선로, 살아 있는 우회선과 전체 수요를 한 숫자로 섞지 않는다. 상부 154 kV
회선은 명판 180 MW지만 현재 사용불가라 0 MW다. 하부 임시 우회선은 35 MW 중 34 MW를
전달하고, 현지 가스·배터리 24 MW가 더해져 54 MW 수요에 58 MW 공급력이 남는다. 병원은
살아 있지만 계통은 안전하지 않다.

폭염의 밝은 눈부심과 구리색 먼지는 환경 압력을 전달하되 전기 경로를 가리지 않아야 한다.
적색은 실제 고장 구간에만 쓰고, 용량 한계는 호박색으로 구분한다. 플레이어가 화면만 보고도
“고장선은 복구 대상, 우회선은 현재 병목, 배터리는 시간 제한 자원”이라고 설명할 수 있어야
한다.

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

이 화면은 짧은 A와 긴 B를 미적 취향으로 고르는 장면이 아니다. A는 싸고 빠르지만 기존
상위 모선·보호구역을 공유해 독립 N-1을 통과하지 못한다. B는 비용과 공기가 조금 더 들지만
전기적으로 독립된 접속과 별도 공간 회랑을 확보한다. N-1 실패와 공통회랑 위험은 관련되어
있어도 같은 판정은 아니므로 별도 행과 표식으로 나타낸다.

지도상의 경로를 가리키면 비교 카드의 길이, 철탑, 비용, 완공일, 피크 부하율과 안전 판정이
함께 강조되어야 한다. 사용자는 두 버튼을 보기 전에 추가 1.2 M과 6일이 무엇을 사는지
문장으로 이해할 수 있어야 한다.

```text
Use case: ui-mockup
Asset type: original PC strategy game transmission-route planning mockup, 16:9 landscape.
Primary request: show two mutually exclusive planned 154 kV routes between the industrial power area and a town substation, with exact cost, schedule, tower count, peak loading, and N-1 comparison.
Scene: severe semi-rural industrial valley with river, road corridor, power plant, factory, town, hospital, substations, and an existing cyan-white energized network. Desaturate terrain slightly while keeping ports and overlays crisp.
Route A: shorter amber dashed river-crossing route, 3.8 km, 12 ghost towers, tight bends, a river warning icon, shared upstream bus and protection zone causing electrical N-1 failure; also mark shared-corridor common-mode exposure as a separate risk.
Route B: longer pale-cyan dashed route following a live public road, 5.1 km, 15 ghost towers, a safer-path shield, independent source-side bus and protection zone, N-1 success, and lower common-mode exposure. Show draggable waypoint handles. Planned routes must remain different from solid energized lines.
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

이 화면은 원전 본체 가격만 비교하지 않는다. 가까운 후보 A는 송출망이 짧지만 주민 노출에
따른 입지비가 커지고, 먼 후보 B는 입지비를 피하는 대신 긴 독립 회선과 복구 위험을 부담한다.
주거지 최단거리와 실제 송전 경로 길이는 다른 측정값이므로 이름을 분리한다.

오른쪽 패널은 기술비용과 지금 지불할 자기자본을 구분한다. 현금 120 M인 상태에서 B의
120 M 자기자본은 확정 가능하지만 A의 228 M은 불가능하다. 이는 A가 게임 규칙상 금지된
후보라는 뜻이 아니라 금융 조치가 먼저 필요하다는 뜻이다. 입지 띠는 서비스 범위가 아니며,
화면의 색만 보아도 두 시스템을 혼동하지 않게 해야 한다.

현재 정적 스케치는 캠페인 day 0, 개발 옵션 미구매 상태에서 최종 투자를 바로 비교하는
화면이다. `D-630/D-664`는 오늘부터의 소요일수이고 `CAPITAL`은 옵션 선납 전 총 자기자본이다.
옵션을 구매한 실제 화면은 A/B 각각 선납 6/10 M을 표시하고, 남은 자기자본 222/110 M,
절대 `COD DAY`, 옵션·최종 투자 마감과 승인 진행률을 보여준다. 옵션 뒤에도 총액이나 채권을
선납액만큼 다시 가산하지 않는다.

```text
Use case: ui-mockup
Asset type: original PC strategy game nuclear plant-siting and project-finance mockup, 16:9 landscape.
Primary request: compare a near and remote nuclear plant site against a town, hospital, factory, and existing grid, showing deterministic NIMBY cost, required capital, construction date, and the mandatory export network for a 900 MW station.
Scene: isometric semi-rural industrial valley. A compact town and hospital sit on the right, factory and a 1,200 MW regional hub near the lower center, river and barren hills divide the map, and a remote plateau lies on the left.
Siting overlay: regulatory and community-exposure distance bands measured from residential parcels—muted red inside 1.5 km, amber 1.5–3 km, desaturated yellow 3–6 km, cool gray-green beyond 6 km. These bands are not service fields. Show distance labels and dotted measurements from the nearest town edge.
Candidate A: translucent nuclear station ghost at 2.4 km residential distance near town; two closely spaced independent amber 345 kV circuit routes on separate tower rows; compact route tags "2×345 kV" and "EXPORT 1.85 km".
Candidate B: translucent nuclear station ghost at 6.8 km residential distance on the distant plateau; a visibly longer paired 345 kV export corridor on separate tower rows; compact route tags "2×345 kV" and "EXPORT 6.9 km".
Composition: wide map occupies 70 percent; right rugged dark-steel comparison inspector occupies 30 percent; narrow top bar; left plant toolbar with LNG, COAL, NUCLEAR, SOLAR, WIND, with NUCLEAR selected.
Exact SITE A card:
"SITE A — RESIDENTIAL 2.4 km"
"PLANT 420 M"
"SITING +630 M"
"GRID 90 M"
"TOTAL 1,140 M"
"CAPITAL 228 M · D-630"
Exact SITE B card:
"SITE B — RESIDENTIAL 6.8 km"
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

## 7. 병원 독립공급·자동절체 화면

이 1.0 런타임 캡처는 병원에 선이 두 개 보이는 것과 실제 독립 공급이 다르다는 사실을 한
화면에서 설명한다. 공간 지도와 작은 단선결선도를 함께 보이고 같은 병원 중요모선으로 들어오는
E1·E2가 서로 다른 상위 모선, 보호구역과 공간 회랑을 사용해야 한다.

캡처 시점은 E1 계획정지가 시작된 직후다. E1은 개방·무전압이고 병원 핵심부하는 UPS가
지탱한다. E2는 살아 있지만 고정 자동절체 검증 중이며, 작은 타임라인은
`E1 OPEN → UPS <1s → E2 TRANSFER <10s`를 보여준다. 전환 완료 상태에서는 병원이 계속
밝고 고객 경계 P0 미공급은 0이어야 한다. 비상 디젤은 대기이며 E1/E2와 같은 전원으로
계산하지 않는다.

필수 구조 토큰은 `HOSPITAL P0`, `E1 OPEN`, `E2 READY`, `UPS`, `TRANSFER <10s`,
`P0 UNSERVED 0`이다. N-1 제거 대상, 두 경로의 보호구역·회랑 표식과 UPS 남은 시간이 색 없이도
읽혀야 한다. 단순히 같은 선을 두 갈래로 그리거나, 절체 공백을 없었던 것으로 숨기거나,
비상 디젤 MWh를 전력 판매로 표시하면 실패다.

## 8. 제작 검수 기준

1. 네 이미지의 통화 표기는 모두 기호 없는 `M`이다.
2. 통전, 계획, 주의, 고장의 상태 색이 모든 화면에서 동일하다.
3. 서비스 필드는 배전 변전소에만 붙고 1차 변전소에는 붙지 않는다.
4. 위기 화면의 `54/58`, `34/35`, `0/180`은 서로 다른 계량 대상을 뜻한다.
5. 경로 화면의 비용·공기와 발전소 입지 화면의 총액·자기자본은 `GAME_DESIGN_KO.md`의
   산식과 일치한다.
6. 발전소 입지 화면은 A만 자금 부족이며 B만 즉시 확정 가능하다.
7. 특정 작품의 화면·건물·아이콘을 알아볼 수 있게 복제하지 않는다.
8. `N-1`과 회랑 공통원인 위험은 서로 다른 표식과 설명 행을 사용한다.
9. 발전소의 `RESIDENTIAL` 거리와 `EXPORT` 경로 길이를 같은 거리로 오해하게 만들지 않는다.
10. 폭염 화면의 `RESERVE 6%`는 세 시간대 예비력 중 현재 제약을 결정하는 값이며, 상세
    패널에서는 즉시·15분·2시간 구간을 분리한다.
11. 병원 화면은 E1/E2의 상위 모선·보호구역·회랑 독립성과 UPS 절체 공백을 함께 설명한다.
12. 발전소 입지 정적 스케치의 `CAPITAL`은 옵션 전 총 자기자본이며 `D`는 소요일수다.
