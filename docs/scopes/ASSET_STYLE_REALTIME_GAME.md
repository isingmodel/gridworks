# Gridworks — 에셋 스타일 실시간 게임 목표 계약

> 문서 상태: **현재 전체 목표 · UX-R2.1 non-score 완료, 다음 gate 미개방**
> 제품 아트: `A1_NORMAL_OPERATION_ART_SLICE` — 미개방

## 1. 목표

Gridworks의 최종 화면을 루트 `./assets` 네 이미지가 보여 주는 **회화적이고 고밀도인 아이소메트릭
산업도시 전략 게임**으로 전환한다. 기술 도식 위에 UI를 얹는 수준이 아니라, 도로·하천·주거·병원·
산업시설과 전력설비가 하나의 도시 풍경을 이루고 그 위에서 실제 실시간 Core 상태가 읽혀야 한다.

새 목표는 기존 작업을 버리는 전면 재작성도, 네 이미지를 복제하는 작업도 아니다.

- R1의 결정론적 clock·공사·사건·열·forecast를 규칙 기반으로 보존한다.
- R2의 상단 HUD, 수평 사건 지평선, 조건부 inspector·action dock와 입력 원칙을 UX 기반으로 보존한다.
- world와 설비, 재질, 조명, 상태 표현은 `./assets`의 품질선에 맞춰 새 production pipeline으로 만든다.
- 동결 v2는 기본 장면과 회귀·저장 기준으로 유지한다.

## 2. 권위

### 2.1 시각 방향 권위

| 기준 이미지 | SHA-256 | 소유하는 질문 |
|---|---|---|
| `assets/01-grid-construction.png` | `23c9acec1b8026ebcb8eebf329eb6b94201179f8952451aa27a71ab38b7ebedc` | 정상 운전, 건설 ghost, 통전 흐름, UI 재질 |
| `assets/02-heatwave-outage.png` | `47d4e53b9d9bad74b6afce6311acce023dc3f9642ffd0ea16609d67b6960a630` | 폭염 조명, 위험, 보호정지, 도시 분위기 |
| `assets/03-route-comparison.png` | `f471ac24cbab24d9b1aff89953595d70fc3ccaf4e8c08c442651125ab3c65828` | 여러 경로의 동시 비교와 정보 위계 |
| `assets/04-plant-siting.png` | `10908370f3a2d8403e62ce2a97e39b7ba5c43d8eb0e32073f03e5b3521c01092` | 지형·도시·산업의 큰 덩어리와 입지 overlay |

이 네 파일은 **스타일과 품질의 권위**다. 다음은 소유하지 않는다.

- 게임 수치, 설비 class, 캠페인 또는 전기 규칙
- runtime sprite·texture·font·icon의 재사용 권리
- 영어 문구, 송전철탑·원전·석탄·입지 시스템의 제품 포함 여부
- 고정 오른쪽 패널이나 세로 도구막대의 최종 UX 배치

### 2.2 규칙과 UI 권위

- 규칙: R1 `Release.V3` Core와 해당 typed snapshot·forecast·transition
- 제품 맥락: [게임 기획서](../product/GAME_DESIGN_KO.md)
- 설비 능력: [오브젝트 카탈로그](../product/OBJECT_CATALOG.md)
- 표현: [비주얼 제작 명세](../product/VISUAL_PRODUCTION_SPEC.md)
- 단계: [로드맵](../ROADMAP_2D.md)과 [체크리스트](../ROADMAP_2D_CHECKLIST.md)
- runtime 자산 provenance: 루트 [ASSET_MANIFEST](../../ASSET_MANIFEST.md)

Core와 화면이 충돌하면 화면이 규칙을 재계산하지 않고 Core를 따른다. 기준 이미지와 실제 게임
오브젝트가 충돌하면 이미지의 미감만 유지하고 현재 제품의 배전 설비·한국어·UX를 따른다.

## 3. 보존하는 기반

### R1

