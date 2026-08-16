# Gridworks — Scope 1 수동 선로 건설 계약

> 상태: **IMPLEMENTATION-READY CANDIDATE — 구현·fixture 파일·공식 실행은 CLOSED**
>
> 다음 위험 선정: `Interaction — manual supports + one MaxSpan`
>
> 사람 증거: `HumanValidationStatus = NOT_COLLECTED`

이 문서는 Scope 0B `GO` 뒤의 적응형 점검이 선택한 다음 단일 위험을 구현 가능한 크기로 닫는다.
사용자는 Scope 1의 계획 준비까지 승인했지만 구현을 승인하지 않았다. 따라서 이 문서가 완결돼도 `src/`, `game/`, `tools/`, `data/`를 변경하거나 공식 proxy를 실행할 수 없다. 별도 사용자 승인과 활성화 checkpoint가 있어야 구현이 열린다.
[준비 checkpoint](../../playtests/scope-1/CHECKPOINT_0_CONTRACT_PREPARATION.md)가 이 문서의 review와 구현 폐쇄 상태를 기록한다.

## 1. 왜 이 위험이 다음인가

Scope 0B는 service area와 실제 공급, 전기 회로와 공간 회랑, 병원 내부전원과 전력회사 공급을 native UI에서 구분하는지 확인했다. 그러나 플레이어는 authored 회랑을 버튼으로 골랐을 뿐, 전신주를 직접 놓거나 거리 제한을 만족시키지 않았다.

수동 지지물 배치와 `MaxSpan`은 완성 제품의 직접 건설 약속 중 아직 증거가 전혀 없고, 다른 시스템 없이 독립시킬 수 있는 가장 작은 위험이다. 다음 항목은 이번 gate에 섞지 않는다.

- 변전소·발전소 부지와 서비스 권역
- 철거와 재구성
- 비용·매출·보상과 경로 선택 밸런스
- 사건·정비·타임라인
- 사람 사용성·재미

다섯 Scope 0B 세션이 모두 북부선을 선택한 결과는 이 결정을 위한 비용 튜닝 근거가 아니다. 반복된 switch/radio 관찰도 Scope 1이 같은 control을 쓰지 않으므로 별도 수정 과제가 아니다.

## 2. 한 문장 가설과 주장 상한

> 고정된 통전 source와 무전압 target 사이에서 플레이어가 지지물을 직접 놓아 모든 인접 span을 하나의 `MaxSpan` 이하로 만들고, 경로 추천 없이 선로를 발주·완공하며, 공사 중에는 무전압이고 전체 완공 뒤에만 target이 통전됨을 화면에서 이해할 수 있는가?

자동검사는 거리·상태·입력 연결의 정확성만, 조건부 LLM proxy는 동일 모델이 이 한 흐름을 수행하고 설명하는지만 지지한다. 통과해도 경로 최적화, 경제적 재미, 일반 graph, 사람 사용성과 발전소·변전소·철거의 품질을 입증하지 않는다.

## 3. 포함과 제외

### 포함

- 고정 2D 지도, source terminal 하나와 target terminal 하나
- integer grid, support 한 종류, line 한 종류, `MaxSpan` 하나
- source에서 시작된 ordered draft에 support를 한 개씩 직접 추가
- 마지막 support 하나 `Undo`
- target까지 마지막 span이 유효할 때 전체 선로 발주
- `Drafting → Building → Commissioned` 한 번의 공사
- 전체 완공 뒤에만 target 통전
- 범위 원, ghost span, 실제 거리/허용 거리와 색 외 피드백
- 실패 명령의 권위 상태 불변과 deterministic snapshot

### 제외

- 시작·목표 terminal 선택, drag 이동, `Cancel`, `Back`, quote 단계와 발주취소
- 현금, 비용, 총길이 요금, 최소 pole 수와 최적 경로
- 기존 회선, 교차·junction, 분기·합류·mesh와 일반 BFS
- 서비스 권역, 수요처, 용량, 급전과 전력조류
- 발전소·변전소 배치, 철거, 정비와 사고
- 복수 support/line class, 지형·장애물, 자동배치·추천
- camera, save/replay, 범용 map editor와 미래 schema
- parameter sweep과 Static Balance Lab

