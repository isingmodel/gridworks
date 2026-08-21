# Gridworks 상용 2D 레퍼런스 정렬 재구축 계획

> 상태: **DRAFT COMPLETE — G.3 구현 활성화 대기, 구현 권한 아님**
> 작성 근거: 2026-08-21 G.2 실행 화면에 대한 소유자 시각 거부
> 대상 해상도: **1920×1080, UI 100%·125%만 해당**

이 계획은 `assets/01~04`의 낮은 아이소메트릭 산업도시, 전력망, 강과 UI 위계를 실제 게임 화면에
가깝게 옮기기 위한 G.3 후보를 정의한다. whole-map plate는 사용하지 않는다. 보이는 지형·건물·시설은
개별 tile/object와 명시 데이터로 구성하고, 게임 규칙과 화면 표현이 어긋나지 않게 한다.

구현은 루트 README가 G.3를 활성 단계로 선언한 뒤에만 시작한다. 상세 비교와 종료 판정은
[레퍼런스 정렬 평가 프로토콜](REFERENCE_PARITY_EVALUATION_PROTOCOL_KO.md)이 소유한다.

## 1. G.2 실패 진단

- 현재 map transform은 평면 직교 좌표다. 3/4 object PNG를 70~104px로 줄여 사각 terrain 위에
  놓으므로 자산 내부의 각도·깊이·재질이 화면에서 사라진다.
- 지형은 정사각형 texture 반복이고 city obstacle은 강·주거·의료 세 polygon뿐이어서 레퍼런스의
  도로·구획·산업시설 밀도와 맞지 않는다.
- 항상 보이는 footprint 원과 긴 label이 시설 실루엣보다 강하다. 선로는 얇은 단일 polyline이라
  복수 도체·철탑 접속·빛이 읽히지 않는다.
- 화면은 지도와 오른쪽 panel을 나눠 지도 자체를 축소한다. 레퍼런스처럼 도시 위에 HUD가 얹히지
  않고, 사건 timeline도 독립적인 단계 모듈로 보이지 않는다.
- 현재 강은 네 꼭짓점의 직선 quadrilateral에 단일 어두운 청록 texture를 clip한 것이다. 굽은 수로,
  제방 높이, 토사·암석 경계, 수면 반사, 수위 상태, 교량과의 접합이 없다.

## 2. 고정 시각 기준

### 2.1 reference 역할

| reference | 고정 역할 |
|---|---|
| `assets/01-grid-construction.png` | 기본 카메라, 야간 도시 밀도, cyan 통전·amber 계획, HUD 비례 |
| `assets/02-heatwave-outage.png` | 폭염 광원, 마른 지면·강, 사용불가·경고 상태 |
| `assets/03-route-comparison.png` | 철탑 크기, 복수 경로, 선로 도체, 우측 비교 panel |
| `assets/04-plant-siting.png` | 지형 깊이, 굽은 강과 계곡, 대형 발전소·도시 거리감 |

### 2.2 style bible

- 고전 2:1 isometric orthographic, yaw `45°`, pitch `35.264°`, perspective convergence 없음
- 모든 object는 같은 top·south-east·south-west 면과 같은 upper-right key light를 가진다.
- graphite steel, weathered concrete, soot-dark 산업 표면을 쓰되 중간톤을 남긴다.
- 통전은 cyan, 계획은 amber, 열·사용불가는 orange/red를 사용하고 pattern·shape도 함께 쓴다.
- 창문·작업등은 작은 amber 광원이다. 순수 검정 matte, checkerboard, baked UI·문구를 금지한다.
- solid object의 그림과 공간 충돌은 일치해야 한다. 통과 가능한 장식이 건물처럼 보여서는 안 된다.

## 3. 구현 전 목표 화면

runtime에 포함하지 않을 1920×1080 target mockup 세 장을 먼저 만든다.

1. 평상·야간 건설: 첫 임무 지도와 통전 전/계획 상태
2. 폭염·사용불가: 열 국면, 보호정지, 낮아진 강물 표현
3. 경로·입지 비교: 복수 경로, 두 전원, 굽은 강과 오른쪽 inspector

각 mockup은 소유자 화면을 layout edit target으로, 위 reference를 역할별 style input으로 사용한다.
고정 멀티모달 LLM jury가 카메라·강·밀도·UI의 첫 formative gate를 통과시키기 전에는 전체 runtime
자산을 만들지 않는다.

## 4. 아이소메트릭 좌표와 입력

Core world 좌표와 거리·충돌·비용·열 계산은 유지하고 presentation transform만 바꾼다.

