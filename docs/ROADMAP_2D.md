# Gridworks — 에셋 스타일 실시간 게임 로드맵

> 현재 상태: **A0+A0.1+UX-R0+UX-R1 완료 · UX-R2.1 runtime carve-out 활성**
> 제품 아트: **A1 일반 운전 아트 vertical slice — 미개방**

이 로드맵은 `./assets`의 회화적 아이소메트릭 스타일을 R1/R2 기반의 실제 게임으로 옮기는 순서를
정한다. 로드맵의 다음 단계는 자동 구현 권한이 아니다. 권한은 루트 [README](../README.md), 정확한
목표는 [현재 계약](scopes/ASSET_STYLE_REALTIME_GAME.md), 진행 증거는
[체크리스트](ROADMAP_2D_CHECKLIST.md)가 소유한다.

## 제작 원칙

- 한 번에 한 gate만 연다.
- 각 gate는 player outcome 하나, exact 파일 경계, 자산 allowlist와 종료 증거를 먼저 고정한다.
- R1 Core를 UI에서 재계산하지 않고 typed snapshot·forecast·transition을 사용한다.
- `./assets`와 비슷한 팔레트만 만드는 것을 완료로 인정하지 않는다.
- source asset의 provenance·camera·scale·anchor 없이 world 통합을 시작하지 않는다.
- placeholder와 production asset을 같은 scene에서 조용히 혼합하지 않는다.
- 자동검사·native capture·사람 미감·전문 검토를 서로 대신하지 않는다.
- 라이브 테스트는 필요한 경계 직전의 이름 붙은 deterministic checkpoint에서 시작한다.
- 전체 시작 E2E는 onboarding, save/migration, 누적 상태, default scene·package와 전체 campaign에만 쓴다.
- 과거 v2/R2 증거는 회귀 기반일 뿐 새 아트 gate의 PASS가 아니다.

## A0 — 목표·문서 기준선 — 완료

### 결과

- `./assets` 네 이미지와 hash를 visual reference authority로 고정
- 회화적 fixed-oblique world, 도시 밀도, 재질, 실루엣, 조명과 상태 언어 정의
- R1 규칙·R2 UX 보존, default `CommercialMain`, no-active-code-gate 경계 기록
- 현재 목표 문서만 남기고 완료·중단된 과거를 압축 아카이브로 이전
- 실패한 HTML/vector 목표를 현재 목표와 증거에서 제외

### 상한

A0는 runtime 화면, 새 sprite, native capture나 미감 검토를 만들지 않는다.

## A0.1 — A1 전 구조 준비 — 완료

### 결과

- Core와 Game의 Debug/Release 실시간 authority를 상용 `ExportRelease` v2 package에서 분리
- 미승인 persistence·future world/data의 wildcard compile/package 유입 차단
- 동일 state/horizon forecast cache와 cheap minute query, pointer-only world 갱신 경로
- `IRealtimeWorldView`를 통한 placeholder/향후 asset renderer 교체 경계
- exact Core suite filter와 `A1_NORMAL_READY`·`A1_CONSTRUCTION_DUE_1M` 단일 구간 runner

### 상한

A0.1은 A1의 구조·검증 진입점만 준비했다. runtime asset authority, 도시 아트, reference capture와
사람 미감 증거는 만들지 않았고 A1은 계속 미개방이다.

## UX-R2.1 — FIRST_LIGHT release tutorial/rail — 활성

### player outcome

nondefault Debug R2에서 실제 release `FIRST_LIGHT` 장의 briefing을 읽고, `FIRST_LIGHT_SUPPLY`
phase/event 동안 실시간 clock·공사·사건을
조작해 authored standard result까지 도달한다. future-event rail에서 현재 시각, 다음 사건 countdown,
event start/end와 actual/draft construction completion을 한 축으로 비교한다. named checkpoint에서는
자동 frame injection이 아니라 실제 production mouse/keyboard 입력으로 한 minute 경계를 직접 플레이한다.

