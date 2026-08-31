# Current R2 개발 구조

이 문서는 current R2를 이해하고 변경할 때 따라갈 **하나의 개발 지도**다. 여기서 “빠른 개발”은
compile 시간이 아니라, 한 변경에 필요한 권위·분기·파일 추적 수를 줄이는 뜻이다. 새 기능의 구현
권한은 이 문서가 아니라 [현재 작업 범위](ACTIVE_SCOPE.md)가 정한다.
저장소가 소유하는 current R2 내부 후보 완료와 실제 device·사람·승인은
[외부 출시 gate](RELEASE_GATES.md)로 분리한다.

## 가장 짧은 실행 경로

```text
./dev
├─ build/check/story/checkpoint
├─ play product/layout/fixture/chapter/through
├─ candidate build/verify → tools/r2_candidate.py
└─ qualify run/verify → tools/r2_qualification.py

candidate build → clean HEAD + current ExportRelease selector + selected-resource preset
→ universal ad-hoc app/ZIP → independent manifest reconstruction → temporary-install headless title marker
candidate verify → exact sibling ZIP + clean HEAD → same reconstruction/headless marker
qualify run/verify → exact candidate private copy → empty app-owned data root
→ packaged missing/settings/progress/completed data stages
→ seven default-scene lifecycle InputEvent stages → canonical record v2/fresh reconstruction

launch argument → RealtimeLaunchCatalog
├─ no args → ProductTitle
│  ├─ Main이 strict product settings load → engine/UI projection (session 없음)
│  └─ Main이 save probe (session 없음)
│     ├─ missing save → RealtimeProductTitle → NewGameRequested
│     │  └─ Product write ownership + NativeRelease(RealtimeNativeRouteCatalog.ProductCampaign)
│     │     └─ RealtimeSliceResources.LoadNativeRelease
│     └─ ProductCampaign 또는 exact prior FIRST_LIGHT source
│        └─ codec replay + RealtimeSession.ValidateProgressResume → validated Continue availability
│           ├─ ContinueRequested → Product write ownership → RealtimeSession.Resume
│           │  ├─ story-idle/prior v1 → PlayerPaused·Normal·no-modal
│           │  ├─ supported active v3 또는 normalized v2 story → same authored modal·AutoPaused
│           │  │  └─ non-final result close → bounded next briefing(+decision) FIFO
│           │  └─ current-v3 full terminal completion → Ended·World·no-modal, epilogue replay 없음
│           ├─ completed NewGameRequested → 기존 missing-save ProductCampaign bootstrap 재사용
│           └─ in-progress/readable blocked NewGameRequested
│              └─ typed confirm → raw-byte sibling backup 성공 → 같은 ProductCampaign bootstrap
├─ explicit DEBUG technical fixture/known checkpoint → TechnicalFixture
│  └─ No write ownership + RealtimeSliceResources.LoadTechnicalFixture → stage R1 fixture data
└─ exact native argument → NativeRelease → RealtimeNativeRouteCatalog
   └─ No write ownership + RealtimeSliceResources.LoadNativeRelease
      └─ strict Release V2 base + Release V3 overlay

`play layout` → actual Godot Editor 2D view + strict `RealtimeVisualLayoutAuthoring.tscn`
→ district/source `Sprite2D` transform과 road `Line2D` point를 scene 저장
→ normal product/chapter renderer가 같은 scene을 strict projection함 (Core node·radius·simulation authority와 분리)

technical/native resource load → RealtimeSliceData

normal journal-restorable product-owned exit
→ RealtimeSession.TryCaptureProgress
→ RealtimeSliceData.RequireSaveSourceIdentity
→ RealtimeCampaignSaveCodec.Capture
→ RealtimeCampaignSaveStore atomic replace

non-saveable product-owned normal exit → no store call → prior primary bytes 보존

title/gameplay SettingsRequested
→ RealtimeSettingsSurface가 typed candidate만 생성
→ RealtimeSliceMain이 RealtimeProductSettingsStore atomic save
→ save 성공 뒤에만 window/audio bus + UiRoot scale + Session ReduceMotion 적용

title boot → RealtimeAudio ambient start-once
live public operation → RealtimeSession.SelectLiveAudioCue (Outage > Energize > Breaker, 최대 1개)
→ RealtimeSliceMain typed C# event seam → RealtimeAudio generated PCM playback → Ambient/SFX bus

Godot InputEvent → RealtimeInputRouter → typed RealtimeInputRequest
Godot signal/frame 또는 typed request
→ RealtimeSliceMain                       scene·input·publication adapter
→ RealtimeSession                         application interaction과 story handoff
   ├─ RealtimeChapterStoryFlow            장별 story queue와 달력 전환
   └─ RealtimeEpilogueFlow                full-campaign finale 뒤 세 카드
→ RealtimeCampaignRun                     규칙·시간·공사·결과·canonical state
→ RealtimePresentationSource
→ RealtimeSlicePresenter                  한 곳에서 immutable 화면 조립
   ├─ RealtimeWorldPresenter
   ├─ RealtimeTimelinePresenter
   ├─ RealtimeContextPresenter
   ├─ RealtimeConstructionPresenter
   ├─ RealtimeShellPresenter
   └─ RealtimeModalPresenter
→ RealtimeSlicePresentation
→ RealtimeSliceMain → world/UI nodes
```

