# Gridworks — `./assets` 스타일 비주얼 제작 명세

이 문서는 루트 `./assets` 네 이미지를 **실제 조작 가능한 Gridworks 화면**으로 번역하는 제작 규격이다.
현재 구현 권한과 repository backlog는 [현재 작업 범위](../ACTIVE_SCOPE.md)와
[남은 구현 작업](../NEXT_TASKS.md), 실제 display·사람 검수는 [외부 출시 gate](../RELEASE_GATES.md), 제품
규칙은 [게임 기획서](GAME_DESIGN_KO.md), 설비와 표현 coverage는
[오브젝트 카탈로그](OBJECT_CATALOG.md)가 소유한다.

## 1. 비주얼 선언

목표는 다음 한 문장으로 판정한다.

> 어두운 회화적 아이소메트릭 도시 속에 배전 설비가 물리적으로 존재하고, 따뜻한 생활광과 차가운
> 전력광 사이에서 건설·공급·비상·보호정지가 Core와 같은 사실로 읽히는 운영 전략 게임.

다음 다섯 축이 모두 맞아야 한다.

1. **카메라** — 고정 사선 아이소메트릭/oblique, 회전 없음
2. **밀도** — 도로·필지·건물·지형이 이어지는 도시, 큰 빈 test plane 없음
3. **재질** — 회화적 콘크리트·강철·토양·수면, 단색 vector 도식 아님
4. **실루엣** — 설비 class와 시설 역할이 world에서 형태로 구분됨
5. **상태광** — cyan/amber/orange-red를 사용하되 형태·pattern·text가 함께 증명

한두 축만 닮은 화면은 스타일 일치가 아니다.

## 2. 기준 이미지 역할

| 파일 | 채택 | 채택하지 않음 |
|---|---|---|
| `01-grid-construction.png` | 야간 산업도시 밀도, cyan 통전, amber 건설, 금속 HUD | 세로 palette, 송전철탑, 영어·수치 |
| `02-heatwave-outage.png` | 건조한 amber 대기, 위험과 정지의 공간적 분위기 | 태양 glare로 정보 가리기, 단일 색 상태 |
| `03-route-comparison.png` | 두 경로를 지도 위에서 동시에 읽는 위계 | 현재 없는 N-1 규칙·BUILD A/B UX 복제 |
| `04-plant-siting.png` | 지형·도시·산업의 큰 덩어리, 거리 overlay의 깊이 | 원전·석탄·발전 입지 gameplay |

네 이미지는 1672×941 합성 reference다. runtime texture atlas나 배경으로 사용하지 않는다.

## 3. 화면 구성

R2의 정보 구조를 유지하고 reference의 재질만 번역한다.

```text
┌───────────────────────────────────────────────────────────────┐
│ 현재 목표 · 시각 · 자금 · 필수 공급 · pause/1×/2×/4×         │
├───────────────────────────────────────────────────────────────┤
│                                                               │
│              회화적 아이소메트릭 청류시 world                │
│       (선택 시 context, 건설 시 shelf/action만 등장)          │
│                                                               │
├───────────────────────────────────────────────────────────────┤
│ 수평 사건 지평선 · 공사 · 사건 · 열 노출 · 정지 · 복귀       │
└───────────────────────────────────────────────────────────────┘
```

- world가 기본 면적의 주인이다.
- 항상 열린 오른쪽 inspector와 세로 도구막대를 두지 않는다.
- heavy metal frame은 상단·하단 경계와 중요한 결정 panel에 집중한다.
- tooltip·popup·inspector가 world의 같은 위치를 동시에 가리지 않는다.
- 한 화면에서 primary CTA는 하나다.

## 4. 월드 카메라와 스케일

- world는 고정 사선 투영을 사용하며 모든 asset은 같은 방위와 elevation을 따른다.
- camera 회전·자유 pitch·원근 zoom은 사용하지 않는다.
- `전체 보기 / 작업 보기 / 상세 보기`의 제한된 zoom을 사용한다.
- default FHD 작업 보기에서 시설은 이름 없이도 역할이 구분되고, 전신주는 기둥·완금·도체 부착
  방향이 읽혀야 한다.
- 고해상도 원본을 임의 축소해 1 px 선과 검은 얼룩으로 만들지 않는다.
- selection outline, hit target와 conductor는 zoom에 관계없이 조작 가능한 최소 화면 크기를 유지한다.
- sprite base와 그림자는 같은 world footprint를 가리키며 선택 bounds가 보이는 실루엣을 넘지 않는다.

## 5. 월드 레이어

아래 순서를 하나의 authority로 고정한다.

```text
terrain base
→ river/water and banks
→ roads/bridges/parcel cuts
→ low city ground props
→ facilities and industrial masses
→ grid equipment bases
→ poles/substations/source structures
→ three-phase conductors and attachments
→ construction/weather/thermal overlays
→ selection/candidate/route highlights
→ world labels and accessibility cues
```

