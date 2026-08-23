# Gridworks

`Gridworks`는 성장하는 도시의 전력망을 직접 건설하고, 예고된 수요·폭염·설비 정지 속에서도
사람과 필수 서비스를 지키는 싱글 플레이 2D 전략 게임이다.

## 현재 전체 목표

현재 목표는 **`./assets`의 회화적이고 고밀도인 아이소메트릭 산업도시 스타일로 실제 플레이 가능한
실시간 전력망 게임을 완성하는 것**이다. 네 기준 이미지는 목표의 조명·재질·공간 밀도·설비 존재감과
상태 색 언어를 정한다.

- [망 건설](assets/01-grid-construction.png)
- [폭염과 보호정지](assets/02-heatwave-outage.png)
- [경로 비교](assets/03-route-comparison.png)
- [입지 비교](assets/04-plant-siting.png)

이 이미지를 그대로 게임에 붙이거나 UI 배치를 복제하지 않는다. 다음 조합이 목표다.

- 기준 이미지의 어두운 회화적 지형, 촘촘한 도시·산업 디테일, 묵직한 설비 실루엣
- 청록 통전·호박색 계획·주황/적색 위험, 금속 프레임과 절제된 광원
- R1 결정론적 실시간 규칙과 R2의 상단 HUD·수평 사건 지평선·조건부 작업 패널
- 한국어, 배전 규모 설비, 색 외 상태 표현과 실제 Core 결과

정확한 목표와 금지 범위는
[에셋 스타일 실시간 게임 계약](docs/scopes/ASSET_STYLE_REALTIME_GAME.md), 표현 기준은
[비주얼 제작 명세](docs/product/VISUAL_PRODUCTION_SPEC.md)가 소유한다.

현재 사용자 승인 작업은 이 실시간 방향 위에서 **판매 가능한 게임 경험을 평가·개선해 고정
`gpt-5.6-sol`/`ultra`의 공식 `CommercialUXProxy >= 87`을 만드는 것**이다. 단일 활성 범위는
[실시간 상용 UX 87 계약](docs/scopes/COMMERCIAL_UX_87.md), 점수 절차는
[평가 프로토콜](docs/product/COMMERCIAL_UX_EVALUATION_PROTOCOL_KO.md)이 소유한다.

## 현재 상태와 권한

제품의 Release.V3/R2 실시간 방향은 유지한다. UX-R0 텍스트 패널은 형성평가 83.4475와 독립 검토
P0 0/P1 0으로 완료했다. 현재는 UX-R1 native evaluator authority port만 열었고, runtime art A1과
전체 캠페인 구현은 아직 열지 않았다.

- 기본 실행 장면: `CommercialMain`
- 동결 상용 v2 기준선: 자유 배치·열 한계·8개 임무·save v3·내부 macOS 후보
- R1 실시간 Core: 커밋 `3da1897`, `FIRST_LIGHT` 결정론적 vertical slice
- R2 실시간 UX 기반: 커밋 `4c27f65`, 비기본 `RealtimeSliceMain`
- R2 마지막 전체 harness: 사용자 지시로 중단, 종료 PASS 아님
- 새 목표 문서 기준선: `A0` 완료
- A1 전 구조 준비: build authority 격리, renderer-neutral world seam, 두 DEBUG checkpoint 완료
- UX-R0: V2 authored content와 V3 실시간 일정에 결속한 34-part story 단독 실행, 형성평가
  `TextPlanProxy = 83.4475`
- UX-R1: V3/R2 candidate·replay·session·evidence provenance 포팅 활성, 실제 capture는 아직 금지
- future-event status bar: R2 `RealtimeEventRail` 존재, 실제 플레이 품질은 아직 미관찰
- 공식 점수: `CommercialUXProxy = null`, score-bearing capture 미허용
- 활성 평가 gate: `UX-R1_NATIVE_EVALUATOR_AUTHORITY_PORT`
- 활성 제품 아트 gate: 없음, A1 미개방
- native capture: 현재 Mac 잠금으로 대기