## 4. 활성화 때 옮길 단일 fixture와 checker oracle

구현 승인 전 숫자와 JSON 형태의 권위는 아래 skeleton 하나뿐이다. 승인되면 같은 key·값·자료형의 `data/scope-1-v1.json`으로 옮기고, 독립 인계검사를 통과한 reviewed checkpoint부터 그 JSON이 기계 권위가 된다.

```json
{
  "schemaVersion": "1",
  "fixtureId": "scope-1-v1",
  "units": {
    "position": "GridUnit",
    "time": "GameMinute"
  },
  "mapBounds": {
    "minX": 0,
    "maxX": 11,
    "minY": 0,
    "maxY": 7
  },
  "source": { "x": 1, "y": 4 },
  "target": { "x": 11, "y": 4 },
  "maxSpan": 4,
  "initialMinute": 0,
  "buildMinutes": 60
}
```

제품 JSON root는 위 아홉 field만 가진다. 화폐, 정격, 부하, presentation과 `verificationOnly` 객체는 없다. source는 고정 완공·통전 terminal이고 target은 고정 완공·초기 무전압 terminal이라는 의미는 §5의 상태 규칙이며 별도 boolean input으로 복제하지 않는다.

`(5, 4)`, `(9, 4)`는 독립 checker와 Core 검사에서만 쓰는 checker-only witness다. 제품 fixture, Core loader 결과, Game source·기본값·일반 실행과 participant 자료에는 저장하지 않는다. §7의 headless smoke 명령만 이 좌표를 외부 인자로 일시 전달할 수 있다.

거리 판정은 부동소수 tolerance 없이 정수 제곱으로 한다.

```text
dx = bx - ax
dy = by - ay
SpanValid = dx² + dy² <= MaxSpan²
```

화면에 표시하는 소수 거리만 파생 presentation 값이다. 정확히 `MaxSpan`인 span은 유효하다.

## 5. 권위 상태와 명령

저장하는 권위 상태는 현재 필요한 네 값만 가진다.

```text
minute
phase: drafting | building | commissioned
supportPositions: ordered coordinate list
completionMinute: integer | null
```

span과 support ID는 저장하지 않는다. source → ordered supports → target의 인접쌍에서 span을 결정론적으로 파생한다. Core가 반환하는 view에는 `targetEnergized`를 넣되 `phase = commissioned`에서만 true인 파생값이며 입력이나 저장 field가 아니다.

| 명령 | 성공 | 실패 |
|---|---|---|
| `AddSupport(position)` | 유효한 다음 span이면 list 끝에 추가 | `WRONG_PHASE`, `INVALID_POSITION`, `SPAN_TOO_LONG` |
| `UndoSupport()` | 마지막 support 제거 | `WRONG_PHASE`, `NOTHING_TO_UNDO` |
| `OrderLine()` | target까지 final span이 유효하면 `building`, 완료시각 `60` | `WRONG_PHASE`, `SPAN_TOO_LONG` |
| `AdvanceToCompletion()` | minute `60`, `commissioned`, target 통전 | `WRONG_PHASE` |

`INVALID_POSITION`은 전달된 snapped pair가 지도 밖이거나 source·target 또는 기존 support와 같은 점이다.
오류는 `WRONG_PHASE → INVALID_POSITION → SPAN_TOO_LONG` 순서로 하나만 반환한다. `UndoSupport`는 `WRONG_PHASE → NOTHING_TO_UNDO`, `OrderLine`과 `AdvanceToCompletion`은 `WRONG_PHASE`를 먼저 검사한다.
모든 실패는 code만 반환하고 권위 상태를 바꾸지 않는다. `Building`과 `Commissioned`에서는 support를 편집할 수 없다. 성공 명령도 성공 칸에 적힌 field만 바꾸고 나머지 권위 field는 유지한다.