`RealtimeInputRouter`는 raw `InputEvent`를 priority가 있는 typed request로 바꾼다. `RealtimeSliceMain`은
launch/resource bootstrap, title과 session의 경계, Godot lifecycle, signal·typed request 검증과 routing,
session 없는 product-title save probe, route와 분리된 product-write ownership, product settings의 load/save,
audio typed-event seam, focus, canvas와 publication을 소유한다. cue 의미는 `RealtimeSession`, generated stream과
Godot playback은 `RealtimeAudio`, volume/mute projection은 Main이 각각 한 번 소유한다. 게임 규칙이나
chapter 정책을 찾기 위해 이 adapter부터 UI node 안쪽으로 내려가지 않는다.
먼저 `RealtimeSession`과 `RealtimeCampaignRun`을 본다.

`RealtimeSession`은 chapter briefing·decision-window story를 닫거나 새 node/line build tool에 진입할 때
실행 중인 시계를 `PlayerPaused` 계획 상태로 바꾼다. 선택한 `RunningSpeed`는 보존하며 플레이어의 명시적
speed 또는 재생 입력만 실제 진행을 재개한다. presenter와 UI node는 이 pause를 다시 계산하지 않는다.

## 권위와 수정 위치

| 질문 | 단일 권위 | 수정 시작점 |
|---|---|---|
| 망·공사·공급·열·시간·결과는 어떻게 바뀌는가? | `RealtimeCampaignRun`과 Release V3 Core | `src/Gridworks.Core/Release/V3/` |
| 수요가 어느 변전소에서 공급되는가? | `RealtimeSupplyAllocator`: 발전원→가동 변전소 유선 경로 + class 반경 R | `src/Gridworks.Core/Release/V3/RealtimeSupplyAllocator.cs` |
| authored world·chapter의 원문은? | strict Release V2 content와 loader | `data/release-world-v2.json`, `data/release-campaign-v2.json`, `src/Gridworks.Core/Release/V2/` |
| realtime world·schedule overlay는? | V3 world/overlay loader | `data/release-world-v3.json`, `data/release-campaign-v3.json`, `src/Gridworks.Core/Release/V3/` |
| product boot와 개발/native launch를 어떻게 구분하는가? | `RealtimeLaunchCatalog` | `game/realtime/r2/RealtimeLaunchCatalog.cs` |
| product campaign과 허용 continuation route는? | `RealtimeNativeRouteCatalog` | `game/realtime/r2/RealtimeNativeRouteCatalog.cs` |
| 제품 title의 표시·focus·입력 차단은? | `RealtimeProductTitle`과 `RealtimeUiRoot` | `game/realtime/ui/` |
| save v1/v2/v3 wire shape·strict Core replay는? | `RealtimeCampaignSaveCodec` | `src/Gridworks.Core/Release/V3/RealtimeCampaignPersistence.cs` |
| native save source identity는? | `RealtimeSliceData.RequireSaveSourceIdentity` | `game/realtime/r2/RealtimeSliceResources.cs` |
| story candidate 순서·closed prefix·active request 재구성은? | `RealtimeChapterStoryFlow` | `game/realtime/r2/RealtimeChapterStoryFlow.cs` |
| journal-restorable idle/active/terminal capture·Resume interaction 정책은? | `RealtimeSession` | `game/realtime/r2/RealtimeSession.cs` |
| save 파일 상태·atomic write·raw sibling backup은? | `RealtimeCampaignSaveStore` | `game/realtime/r2/RealtimeCampaignSaveStore.cs` |
| title save probe, typed reset 확인과 product-owned write lifecycle은? | `RealtimeSliceMain`; title은 표시·signal만 | `RealtimeSliceMain.cs`, `game/realtime/ui/RealtimeProductTitle.cs` |
| current settings wire·strict load·atomic write는? | `RealtimeProductSettingsCodec`과 `RealtimeProductSettingsStore` | `game/realtime/r2/RealtimeProductSettings.cs` |
| title/gameplay 공용 settings 편집·focus·입력 차단은? | `RealtimeSettingsSurface`와 `RealtimeUiRoot` | `game/realtime/ui/RealtimeSettingsSurface.cs`, `game/realtime/ui/RealtimeUiRoot.cs` |
| settings runtime projection은? | Main의 window/audio seam, `RealtimeUiRoot.UiScalePercent`, `RealtimeSession.SetReduceMotion` | `RealtimeSliceMain.cs`, `RealtimeUiRoot.cs`, `RealtimeSession.cs` |
| live operation의 audio cue 의미·우선순위는? | `RealtimeSession.SelectLiveAudioCue` | `game/realtime/r2/RealtimeSession.cs` |
| generated PCM·Ambient/SFX playback은? | `RealtimeAudio`; Main은 Session typed event만 전달 | `game/realtime/r2/RealtimeAudio.cs`, `RealtimeSliceMain.cs` |
| 입력 뒤 application 상태와 chapter/story flow는? | `RealtimeSession` | `RealtimeSession.cs`, `RealtimeChapterStoryFlow.cs` |
| final result 뒤 epilogue 순서와 약속 집계는? | `RealtimeEpilogueFlow`, strict base epilogue와 completed Core outcome | `RealtimeEpilogueFlow.cs`, `RealtimeModalPresenter.cs` |
| raw input이 어떤 typed request가 되는가? | `RealtimeInputRouter` | `game/realtime/ui/RealtimeInputRouter.cs` |
| intent/action/tool/modal이 지원되는가? | 명시적 capability와 reducer/session 분기 | `game/realtime/r2/RealtimeInteractionReducer.cs`, `game/realtime/ui/RealtimeUiCapabilities.cs`, `game/realtime/r2/RealtimeSession.cs` |
| 같은 snapshot을 화면에서 어떻게 읽는가? | typed immutable presentation | 해당 `Realtime*Presenter.cs`와 `game/realtime/ui/` contract |
| active weather를 어떻게 표현하는가? | `RealtimeWorldPresenter` 우선순위 `risk area → Storm`, `thermal override → Heat`, 그 외 `Clear` | `game/realtime/r2/RealtimeWorldPresenter.cs` |
| typed world를 강·교량·건물·설비·도체로 어떻게 합성하는가? | draw-only `RealtimePlaceholderMap`; Release V3 terrain·hit geometry는 유지 | `game/realtime/r2/RealtimePlaceholderMap*.cs` |
| Godot에서 어떻게 받아 그리고 focus를 옮기는가? | scene adapter와 owning UI node | `RealtimeSliceMain.cs`, `game/realtime/ui/` |
| 무엇을 build·play·검사하는가? | `./dev`와 root `Gridworks.sln` | `dev`, `Gridworks.sln` |
| current R2 package identity를 만들고 검증하는가? | `tools/r2_candidate.py` | `tools/r2_candidate.py`, `game/export_presets.cfg` |
| exact package의 app-owned data와 bounded lifecycle InputEvent를 검증하는가? | `tools/r2_qualification.py` | `tools/r2_qualification.py`, `RealtimeSliceMain.Qualification.cs` |

