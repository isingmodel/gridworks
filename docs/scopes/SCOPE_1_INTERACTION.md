# Scope 1 — 수동 선로 건설 구현 기준

> 상태: `COMPLETED`
>
> 장면: `game/Scope1Main.tscn`
>
> 기계 권위: `data/scope-1-v1.json`

이 문서는 현재 저장소의 가장 작은 수동 선로 건설 slice를 설명한다. 고정 source와 target 사이에
지지물을 직접 놓고 모든 인접 구간의 거리 제한을 지킨 뒤 하나의 선로 project를 발주·완공한다.
발전소·변전소 배치, 비용, 일반 graph, 철거와 제품 캠페인은 포함하지 않는다.

과거 승인·관찰 절차는 [개발 이력](../DEVELOPMENT_HISTORY.md)으로 압축했다. 이 구현은 별도 회귀
기준이며 Scope 0B Core를 일반화하거나 대체하지 않는다.

## 1. 데이터 권위

[`data/scope-1-v1.json`](../../data/scope-1-v1.json)은 schema와 fixture 식별자, 단위, 지도 경계,
source·target 위치, 최대 구간 길이, 시작시각과 공사기간의 아홉 root field를 가진다. nested object의
정확한 key와 모든 실행값은 fixture만 소유한다.

현재 SHA-256은
`f308a739f9e4fcaf9d6f07aacba65af6fdd9ae3600a1e5569254fcb749bb2edc`이다. runtime fixture에는
정답 경로, 추천 위치, 최소 지지물 수나 검사 witness가 없다. 자동검사의 witness는 검사 코드만
소유한다.

loader는 추가·누락·대소문자 오기 field, 중복 key, 명시적 null, 잘못된 숫자형과 범위를 거부한다.
코드·장면은 fixture 숫자와 단위 문자열을 별도 권위로 복제하지 않는다.

## 2. 규칙

source, 순서대로 놓은 지지물과 target은 하나의 경로를 이룬다. 새 구간의 유효성은 제곱거리로
판정한다.

```text
(to.x - from.x)^2 + (to.y - from.y)^2 <= maxSpan^2
```

경계의 정확히 같은 길이는 허용한다. 지지물은 map bounds 안의 비어 있는 정수 위치에만 놓을 수
있고 source, target 또는 기존 지지물과 겹칠 수 없다. 지지물 순서는 전기 경로 순서이며 자동 정렬,
최단경로와 위치 보정은 없다.

상태는 다음 세 개뿐이다.

```text
Drafting → Building → Commissioned
```

- `AddSupport(position)`: drafting에서 유효한 새 지지물을 마지막에 추가한다.
- `UndoSupport()`: drafting에서 마지막 지지물 하나를 제거한다.
- `OrderLine()`: 마지막 endpoint에서 target까지의 구간이 유효하면 전체 선로를 발주한다.
- `AdvanceToCompletion()`: 완료시각까지 진행하고 전체 선로를 한 번에 commissioned로 만든다.

target은 commissioned 상태에서만 통전된다. drafting과 building 중에는 지지물이나 span 일부가
존재해도 전기를 전달하지 않는다.

오류는 `WrongPhase`, `InvalidPosition`, `SpanTooLong`, `NothingToUndo` 네 가지다. 실패 명령과 preview는
minute, phase, 지지물 순서와 완료시각을 바꾸지 않는다. 거리 계산은 극단 정수 좌표에서도 overflow가
오류 우선순위를 바꾸지 않도록 넓은 정수형을 사용한다.

## 3. Core 경계

Scope 1은 기존 타입과 충돌하지 않는 독립 API를 사용한다.

```text
Scope1FixtureLoader.Load(...)
Scope1PlacementSession.GetView()
Scope1PlacementSession.PreviewSpan(position)
Scope1PlacementSession.PreviewTarget()
Scope1PlacementSession.AddSupport(position)
Scope1PlacementSession.UndoSupport()
Scope1PlacementSession.OrderLine()
Scope1PlacementSession.AdvanceToCompletion()
Scope1ViewJson.Serialize(...)
Scope1ViewJson.Sha256Hex(...)
```

preview와 실제 명령은 같은 판정 경로를 사용한다. 반환 view의 지지물 목록은 복사본이며 이후 명령이
과거 결과를 바꾸지 않는다. JSON은 `minute`, 소문자 `phase`, 순서가 보존된 `supportPositions`,
`completionMinute`, 파생된 `targetEnergized`를 고정 순서로 기록한다.