`PreviewSpan(position)`과 `PreviewTarget()`은 대응 명령과 같은 판정 순서와 거리식을 쓰는 순수 query다. 둘 다 유효성, from/to와 `distanceSquared / maxSpanSquared`를 반환하고 상태를 바꾸지 않는다. `PreviewSpan`의 code는 `null | WRONG_PHASE | INVALID_POSITION | SPAN_TOO_LONG`, `PreviewTarget`의 code는 `null | WRONG_PHASE | SPAN_TOO_LONG`이다. Game이 별도 거리 규칙이나 오류 원인을 복제하지 않는다.

## 6. 화면 계약

- source는 처음부터 선택된 시작점으로 표시한다. target은 별도 endpoint로 표시한다.
- map-space cursor의 각 축은 `floor(value + 0.5)`로 visible integer grid에 snap한다. preview와 click 제출은 같은 snapped pair를 쓰며 Core에는 integer pair만 보낸다.
- 마지막 endpoint 중심의 `MaxSpan` 원과 cursor까지 ghost span을 그린다.
- ghost span은 유효/초과를 색뿐 아니라 실선/점선과 짧은 문장으로 구분한다.
- 초과 시 `실제 거리 / 허용 거리`와 `중간 전신주가 필요합니다`만 보여준다.
- pole 위치, 최소 개수와 경로를 추천하거나 자동 보정하지 않는다.
- `Drafting`, `Building`, `Commissioned`는 pattern과 label로 구분한다.
- `Building`은 전기를 전달하거나 빛나는 통전선처럼 보이면 안 된다.
- `Undo`, `발주`, `완공까지 진행`은 표준 button이고 보이는 label과 고유 접근성 이름을 갖는다.
- 지도 클릭 뒤 현재 ordered path와 발주 가능 여부를 즉시 갱신한다.
- 지도는 하나의 custom-drawn input 영역이며 grid point마다 숨은 button이나 좌표 입력칸을 만들지 않는다. 일반 실행은 실제 pointer click만 받으며, headless smoke도 같은 input handler를 통과한다.
- 현재 phase, ordered support 좌표, target 통전 여부와 마지막 오류는 지도 밖의 보이는 상태 text에도 표시하고 접근성 tree에서 읽을 수 있게 한다.

고정 해상도와 exact UI 배치는 구현 checkpoint에서 native clipping·접근성 preflight와 함께 동결한다. 이번 계약은 pixel 좌표를 게임 규칙으로 만들지 않는다.

## 7. 필수 oracle과 자동검사

각 묶음은 새 초기 상태에서 시작한다.

**A — 완공 명령열**

1. 초기: `drafting`, support 없음, completion null, target 무전압.
2. support 없는 `OrderLine`: source→target 길이 `10`, `SPAN_TOO_LONG`, 상태 불변.
3. `AddSupport(6,4)`: 직전 길이 `5`, 실패·불변.
4. `AddSupport(5,4)`: 정확히 `4`, 성공.
5. support 하나 뒤 `OrderLine`: target까지 `6`, 실패·불변.
6. `(5,4)` 뒤 `AddSupport(9,4)`: 길이 `4`, 성공.
7. 두 support 뒤 `OrderLine`: `building`, completion `60`, target 무전압.
8. `AdvanceToCompletion`: minute `60`, `commissioned`, target 통전.

**B — Undo 명령열**

1. `AddSupport(5,4)`, `AddSupport(9,4)`, `UndoSupport()` 뒤 phase는 `drafting`, support는 `[(5,4)]`, minute `0`, completion null이다.
2. 새 초기 상태에서 두 support와 `OrderLine()`으로 `building`을 만든 뒤 `UndoSupport()`는 `WRONG_PHASE`이고 상태가 변하지 않는다.
3. 또 다른 새 초기 상태에서 두 support·발주·완공으로 `commissioned`를 만든 뒤 `UndoSupport()`는 `WRONG_PHASE`이고 상태가 변하지 않는다.
4. 같은 fixture와 명령열은 같은 권위 field와 파생 view를 반환한다.

