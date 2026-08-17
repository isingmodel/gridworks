# 첫 점등 통합 — 완료된 구현 기준

> 상태: `COMPLETED`
>
> 사람 검증: `NOT_COLLECTED`

이 문서는 제품의 첫 기술 단계를 설명한다. fixture, Core, 화면, 자동검사와 native 확인은 완료됐다.
외부 사람 관찰은 수집하지 않았으며, 이 완료가 병원·발전소·사건·캠페인·저장 구현을 자동으로
허가하지 않는다.

## 1. 플레이어 결과

플레이어는 하나의 지도에서 이미 온라인인 발전원과 마을을 본다. 다음 흐름을 직접 끝낸다.

```text
변전소 초안 배치·이동·취소
→ 변전소 견적·발주·완공
→ 완공된 두 terminal 사이에 지지물과 선로 초안 작성·되돌리기·취소
→ 선로 견적·발주·완공
→ 마을 공급 확인
→ 첫 결산
```

변전소와 선로는 별도 공사다. 미완공 변전소 terminal에 선로를 예약하거나 두 공사를 한 package로
발주하지 않는다. 완공 뒤에는 이동·철거하지 않고 `현재 임무 다시 시작`으로 최초 상태로 돌아간다.

## 2. 단일 데이터 권위

[`data/product-first-light-v1.json`](../../data/product-first-light-v1.json)이 이 단계의 유일한
runtime 숫자 권위다. 다음 항목을 소유한다.

- 단위, 지도 경계와 건설 불가 cell
- 기존 발전원과 마을의 위치·정격·수요
- 변전소의 정격·서비스 반경·비용·공기
- 선로의 최대 span, 정격, support/span별 비용·공기
- 시작시각·현금, 결산기간과 판매단가

문서, 장면과 Core에 이 숫자를 별도 상수로 복제하지 않는다. runtime 데이터에는 추천 위치,
정답 경로, 최소 지지물 수, 예상 기말현금이나 검사용 witness를 넣지 않는다. 성공·실패 reference는
제품 검사 코드만 소유한다.

Loader는 exact field set과 type을 요구한다. 추가·누락·대소문자 오기·중복 key·명시적 null,
숫자 문자열과 소수형을 거부한다. 위치와 fixture 시간은 32-bit integer, 전력·에너지·현금·rate는
64-bit integer로 읽고 모든 파생 산술은 checked 연산을 사용한다.

의미 검증은 다음으로 닫는다.

- 두 축에서 엄격히 증가하는 map bounds와 모든 위치의 inclusive bounds
- 비어 있지 않고 서로 고유한 asset·terminal·project·town ID
- source·town·blocked cell의 중복·충돌과 blocked cell 자체의 중복
- line의 두 terminal 참조가 source·substation terminal과 정확히 일치함
- 시작시각·시작현금은 0 이상, 수요·정격·반경·최대 span·공기·결산기간·비용·판매단가는 양수
- map 대각선, 반경·span 제곱, 가능한 최대 견적·완공시각과 결산 산술이 64-bit 범위를 넘지 않음
- 잠재 매출의 분자가 `60_000_000`으로 정확히 나누어짐

현금이 부족하거나 정격이 수요보다 작은 fixture는 정상 runtime 실패 가지를 검증할 수 있도록
loader가 거부하지 않는다. 현재 fixture의 성공 가능성은 검사 코드의 reference flow가 증명한다.

## 3. 이 단계가 고정하는 공간 규칙

변전소 footprint, terminal과 서비스 권역 중심은 한 정수 grid cell이다. 지도 밖, 건설 불가 cell,
기존 발전원·마을 또는 이미 사용한 위치에는 변전소나 지지물을 놓을 수 없다.

마을의 서비스 권역 판정은 inclusive squared Euclidean distance 한 가지다.

```text
dx = town.x - substation.x
dy = town.y - substation.y

TownInServiceArea = dx² + dy² <= serviceRadius²
```

서비스 권역 밖 변전소도 발주할 수 있다. placement·order preview는 `Accepted = true`, 오류 없음으로
유지하고 예상 공급 실패를 별도 경고한다. 화면은 공급에 부적합함을 미리 말하지만 정답 위치로
자동 보정하지 않는다. 이는 서비스 권역과 실제 공급을 구분하는 의도된 실패 설계이며, 결산 뒤
임무를 다시 시작할 수 있다. 정격 부족도 같은 방식으로 공사 명령을 막지 않고 예상 공급 실패로
표시한다.

선로는 다음 순서의 단일 경로다.

```text
기존 발전원 terminal
→ 플레이어가 입력한 순서의 지지물
→ 완공된 변전소 terminal
```