Depth sort는 asset의 임의 이미지 높이가 아니라 authored footpoint/world Y와 stable ID로 결정한다.
도체는 pole attachment 앞·뒤 관계를 보존하고 건물 지붕을 임의로 관통하지 않는다.

## 6. 환경 제작

### 지형

- 숯빛·갈색 토양과 암반, 낮은 채도 녹지, 콘크리트 경계를 큰 형태로 만든다.
- 한 영역을 단색으로 채우지 않고 거친 붓결·마모·배수 흔적을 얕게 쌓는다.
- texture 반복이 눈에 띄는 타일과 화면 전체 noise overlay를 피한다.

### 하천

- 물, 강둑, 제방과 교량이 별도 층으로 보인다.
- 물은 검푸른 반사와 느린 방향성을 가지되 건설 불가 경계가 모호해지지 않는다.
- 강 위 가공선은 허용되지만 지지 설비 ghost는 수면에서 명확히 거부된다.

### 도로와 도시

- 간선도로·생활도로·산업 진입로를 폭·재질·조명으로 구분한다.
- 주거는 반복 stamp가 아니라 3–5개 roof/footprint 변형과 필지·골목·수목 조합을 사용한다.
- 병원·정수장·산업시설은 주변 block보다 큰 silhouette와 고유 landmark를 가진다.
- 빈 공간도 parking, yard, 제방, 나무, utility prop처럼 용도가 읽혀야 한다.

## 7. 설비 제작 규격

### 공통

- 22.9 kV급 배전 규모를 기본으로 한다. reference의 거대한 송전철탑 비례를 그대로 쓰지 않는다.
- 일반/보강 class는 색이 아니라 구조 부재 수, 완금, 기초, 변압기/베이 실루엣으로 구분한다.
- 각 source에는 pivot, footprint, selection shape와 conductor anchor를 저장한다.
- 정상 상태 art와 상태 overlay를 분리해 Core ID가 바뀌지 않아도 표현만 갱신할 수 있게 한다.

### 전신주와 도체

- 일반 pole은 가는 기둥·한정된 접속부, 보강 pole은 더 큰 완금·기초·분기 하드웨어를 가진다.
- 3상 도체는 세 가닥 또는 축소 시 합의된 3상 bundle cue를 유지한다.
- 선은 pole 중심이 아니라 authored attachment에 닿는다.
- 교차는 비접속 gap/높이 차로, 접속은 실제 하드웨어와 node highlight로 구분한다.

### 변전소

- 소형은 한 주기기와 제한된 베이, 대형은 더 큰 주기기·모선·접속 bay로 읽힌다.
- 배치 초안은 실제 footprint와 등급별 exact 반경 R을 동시에 보이고, R 안 수요에는 bracket을 붙인다.
- 선택한 변전소의 service area는 4% 이하 cyan 면과 점선 경계로 표시하되 건물을 물에 잠긴 듯 덮지 않는다.
- 실제 공급은 발전소→변전소의 3상 실선과 변전소→수요의 점선 service link를 구분해 표시한다.
- 사용량·열 상태는 선택 시 world cue와 context에서 같은 값으로 표시한다.

### 시설

- 주거, 의료원, 정수장, 산업단지는 지붕·설비·배관·굴뚝·탱크·표식의 고유 조합을 가진다.
- warm window light는 생활·서비스의 존재를 보여 주되 공급 판정을 대신하지 않는다.
- 미공급은 단순 소등만 쓰지 않고 시설 icon·edge state·문장으로 함께 표현한다.

## 8. 상태 표현표

| 상태 | 색/광원 | 형태·pattern | 아이콘·문장 |
|---|---|---|---|
| 완공·연속 | 낮은 cyan 흐름 | 단일 안정선 | `정상`·현재/연속 |
| 선택 경로 | 밝은 cyan | 발전원→변전소 실선 + 변전소→수요 점선 | 거리/R·첫 병목 |
| 초안 | amber | 점선+footprint | class·배치 가능/거부 |
| 공사 중 | warm amber | scaffold/사선+미완성 부재 | 완공 시각 |
| 비상 운전 | cyan+orange | 이중선/삼각 notch | 노출 남은 시간 |
| 계획 사용불가 | muted orange | 빗금/차단 표식 | 사건명·사용불가 |
| 보호정지 | red-orange | 끊긴 선/X·잠금 | 정지 원인·복귀 시각 |
| 냉각 | dim blue | 점감 pattern | 남은 냉각시간 |
| 복귀 | cyan 회복 | 짧은 재연결 cue | `복귀`·시각 |

glow의 두께·밝기만으로 상태를 구분하지 않는다. motion reduction에서는 pulse를 정지 pattern으로
대체한다.

## 9. UI skin과 타이포그래피