### 경계

- shared strict V2+V3 overlay loader와 release first-chapter in-memory prefix
- R2 main/presenter/event rail, Debug interactive checkpoint host와 관련 exact tests
- 기존 두 technical checkpoint fixture/hash는 보존
- 기존 story-part unit bytes와 FIRST_LIGHT briefing/result native presentation을 동일성 비교
- non-score Debug 직접 관찰은 허용하되 candidate/E2E/official score 증거로 승격하지 않음
- art asset/world, 2–8장, promise, thermal presentation, persistence, default/export/package는 미개방

### 현재 증거

- product source `ec265999bc849ff494d14011f04c718b03a7664a`, 독립 review P0 0/P1 0
- Debug build 0 warnings, Realtime 23/673, Commercial 31/7084, 34 story part와 전체 UI 행렬 PASS
- 두 automated checkpoint의 동결 start/replay/end hash 유지와 interactive host scene-load PASS
- 실제 FIRST_LIGHT와 두 interactive checkpoint 입력은 macOS console 잠금으로 아직 `PENDING`

따라서 이 gate는 source-ready지만 완료가 아니다. headless oracle은 실제 mouse/keyboard record를 대신하지
않으며 `CommercialUXProxy`는 계속 `null`이다.

exact 파일 allowlist와 종료 증거는
[실시간 상용 UX 87 scope](scopes/COMMERCIAL_UX_87.md#ux-r21--first_light-release-tutorialrail--활성)가 소유한다.

## A1 — 일반 운전 아트 vertical slice — 미개방

### player outcome

플레이어가 한 FHD 화면에서 촘촘한 청류시를 보고, 실제 R1 clock이 흐르는 동안 전신주·선로를
계획·발주해 완공하고, 새 경로가 물리 world에서 통전되는 것을 확인한다.

### bounded content

- 한 `FIRST_LIGHT` world와 exact linked fixture
- `A1_NORMAL_READY`, `A1_CONSTRUCTION_DUE_1M` checkpoint
- 주거지 1, 필수시설 1, 산업시설 1
- 발전 접속점, 일반/보강 pole, 일반/보강 line, 소형 변전소
- terrain, river/bank, road set, dense city block
- normal, selected, draft, invalid, building, commissioned 상태
- 상단 HUD, 수평 사건 지평선, 조건부 context/build/action

### gate

- source asset allowlist와 manifest 100%
- 같은 camera·light·scale, alpha fringe와 anchor 오류 0
- 합성 배경·flat SVG/tiny fallback 0
- no-click clock과 한 건설 흐름이 actual scene에서 Core와 동일
- 각 checkpoint의 시작 canonical hash와 종료 상태가 exact contract와 동일
- FHD normal/construction capture와 reference contact sheet
- 카메라·밀도·재질·실루엣·조명·상태 rubric 모두 PASS
- 독립 P0/P1 0

## A2 — 사건·열·복귀 표현 — 미개방

### player outcome

폭염 사건이 시작되면 같은 도시의 대기와 조명이 변하고, pole·line·substation의 비상 노출,
보호정지와 복귀를 world·horizon·context에서 같은 설비·시각·원인으로 읽는다.

### bounded content

- heatwave lighting/weather layer
- planned authored unavailable와 thermal protective outage의 별도 표현
- emergency exposure, trip, cooling, recovery
- pole/line/substation 세 bottleneck witness
- `A2_HEATWAVE_PRETRIP_1M`, `A2_PROTECTIVE_OUTAGE`, `A2_RECOVERY_DUE_1M` checkpoint
- auto-pause reason과 next event

### gate

- actual R1 transition→presentation→draw/AX identity
- 색·형태/pattern·icon/text 3채널
- normal→heatwave→outage→recovery native capture set
- weather가 hit·anchor·Core state를 바꾸지 않음
- forecast=actual과 selected target 불변

## A3 — production city·asset catalog — 미개방

### player outcome

전체 현재 설비와 시설이 한 도시에서 일관된 카메라·재질·scale로 보이고, 어느 zoom에서도 class,
접속과 상태를 식별할 수 있다.

### content

- 일반/보강 pole·line, 소형/대형 변전소, 발전 접속점
- 주거·의료원·정수장·산업단지
- terrain biome, road/bridge, riverbank, city/industrial props
- normal/building/emergency/planned-outage/protective-outage coverage
- 필요한 LOD와 atlas/import preset

### gate

- catalog/manifest completeness 100%
- missing resource·silent fallback·mixed camera/scale 0
- depth, conductor attachment, collision/selection bounds matrix
- FHD/UHD dense worst-case frame budget
- independent art/UX P0/P1 0

## A4 — 전체 실시간 campaign·save 통합 — 미개방

### player outcome

같은 회화적 도시와 전력망이 프롤로그부터 마지막 사건까지 이어지고, 저장·재개 뒤에도 시각·Core·
사건 지평선이 동일하다.

### content

- 여덟 장의 실시간 schedule과 authored profile
- 비용·공사기한·도시 약속·결과·에필로그
- strict production V3 data
- save v4와 안전한 기존 save 보존/migration
- 새 기본 장면 전환 후보

### gate

- 전체 campaign deterministic/headless
- mid-event·mid-construction fresh-process restore
- 이전 save 원본 보존과 migration 반례
- default scene 전환은 이 gate 종료 때만

## A5 — native·사람·전문 검토와 package — 미개방

### 내부 구현 gate

- FHD와 UHD render target에서 clipping·texture·anchor·focus·성능
- macOS clean package, fresh install, save→continue→completion
- asset/license/build manifest와 fallback audit
- unresolved crash·data loss·softlock·P0/P1 0

### 별도 외부 gate

- 처음 보는 사람의 bounded flow와 소유자 전체 campaign
- 실제 FHD/4K panel의 가독성·미감·피로도
- 한국어 전문 교정
- 전력설비 silhouette·용어 전문 검토
- Developer ID 서명·공증·공개 배포 결정

내부 자동 gate가 끝나도 외부 항목이 없으면 `PublicReleaseStatus = NOT_AUTHORIZED`다.

## 현재 열지 않는 것

- UX-R2.1 이후의 interface·schema·placeholder
- production V3 data·persistence·default scene 전환
- `game/assets/realtime/` 또는 `game/realtime/world/` 로컬 후보의 암묵적 채택
- 원전·석탄·재생에너지 기술 tree와 발전 입지
- procedural city·3D·camera rotation·sandbox
- 반복 image generation을 통한 무제한 스타일 탐색

## 공통 종료조건

- gate가 지정한 player outcome이 실제 scene에서 끝난다.
- Core state와 world/HUD/horizon/context가 같은 ID·시각·원인·수치를 쓴다.
- 거부된 입력은 world·draft·journal을 바꾸지 않고 visible result를 남긴다.
- source·hash·권리·camera·anchor·상태 coverage가 manifest와 일치한다.
- 지원 화면에서 clipping, 작은 hit target, focus trap과 색 전용 cue가 없다.
- 영향을 받는 결정론 검사와 build가 통과한다.
- 실제 capture와 독립 범위 검토에서 P0/P1이 0이다.
- 라이브 검증은 가장 가까운 checkpoint에서 bounded하게 실행하고 evidence label을 구간 PASS로 남긴다.
- 처음부터 실행한 경우 checkpoint로 대체할 수 없었던 이유와 전체-flow evidence label을 기록한다.
- README, 목표 계약, 비주얼 명세와 체크리스트를 같은 변경에서 갱신한다.

사람 미감·재미·전문성은 자동 assertion 수로 대신하지 않는다.