Core 비의존 contract checker는 fixture의 exact field·integer 값·direct failure와 checker-only witness success를 검사한다. Scope 1 strict loader는 schema, field exactness, integer 좌표, 유일한 endpoint, 범위·시간·산술 제약과 direct failure만 검증하며 witness를 알지 못한다.

Core 검사는 네 오류 code의 도달성, 실패 불변, 경계 `<=`, 원자 완공과 결정론을 검사한다. preview 전후 상태 불변과, 복제한 같은 초기 상태에서 preview의 accepted/code가 실제 명령과 경계·초과·invalid·`WRONG_PHASE` case마다 일치하는지도 검사한다.

headless smoke는 `Scope1Main.tscn`을 명시적으로 실행하고 다음 smoke-only 인자를 정확히 두 번 받는다.

```text
--smoke --smoke-support 5,4 --smoke-support 9,4
```

Game source와 fixture에는 기본 support 좌표나 경로 계산을 두지 않는다. `--smoke` 없는 좌표 인자는 거부하고, 주입된 grid pair를 canvas 좌표로 바꾼 뒤 일반 실행과 같은 mouse-motion·left-click input handler에 전달한다. Core 명령을 직접 호출하지 않고 표준 button signal로 발주 → 완공 → target 통전을 한 번 통과한다. 이 인자는 participant UI에 좌표 입력 기능을 만들지 않는다.

## 8. 현행 코드와 격리된 구현 경계

### 8.1 완료된 Scope 0B 파일은 범용화하지 않는다

현재 `src/Gridworks.Core/GridworksSession.cs`와 Scope 0B fixture loader·validator·snapshot, `game/Main.cs`, `game/GridMapView.cs`, `game/VisualModels.cs`, `game/TimelineView.cs`, `game/LaunchOptions.cs`, `game/DiagnosticLog.cs`, `game/Main.tscn`, `data/scope-0b-v1.json`과 `tools/Gridworks.Checks/`는 완료된 Scope 0B 전용 구현이다.

Scope 1을 위해 이 파일들에 phase, support, pointer input, 새 schema 분기나 공통 interface를 추가하지 않는다. `game/project.godot`의 기본 scene도 `res://Main.tscn`으로 유지한다. Scope 0B의 scenario/presentation/oracle envelope도 Scope 1에 복제하지 않는다.

### 8.2 별도 승인 뒤 추가할 최소 파일

```text
data/scope-1-v1.json

playtests/scope-1/
└── verify_contract.rb

src/Gridworks.Core/
├── Scope1Contracts.cs
├── Scope1FixtureLoader.cs
└── Scope1PlacementSession.cs

tools/Gridworks.Scope1Checks/
├── Gridworks.Scope1Checks.csproj
└── Program.cs

game/
├── Scope1Main.tscn
├── Scope1Main.cs
└── Scope1PlacementMapView.cs
```

`Scope1Contracts.cs`는 fixture·phase·command result·preview·view와 결정론적 view 직렬화만 가진다. `Scope1FixtureLoader.cs`는 Scope 1 전용 입력 형태와 strict validation을 한 파일에 둔다.
`Scope1PlacementSession.cs`는 §5의 네 명령과 두 preview query만 구현한다. 새 Core 파일은 Godot을 참조하지 않고, 새 checker와 Game은 기존 `Gridworks.Core.csproj`를 참조한다.

기존 `Definitions.cs`, `RawFixtureModels.cs`, `FixtureLoader.cs`, `FixtureValidator.cs`, `Gridworks.Core.csproj`와 `Gridworks.Game.csproj`도 수정하지 않는다. 새 C# script에 따라 Godot이 만드는 `.cs.uid`는 동작 코드가 아닌 generated metadata 예외다.

`Scope1PlacementMapView`가 지도 변환·integer snap·pointer event·그리기를 맡고, `Scope1Main`은 snapped integer pair를 Core query/command에 전달한 뒤 반환 view만 그린다. 새 scene은 `Godot --path game --scene res://Scope1Main.tscn`처럼 명시적으로 실행하며 기본 `Main.tscn`을 바꾸지 않는다.