- panel base: graphite black, 미세한 냉청색 또는 갈색 편차
- frame: dark iron/bronze, 얕은 bevel, 모서리만 제한된 rivet
- primary: cyan, planning/secondary: amber, destructive/critical: orange-red
- body text: warm gray/ivory, 숫자는 정렬 가능한 폭과 충분한 contrast
- 제목은 한국어 가독성을 우선한다. reference의 condensed English를 그대로 모사하지 않는다.
- 장식 frame가 content padding과 최소 hit target를 잠식하지 않게 한다.
- disabled는 opacity만 낮추지 않고 pressed·focus와 다른 실루엣/명도/문장을 쓴다.
- icon-only control은 tooltip, 접근성 이름과 44 logical px 이상의 target를 가진다.

## 10. 날씨·시간과 효과

- 정상: 차가운 청회색 ambient, warm city practical lights
- 폭염: amber sky/ground bounce, 건조 haze와 미세 heat distortion
- 비: 젖은 road highlight, 낮은 cloud, restrained rain streak
- 야간: world detail은 유지하고 전력·생활광 대비를 높임

weather layer는 hit test와 Core 상태를 바꾸지 않는다. heat distortion, smoke와 bloom은 선택 outline,
도체 attachment, 숫자·문장을 흐리지 않는다.

## 11. 자산 채택 절차

1. source master의 카메라·광원·scale sheet 작성
2. 배경 없는 RGBA와 alpha fringe 검수
3. asset ID, source, 제작 방법, 날짜와 사용 경계를 manifest에 기록
4. footprint·pivot·attachment·selection bounds authoring
5. normal/building/emergency/outage 또는 공통 overlay 조합 검증
6. 세 zoom과 FHD/UI 100–200%에서 silhouette·anchor 검수
7. 실제 scene에서 city density, depth, hit target와 frame time 검수
8. reference와 나란히 놓고 카메라·밀도·재질·광원·상태 다섯 축을 독립 평가

Git에 포함되지 않은 로컬 후보, prompt만 있는 파일, 합성 화면에서 자른 sprite와 provenance 없는
파일은 채택할 수 없다.

## 12. 상태별 reference sheet

시각 polish scope를 열면 같은 재현 상태에서 다음 네 장을 만든다.

1. normal FHD 전체 화면
2. construction FHD 전체 화면
3. selected asset 상세 crop
4. reference `01-grid-construction.png`와의 side-by-side contact sheet

비교는 pixel similarity가 아니라 다음 rubric을 사용한다.

| 축 | 실패 | 통과 기준 |
|---|---|---|
| 카메라 | 평면 top-down/혼합 방위 | 모든 asset이 같은 fixed oblique 방위 |
| 밀도 | 큰 빈 polygon/test map | 도시·도로·지형이 연속적인 scene |
| 재질 | 단색 vector/SVG 인상 | 회화적 표면과 물질 구분 |
| 실루엣 | 확대해야 class 식별 | 작업 zoom에서 시설·설비 class 식별 |
| 조명 | glow가 형태를 삼킴 | warm city/cool grid 대비와 형태 보존 |
| 상태 | 색 또는 panel만 다름 | world에서 3채널 구분, Core copy 일치 |

한 축이 실패하면 “assets 스타일 완료”라고 부르지 않는다.

## 13. 해상도·접근성·성능

- FHD logical canvas에서 UI 100/125/150/200%를 지원한다.
- UHD는 2× render density에서 texture, conductor, outline과 Korean glyph가 흐려지지 않아야 한다.
- world label·status glyph도 UI scale authority를 따른다.
- icon/line/pattern은 색각 변화와 grayscale에서도 서로 다른 형태를 유지한다.
- 최악의 weather·dense city·모든 state overlay의 reference hardware 예산을 해당 시각 scope에서 수치로
  고정한다.
- 실제 4K panel과 사람의 미감·가독성은 자동 offscreen 결과로 대체하지 않는다.

## 14. 제작 검수표

- 첫눈에 `./assets`와 같은 계열의 세계로 보이는가?
- 도시가 촘촘하지만 전력설비와 건설 위치가 묻히지 않는가?
- 설비가 작은 UI icon이 아니라 물리 구조물로 보이는가?
- 모든 도체가 올바른 attachment에 닿고 비접속 교차가 구분되는가?
- 일반/보강 pole과 소형/대형 변전소가 색 없이 구분되는가?
- 정상·공사·비상·계획 사용불가·보호정지·복귀가 세 채널로 구분되는가?
- world, horizon, context의 ID·시각·원인·수치가 Core와 같은가?
- UI frame 장식이 한국어·클릭 영역·focus를 침범하지 않는가?
- 합성 background, tiny sprite, flat SVG fallback이 없는가?
- 모든 runtime 자산의 source·제작 방법·권리·camera·anchor가 기록됐는가?

자동검사와 내부 리뷰가 모두 통과해도 사람 미감 검토가 없으면
`HumanVisualValidation = NOT_COLLECTED`다.