한 규칙을 presenter나 Godot adapter에서 다시 계산하지 않는다. Core fact가 부족하면 Core의 snapshot 또는
typed contract를 보강하고, application 전용 결정은 Session에서 한 번 계산해 presentation source로
넘긴다. presenter는 상태를 바꾸지 않고 화면 의미만 만든다.

Release V3 공급은 수요 접속점까지의 유선 경로를 요구하지 않는다. allocator가 완공·사용 가능한
`발전 접속점 → 변전소` 경로를 먼저 찾고, 그 변전소 class의 `serviceRadiusUnit` 안(경계 포함)에 있는
dedicated load를 직접 배정한다. 여러 후보의 용량·열 한계·사용불가·보호정지와 결정론적 선택도 같은
allocator가 소유한다. presenter는 선택된 변전소, 거리/R와 유선 경로를 typed fact로 받고, map은 유선
도체와 비유선 service link를 서로 다른 표식으로만 그린다.

`RealtimePlaceholderMap`은 typed world의 시각 합성만 소유한다. water polygon에서 결정론적 양안 contour를
만들어 surface·flow·measured bridge가 같은 화면 geometry를 공유하고, pole sprite 크기에서 상단 conductor
attachment를 계산한다. building parcel의 낮은 대비 fill도 여기서만 그린다. 이 draw geometry는 Core의
terrain, construction legality, pointer hit geometry나 save fact를 다시 정의하지 않는다.