```text
screenX = originX + (worldX - worldY) × scaleX
screenY = originY + (worldX + worldY) × scaleY
scaleY = scaleX × 0.5
```

역변환을 같은 transform이 소유한다. home zoom에서 `3200×2000` world는 약 `1500×750px` diamond가
되도록 시작하고, 기존 1×·1.5×·2.25× zoom과 anchor pan을 유지한다.

- world→screen→world 오차 1 unit 이하
- 세 zoom의 click, keyboard cursor, point drag, candidate snap이 같은 Core 좌표를 선택
- world circle은 72점 polygon을 투영해 ground ellipse로 표시
- risk, service radius, footprint와 draft가 같은 transform을 사용
- depth key는 projected footprint bottom Y, tie는 node ID 총순서

## 5. 강물 재구축

강은 별도 핵심 제작 단위다. `CHEONGRYU_RIVER`의 네 점 직선 polygon을 그대로 그림만 꾸미지 않는다.

### 5.1 권위 geometry

- 기존 통과 회랑과 두 bridge foundation은 유지하면서 좌·우 bank를 각각 8~12점 polyline으로 만든다.
- centerline은 화면에서 최소 세 번 방향이 변하고, 폭은 완만하게 `10~25%` 변한다.
- 인접 segment의 폭 변화는 `20%`를 넘지 않는다. foundation 접점에서 두 bank가 정확히 이어진다.
- water collision 변경은 world v2에 기록하고, 수면 금지점·foundation 허용점·여덟 임무 원형을 다시
  검증한다. flood가 넓어 보인다면 그 범위도 authored risk authority 안에 있어야 한다.

### 5.2 개별 river kit

- neutral water diamond 2종
- low-water/heat surface 1종
- flood/rain surface 1종
- left/right bank straight·inner bend·outer bend 6종
- bridge abutment·rock/soil transition 3종
- reflection/ripple overlay 2종

water surface는 흐름 방향이 일치하는 2:1 tile이고 bank는 transparent object다. 3×3 반복에서 seam과
같은 물결의 반복이 보이지 않아야 한다. 수면은 asphalt보다 푸른 반사와 넓은 highlight를 가지며,
하류 bank에는 `2~8px` 깊이 그림자를 둔다.

### 5.3 상태 표현

- 평상: 짙은 청회색 수면, cyan 설비광의 약한 반사, 젖은 bank
- 폭염: 낮은 수면, 드러난 토사·돌, warm haze. collision 밖의 가짜 마른 땅은 만들지 않는다.
- 폭우·범람: 더 거친 ripple과 차가운 반사, authored flood risk의 hatch·경계
- ReduceMotion: 흐름과 ripple 이동을 멈추고 고정 highlight·pattern으로 같은 상태를 전달

## 6. 개별 art 목록

G.3 예상 runtime raster는 **48종**이다. `5.2`의 river kit 15종을 모두 독립 file로 센 수치이며,
water/bank 기반 3종으로 축약하지 않는다.

- ground·parcel 15종: diamond ground 4, neutral 2·heat 1·flood 1 water surface 4,
  두 방향 road·교차·yard·plaza 5, residential/hospital base 2
- transparent river edge·effect 11종: bank straight·inner bend·outer bend 6,
  bridge abutment·rock/soil transition 3, reflection/ripple overlay 2
- world object 16종: main plant, auxiliary switchyard, pole 2, bridge foundation, substation,
  residential cluster 3, hospital 2, water facility 2, industry/warehouse 3
- UI chrome 6종: top metric plate, inspector 9-slice, tool slot, default/cyan/amber button plate

합계는 `15 + 11 + 16 + 6 = 48`이며 manifest에서 family별 개수와 전체 개수를 모두 검사한다.

ImageGen은 atlas batch가 아니라 자산 하나당 한 호출을 사용한다. 모든 호출은 고정된 style anchor와
해당 family에 가장 관련 있는 원본 reference를 함께 받는다. camera drift·투명 배경 실패·edge halo는
폐기 사유다. reference screenshot이나 target mockup의 pixel을 crop해 runtime sprite·tile·background로
재사용하는 것은 금지한다. background removal은 **그 호출에서 새로 생성한 단일 object**를 투명화할
때만 허용한다. whole-map image를 잘라 여러 asset인 것처럼 등록하는 것도 금지한다.

각 raster는 `assetId`, family, generator run ID, prompt SHA, reference SHA, 원본 출력 SHA, 투명화 여부,
최종 PNG SHA와 실제 runtime binding을 manifest에 남긴다. asset-sheet는 이 manifest의 개별 PNG에서
자동 조립하며 별도의 합성 원화를 사용하지 않는다.

