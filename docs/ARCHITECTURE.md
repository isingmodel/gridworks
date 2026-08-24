# Current R2 개발 구조

이 문서는 current R2를 이해하고 변경할 때 따라갈 **하나의 개발 지도**다. 여기서 “빠른 개발”은
compile 시간이 아니라, 한 변경에 필요한 권위·분기·파일 추적 수를 줄이는 뜻이다. 새 기능의 구현
권한은 이 문서가 아니라 [현재 작업 범위](ACTIVE_SCOPE.md)가 정한다.

## 가장 짧은 실행 경로

```text
./dev
├─ build/check/story/checkpoint
└─ play fixture/chapter/through

정확한 launch argument
→ RealtimeNativeRouteCatalog
→ RealtimeSliceResources.LoadNativeRelease
→ strict Release V2 base + Release V3 realtime overlay
→ RealtimeSliceData

Godot signal/frame
→ RealtimeSliceMain                       scene·input·publication adapter
→ RealtimeSession                         application interaction과 chapter flow
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

`RealtimeSliceMain`은 route/resource bootstrap, Godot lifecycle, signal/raw input adaptation, focus,
canvas와 publication을 소유한다. 게임 규칙이나 chapter 정책을 찾기 위해 이 adapter부터 UI node
안쪽으로 내려가지 않는다. 먼저 `RealtimeSession`과 `RealtimeCampaignRun`을 본다.

## 권위와 수정 위치

| 질문 | 단일 권위 | 수정 시작점 |
|---|---|---|
| 망·공사·공급·열·시간·결과는 어떻게 바뀌는가? | `RealtimeCampaignRun`과 Release V3 Core | `src/Gridworks.Core/Release/V3/` |
| 어떤 release route가 native인가? | `RealtimeNativeRouteCatalog` | `game/realtime/r2/RealtimeNativeRouteCatalog.cs` |
| 입력 뒤 application 상태와 chapter/story flow는? | `RealtimeSession` | `RealtimeSession.cs`, `RealtimeChapterStoryFlow.cs` |
| intent/action/tool/modal이 지원되는가? | 명시적 capability와 reducer/session 분기 | `RealtimeInteractionReducer.cs`, `RealtimeUiCapabilities.cs`, `RealtimeSession.cs` |
| 같은 snapshot을 화면에서 어떻게 읽는가? | typed immutable presentation | 해당 `Realtime*Presenter.cs`와 `game/realtime/ui/` contract |
| Godot에서 어떻게 받아 그리고 focus를 옮기는가? | scene adapter와 owning UI node | `RealtimeSliceMain.cs`, `game/realtime/ui/` |
| 무엇을 build·play·검사하는가? | `./dev`와 root `Gridworks.sln` | `dev`, `Gridworks.sln` |

한 규칙을 presenter나 Godot adapter에서 다시 계산하지 않는다. Core fact가 부족하면 Core의 snapshot 또는
typed contract를 보강하고, application 전용 결정은 Session에서 한 번 계산해 presentation source로
넘긴다. presenter는 상태를 바꾸지 않고 화면 의미만 만든다.

## current graph와 historical graph

일반 Debug/Release의 root `Gridworks.sln`은 다음 네 project만 포함한다.

- `Gridworks.Core`: strict Release V2 base와 Release V3 realtime 규칙. 현재 사용하지 않는 V3 persistence는
  compile graph에서 제외한다.
- `Gridworks.Game`: `realtime/r2/`, `realtime/ui/`, 현재 map transform과 실제 여섯 embedded resource만
  포함한다.
- `Gridworks.RealtimeChecks`, `Gridworks.CommercialChecks`: current deterministic checks와 story selector.

기존 Product/V1 prototype과 옛 check project는 current root solution 밖의 historical 기준선이다.
`game/Gridworks.Game.sln`도 과거 solution이며 current 개발 진입점이 아니다. `ExportRelease`는 동결된 V2
내부 export allowlist라서 current R2 candidate를 만들 수 없다.

## 변경 종류별 가장 짧은 경로

### 기존 규칙으로 chapter를 하나 더 연결할 때

1. strict content와 realtime schedule/overlay를 먼저 검증한다.
2. 새 mechanic이 없으면 loader, Session, Main에 chapter별 branch를 추가하지 않는다.
3. `RealtimeNativeRouteCatalog`의 명시적 endpoint/capability를 한 단계만 전진시킨다.
4. generic `RealtimeChapterStoryFlow`가 briefing, window, event, result를 데이터에서 선택하게 둔다.
5. 해당 story selector, chapter 단위 검사, 누적 route와 전체 `./dev check`를 실행한다.

### 새 gameplay mechanic을 추가할 때

1. Release V3 Core에 상태·명령·전이를 두고 accepted/rejected 결과와 canonical hash를 검사한다.
2. 입력은 explicit intent와 capability에 추가한다. handler 누락은 성공 no-op이 아니라 unsupported로
   닫혀야 한다.
3. Session은 Core command 또는 명시적인 interaction-only 전이만 호출한다.
4. 필요한 fact를 typed presentation contract로 노출한다.
5. Main/UI는 Godot 신호와 render/focus만 연결한다.
6. accepted, rejected, unsupported 경로와 가장 작은 checkpoint를 먼저 검증한 뒤 `./dev check`를 실행한다.

### 화면 표현만 바꿀 때

1. 해당 presentation contract와 owning component presenter에서 의미를 바꾼다.
2. UI node는 전달받은 값의 layout/render/focus만 바꾼다.
3. Session 변경은 interaction 정책이 달라질 때만, Main 변경은 engine seam이 달라질 때만 한다.
4. 같은 Core snapshot/hash가 유지되는 targeted smoke와 전체 회귀를 확인한다.

## Fail-closed 규칙

- release 인자는 catalog의 정확한 capability와 일치해야 한다. 알 수 없는 chapter, 초과 prefix, 복수 인자는
  거부한다.
- intent, action, build tool, modal action, timeline navigation과 raw input은 명시적으로 지원된 값만
  처리한다.
- 지원하지 않는 요청은 Core나 interaction 상태를 바꾸기 전에 거부한다.
- 이미 닫힌 modal처럼 stale하지만 무해한 요청은 명시적인 no-op으로만 다룬다.
- 화면 전체는 한 `RealtimePresentationSource`에서 한 번 조립한다. projection 뒤 modal 같은 일부를 다시
  덮어써 두 번째 권위를 만들지 않는다.

## 검증 명령

```sh
./dev build
./dev checkpoint A1_NORMAL_READY
./dev checkpoint A1_CONSTRUCTION_DUE_1M
./dev story NORTH_BANK_PROMISE/result/keep
./dev check
```

가장 가까운 unit/story/checkpoint에서 시작하고, 완료 전 `./dev check`로 current graph 전체를 닫는다.
default fixture, `FIRST_LIGHT`, `SECOND_SOURCE`, `NORTH_BANK_PROMISE`의 production route 의미와 canonical
state hash를 구조 변경의 불변조건으로 취급한다. 자동 PASS는 사람 직접 플레이나 UX 품질의 증거가
아니다.

## 구조를 다시 얽히게 하는 신호

- 한 chapter를 추가할 때 loader, Session, Main과 UI 모두에 같은 ID branch를 더함
- presenter가 Core 규칙을 복제하거나 Session이 presentation DTO를 사후 수정함
- UI node가 Core command나 chapter transition을 직접 호출함
- 새로운 입력 enum 값이 추가됐지만 capability/rejection 검사가 없음
- current 개발에 historical solution, Product/V1 또는 `ExportRelease` 명령이 필요함

이 신호가 보이면 파일을 더 나누기 전에 권위가 두 곳으로 복제됐는지 먼저 확인한다. 작은 파일 수 자체가
목표가 아니라, 변경 이유 하나가 ownership boundary 하나로 이어지는 구조가 목표다.