current v3의 유일한 application cursor는 initial briefing까지 포함한 `closedStoryCount`다.
`RealtimeChapterStoryFlow`가 transition history + selected campaign의 pure projection으로 candidate prefix를
해석한다. prior v2는 raw cursor를 보존하고 Restore 결과만 checked `+1`, prior v1은 cursor 없는 read-only
all-closed 상태다. first unclosed candidate와 exact saved minute가 exact initial, queue-empty Event/Decision
또는 bounded non-final Result→next Briefing→optional Decision shape인지 Flow가 live/restore에서 한 번
검증한다. Session은 그 application 위치가 exact initial, started chapter 또는 typed between-chapter result의
Core snapshot과 일치하는지만 검증한다. Main과 title view는 story count, handoff phase나 modal body를
계산하지 않는다.

terminal completion도 새 wire cursor를 만들지 않는다. current-v3 full `ProductCampaign`의 completed Core,
Flow all-candidates-closed, `Ended`·World·no-modal과 final 성공/실패를 한 predicate에서 맞춘다. 성공이면
`RealtimeEpilogueFlow.RestoreCompleted`가 authored 세 카드를 소비된 상태로 재구성하고, 실패면 epilogue는
시작하지 않는다. Restore 결과가 보존한 원본 schema가 v3가 아니면 crafted completion을 거부한다.

fresh cumulative Session만 neutral interaction에서 Core의 initial transition batch를 current minute에 한 번
drain해 Flow에 전달한다. Resume는 codec replay history를 그대로 복원하고 다시 drain하지 않으며,
standalone/fixture의 synthetic first briefing은 cumulative Flow projection에 섞지 않는다.

`CityPromise` 문구와 집계는 chapter ID가 아니라 event의 typed promise duty(profile/outcome)를 따른다.
실제 열 rail/detail은 소진된 pending 목록을 다시 읽지 않고 Session이 보존한 Core transition history와
stable target resolver에서 투영한다.

full authored campaign의 성공한 마지막 result를 닫으면 `RealtimeSession`이 chapter queue를 늘리지 않고
별도 `RealtimeEpilogueFlow`로 handoff한다. 이 flow는 strict `BaseCampaign.Epilogue`의 세 authored card와
completed outcome을 chapter ID로 generic join한 Keep/Defer 문장·남은 자금만 typed request로 만든다.
`RealtimeModalPresenter`는 이를 generic `Story` modal에 투영하며 Core 상태나 카드 순서를 다시 계산하지
않는다. 세 카드를 닫은 성공 terminal과 epilogue를 시작하지 않은 실패 terminal은 같은 current v3
product save lifecycle을 사용하며, completed Continue는 카드를 다시 열지 않고 exact `Ended` world를 연다.