```text
CurrentGoal = ASSET_STYLE_REALTIME_GAME
GoalDirection = ACTIVE
ActiveScope = COMMERCIAL_UX_87_REALTIME
ActiveEvaluationGate = UX_R1_NATIVE_EVALUATOR_AUTHORITY_PORT
DocumentationBaseline = A0_COMPLETE
ArchitecturePreparation = COMPLETE
ProductArtImplementationGate = NONE
NextCandidate = A1_NORMAL_OPERATION_ART_SLICE_NOT_OPENED
VisualReferenceAuthority = ROOT_ASSETS_FOUR_IMAGES
RuntimeArtAuthority = NOT_ESTABLISHED
RealtimeRuleAuthority = RELEASE_V3
RealtimeUxAuthority = R2_FIRST_LIGHT_TARGETED_SLICE
FutureEventStatusBar = REQUIRED_R2_EVENT_RAIL_PRESENT_NATIVE_QUALITY_NOT_OBSERVED
LiveTestDefault = TARGETED_DETERMINISTIC_CHECKPOINT
TargetedCheckpointRuntime = A1_NORMAL_READY_AND_A1_CONSTRUCTION_DUE_1M_READY
FullFlowE2EPolicy = EXCEPTION_ONLY
FullCampaignNativeE2E = NOT_IMPLEMENTED
TextPlanProxy = 83.4475_FORMATIVE
TextJudgeExecutionReceipt = NOT_EXPORTED_FORMATIVE_ONLY
CommercialUXProxy = null
ScoreBearingCaptureAllowed = false
UXR0ClosureReview = PASS_P0_0_P1_0_COMMIT_746C0AA
NativeEvaluatorAuthority = PORT_IN_PROGRESS
DefaultMainScene = CommercialMain
R1RealtimeCore = PRESERVED
R2RealtimeUx = PRESERVED_GATE_NOT_COMPLETED
NativeCapture = BLOCKED_MAC_LOCKED
PhysicalUhdPanelEvidence = OPEN_EXTERNAL_HARDWARE_NOT_AVAILABLE
HumanVisualValidation = NOT_COLLECTED
PublicReleaseStatus = NOT_AUTHORIZED
```

`game/assets/realtime/`, `game/realtime/world/`, production V3 data와 persistence처럼 작업 폴더에 있을 수
있는 미래 후보는 현재 목표의 승인된 runtime 자산이나 구현이 아니다. provenance·스케일·부착점·상태
표현 검수를 통과해 명시적으로 채택되기 전에는 포함하지 않는다.

## 제품 경험

플레이어는 같은 청류시 지도에서 발전 접속점, 전신주, 선로와 배전 변전소를 연결한다. 전기는 총량이
아니라 완공된 실제 경로를 따라 흐르고, 공유 선로·접속부·변전소의 연속·비상 한계가 병목을 만든다.
공사는 시간 속에서 진행되고 사건은 미리 보이며, 비상 운전은 노출 허용시간 뒤 보호정지와 냉각·복귀로
이어진다.

화면은 “전력망을 설명하는 도식”이 아니라 **살아 있는 도시 위에서 전력 인프라를 운영하는 게임**으로
보여야 한다. 확대하지 않아도 주거지·의료원·정수장·산업지대·도로·하천·발전 접속점·배전망이 서로
다른 덩어리로 읽혀야 하며, 선택·계획·공사·비상·정지 상태는 광원만이 아니라 형태·패턴·아이콘·문장으로
구분해야 한다.

## 문서와 저장소

```text
assets/                              현재 시각 방향을 고정하는 네 기준 이미지
docs/
  README.md                          현재 문서 지도와 질문별 소유권
  scopes/ASSET_STYLE_REALTIME_GAME.md 현재 전체 목표·단계·권한 계약
  scopes/COMMERCIAL_UX_87.md         현재 단일 UX 평가·개선 scope
  product/                           현재 게임·오브젝트·비주얼 기준
  product/COMMERCIAL_UX_EVALUATION_PROTOCOL_KO.md 고정 judge·점수·증거 계약
  ROADMAP_2D.md                      새 목표의 단계 순서
  ROADMAP_2D_CHECKLIST.md            현재 상태와 종료 증거 장부
  archive/                           완료·중단된 과거의 압축 기록
game/                                Godot .NET 화면·adapter
src/Gridworks.Core/                 Godot 비의존 규칙
tools/                               결정론적 자동검사
```

읽는 순서와 질문별 소유자는 [문서 안내](docs/README.md)가 관리한다. 과거 prototype, release v1,
상용 v2 단계 B–G와 R0–R2의 세부 문서는 Git 이력에 남고,
[완료 이력](docs/archive/COMPLETED_HISTORY.md)은 현재 필요한 사실만 압축해 보존한다.

## 개발 원칙