모든 인접 span은 `distanceSquared <= maxSpanSquared`를 만족해야 한다. 경계는 허용한다. 자동 경로,
자동 정렬, 위치 추천과 보정은 없다. 선이 공간에서 교차해도 terminal이 아니면 접속되지 않으며,
이 단계에는 span 장애물 교차·분기·지지물 공유 규칙을 추가하지 않는다.

## 4. 공사, 견적과 현금

변전소 견적은 fixture의 고정 비용과 공기다. 선로 견적은 다음 식만 사용한다.

```text
supportCount = supports.Count
spanCount = supportCount + 1

lineCost =
    supportCount × supportCost
  + spanCount × spanCost

lineBuildMinutes =
    supportCount × supportBuildMinutes
  + spanCount × spanBuildMinutes
```

거리별 conductor 적산, 지형 multiplier, 공사 queue와 project dependency graph는 만들지 않는다.
현재 phase에서 동시에 공사 중인 project가 최대 하나이므로 한 공사 자원 규칙이 성립한다.

발주 성공은 현금 차감, `Building` 전환과 완공시각 설정이 한 번에 일어나는 원자 변경이다. 견적이
현금을 넘으면 `INSUFFICIENT_CASH`로 거부하고 상태·시각·현금은 전혀 바뀌지 않는다. 공사 중
취소·환불은 없다. 완공 명령은 현재시각을 완공시각으로 옮기고 project 전체를 한 번에
`Commissioned`로 바꾼다. 공사 중인 변전소·선로·지지물은 공급에 참여하지 않는다.

## 5. 공급과 결산

이 단계에는 후보 경로가 하나뿐이다.

```text
온라인 기존 발전원 terminal
→ 완공된 선로
→ 완공된 배전 변전소
→ 서비스 권역
→ 마을 경계
```

공급 실패 이유는 다음 우선순위를 사용한다.

1. 변전소 미완공
2. 선로 미완공
3. 서비스 권역 밖
4. 발전원 정격 부족
5. 선로 정격 부족
6. 변전소 정격 부족
7. 실패 없음

마을은 수요 전량을 공급받거나 0을 공급받는다. 정격과 수요가 정확히 같은 경계는 공급된다.
부분공급, flow split, 최단경로, authored 허용경로, 우선순위 allocator와 복수 발전원 급전은 없다.

결산은 fixture의 고정 기간만 진행한다.

```text
deliveredEnergyKwMinute = deliveredKw × settlementMinutes

revenueCashUnit =
    deliveredEnergyKwMinute × saleRateCashUnitPerGWh
    / 60_000_000
```

fixture는 나눗셈이 정확히 떨어져야 한다. 실제 인도 전력만 매출이다. 공급에 성공하면 임무
`Success`, 실패하면 매출 0인 `Failure`로 결산한다. 발전 변동비, 미공급 보상과 LostSales는
병원·경제 단계 전에는 만들지 않는다. 결산은 한 번만 가능하며 그 전의 임무 결과는 `Pending`이다.

## 6. 권위 상태와 명령

mutable 상태는 다음뿐이다.

- 현재시각과 현금
- 변전소 위치, project 상태와 완공시각
- 입력 순서의 지지물 목록, 선로 project 상태와 완공시각
- 결산 완료 여부, 인도에너지와 매출

현재 phase, 서비스 권역 판정, 공급 실패 이유, 인도전력과 임무 결과는 매 snapshot에서 파생한다.
project 상태는 `NotOrdered → Building → Commissioned`, 임무 phase는 다음 여섯 개뿐이다.

```text
SubstationPlanning
SubstationBuilding
LinePlanning
LineBuilding
SettlementReady
Complete
```

Core의 공개 명령은 다음으로 닫는다.

```text
GetSnapshot

PreviewSubstationPlacement
SetSubstationDraft
CancelSubstationDraft
PreviewSubstationOrder
OrderSubstation

PreviewLineSupport
AddLineSupport
UndoLineSupport
CancelLineDraft
PreviewLineOrder
OrderLine

AdvanceToConstructionCompletion
AdvanceToSettlement
RestartMission
```

명령 오류는 `WRONG_PHASE`, `NO_DRAFT`, `OUT_OF_BOUNDS`, `NOT_BUILDABLE`,
`POSITION_OCCUPIED`, `SPAN_TOO_LONG`, `NOTHING_TO_UNDO`, `INSUFFICIENT_CASH`만 사용한다.
preview와 실제 명령은 같은 순수 판정 함수를 사용한다. preview와 모든 거부 명령은 상태를 바꾸지
않으며 반환 snapshot의 collection은 복사본이다.