## current graph와 historical graph

일반 Debug/Release의 root `Gridworks.sln`은 다음 네 project만 포함한다.

- `Gridworks.Core`: strict Release V2 base, Release V3 realtime 규칙과 current accepted-journal replay
  persistence를 포함한다.
- `Gridworks.Game`: `realtime/r2/`, `realtime/ui/`, 현재 map transform과 실제 여섯 embedded resource만
  포함한다.
- `Gridworks.RealtimeChecks`, `Gridworks.CommercialChecks`: current deterministic checks와 story selector.

기존 Product/V1 prototype과 옛 check project는 current root solution 밖의 historical 기준선이다.
`game/Gridworks.Game.sln`도 과거 solution이며 current 개발 진입점이 아니다. `ExportRelease`는 암묵적인
기본 graph가 없으며 `GridworksCurrentR2Export=true`와 `GridworksLegacyV2Export=true` 중 정확히 하나를
요구한다. current graph는 strict V2 base+V3 Core와 `realtime/r2`, `realtime/ui`, 중립 shared leaf
`realtime/MapViewportTransform.cs`만 포함한다. frozen legacy graph는 V2 Core와 `CommercialMain` allowlist만
포함하며 두 selector의 missing/both는 build 전에 실패한다.

과거 `tools/commercial-ux/native/` 30개 파일은 editor-native First Light non-score 정책에 고정되고
current `./dev`·package·combined 2B와 연결되지 않아 제거했다. Git 이력이 그 기준선을
보존한다. score-bearing 평가가 나중에 승인되면 historical policy/schema를 복제하지 않고
current candidate·qualification·rubric을 소비하는 하나의 새 execution authority로 연다.

## current R2 package identity

macOS 내부 package identity의 단일 application authority는 `tools/r2_candidate.py`이고 `./dev candidate
build | verify`만 이를 호출한다. build는 clean committed HEAD, 고정 Godot/.NET version과
`GridworksCurrentR2Export=true`를 요구하고 current preset의 selected-resource closure를 export한다.
verifier는 manifest를 신뢰해 실행하지 않고 ZIP/tree, plist·signature·architecture, managed runtime,
PCK와 G3 import backing, legal closure와 source identity를 독립적으로 재구성한 뒤 no-arg headless title
marker를 확인한다.

manifest의 `freshUserDataQualified`, `fullProductionInputE2E`, `humanQa`, `evaluationReady`,
`developerIdSigned`, `notarized`, `scoreBearing` false ceiling은 구조 경계다. package identity/headless title
marker 자체는 packaged settings/audio를 qualification하지 않는다.

current 2B qualification의 단일 application authority는 `tools/r2_qualification.py`와 `./dev qualify
run | verify`다. candidate verifier를 재사용하고 manifest/archive를 private copy로 고정한 뒤, 2B1은
source actual-scene의 settings·initial save·terminal save를 fresh packaged title이
`LOADED | RESTORABLE | COMPLETED`로 분류하는지 확인한다. 2B2는 release-safe dormant
`RealtimeSliceMain.Qualification.cs`가 exact scenario env에서만 actual `Viewport.PushInput`을 default scene에
넣어 empty New Game, progress/completed Continue, completed/reset New Game, settings Apply→fresh Restore와
generated audio wiring을 확인한다. invalid scenario/root는 title 전에 거부한다. env가 없으면 새 marker·
입력 없이 기존 product boot와 `user://`를 유지한다.

record v2는 source/package/tool, 네 data stage, 일곱 lifecycle stage의 exact input marker와 before/after
file hash, normalized reset backup을 canonical JSON으로 결속하며 verify는 전부 fresh reconstruction한다.
qualification partial은 제품 규칙 권위가 아니라 package-sensitive engine seam만 관찰하는 adapter다. 이
경계는 current save/settings 두 fixed filename만 소유한다. Godot engine `user://` 전체, authored 8장
packaged E2E, OS hardware input, 실제 audio device·speaker와 사람 UX를 검증했다고 해석하지 않는다.