- 고정 tick 실시간 clock과 pause·1×·2×·4×
- timestamped command와 시간에 따른 공사 완공
- event reveal/start/end와 deterministic priority
- 선로·전신주 접속부·변전소의 비상 노출→보호정지→냉각·복귀
- forecast=actual과 canonical state

### R2

- 상단 bounded HUD
- 현재 시각을 기준으로 움직이는 수평 사건 지평선
- 선택할 때만 나타나는 inspector, build shelf와 action dock
- simulation/tool/surface의 단일 reducer
- 마우스·키보드, focus, 한 입력→한 owner→한 결과
- FHD logical canvas, UI scale와 색 외 상태 표현 원칙

R2의 마지막 전체 harness는 완료되지 않았으므로 이 목록은 구현 기반이지 종료 증거가 아니다. 새 아트
단계는 필요한 범위의 연결을 다시 증명해야 한다.

## 4. `./assets`에서 추출한 스타일 DNA

### 4.1 공간

- 고정된 사선 아이소메트릭/oblique 관점, 회전 없음
- 빈 배경이 아니라 도로망·필지·하천·주거·산업 설비가 겹쳐 보이는 연속 도시
- 발전 접속점, 변전소, 전신주와 시설이 장난감 아이콘이 아니라 공간을 차지하는 구조물로 보임
- 지형은 단색 polygon이 아니라 높낮이·암반·토양·제방·수면·도로 재질의 큰 덩어리로 읽힘
- 안개·연기·열기·날씨는 깊이를 만들되 클릭 대상과 전력 상태를 덮지 않음

### 4.2 재질과 광원

- 숯빛·청회색·갈색의 낮은 채도 기반
- 산화된 철, 주철, 콘크리트, 젖은 도로와 오래된 산업설비의 회화적 표면
- 따뜻한 창·가로등·작업등과 차가운 전력 glow의 대비
- 모든 가장자리를 같은 vector stroke로 두르지 않고 빛·재질·실루엣으로 깊이를 만듦
- bloom은 신호를 돕는 얇은 층이며 오브젝트 형태와 선로 부착점을 흐리지 않음

### 4.3 상태 색 언어

- cyan/blue: 통전, 선택 경로, 활성 설비와 비교안 B
- amber: 계획, 공사, 비교안 A와 주의
- orange/red: 열 위험, 보호정지, 사용불가와 실패
- warm white: 도시 생활과 필수 서비스
- graphite/steel: UI shell과 비활성 기반

색 하나만 쓰지 않는다. 상태마다 최소 세 채널을 쓴다.

```text
색 또는 명도
+ 선/실루엣/패턴
+ 아이콘 또는 짧은 한국어 문장
```

### 4.4 UI 재질

- 검은 금속·청동 계열의 깊이 있는 frame, 얇은 bevel과 제한된 rivet
- 지도보다 어두운 panel body와 명확한 active/disabled/critical 상태
- condensed한 수치 위계와 넉넉한 클릭 영역
- 장식은 정보 경계를 강화할 때만 사용하며 모든 카드에 무거운 frame을 반복하지 않음

R2의 상단 HUD·사건 지평선·조건부 dock 구조는 유지한다. 기준 이미지의 고정 세로 palette와 항상 열린
오른쪽 패널은 복제하지 않는다.

## 5. 금지하는 근사

아래 결과는 `./assets` 스타일로 인정하지 않는다.

- 단색 polygon 지형 위에 작은 SVG·선·원만 그린 graph-first world
- 합성 이미지를 한 장 배경으로 깔고 hit area만 얹은 화면
- 고해상도 sprite를 30–40 px로 줄여 실루엣과 부착점이 사라진 화면
- 도시 대부분이 빈 평면이고 전력설비만 흩어진 test map
- cyan glow만 추가한 기존 placeholder
- 모든 상태를 색 하나로 구분하거나 텍스트 panel에만 설명한 화면
- asset마다 카메라·빛·그림자 방향·scale이 다른 collage
- 생성 출처·권리·source hash·alpha edge가 없는 production 파일
- 기준 이미지의 영어, 송전급 철탑, 발전기술과 수치를 현재 배전 게임 규칙처럼 복제한 화면