명령별 오류 우선순위는 다음으로 닫는다.

- 변전소 배치: `WRONG_PHASE → OUT_OF_BOUNDS → NOT_BUILDABLE → POSITION_OCCUPIED`
- 변전소 발주: `WRONG_PHASE → NO_DRAFT → INSUFFICIENT_CASH`
- support 추가: `WRONG_PHASE → OUT_OF_BOUNDS → NOT_BUILDABLE → POSITION_OCCUPIED → SPAN_TOO_LONG`
- support undo: `WRONG_PHASE → NOTHING_TO_UNDO`
- 선로 발주: `WRONG_PHASE → SPAN_TOO_LONG → INSUFFICIENT_CASH`
- 잘못된 phase의 완공·결산: `WRONG_PHASE`

같은 유효 변전소 draft 위치를 다시 지정하는 것은 성공하며 값은 그대로다. 두 cancel은 각 planning
phase 안에서는 draft가 비어 있어도 성공하는 idempotent clear이고 다른 phase에서는
`WRONG_PHASE`다. `RestartMission`은 어느 phase에서든 항상 성공해 최초 snapshot과 값이 같은
권위 상태로 돌아간다. restart 횟수나 replay 기록은 저장하지 않는다.

public snapshot은 다음 값만 가진다.

- `minute`, `cash`, `phase`
- 변전소 position·project state·completion minute
- 입력순서 support 목록과 선로 project state·completion minute
- `townInServiceArea`, typed supply failure, `townDeliveredKw`
- 결산 완료 여부, 인도 `kW·minute`, 매출과 `Pending/Success/Failure` 결과

명령 결과는 `Accepted`, nullable error와 snapshot을 반환한다. 배치 preview는 위치·서비스 적격성과
예상 공급 실패, span preview는 from/to·제곱거리·허용 제곱거리, order preview는 견적 비용·공기·
완공시각과 예상 공급 실패를 반환한다. 저장 단계 전에는 snapshot JSON 형식이나 field 순서를 제품
계약으로 만들지 않고 record 값 동등성으로 결정론과 restart를 검사한다.

preview의 예상 공급 실패는 선택한 draft를 사용하고 변전소·선로가 모두 완공됐다고 가정한 결과다.
따라서 현재 snapshot의 `변전소 미완공`·`선로 미완공` 상태와 별개로 권역·정격 문제를 미리 말한다.

## 7. 코드와 화면 경계

제품 규칙은 기존 prototype과 분리된 `Gridworks.Core.Product` namespace에 둔다. 기존
`GridworksSession`, `Scope1PlacementSession`, fixture와 장면을 상속·확장하지 않는다.

제품 기본 장면은 하나이며 내부 책임은 작게 나눈다.

```text
ProductMain
├── FirstLightMapView
└── FirstLightTaskPanel
```

- `ProductMain`만 session을 소유하고 두 화면에 snapshot·preview를 전달한다.
- `FirstLightMapView`는 viewport 좌표를 정수 grid로 한 번 변환하고 intent를 보낸 뒤 상태를 그린다.
- `FirstLightTaskPanel`은 Core가 준 견적·첫 실패 이유와 표준 button만 표시한다.
- Game은 거리, 비용, 서비스 권역, 공급과 결산을 다시 계산하지 않는다.

제품 화면은 처음부터 container·anchor 기반의 resizable layout을 사용한다. 1280×720과
1920×1080에서 map과 panel이 겹치지 않아야 한다. 지도·버튼은 keyboard focus가 있고, 계획·공사·
통전은 색뿐 아니라 점선·사선·실선과 문장으로 구분한다. preview는 polite, 명령 오류는 assertive
접근성 live region을 사용한다.

## 8. 이 단계에서 만들지 않는 것

- 발전소 신규 건설, 병원, 공장과 복수 수요
- 복수 발전원, 경로 선택·우선순위·공유 용량 예약
- 전기·공간 사건, 내부전원, 발전비·보상·LostSales
- 완공 자산 철거와 수리
- 연속 시계, 타임라인, 공사 queue와 여러 crew
- 캠페인, 저장, 설정, 사운드와 최종 아트
- 범용 command bus, DI/ECS, plugin과 schema placeholder
- LLM 또는 외부 사람 플레이

## 9. 완료 증거

### 기계 검증

