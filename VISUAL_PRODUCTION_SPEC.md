# Gridworks — 비주얼 제작 명세

이 문서는 게임의 화면 언어, 첫 플레이 검증판과 장기 콘셉트 스케치의 제작 명세다. 현재
범위·완료조건과 첫 코드 전 fixture 숫자는 `SCOPE_0_PLAYABLE.md`, 첫 코드 후 fixture 숫자는
`data/scenario.json`, 개방된 제품 규칙은 `GAME_DESIGN_KO.md`를 따른다. 이 문서는 규칙이나
숫자를 재정의하지 않으며, 아래 프롬프트는 각 화면을 처음부터 생성할 수 있는 독립 프롬프트다.

현재 승인된 Scope 0는 최종 이미지를 재현하지 않는 디버그 2D 화면 하나만 만든다. 1.0 후보의
제작 캡처는 `construction`, `route-comparison`, `hospital-transfer`, `heatwave`지만 각 기능과
정식 3D 게이트가 통과하기 전에는 제작 의무가 아니다.
현재 발전소 입지 이미지는 공간 비교 레이아웃을 떠올리기 위한 비권위 참고일 뿐이며 그 안의
금융·비용 수치는 구현 결정이 아니다. 장기 방향은 원전·데이터센터·공유 냉각수로 바뀌었지만
해당 이미지는 이번 문서 작업에서 새로 만들지 않는다. 병원 자동절체 화면은 아래 텍스트 명세를
바탕으로 런타임에서 새로 캡처한다.

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

| 화면 | 파일 | 상태·목적 |
|---|---|---|
| Scope 0 디버그 지도 | 런타임 화면 | 현재, 서비스·A/B·고장의 사람 검증 |
| 핵심 건설 | `assets/01-grid-construction.png` | 비권위 참고, 서비스 필드와 실제 연결 설명 |
| 폭염 위기 | `assets/02-heatwave-outage.png` | 비권위 참고, 노후 회선 사용불가와 우회 설명 |
| 경로 비교 | `assets/03-route-comparison.png` | 비권위 참고, 비용·공기·독립성 선택 설명 |
| 병원 자동절체 | 조건부 런타임 캡처 | 1.0 후보, E1 상실·UPS·독립 E2 절체 설명 |
| 기존 발전소 입지 구도 | `assets/04-plant-siting.png` | 후속 화면의 레이아웃 참고; 재현·수치 검증 대상 아님 |

조건부 정식 캡처를 만들게 되면 1672×941 RGB PNG, 16:9 가로형을 사용한다.

### 2.1 현재 Scope 0 화면

첫 화면은 탑다운 2D이며 원·사각형 설비, 굵은 선로와 반투명 서비스 권역만 쓴다. A/B 회랑은
모두 호박색 계획선이고 색이 아닌 `공통 경계` 파선과 `독립 경계` 이중선 패턴으로 구분한다.
오른쪽 `왜?` 패널은 현재 공급 원인, 한 자산 상실 결과, 선택이 바꾸는 비용·완공·연속성의
세 줄만 보여준다. 산업 미학은 색·프레임·타이포의 낮은 수준으로만 적용하고 3D 모델,
후처리, 날씨 VFX와 골든 캡처는 만들지 않는다. 오른쪽 아래 `다음 이정표` 버튼은 미결정
마을 부지나 A/B가 있을 때 비활성화되고 필요한 결정을 한 줄로 설명한다.

Scope 0 경로 카드는 `A · 8 M · 2 DAYS · SHARED BOUNDARY`와
`B · 12 M · 3 DAYS · INDEPENDENT BOUNDARY`만 표시한다. 아래 1.0 콘셉트의 18.4/19.6 M와
52/58일 값을 Scope 0에 복사하지 않는다.