## 6. 목표 플레이 화면

### 6.1 정상 운전

기본 FHD 화면 하나에서 다음이 동시에 읽힌다.

- 주거 밀집지, 의료원 또는 정수장, 산업 구역
- 하천, 두 종류 이상의 도로와 지형 경계
- 발전 접속점, 배전 변전소, 전신주·3상 도체와 실제 연결
- 현재 통전 경로와 여유, 계획/공사 중 설비
- 상단 목표·시각·자금·신뢰도·속도
- 수평 사건 지평선
- 선택 시에만 나타나는 맥락 정보와 정확한 발주 견적

### 6.2 폭염·정지

같은 world가 교체되는 것이 아니라 조명·대기·상태 layer가 변한다. 설비 ID와 위치는 유지되고,
비상 노출·보호정지·냉각·복귀가 world, horizon, inspector에서 같은 시각·원인으로 보인다.

### 6.3 건설·경로 비교

건설 ghost는 지형 위에 떠 있는 UI 선이 아니라 실제 footprint·지지점·도체 부착과 공사 상태를 가진다.
두 경로를 비교할 때 비용·완공 시각·열여유·첫 병목은 Core typed quote/forecast에서 오며, 두 색과 두
형태로 지도에서 동시에 구분된다.

## 7. 자산 파이프라인

production 자산 하나는 다음을 가져야 한다.

```text
stable asset ID
source 경로와 원본 hash
제작/생성 방법과 날짜
재사용·배포 경계
카메라 방위·고도·기준 scale
광원 방향과 그림자 규칙
alpha fringe·premultiply 검수
world footprint와 selection bounds
전선/입출력/부착 anchor
normal/building/emergency/outage 변형 또는 overlay 계약
LOD 또는 축소 규칙
```

source master와 runtime derivative를 분리한다. 임의로 잘라낸 합성 화면, prompt만 남은 파일,
hash가 없는 후보와 Git에 포함되지 않은 로컬 파일은 production authority가 될 수 없다.

## 8. 구간 직접 진입형 라이브 테스트

### 8.1 기본 원칙

라이브 테스트는 캠페인 처음부터 재생하지 않는다. **검증하려는 경계 직전의 이름 붙은 결정론적
checkpoint에서 시작해 필요한 실제 조작과 렌더 구간만 실행하는 것**을 기본으로 한다.

```text
정확한 checkpoint를 만든다
→ 시작 identity와 canonical state를 확인한다
→ production controller·presentation·input·render 경로에 연결한다
→ 필요한 조작·시간만 진행한다
→ bounded 결과와 종료 identity를 기록한다
```

중간 상태 진입은 테스트 대상 adapter를 우회하는 임의 UI 주입이 아니다. 초기 Core 상태를 짧게 만들
뿐이며, 진입 뒤에는 실제 scene의 clock, reducer, presenter, hit routing, accessibility와 draw 경로를
그대로 사용한다.

### 8.2 checkpoint 계약

checkpoint 하나는 최소 다음을 고정한다.

```text
CheckpointId
authoritative fixture/schema ID와 source hash
Core state 생성 방법과 command replay hash
시작 minute와 canonical state SHA-256
active construction/event/duty/thermal state
예상 world selection과 presentation anchor
허용된 다음 입력·시간 범위
종료 assertion과 evidence label
```

상태 생성은 다음 우선순위를 따른다.

1. exact fixture와 deterministic scenario builder
2. baseline부터의 bounded canonical command replay
3. save/restore 자체가 테스트 대상일 때만 production save

수동으로 JSON을 임의 수정한 save, 내부 필드 reflection, presentation DTO 직접 주입과 테스트만을 위한
Core 규칙 분기는 checkpoint가 될 수 없다.

### 8.3 표준 checkpoint 후보