- 한 번에 구현 gate 하나만 연다. 다음 단계의 schema·scene·placeholder를 미리 만들지 않는다.
- Core가 규칙을 계산하고 Game은 typed 결과를 표현한다.
- `./assets`는 시각 방향의 권위지만 수치·게임 규칙·runtime 파일의 권위는 아니다.
- “비슷한 색의 평면 도형”을 스타일 일치로 인정하지 않는다. 카메라, 밀도, 재질, 실루엣, 조명과
  상태 표현을 함께 검증한다.
- 합성 화면을 runtime 배경으로 사용하지 않는다. 오브젝트는 분리된 source, alpha, 부착점, LOD와
  provenance를 가져야 한다.
- 자동검사는 규칙·상태·build·wiring을 판정한다. 미감·가독성·재미는 실제 화면과 사람 검토를 별도로
  기록한다.
- 라이브 검증은 처음부터 재생하지 않고 이름 붙은 결정론적 checkpoint에서 필요한 구간만 실행하는
  것을 기본으로 한다. checkpoint는 실제 controller·presentation·input·render 경로를 우회하지 않는다.
- future-event status bar는 현재 시각·다음 사건 countdown·시작/종료·공사 완료·결정 기한·열 보호 경계를
  한 시간축에서 보여 주며, 코드 존재가 아니라 실제 가시성·조작·이해로 판정한다.
- 처음부터 실행하는 E2E는 onboarding, save/migration, 누적 상태, default scene·package와 전체
  campaign처럼 앞선 경로 자체가 검증 대상일 때만 사용한다.
- 과거 후보의 PASS를 새 목표의 아트·native·사람 증거로 합산하지 않는다.

## 개발 실행

[`global.json`](global.json)은 .NET SDK `8.0.129`를 고정한다. 확인한 Godot은
`4.7.1.stable.mono.official.a13da4feb`이며 로컬 기본 경로는
`.tools/godot-4.7.1/Godot_mono.app/Contents/MacOS/Godot`이다.

```sh
dotnet restore game/Gridworks.Game.csproj
dotnet build game/Gridworks.Game.csproj -c Debug
./.tools/godot-4.7.1/Godot_mono.app/Contents/MacOS/Godot --path game
```

현재 기본 실행은 동결 v2 `CommercialMain`이다. 비기본 R2 장면은 명시적으로만 실행한다.

```sh
./.tools/godot-4.7.1/Godot_mono.app/Contents/MacOS/Godot \
  --path game --scene res://realtime/r2/RealtimeSliceMain.tscn
```

실시간 구간 검증은 전체 harness 대신 필요한 checkpoint 하나만 선택한다. 이 두 명령은 각각 실제
`RealtimeSliceMain` controller·HUD signal·frame accumulator·presentation·world draw 경로에서 정확히
1분만 진행하며, A1 아트나 전체 campaign PASS를 뜻하지 않는다.

```sh
./.tools/godot-4.7.1/Godot_mono.app/Contents/MacOS/Godot \
  --headless --path game \
  --scene res://realtime/r2/RealtimeSliceCheckpointRunner.tscn \
  -- --checkpoint=A1_NORMAL_READY

./.tools/godot-4.7.1/Godot_mono.app/Contents/MacOS/Godot \
  --headless --path game \
  --scene res://realtime/r2/RealtimeSliceCheckpointRunner.tscn \
  -- --checkpoint=A1_CONSTRUCTION_DUE_1M
```

Core 회귀도 exact suite 하나만 선택할 수 있다.

```sh
dotnet run --project tools/Gridworks.RealtimeChecks -c Release -- \
  --suite frame-speed-canonical-hash
```

작성된 스토리 파트 하나만 실행하거나 전체 34-part manifest를 검사할 수 있다.

```sh
dotnet run --project tools/Gridworks.CommercialChecks -c Release -- \
  --story-part FIRST_LIGHT/briefing
dotnet run --project tools/Gridworks.CommercialChecks -c Release -- --story-manifest
python3 tools/commercial-ux/test-realtime-text-plan-tools.py
```

설치·저장·내부 후보 경계는 [INSTALL](INSTALL.md)을 따른다.

## 법적·출시 상태

`./assets`와 이후 채택할 모든 source asset은 [ASSET_MANIFEST](ASSET_MANIFEST.md)에 출처·생성 방법·
hash·사용 경계를 기록해야 한다. 저장소 공개 열람은 재사용·재배포 허가가 아니다. 현재 공개 package,
Developer ID 서명·공증, 사람 미감·사용성 검토, 한국어·전력설비 전문 검토는 승인되지 않았다.