마을의 외곽 셀은 실제 개별 부하가 아니다. 집계 공급률이 72%라면 authored 바깥→안쪽
순서로 셀 28%를 어둡게 그리는 결정론적 표현이다.

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
Electrical rules: solid cyan-white transmission lines must connect the generator to a regional primary substation and then to the single standard type of local distribution substation. The regional primary substation has no service field. Only local distribution substations project translucent cyan polygonal service fields. Buildings inside an energized field are softly lit; unserved outskirts remain dim.
Construction state: show one ghosted amber planned transmission route with planned tower nodes and clearly separate it from solid energized lines.
Composition: wide isometric map fills most of the canvas; narrow top resource bar; left construction palette for power plant, transmission line, primary substation, and local substation; right inspector for the selected 40 MW town substation; bottom time controls.
Exact readable UI tokens: top "120.0 M", top and inspector "32 MW / 40 MW", "12 DAYS TO DEADLINE", "PAUSED". Add no paragraphs or other numbers.
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
Exact readable UI tokens: top "54 MW / 58 MW", "HEAT +8°C", "GRID RESERVE LOW", "DAY 75 16:00", "PAUSED"; right inspector "UNAVAILABLE", "AGED LINE", "0 MW / 180 MW"; compact tag beside the lower bypass bottleneck "34 MW / 35 MW".
Meaning: 54 MW is demand, 58 MW is live supply from the 34 MW bypass plus 24 MW local generation and storage. The failed line carries 0 MW against a 180 MW nameplate rating.
Art direction: original bleak industrial survival-management mood, charcoal steel UI, dirty ivory glare, oxidized brown, cyan-white live current, localized amber-red danger only.
Constraints: communicate both heatwave stress and aged-line unavailability; preserve route readability; no entire-screen red tint, snow, fire, characters, logos, trademarks, watermark, or copied franchise assets.
Avoid: fantasy effects, cheerful mood, tiny text, darkness that hides the network.
```

## 5. 송전 경로 비교 화면

이 화면은 짧은 A와 긴 B를 미적 취향으로 고르는 장면이 아니다. A는 싸고 빠르지만 기존
상위 모선·전기적 차단 경계를 공유해 독립 N-1을 통과하지 못한다. B는 비용과 공기가 조금 더 들지만
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
Route A: shorter amber dashed river-crossing route, 3.8 km, 12 ghost towers, tight bends, a river warning icon, shared upstream bus and electrical contingency boundary causing N-1 failure; also mark shared-corridor common-mode exposure as a separate risk.
Route B: longer amber double-dashed route following a visible public road landmark, 5.1 km, 15 ghost towers, a safer-path shield, independent source-side bus and electrical contingency boundary, selected-path contingency success, and lower common-mode exposure. Show draggable waypoint handles. Both planned routes are amber but use different line patterns, and both must remain different from solid energized lines.
Composition: map occupies about 70 percent; right rugged steel ROUTE COMPARISON panel contains two stacked cards; top bar, left build palette, and bottom time controls remain visible.
Exact right-panel tokens:
"ROUTE A"
"3.8 km"
"12 TOWERS"
"18.4 M"
"52 DAYS"
"PEAK 78%"
"PATH CONTINGENCY: NO"
"ROUTE B"
"5.1 km"
"15 TOWERS"
"19.6 M"
"58 DAYS"
"PEAK 62%"
"PATH CONTINGENCY: YES"
Buttons: two amber planned-action buttons, "BUILD A" and "BUILD B"; use focus outline and pattern, not route color, to show the hovered choice.
Top bar tokens: "120.0 M", "32 MW / 40 MW", "12 DAYS TO DEADLINE", "PAUSED".
Art direction: original bleak industrial survival-management atmosphere, weathered charcoal steel UI, restrained brass, cyan-white live electricity, amber planning language.
Constraints: practical shippable strategy-game UX; exact legible numbers; no extra text, logos, trademarks, watermark, copied UI, or fantasy energy.
Avoid: cinematic-only composition, cheerful palette, unreadable routes, excessive darkness.
```

## 6. 후속 원전·데이터센터·냉각수 화면 계획

`assets/04-plant-siting.png`는 후보 부지와 송출 경로를 한 화면에서 비교하는 레이아웃만
참고한다. 이미지의 발전소 가격, 자기자본, 공기, 후보 A/B와 버튼은 새 장기 방향의 규칙이나
재현 대상이 아니다. 현재 구현은 이 이미지를 데이터·UI·schema의 근거로 사용하지 않는다.