| ID | 시작 상태 | 검증 구간 |
|---|---|---|
| `A1_NORMAL_READY` | 정상 운전, 선택·초안 없음 | no-click clock·선택·normal capture |
| `A1_CONSTRUCTION_DUE_1M` | 실제 공사 완공 1분 전 | frame 진행→원자 완공→통전 |
| `A2_HEATWAVE_PRETRIP_1M` | 비상 허용시간 소진 1분 전 | exposure→trip·auto-pause |
| `A2_PROTECTIVE_OUTAGE` | 보호정지 직후 | 사용량 0·world/horizon/context 원인 |
| `A2_RECOVERY_DUE_1M` | 냉각 종료 1분 전 | recovery→재배정·상태 회복 |
| `A2_EVENT_CONSTRUCTION_COLLISION` | 사건 경계와 완공이 같은 minute | transition order와 공급 재계산 |
| `A4_CAMPAIGN_RESULT_DUE` | 실제 campaign 완료 직전 | completed transition·Ended shell·result |

단계 계약은 필요한 ID만 연다. 미래 checkpoint를 미리 구현하지 않는다.

현재 구현된 checkpoint는 A1의 다음 두 개뿐이다. 이는 사용자가 별도로 승인한 구조 준비 결과이며
A1 아트 gate 개방이나 전체 R2 종료를 의미하지 않는다.

| ID | start minute | start canonical SHA-256 | replay SHA-256 | 1분 뒤 canonical SHA-256 |
|---|---:|---|---|---|
| `A1_NORMAL_READY` | 1020 | `7094f631c89fe072800858a205d08358be07a6e0e7341b83026ff619fc03f9a3` | `4f4d3748681585f49eeb4291262db3c99676baba10913450c94d5e1eda9e1611` | `d61217a830053e59f9c75a69eef110da2604892baf9b52ea74cb04d406ad6fec` |
| `A1_CONSTRUCTION_DUE_1M` | 1259 | `3a00c6c937d130cc7574e3971403445cb036a26aecba6671e300e1398d4b9989` | `9bd7c3226fd36396d9d9f7a8d81da25379cedb8e0e54441601bb7c89e947c65c` | `304b96410d7652db9928613fe77443d8d50e29efcb273ff8061c064f876f37f9` |

두 runner는 exact embedded R1 fixture와 bounded Core advance·실제 command replay로 시작 상태를 만들고,
진입 뒤 실제 HUD speed signal과 frame/controller/presentation/world draw 경로를 60 frames/60 fps만
진행한다. 누락·중복·알 수 없는 checkpoint ID는 fail-fast다.

UX-R2.1의 `RealtimeInteractiveCheckpointHost`는 같은 start/replay/end identity에서 paused로 대기하지만
runner처럼 HUD press나 frame을 자동 주입하지 않는다. 실제 production mouse/keyboard의 1× 선택 뒤 wall
clock callback으로 한 minute가 끝나야만 interactive record를 낸다. host scene-load와 automated runner
PASS는 이 actual-input record를 대신하지 않는다. source `e385707`은 first-light와 single-rail 독립
review P0 0/P1 0을 통과했고, 실제 production 1× 입력으로 두 interactive record의 canonical
start/replay/end hash를 다시 확인했다.

### 8.4 처음부터 실행해야 하는 예외

다음은 시작 경로 자체가 검증 대상이므로 checkpoint로 대체하지 않는다.

- 처음 보는 사용자의 onboarding·첫 이해 관찰
- 새 설치·기본 장면·초기 modal과 최초 입력
- save 생성→종료→fresh process restore
- save schema migration과 손상/불일치 보존
- 이전 결정의 누적이 원인인 campaign·경제·열 상태 문제
- 전체 campaign completion, package와 공개 후보 E2E

전체 시작 테스트를 사용할 때는 왜 checkpoint로 답할 수 없는지 gate에 한 줄로 기록한다. 단지 기존
harness가 처음부터 시작한다는 이유는 충분하지 않다.