초기 수직 slice에는 공통 lifecycle, graph, editor, plugin interface, save schema, DI container, scheduler, command bus와 범용 diagnostic framework를 만들지 않는다. `Scope1Main.cs`가 headless smoke의 accepted input·phase·final view를 확인할 작은 scope-local JSONL만 직접 남기며, 조건부 proxy가 열리면 같은 기록을 그대로 사용한다.

### 8.3 Scope 0B 회귀 보존

Scope 1 구현 중에도 다음은 계속 통과해야 한다.

- `ruby playtests/scope-0b/verify_contract.rb`
- `dotnet run --project tools/Gridworks.Checks/Gridworks.Checks.csproj -c Release`
- `dotnet build game/Gridworks.Game.csproj -c Debug -t:Rebuild`
- 기본 `Main.tscn`의 기존 `ab`·`ba` headless smoke
- `data/scope-0b-v1.json` 불변 확인

`playtests/scope-0b/verify_implementation.rb`는 공식 Scope 0B build의 역사적 source hash를 고정하는 증거 검사다. 새 Scope 1 source가 생긴 뒤의 일반 회귀검사로 사용하지 않으며, 이를 통과시키려고 과거 hash나 checkpoint를 수정하지 않는다.

## 9. 임시 LLM proxy 계약

구현 review, 자동검사·headless smoke와 §6의 native 시각검토가 끝난 뒤에도 남는 질문은 “범위 피드백을 보고 직접 경로를 완성하고, 거리 제한과 완공 전 무전압을 설명하는가” 하나뿐이다. 이 질문이 실행 시점에도 남아 있고 사용자가 별도 승인했을 때만 LLM proxy를 한 번 실행한다. 판정 구조는 다음처럼 제한한다.

§6의 구현단계 native 검토는 고정 화면의 clipping·접근성만, §7의 headless smoke는 scene과 Core의 연결만 확인한다. 아래 global native preflight는 proxy 직전 동일 build에서 실제 창·입력과 원본 기록이 끝까지 동작하는지 한 번 확인한다.

- 세 cold session은 같은 build·fixture·prompt·model 설정을 쓰고 도움·교체가 없다.
- 플랫폼과 앱이 자동 보존하는 원본만 쓰며 별도 transcript·manifest·export를 만들지 않는다.
- 실행·증거 실패는 해당 row의 false로 남겨 상호작용 실패와 분리하고, 원본이 증명하지 못한 내용은 결과의 한계로 기록한다.
- `HumanValidationStatus = NOT_COLLECTED`를 유지한다.

한 row의 유일한 scored 판정은 다음 conjunction이다.

```text
IntegratedPlacementPass =
  도움 없이 실제 UI에서 support를 직접 놓고 final 완료
  AND 모든 인접 span이 MaxSpan 이하여야 한다고 설명
  AND Drafting/Building은 무전압이고 전체 완공 뒤 통전된다고 설명
  AND FacilitatorHelp = false
```

직접 연결 실패를 일부러 시도할 필요는 없다. 올바른 범위 표시를 보고 처음부터 유효한 경로를 만들어도 통과다. pole 수, 클릭수, 경로 모양, 완료시간과 `Undo` 사용은 진단값이다.

- `GO`: `IntegratedPlacementPass >= 2/3`
- `NO-GO`: global preflight는 통과했지만 `IntegratedPlacementPass < 2/3`
- global preflight 실패: participant 관찰 전 `PROXY-RUN-BLOCKED`

같은 gate에서 revision round나 추가 LLM session을 열지 않는다. 실패 원인은 자동검사로 재현 가능한 제품 결함, 참가자 미완료, 실행·증거 문제로 나눠 기록하고, 수정이나 사람 테스트는 별도 사용자 결정으로 연다. `2/3`은 동일 모델의 작은 실행 가능성 probe이지 모집단 성공률이나 사람 검증이 아니다.