## 4. Game 경계

`Scope1Main.cs`가 fixture, session, button과 상태 label을 연결하고
`Scope1PlacementMapView.cs`가 grid·canvas 변환, draw와 pointer event를 맡는다.

- map은 `MouseFilter=Stop`이고 왼쪽 pointer 입력 한 경로만 사용한다.
- map이 좌표를 한 번 정수 grid로 snap해 Main에 전달한다.
- hover와 click은 같은 `Scope1Point`를 Core preview·command에 보낸다.
- Game은 거리 유효성이나 target 통전을 계산하지 않는다.
- Undo, 발주와 완공은 고유 이름을 가진 표준 Button이다.
- 지도 밖의 visible·accessible 상태는 phase, 지지물 순서, target 통전과 마지막 오류를 보여준다.
- custom map에 수십 개의 투명 button이나 좌표 입력 fallback을 만들지 않는다.

headless smoke의 두 support 위치는 명령행의 `--smoke-support`로만 주입되고 실제 viewport input 경로를
거친다. Game source, fixture와 참가자 화면에는 기본 정답 좌표가 없다. JSONL 진단은 좌표를 기록하지
않고 `READY → SUPPORT_ADDED → SUPPORT_ADDED → ORDERED → COMPLETED → FINAL`의 상태 hash만 남긴다.

기본 `project.godot`은 Scope 0B를 유지한다. Scope 1은 `--scene res://Scope1Main.tscn`을 명시해 연다.

## 5. 현재 검사

```sh
ruby playtests/scope-1/verify_contract.rb
dotnet run --project tools/Gridworks.Scope1Checks/Gridworks.Scope1Checks.csproj -c Release -- data/scope-1-v1.json
dotnet build game/Gridworks.Game.csproj -c Debug -t:Rebuild
```

Ruby 검사는 아홉 field와 거리 reference oracle을 독립 계산한다. C# 검사는 strict loader, 직접 연결
실패와 성공 witness, 되돌리기, 모든 오류와 실패 불변, preview parity·purity, 경계와 overflow,
원자 완공, defensive view와 결정론적 JSON을 검사한다. 현재 자동검사는 8개 묶음, 646개 assertion을
통과한 기준을 가진다.

headless wiring smoke는 실제 viewport pointer 경로로 support를 추가하고 standard button signal로
발주·완공한다. native 1280×720 검토는 clipping, phase·좌표·통전 label과 접근성 tree를 확인했다.
Scope 0B 검사와 기본 장면 AB/BA smoke도 함께 회귀한다.

저장소 root에서 현재 Scope 1 smoke를 재현하는 명령은 다음과 같다. 두 좌표는 검사 전용 입력이며
fixture나 일반 플레이의 기본값이 아니다.

```sh
godot_bin="$PWD/.tools/godot-4.7.1/Godot_mono.app/Contents/MacOS/Godot"
smoke_dir="$(mktemp -d /private/tmp/gridworks-s1-smoke.XXXXXX)"

"$godot_bin" --headless --path "$PWD/game" --scene res://Scope1Main.tscn \
  --log-file "$smoke_dir/engine.log" -- \
  --session-id S1-SMOKE --diagnostic-log "$smoke_dir/app.jsonl" \
  --smoke --smoke-support 5,4 --smoke-support 9,4
```

## 6. 완료 증거와 한계

한 번의 공식 LLM 관찰에서 참가자는 도움 없이 지지물 두 개를 놓고 완공했으며 거리 제한과 공사
중 무전압·완공 후 통전을 설명했다. 사용자가 이를 이 slice의 종료 근거로 수용했다. 사전에 검토한
3회 집계는 평가하지 않았으며, 한 번의 관찰을 성공률로 해석하지 않는다.

이 구현에는 임의 terminal 선택, 분기·합류, 교차 접속, 비용, 여러 line class, 발전소·변전소 건설,
철거, save와 캠페인이 없다. 사람 검증도 수집하지 않았다. 다음 제품 단계는 이 타입을 범용화하지
않고 [2D 완성 로드맵](../ROADMAP_2D.md)의 첫 통합본에서 별도 제품 경로로 시작한다.