## 변경 종류별 가장 짧은 경로

### 기존 규칙으로 chapter를 하나 더 연결할 때

1. strict V2 authored content와 V3 world/schedule overlay를 먼저 검증한다.
2. `RealtimeSliceResources`와 strict loader가 두 입력을 하나의 composed campaign으로 만들게 둔다.
3. 새 mechanic이 없으면 loader, Session, Main에 chapter별 branch를 추가하지 않는다.
4. `RealtimeNativeRouteCatalog`의 명시적 endpoint/capability를 한 단계만 전진시킨다.
5. generic `RealtimeChapterStoryFlow`는 Core transition에서 modal timing/request를 만들고,
   `RealtimeModalPresenter`는 composed campaign의 authored card를 projection하게 둔다.
6. 해당 story selector, chapter 단위 검사, 누적 route와 기본 `./dev check`를 실행한다.

### 새 gameplay mechanic을 추가할 때

1. Release V3 Core에 상태·명령·전이를 두고 accepted/rejected 결과와 canonical hash를 검사한다.
2. 입력은 explicit intent와 capability에 추가한다. handler 누락은 성공 no-op이 아니라 unsupported로
   닫혀야 한다.
3. Session은 Core command 또는 명시적인 interaction-only 전이만 호출한다.
4. 필요한 fact를 typed presentation contract로 노출한다.
5. `RealtimeInputRouter`는 engine input을 typed request로 바꾸고, Main/UI는 이를 검증·routing하며
   render/focus를 연결한다.
6. accepted, rejected, unsupported 경로와 가장 작은 checkpoint를 먼저 검증한 뒤 `./dev check`를 실행한다.

### 화면 표현만 바꿀 때

1. 해당 presentation contract와 owning component presenter에서 의미를 바꾼다.
2. UI node는 전달받은 값의 layout/render/focus만 바꾼다.
3. Session 변경은 interaction 정책이 달라질 때만, Main 변경은 engine seam이 달라질 때만 한다.
4. 같은 Core snapshot/hash가 유지되는 targeted smoke와 전체 회귀를 확인한다.

### save 범위를 바꿀 때

1. canonical route와 bundled base/realtime source identity를 먼저 고정한다.
2. `RealtimeCampaignSaveCodec`의 strict journal replay와 canonical hash를 Core에서 검증한다.
3. `RealtimeSession`의 shared predicate에서 command count를 포함한 in-progress Core replay와 full terminal
   completion 경계를 나누고, modal/story/application 경계와 typed resume interaction을 그 바깥에서 한
   번 정한다.
4. session 없는 product title/Main만 save를 probe하고, 그 title action에서 시작한 session만 write
   ownership을 갖게 한다. explicit development route는 같은 route라도 읽거나 쓰지 않는다.
5. Main은 store·title·Godot lifecycle만 연결하고 title view에는 파일 또는 Core 권위를 주지 않는다.
6. 가장 작은 Core strict suite 뒤 별도 fresh process의 save-create→Continue와 guarded/reset title 상태를 검증한다.

### 제품 설정을 바꿀 때

1. 지원 값과 strict JSON shape는 `RealtimeProductSettings`와 codec 한 곳에서 바꾼다.
2. `RealtimeSettingsSurface`는 committed 값을 표시하고 typed candidate만 내보낸다. 파일과 engine을 직접
   만지지 않는다.
3. Main은 product boot에서만 load하고, store 성공 뒤에만 committed 설정과 window/audio/UI/Session
   projection을 갱신한다.
4. title과 gameplay는 `RealtimeUiRoot`의 같은 surface·focus scope를 쓰며 explicit 개발 route는
   read-only로 둔다.
5. 가장 작은 create→fresh restore와 invalid/unsupported/read/write failure smoke 뒤 `./dev check`를
   실행한다. 실제 window mode가 쟁점이면 격리 경로의 bounded non-headless smoke를 별도로 실행한다.

### 제품 오디오를 바꿀 때

1. 어떤 live operation이 어떤 cue를 뜻하는지는 `RealtimeSession.SelectLiveAudioCue` 한 곳에서 바꾼다.
2. PCM 생성·stream shape·Godot playback만 `RealtimeAudio`에서 바꾸며 Core와 save/settings schema를
   건드리지 않는다.