홈 zoom 목표 표시 크기는 main plant `200~240px`, switchyard `110~130px`, substation `130~160px`,
standard pole `70~80px`, reinforced pole `85~100px`, landmark `170~220px`, residential cluster
`150~200px`다.

## 7. 도시 밀도와 공간 진실성

- road paint, 균열, 낮은 토사처럼 통과 가능한 시각 요소는 별도 visual-layout authority에 둔다.
- 건물·탱크·창고처럼 solid로 보이는 요소는 world v2 obstacle polygon을 가진다.
- 장애물은 기존 checker-owned 성공 회랑을 먼저 표시한 뒤 그 밖에 배치한다.
- 지도 영역의 준검정 공백은 `10%` 이하, 각 주요 district는 서로 다른 cluster 3개 이상을 가진다.
- plant와 industry는 단일 icon이 아니라 yard·부속 건물로 읽히되 player path를 거짓으로 막지 않는다.

## 8. 전력망·상태 renderer

- energized edge: 8px shadow + 5~6px cyan glow + 2px core, attachment point 사이 dual conductor
- planned edge: amber dual conductor, dash와 ground guide
- protective outage: 끊긴 red/orange core와 hatch; ReduceMotion에서는 pulse 없음
- span은 규칙상 직선이지만 화면에서는 접속점 사이에 얕은 sag curve를 그린다.
- 항상 보이는 footprint ring과 non-pole label은 제거하고 hover·selection·keyboard focus에서만 연다.
- pole과 cable은 projected Y로 정렬하고 cable은 tower 뒤/앞 접속 관계가 자연스럽게 보이게 두 pass로
  나눈다.

## 9. 화면 구성

map은 1920×1080 canvas 전체를 채우고 HUD가 위에 겹친다.

- top HUD `80px`
- left tool rail `86px`
- right inspector `340px`
- bottom event timeline `128px`
- UI 아래 지도 입력은 차단하되 도시 이미지는 이어진다.

inspector는 선택·공급/열·비용/시간·현재 행동을 먼저 보여주고 긴 briefing은 펼침 영역으로 옮긴다.
timeline은 최소 `15px` 글자와 `16px` marker로 briefing→authored window/phase→actual result를 표시한다.
현재는 amber plate, 완료는 cyan check, 예정은 graphite empty marker다. timeline은 계속 읽기 전용이다.

## 10. LLM jury checkpoint와 종료

1. target mockup 세 장: camera·river·density·HUD formative jury
2. 48개 개별 asset-sheet와 west plant→river→east district vertical slice: asset provenance·실제 입력·
   first-light runtime jury
3. 평상·폭염·폭우·겨울밤 전체 상태: exact-tree final jury

첫 번째 checkpoint를 통과하기 전에는 전체 asset family를 만들지 않는다. 최종 native evidence는
first-light, substation draft, pole draft, energized route, heat/outage, flood, winter,
UI 125% ReduceMotion 여덟 장이다.
720p mode는 만들거나 실행·검수하지 않는다.

파일·authority·input·build hard gate는 LLM visual score와 별도로 모두 통과해야 한다. 사람 review는
사용하지 않는다. 상세 jury 구성, calibration, 점수·차이 보고서와 pass gate는 평가 프로토콜을 따른다.

## 11. 사용자 요구사항 추적

| 요구사항 | 계획 소유 위치 | 종료 증거 |
|---|---|---|
| `assets/`와 최대한 같은 느낌·각도·설계 | §2~4, §7~9 | runtime 5 pair의 camera·density·material·HUD jury |
| whole-map image를 뒤에 두지 않음 | §6 | 48개 provenance·runtime binding hard gate와 5개 asset-kit board jury |
| 개별 tile/object 생성·적용 | §5.2, §6 | 개별 generator run·PNG SHA와 kit sheet jury |
| 강물·제방 품질 | §5 | `PAIR-KIT-RIVER`, `PAIR-NORMAL/HEAT/FLOOD`, river category `≥85` |
| 독립 event timeline bar | §9 | actual-input timeline round trip과 `PAIR-KIT-UI/NORMAL/HEAT/ROUTE` |
| 사람 review 없이 LLM judge만 사용 | §10과 평가 프로토콜 | diverse 3-judge jury, order reversal, replicate, LLM adjudication |
| 720p 미지원 | 문서 상단과 §10 | 1920×1080 UI 100%·125% manifest만 허용 |