### 9.1 파라미터 inventory

- `ActiveKnob = 0`
- `Unverified FrozenFixture`: §4의 제품 JSON 아홉 field와 그 값
- `Structural`: integer snap, 제곱거리 `<=`, implicit support/line 하나, lifecycle과 원자 완공
- `Presentation`: 범위 원, ghost span, pattern, label과 이후 동결할 pixel layout
- `Derived`: span 거리, 발주 가능 여부와 `targetEnergized`

fixture 또는 Structural 변경은 같은 gate에서 허용하지 않는다. 필요하면 별도 사용자 결정 뒤 reviewed 새 계약으로 연다. registry, type catalog와 parameter sweep을 만들지 않는다.

## 10. 구현 순서 — 별도 승인 뒤에만

1~2는 권위 인계, 3~4는 Core와 Core 검사, 5~8은 Game·회귀·native 화면 검토, 9~10은 별도 승인된 관찰 단위다. 각 단위의 마지막 검사 뒤 문서 최신화·commit·bounded review·scope-valid 수정·재검사를 마치고 다음 단위로 넘어간다.

1. 루트 README와 이 문서를 같은 변경에서 활성 구현 scope로 전환한다. 활성화 checkpoint는 2~8단계만 열며 공식 proxy 실행은 별도 승인으로 남긴다.
2. `data/scope-1-v1.json`과 Core 비의존 `playtests/scope-1/verify_contract.rb`만 먼저 만들고, checker-only witness로 §4 skeleton에서 JSON으로의 권위 인계를 review한다.
3. `Scope1Contracts.cs`, `Scope1FixtureLoader.cs`, `Scope1PlacementSession.cs`와 독립 `tools/Gridworks.Scope1Checks/`를 구현한다. 기존 Scope 0B Core 파일은 수정하지 않는다.
4. §7의 Core oracle, preview parity, 실패 불변, 원자 완공과 결정론 검사를 통과한다.
5. `Scope1Main.tscn`, `Scope1Main.cs`, `Scope1PlacementMapView.cs`만 추가한다. 기본 scene과 기존 `Main.cs`·`GridMapView.cs`는 수정하지 않는다.
6. Game을 rebuild하고 `--scene res://Scope1Main.tscn` headless smoke를 통과한다.
7. §8.3의 Scope 0B 회귀를 모두 다시 통과한다.
8. 고정 화면의 clipping·접근성을 실제 native 창에서 한 번 검토한다. 여기까지 통과하면 구현 증거는 완료되지만 Scope 1 `GO`는 아직 아니다.
9. §2의 이해·상호작용 질문이 남고 사용자가 별도 실행을 승인한 경우에만 global native preflight와 §9의 세 고정 session을 한 번 연다. 승인하지 않으면 여기서 멈춘다.
10. 관찰 결과와 주장 상한을 기록하고 다음 위험을 다시 선정한다. 공통 framework나 다음 scope를 자동 구현하지 않는다.

## 11. 즉시 중단 조건

- terrain, optimizer, 복수 support class나 terminal 선택 없이는 fixture를 완성할 수 없다.
- invalid 명령이 draft를 바꾸거나 `Building` 중 부분 통전을 만든다.
- 수동 배치를 설명하려면 변전소·발전소·철거 또는 경제를 함께 열어야 한다.
- 고정 세 row에서 자동 pole 추천 없이는 완료할 수 없음이 관찰된다.
- implementation-ready 계약과 실제 코드 사이에 범용 graph refactor가 선행조건이 된다.
- 기존 `GridworksSession`, `Main`, `GridMapView`의 범용화나 기본 main scene 교체가 필요하다.
- Scope 1을 통과시키기 위해 Scope 0B fixture·검사·역사 checkpoint를 수정해야 한다.

이 경우 기능을 추가하지 않고 범위를 다시 검토한다. Scope 1 `GO`도 발전소·변전소·철거, 완성 게임의 재미나 다음 gate 구현을 승인하지 않는다.