3. Main은 Session typed C# event를 audio node로 전달하고 기존 settings bus projection을 유지한다.
4. selector와 가장 작은 실제 checkpoint, fresh Continue 무재생을 확인한 뒤 `./dev check`를 실행한다.
5. headless 결과는 request/routing 증거로만 쓰고 실제 청감·device·package 품질은 별도 후보에서 검증한다.

## Fail-closed 규칙

- release 인자는 catalog의 정확한 capability와 일치해야 한다. 알 수 없는 chapter, 초과 prefix, 복수 인자는
  거부한다.
- intent, action, build tool, modal action, timeline navigation과 raw input은 명시적으로 지원된 값만
  처리한다.
- 지원하지 않는 요청은 Core나 interaction 상태를 바꾸기 전에 거부한다.
- save는 canonical lowercase source/hash, exact schema/field/command shape와 ordered journal만 받는다. source가
  다르거나 replay 결과가 final hash와 다르면 validated `이어하기`를 열지 않는다.
- `ProductCampaign`과 exact prior `FIRST_LIGHT`만 product continuation route로 허용한다. 다른 개발 route,
  형식 손상·지원하지 않는 schema/version·source/hash/replay 불일치처럼 raw bytes를 읽을 수 있는 save는
  `이어하기`를 차단하고 확인형 `새 게임`만 연다. I/O 실패는 두 action을 모두 차단한다.
- active event·duty는 Core journal에서 exact replay한다. undelivered pending transition과 draft는 shared
  Core predicate가 capture와 title probe에서 함께 차단한다. command-bearing progress 외에는 first
  chapter exact minute의 drained zero-command initial active `c0` 또는 closed-idle `c1`만 허용한다. Session은
  story-idle, exact-minute queue-empty active `EventStory | DecisionWindowStory`, 또는 Flow가 제한한 non-final
  Result→next Briefing→optional Decision suffix만 허용한다. general queued story, active final result/
  epilogue와 frame debt는 차단한다. active result는 마지막 completed chapter와, later briefing은 바로 다음 started
  chapter와 일치해야 한다. live active capture는 blocking Story modal·pause reason·AutoPaused·restorable
  Running/PlayerPaused interaction까지 확인하고, Resume는 저장된 interaction DTO 없이 authored request에서
  same-modal AutoPaused 상태를 재구성한다.
- completion은 current-v3 canonical full route, completed Core, all-closed chapter Flow와 exact `Ended`
  interaction만 허용한다. 성공 final은 epilogue completed, 실패 final은 epilogue never-started여야 한다.
  prior v1/v2 completion, partial route와 nonterminal cursor는 fail-closed한다. terminal callback overrun은
  authoritative completion minute에서 폐기해 저장을 막는 frame debt로 남기지 않는다.
- completed title의 New Game은 completed journal을 rewind하지 않고 missing-save와 같은 canonical
  bootstrap을 즉시 사용하며, saveable product exit 전까지 terminal bytes를 바꾸지 않는다. in-progress와
  readable blocked save는 typed reset action을 함께 연다. 첫 activation은 presentation만 confirm으로
  바꾸고, 두 번째 activation의 raw sibling backup이 성공한 뒤에만 같은 canonical bootstrap을 사용한다.
  backup 실패는 confirm action, Continue 가능성·continuation, ownership과 primary bytes를 그대로 두고
  title 문구만 실패 이유로 갱신한다.
- Main은 cached title availability를 handler에서 다시 검사해 stale/programmatic action도 상태 변경 전에
  거부한다.
- settings는 exact current schema와 열거된 값만 받는다. missing은 write 없이 기본값, malformed·unsupported·
  read failure는 원본 보존과 보이는 오류로 닫고, save 성공 전에는 runtime/control을 바꾸지 않는다.
  explicit 개발 route는 settings path를 소유하지 않는다.