- strict loader와 overflow 거부
- 변전소 초안 배치·이동·취소와 오류 우선순위
- 서비스 원의 경계 포함·한 칸 밖 제외·권역 밖 발주 허용
- support 입력순서, 최대 span 경계, target 마지막 span, undo·전체 취소
- 견적, 현금 부족, 실패 불변과 음수 현금 없음
- 별도 project 생명주기, 공사 중 무전압과 원자 완공
- 공급 실패 우선순위와 세 정격의 경계
- 실제 인도분만 매출, 성공·실패 결산과 이중 결산 거부
- 모든 phase의 restart와 결정론적 snapshot 값
- 검사 코드만 소유한 성공 reference와 권역 밖 실패 reference
- 기존 Scope 0B·Scope 1 회귀

### native 확인

headless smoke도 handler를 직접 호출하지 않고 실제 viewport map click과 표준 button signal을 지난다.
변전소 초안 이동·취소, 지지물 undo·전체 취소, 별도 발주·완공과 성공 공급·결산을 한 번 수행한다.
1280×720과 1920×1080 실행의 최종 snapshot은 같아야 한다. 좌표 witness는 smoke 명령행과 검사만
소유하고 runtime fixture·화면·진단에는 기록하지 않는다.

별도 native 화면 검토에서는 두 해상도의 clipping·text overflow, keyboard focus, 접근성 이름과
색 이외 상태 표현을 확인한다. 자동 smoke가 이 시각 검토를 대신하지 않는다.

### 단계 종료

- 제품 fixture, Core, 검사와 기본 장면이 구현됐다.
- 첫 상태부터 성공 결산이 한 native 흐름에서 끝나고, 실패 결산과 모든 phase의 restart는 Core
  검사에서 닫힌다.
- 기존 두 prototype의 source·fixture·규칙 결과가 보존됐다.
- 미해결 critical과 다음 단계가 의존하는 core-flow major가 없다.
- README, 체크리스트, 오브젝트와 비주얼 문서가 실제 상태와 일치한다.
- 한 번의 독립 검토를 마쳤다.

외부 사람 관찰은 이 구현이 기술적으로 닫힌 뒤 별도 테스트 단계에서 수행한다. 현재 목표의 종료점은
그 관찰을 시작하기 직전이다.

## 10. 현재 검사와 종료 기록

저장소 root에서 기계 검사를 실행한다.

```sh
dotnet run --project tools/Gridworks.ProductChecks/Gridworks.ProductChecks.csproj -c Release -- data/product-first-light-v1.json
dotnet build game/Gridworks.Game.csproj -c Debug -t:Rebuild
```

다음 smoke는 map handler를 직접 호출하지 않고 실제 viewport 입력과 표준 button signal을 거친다.
좌표는 재현용 검사 입력일 뿐 fixture, 일반 플레이와 진단 기록의 기본값이 아니다.

```sh
godot_bin="$PWD/.tools/godot-4.7.1/Godot_mono.app/Contents/MacOS/Godot"
smoke_dir="$(mktemp -d /private/tmp/gridworks-first-light-smoke.XXXXXX)"

"$godot_bin" --headless --path "$PWD/game" --scene res://ProductMain.tscn \
  --resolution 1280x720 --log-file "$smoke_dir/engine-1280.log" -- \
  --session-id FIRST-LIGHT-1280 --diagnostic-log "$smoke_dir/app-1280.jsonl" \
  --smoke --smoke-substation 13,6 --smoke-substation 14,6 \
  --smoke-support 6,6 --smoke-support 10,6

"$godot_bin" --headless --path "$PWD/game" --scene res://ProductMain.tscn \
  --resolution 1920x1080 --log-file "$smoke_dir/engine-1920.log" -- \
  --session-id FIRST-LIGHT-1920 --diagnostic-log "$smoke_dir/app-1920.jsonl" \
  --smoke --smoke-substation 13,6 --smoke-substation 14,6 \
  --smoke-support 6,6 --smoke-support 10,6
```

종료 시 Product 검사는 10개 묶음, 664개 assertion을 통과했다. Scope 0B·Scope 1 회귀, Game
rebuild와 두 해상도 viewport smoke도 통과했고 두 실행은 같은 성공 결산에 도달했다. 별도 native
검토에서 clipping·text overflow가 없고 계획·공사·통전이 색 이외의 선 모양과 문장으로 구분됨을
확인했다. 지도와 표준 버튼의 접근성 이름·설명, Tab focus와 화살표·Enter 지도 입력도 확인했다.
독립 코드·화면·문서 검토 뒤 남은 critical과 core-flow major는 없다.

`HumanValidationStatus = NOT_COLLECTED`다. 이 기록은 사람 사용성·재미·밸런스를 통과했다는 뜻이
아니며 다음 로드맵 단계를 열지 않는다.