원전·데이터센터 milestone을 실제로 열면 새 `nuclear-datacenter-cooling` 화면을 별도로
설계한다. 지도에는 원전·데이터센터 후보 위치, `LOW/HIGH` 원전 입지 부담, 수원지 geometry,
수랭 가능 hard cut과 두 시설의 송전 연결이 동시에 보여야 한다. hard cut 밖의 데이터센터
후보는 `AIR ONLY`로 표시한다. 선택한 데이터센터 패널은 `SERVICE 0–100%`를 5% 단위로
조작하게 하고, `최종 전력 MW`, `최종 냉각수 CW/h`, 수원지 거리, 거리 펌프 MW,
`WATER/AIR` 상태와 예상 계약 위약을 즉시 미리 본다. 선택한 원전 패널은 목표출력 MW,
거리 펌프 MW와 순계통주입 MW를 서로 다른 행에 표시한다. 냉각수 패널은 원전과 수랭
데이터센터의 점유량을 한 용량 막대에서 분리한다.

냉각수 초과로 시간이 자동 정지한 화면은 `원전 감발 / AIR 전환 / SERVICE 축소` 세 조치의
전력·물·판매·위약 결과를 나란히 비교해야 한다. 자동으로 한 해법을 고르거나 일부 냉각수를
임의 배분하지 않는다. `LOW/HIGH` 배지는 원전 후보의 입지 부담이고 데이터센터 등급이 아니다.

구체 숫자와 UI 토큰은 모두 `TBD`이므로 지금 이미지 생성 프롬프트나 골든 캡처를 만들지 않는다.
후속 adaptive planning checkpoint에서 [1.0 이후 방향](POST_1_0.md)의 범위를 다시 승인한 뒤
텍스트 명세부터 확정한다.

## 7. 병원 독립공급·자동절체 화면

이 1.0 런타임 캡처는 병원에 선이 두 개 보이는 것과 실제 독립 공급이 다르다는 사실을 한
화면에서 설명한다. 공간 지도와 작은 단선결선도를 함께 보이고 같은 병원 중요모선으로 들어오는
E1·E2가 서로 다른 상위 모선, 전기적 차단 경계와 공간 회랑을 사용해야 한다.

캡처 시점은 E1 계획정지가 시작된 직후다. E1은 개방·무전압이고 병원 핵심부하는 UPS가
지탱한다. E2는 살아 있지만 고정 자동절체 검증 중이며, 작은 타임라인은
`E1 OPEN → UPS <1s → E2 TRANSFER <10s`를 보여준다. 전환 완료 상태에서는 병원이 계속
밝고 고객 경계 P0 미공급은 0이어야 한다. 비상 디젤은 대기이며 E1/E2와 같은 전원으로
계산하지 않는다.

필수 구조 토큰은 `HOSPITAL P0`, `E1 OPEN`, `E2 READY`, `UPS`, `TRANSFER <10s`,
`P0 UNSERVED 0`이다. N-1 제거 대상, 두 경로의 차단 경계·회랑 표식과 UPS 남은 시간이 색 없이도
읽혀야 한다. 단순히 같은 선을 두 갈래로 그리거나, 절체 공백을 없었던 것으로 숨기거나,
비상 디젤 MWh를 전력 판매로 표시하면 실패다.

패널의 설치 표기는 `2×40 MW`, firm service 표기는 정상과 한 베이 제거 뒤 모두 `40 MW`다.
두 베이를 합산한 `80 MW AVAILABLE` 표현은 금지한다.

## 8. 제작 검수 기준

1. 1.0 화면의 통화 표기는 모두 기호 없는 `M`이다.
2. 통전, 계획, 주의, 고장의 상태 색이 모든 화면에서 동일하다.
3. 서비스 필드는 배전 변전소에만 붙고 1차 변전소에는 붙지 않는다.
4. 위기 화면의 `54/58`, `34/35`, `0/180`은 서로 다른 계량 대상을 뜻한다.
5. 경로 화면의 비용·공기는 `GAME_DESIGN_KO.md`의 단순 산식과 일치한다.
6. 특정 작품의 화면·건물·아이콘을 알아볼 수 있게 복제하지 않는다.
7. `N-1`과 회랑 공통원인 위험은 서로 다른 표식과 설명 행을 사용한다.
8. 폭염 화면의 `GRID RESERVE LOW`는 공급력 합계와 다른 판정이다. 예비력 게이트가 열린 뒤
   숫자를 표시하려면 제약 시간대, 적격 MW와 요구 MW를 골든 상태에 함께 고정한다.
9. 병원 화면은 E1/E2의 상위 모선·차단 경계·회랑 독립성과 UPS 절체 공백을 함께 설명한다.
10. 기존 발전소 입지 이미지는 1.0 또는 원전·데이터센터 확장의 UI·숫자 검수에 사용하지 않는다.