- 이미 닫힌 modal처럼 stale하지만 무해한 요청은 명시적인 no-op으로만 다룬다.
- 한 full projection은 하나의 `RealtimePresentationSource`에서 조립한다. projection 뒤 modal 같은 일부를
  다시 덮어써 두 번째 권위를 만들지 않는다. pointer feedback만 마지막 authoritative presentation에서
  pointer·build guidance·action dock을 좁게 다시 만들며 snapshot·forecast·modal은 재계산하지 않는다.

## 검증 명령

```sh
./dev build
./dev check realtime [EXACT_SUITE]
./dev check commercial [EXACT_SUITE]
./dev check controller [EXACT_CASE]
./dev check ui
./dev checkpoint A1_NORMAL_READY
./dev checkpoint A1_CONSTRUCTION_DUE_1M
./dev story SWITCH_OFF_TO_PROTECT/result/standard
./dev check
./dev candidate verify dist/Gridworks-current-r2-macOS-internal.manifest.json
./dev qualify verify dist/Gridworks-current-r2-macOS-internal.qualification.json
```

가장 가까운 unit/story/checkpoint에서 시작하고, 완료 전 `./dev check`로 current root graph의 Debug
build와 기본 자동 회귀를 닫는다. 이 명령은 no-arg 제품 title과 명시적 technical fixture entry smoke도
포함하며, Core의 누적 8장 stable replay와 pending fail-closed, 같은 save path의
initial briefing create→non-saveable draft exit의 prior bytes 보존→fresh Continue와 safe write,
진행 저장의 confirm→backup 실패 차단→raw sibling backup→initial write→fresh Continue,
`FLOOD_ISOLATION_TEST`→`SECOND_HEART` result→`SECOND_SOURCE` briefing write/Continue, exact prior
`FIRST_LIGHT` v1 Continue→current v3 write와 all-controller PASS 뒤 성공 8장 terminal create→fresh title
probe→Continue→`Ended`·terminal write,
fresh completed title→New Game→initial write→fresh Continue, readable blocked reset 확인과 I/O 차단 상태를
검사한다. product settings의 create→fresh restore, invalid/unsupported/read/write failure 보존,
explicit fixture read-only surface와 controller를 다시 실행하지 않는 Godot UI layout harness도 포함한다.
controller runner는 exact case 하나를 선택할 수 있고 unknown case를 전체 허용 목록과 함께 거부한다. 전체
gate에서는 모든 case가 PASS한 뒤에만 absent 임시 path에 terminal save를 쓰며, 별도 fresh product-title
process가 같은 bytes를 completed로 probe한다. root `Gridworks.sln` 전체의
Release build는 포함하지 않는다. headless harness는 물리 display·native fullscreen·사람 UX 증거가 아니며,
그 검사가 필요한 변경은 active scope의 완료 검사에 별도로 적는다.
world presentation은 actual campaign의 Clear/Heat/Storm 우선순위와 `Reduce Motion` weather phase
고정도 같은 기본 harness에서 검사한다.

product title과 explicit fixture/native route의 launch·save ownership 의미를 구조 변경의 불변조건으로
취급한다. session을 만드는 fixture와 `FIRST_LIGHT`, `SECOND_SOURCE`, `LONGEST_NIGHT` native route의
canonical state hash도 유지한다.
자동 PASS는 사람 직접 플레이나 UX 품질의 증거가 아니다.

## 구조를 다시 얽히게 하는 신호

- 한 chapter를 추가할 때 loader, Session, Main과 UI 모두에 같은 ID branch를 더함
- presenter가 Core 규칙을 복제하거나 Session이 presentation DTO를 사후 수정함
- UI node가 Core command나 chapter transition을 직접 호출함
- 새로운 입력 enum 값이 추가됐지만 capability/rejection 검사가 없음
- 일반 current 개발에 historical solution이나 Product/V1 graph가 필요하거나 current package에 legacy
  export selector가 섞임
- current package와 연결되지 않은 candidate/session/schema 계층을 미리 만들어 외부 gate를 흠내 냄

이 신호가 보이면 파일을 더 나누기 전에 권위가 두 곳으로 복제됐는지 먼저 확인한다. 작은 파일 수 자체가
목표가 아니라, 변경 이유 하나가 ownership boundary 하나로 이어지는 구조가 목표다.