### 8.5 증거 표기

- 구간 테스트: `TARGETED_LIVE_CHECKPOINT_PASS:<CheckpointId>`
- 한 장 비점수 직접 플레이: `FORMATIVE_DIRECT_PLAY_PASS:<ChapterId>`
- 처음부터 실제 흐름: `FULL_FLOW_E2E_PASS:<FlowId>`
- save/fresh process: `FRESH_PROCESS_RESTORE_PASS:<SaveId>`

UX-R2.1의 위 한 장·두 구간 actual-input label은 production mouse/keyboard 입력으로 생성됐다. label은
source/controller/headless 검사만으로 만들지 않았으며 exact record는 상용 UX scope가 소유한다. 세 record는
모두 non-score 증거이고 official capture나 `CommercialUXProxy` 근거가 아니다.

구간 PASS를 onboarding·전체 campaign·package PASS로 확대하지 않고, 전체 E2E를 좁은 결함 재현에
매번 반복하지 않는다. 실패한 구간은 가장 가까운 앞 checkpoint까지 좁혀 재현한다.

## 9. 단계와 권한

### A0 — 목표·문서 기준선 — 완료

- 네 reference hash와 스타일 DNA 고정
- R1/R2 보존 경계와 금지 근사 고정
- 현재 문서만 전면에 두고 과거 기록 압축

### A0.1 — A1 전 구조 준비 — 완료

- Debug/Release 실시간 Core와 상용 `ExportRelease` v2 authority를 compile allowlist로 분리
- 미승인 persistence·future world/data가 wildcard build나 package에 들어오지 않도록 차단
- Core minute의 cheap query와 동일 state/horizon forecast cache를 추가하고 caller 0 API 제거
- renderer-neutral world presentation/interaction/camera seam과 pointer-only 갱신 경로 추가
- exact suite 하나만 고르는 Core check와 위 두 targeted checkpoint runner 추가

이 단계는 새 runtime art, production V3 data, persistence, 기본 장면 전환을 승인하지 않는다.

### UX-R2.1 — FIRST_LIGHT release tutorial/rail carve-out — 완료

사용자의 “87점 이상까지 계속 개선”과 직접 플레이 지시는 상용 UX scope의 순차 runtime 구현을
명시적으로 승인했다. 완료된 이 단위는 제품 A1–A4를 한꺼번에 열지 않고, A1 이전 logic/presentation
carve-out으로 실제 release `FIRST_LIGHT` 장의 briefing→`FIRST_LIGHT_SUPPLY` phase/event→authored result,
future-event rail의 현재 시각·countdown·event interval·actual/draft construction completion, Debug interactive
checkpoint host와 관련 결정론 검사만 허용했다.

exact 파일 allowlist와 종료 조건은 [상용 UX scope의 UX-R2.1](COMMERCIAL_UX_87.md#ux-r21--first_light-release-tutorialrail--완료)이
소유한다. `data/**`, runtime asset/world, persistence, default scene, export/package와 2–8장 presentation은
금지한다. 현재 tracked `game/assets/realtime/**`와 `game/realtime/world/**`도 provenance 검수 없이 채택하지
않는다. product source `e385707071e4ccfb34d5200e3401897db7f164ad`는 build·회귀와 두 독립 review
P0 0/P1 0을 통과했고 고정 세 명령의 non-score Debug actual-input record까지 생성했다. 이 완료 기록은
official capture나 `CommercialUXProxy` 증거가 아니며 UX-R2.2를 자동으로 열지 않는다.

### A1 — 일반 운전 아트 vertical slice — 미개방

- 한 bounded `FIRST_LIGHT` world
- 한 주거지, 한 필수시설, 한 산업시설, 발전 접속점, 두 pole class와 작은 변전소
- textured terrain·road·river와 3상 conductor attachment
- 정상·선택·계획·공사 상태
- 실제 R1/R2 adapter로 no-click clock, 건설·완공과 선택
- Debug 전용 `A1_NORMAL_READY`와 `A1_CONSTRUCTION_DUE_1M` 진입

### A2 — 사건·열 상태 표현 — 미개방

- 폭염 조명, 비상 노출, 보호정지, 냉각·복귀
- event horizon·world·inspector의 동일 typed truth
- 계획 사용불가와 보호정지의 다른 icon·pattern·문장

### A3 — production city·asset catalog — 미개방

- 현재 오브젝트 전체의 source master, LOD, anchor와 state coverage
- 도시 block, road, riverbank, vegetation, weather와 조명
- manifest completeness와 missing/fallback 0

### A4 — campaign·save 통합 — 미개방

- 필요한 실시간 campaign/data 전환
- 이전 저장 보존과 명시적 migration
- 기본 장면 전환 후보

### A5 — native·사람·전문 검토와 package — 미개방

- FHD와 실제 UHD render target·성능·clipping
- 소유자와 처음 보는 사람의 bounded 흐름
- 한국어·전력설비 전문 검토
- 권리·서명·공증·공개 배포의 별도 승인

로드맵 항목은 구현 권한이 아니다. A0·A0.1·UX-R2.1은 완료됐고 `ActiveEvaluationGate = NONE`이다.
현재 추가 코드 권한은 없으며 art gate도 없다.

현재 사용자가 별도로 승인한 [실시간 상용 UX 87 scope](COMMERCIAL_UX_87.md)는 이 계약의 실시간
제품 방향을 바꾸지 않는다. UX-R0·UX-R1·UX-R2.1은 완료됐고 UX-R2.2는 아직 열리지 않았다. 이후
runtime gate는 UX scope의 현재 상태와 이 계약의 allowlist를 같은 변경에서 명시적으로 재조정한 뒤에만
열린다.

## 10. A1 개방 조건

완료된 UX-R2.1은 logic/presentation carve-out이었으며 A1 art 채택 승인이 아니었다. A1을 별도 gate로
개방하기 전에는 완료 범위를 넘어 runtime asset/world 파일을 추가·수정하지 않는다. 개방 시 계약은
다음을 먼저 고정해야 한다.

- exact source asset allowlist와 provenance
- 수정 가능한 Game·Core·fixture 경계
- 한 장면·한 사건·한 건설 흐름의 bounded player outcome
- exact checkpoint ID, 생성 방식, 시작 hash와 종료 assertion
- reference comparison sheet와 FHD capture protocol
- 성능 예산, fallback 금지 목록과 종료 gate

## 11. 완료 판정 상한

자동검사는 다음을 강하게 판정할 수 있다.

- asset ID·hash·manifest·import와 missing fallback
- Core→presentation identity, 상태·시각·원인
- hit target, anchor, depth ordering, clipping와 frame budget
- build, scene boot, deterministic input·save·package

자동검사는 다음을 대신하지 않는다.

- `./assets`와 같은 세계로 느껴지는지
- 설비가 작고 장난감처럼 보이지 않는지
- 도시가 충분히 촘촘하면서도 조작 가능한지
- 장시간 플레이에서 피로·혼란이 없는지
- 한국어와 전력설비 표현이 전문적인지

따라서 최종 상태는 native capture와 사람·전문 검토가 수집되기 전까지
`HumanVisualValidation = NOT_COLLECTED`, `PublicReleaseStatus = NOT_AUTHORIZED`다.

## 12. 명시적 제외

- full 3D, 자유 회전·원근 camera와 photoreal digital twin
- AC power flow, 전압·무효전력·상불평형·상세 보호계전
- 원전·석탄·LNG·태양광·풍력 기술 tree와 입지 simulation
- procedural city, 무한 sandbox, multiplayer·mobile 동시 개발
- reference 게임의 UI·font·icon·자산 복제
- 기존 합성 reference를 runtime background로 사용하는 shortcut
- 반복 image generation만으로 production consistency를 해결하는 방식

이 제외를 바꾸려면 현재 목표를 수정하는 별도 사용자 승인이 필요하다.
