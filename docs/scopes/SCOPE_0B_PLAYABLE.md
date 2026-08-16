# Gridworks — Scope 0B authored 2D playable

> 상태: **ACTIVE — L00 title-target 수정 review 중, 공식 proxy 미개방**
>
> 선행 증거: [Scope 0A R2](SCOPE_0A_R2_CARD_TEST.md) `PROXY-PASS`, 네 field와 integrated 모두 `5/5`
>
> 사람 증거: `HumanValidationStatus = NOT_COLLECTED`

이 문서는 카드에서 확인한 인과가 실제 상태변화와 클릭이 있는 화면으로 전이되는지만 검사하는
Scope 0B의 완전한 실행 계약이다. 계약·fixture의 독립 review checkpoint는 완료됐고, 이 문서의
구현·자동검사와 독립 코드 review는 완료됐다. 첫 [L00 결과](../../playtests/scope-0b/L00_RESULT.md)는
Computer Use transport가 AX·screenshot을 반환하지 못한 `PROXY-RUN-BLOCKED`였다. 재시작 뒤 AX
읽기는 복구됐고, Godot editor build의 실제 `(DEBUG)` 창 제목을 target에 반영하는 preflight 수정이
review 중이다. 공식 조작 proxy는 같은 build의 L00가 실제 full run을 통과할 때까지 열리지 않는다.
후보였던 범위보다 이 문서가 더 작으며, 여기에 없는 기능은 현재 backlog가 아니다.

## 1. 증거와 한 문장 가설

R2에는 통과하지 못한 scored 오해가 없다. 따라서 새 오해를 발명하거나 카드를 다시 튜닝하지
않는다. 남은 위험은 하나다.

> 구조화된 카드에서 네 인과를 적용한 cold LLM proxy가, 같은 정보를 가진 10~15분짜리
> stateful 2D 화면에서도 도움 없이 마을 접속공사를 발주하고, 두 회랑×두 사건을 결과 공개 전에
> 예측하고, 회랑을 선택해 고정 사건과 복구까지 완료할 수 있는가?

이 gate가 입증할 수 있는 것은 동일-model LLM의 **실제 UI 조작 전이**뿐이다. 사람 이해도,
자발적 발견, 재미, 접근성, 경제 밸런스와 자유 배선의 품질은 입증하지 않는다.

## 2. 권위와 version

| 항목 | 단일 권위 |
|---|---|
| 규칙·상태전이·범위·판정 | 이 문서 |
| 숫자·ID·좌표·경로·oracle | [`data/scope-0b-v1.json`](../../data/scope-0b-v1.json) |
| Scope 0A 인계 원본 | [종료된 Scope 0A R1 §5](SCOPE_0A_CARD_TEST.md#5-동결-fixture) |
| 구현 후 participant 절차 | 구현 checkpoint에서 동결하는 [`playtests/scope-0b/FACILITATOR_SHEET.md`](../../playtests/scope-0b/FACILITATOR_SHEET.md) |

- `ContractVersion = S0B-CONTRACT-v1`
- `FixtureVersion = S0B-FIXTURE-v1`
- `BuildVersion = S0B-BUILD-v1` — 구현 commit hash와 함께 동결한다.
- `PromptVersion = S0B-PROXY-v1`
- `DecisionRuleVersion = S0B-GATE-v1`

JSON의 `verificationOnly`는 handoff·oracle 검사 전용이다. Core assembly의 경계 loader는 같은
파일을 한 번 strict parse해 `ScenarioDefinition`, `PresentationDefinition`, `FixtureOracle`을
분리한다. transition engine에는 scenario만, Game adapter에는 scenario와 presentation만 전달하고
Checks만 oracle을 받는다. transition engine이나 Game이 oracle 값을 읽어 결과를 만들면 실패다.
알 수 없는 JSON field, 중복 ID와 끊어진 참조는 즉시 거부한다.

## 3. 포함과 제외

### 포함

- `1280×720` 고정 창의 단일 탑다운 2D scene
- 기존 가스발전·마을·병원·고정 변전소와 authored edge
- 마을 상위 피더 공사 한 개
- 결과 공개 전 네 prediction 입력과 강변/북부 회랑 중 한 개의 원자 발주
- 반개구간 정산, 프로젝트 원자 완공, 고정 이정표 진행
- load별 authored 공급경로, shared-edge·발전원 용량 fixture 검증
- E1 단일제거의 counterfactual 평가와 강변 공간제거의 실제 4시간 사건
- 병원 소유 UPS·디젤과 전력회사 공급·계량 분리
- 판매, 가스 변동비, 미공급 보상, 진단용 LostSales
- 화면 구석의 읽기 전용 선형 예고 타임라인과 원인 패널
- 실패 명령의 권위 상태 불변, deterministic snapshot

### 제외

- 자유 배치·선 긋기·전신주·`MaxSpan`과 교차 접속 — 이는 Scope 1 후보조차 아직 미개방이다.
- 일반 BFS/최단경로, 임의 mesh, 역송, 병렬 경로 합산과 자동 절체 최적화
- 부분공급, 급전, DC/AC 전력조류, 손실·열·보호·주파수
- 확률사건, 정비·수리, 폭염·공장 증설, 발전원 추가
- 공사 queue, 작업반, 발주취소, 철거, save/replay와 범용 scheduler
- 3D, camera control, animation pipeline, localization framework, telemetry service
- 선택률·완료시간·현금값을 목표로 한 parameter 조정
- 미래 interface, 빈 schema field와 확장용 abstraction

## 4. Fixture 의미와 인계 계약

### 4.1 Schema 경계

fixture root는 다음 현재 사용 field만 가진다.

```text
schemaVersion, fixtureId, displayName, units, calendar, economy,
nodes, edges, projects, loads, requirements, permittedSupplyPaths,
evaluationCases, events, hospitalInternalPower, milestones, presentation, verificationOnly
```

- `serviceSubstationId`가 존재하면 해당 수요가 그 변전소의 authored 서비스 권역 안에 있다는
  뜻이다. Core는 반경을 계산하지 않는다. 지도 원은 비권위 표현일 뿐 공급 판정에 쓰지 않는다.
- edge endpoint는 topology 검사용이다. 공급 판정은 `permittedSupplyPaths.edgeIds`만 사용한다.
- project는 `allowedOrderMinute`에만 발주할 수 있다. 지연 발주·queue는 없다.
- actual event는 `evaluationCaseId`를 참조해 제거 selector를 복제하지 않는다.
- milestone은 pause 시각과 label만 가진다. prediction 입력은 scene-local 상태다.
- `presentation`은 현재 stimulus의 map bounds, service ellipse, 같은 `OLD_CORRIDOR`를 나타내는
  좁은 위험 band들, edge별 polyline과 AB/BA order만 가진다. simulation은 이를 읽지 않는다.
  모든 polyline 첫·끝은 해당 edge endpoint와 같아야 한다. verifier는 OLD group 선로의 interior만
  위험 band 안에 있고 다른 선로 interior는 밖에 있는지, shared terminal 이외의 선로 교차가 없는지
  이 fixture에 한정해 검사한다. 이 검사는 범용 geometry나 pathfinding 규칙이 아니다.
- `NO_BUILD`는 handoff oracle에서만 쓰며 player 선택지로 노출하지 않는다.

### 4.2 Authored 공급경로

각 active load는 먼저 primary path 하나를 검사한다. 병원 primary가 끊겼을 때만 선택한 회랑에
해당하는 backup 하나를 검사한다. 배열 순서나 같은 rank의 tie-break로 backup을 고르지 않는다.
마을은 primary 하나뿐이다.

경로는 다음을 모두 만족할 때만 usable이다.

1. 첫 edge가 온라인 발전원에서 시작하고 마지막 edge가 해당 load에서 끝난다.
2. 모든 edge가 연속되고 `commissioned && !eventRemoved`다.
3. backup이면 그 회랑이 실제 선택·완공됐다.

Core는 edge endpoint를 탐색해 새 경로를 만들지 않는다. 특히 북부선에서 병원을 거쳐 마을로
역송하는 경로는 존재하지 않는다. 각 load의 경로가 결정된 뒤 edge별 할당수요와 온라인 발전원의
총 인도가 정격 이하인지 검사한다. 초과하면 부분배분하지 않고 `FIXTURE_INVALID`로 중단한다.

### 4.3 공급·내부전원·현금

```text
UtilityDelivered = active load에 usable authored path가 있고 전체 정격검사를 통과
UtilityUnserved = active demand - utility delivered
GasInjection = utility delivered power 합계
HospitalInternalUsed = 병원 utility가 없을 때 P0를 UPS → diesel 순서로 보충
HospitalP0Unserved = P0 demand - utility delivered - internal used
```

병원 내부전원은 병원 연속성에는 포함하지만 전력회사 인도·판매·가스주입에는 포함하지 않는다.
내부전원이 P0를 지켜도 병원 계약경계의 utility 미공급 보상은 발생한다. `LostSales`는 진단값이고
현금에서 다시 차감하지 않는다.

모든 화폐 계산은 checked `long`으로 수행한다.

```text
EnergyKwMinute = PowerKw × ElapsedMinutes
Numerator = EnergyKwMinute × RateCashUnitPerGWh
CashUnit = Numerator / 60,000,000
```

나머지가 0이 아니거나 overflow가 나면 fixture 오류다. 파생값은 소수 부동소수점으로 계산하지
않는다.

### 4.4 Scope 0A 인계검사

구현 전에 contract verifier가 JSON과 R1 §5를 대조해 다음을 고정한다.

- 7 node·8 edge의 ID, endpoint, 좌표, 정격, contingency, spatial group와 초기 공사상태
- 시작 현금·공사비·공기·의무시각·사건구간·단가
- 시작 시 마을은 `service eligible = true`, `utility path = false`; 병원 utility path는 true
- 여섯 제거행렬, 정상 32 MW·trunk 40 MW·발전 80 MW의 용량여유
- UPS 15분, diesel 285분, 총 300분과 4시간 뒤 60분 잔여
- 초기·발주·완공·사건 시작·강변 분기의 숨은 15분·복구 boundary의 full snapshot과
  interval/cumulative settlement
- 모든 display polyline endpoint, OLD-group risk-band 일치, 비노드 교차 부재, service shape와
  AB/BA order의 완전성
- 선택 직전 현금, 정상구간 순현금, 사건 energy·정산·기말현금

인계검사가 통과하고 계약 checkpoint가 reviewed 상태가 된 commit부터 JSON이 **Scope 0B의** 기계
숫자 권위다. R1 문서는 Scope 0A 역사 증거로 그대로 남는다.

## 5. Core state와 명령

### 5.1 권위 상태

직렬화되는 권위 `PublicSnapshot`은 현재 필요한 값만 갖는다.

```text
Minute, Cash,
TownProjectState, CorridorProjectState, SelectedCorridor,
CommissionedEdgeIds, EventRemovedEdgeIds,
ActiveLoadIds, UtilityPathByLoad,
HospitalInternalStage, HospitalInternalRemainingKwMinute,
Interval, Cumulative, IsComplete
```

transition engine 내부 clock 이름 `CurrentMinute`가 snapshot의 `Minute`로 투영된다. JSON은 이 목록의
lower-camel 이름을 사용하므로 `minute`, `interval`, `cumulative`가 exact field다.

machine enum은 project `not_ordered → building → commissioned`, internal source
`none | ups | diesel`로 직렬화한다. `UtilityPathByLoad`는 active load를 정확히 한 번씩 key로 갖고,
공급되지 않으면 value가 null이다. 모든 ID array와 object key는 ordinal 정렬한다.

`Interval`과 `Cumulative`의 두 settlement는 같은 닫힌 shape를 쓴다.

```text
revenueCashUnit, gasCostCashUnit, compensationCashUnit, lostSalesCashUnit,
utilityDeliveredKwMinuteByLoad, utilityUnservedKwMinuteByLoad,
gasInjectionKwMinute, hospitalInternalUsedKwMinute, hospitalP0UnservedKwMinute
```

load별 energy map은 active 여부와 무관하게 병원·마을 두 key를 항상 가지며, 비활성 구간은 0이다.
`interval`은 **현재 accepted command 시작부터** 누적한다. 발주 command는 모두 0이고 Advance는
숨은 boundary를 지나도 초기화하지 않는다. 따라서 강변 final의 interval은 마지막 225분이 아니라
한 번의 공개 Advance 전체 240분이며, 숨은 trace는 같은 interval의 첫 15분 누계다. `cumulative`는
session 시작부터다. prediction 선택·hover·panel reveal은 Core state가 아니다. Core는 별도 command
bus, receipt 저장소나 replay log를 만들지 않는다. `verificationOnly.commonBoundaryStates`와
`routeBoundaryStates`가 PublicSnapshot과 trace projection의 exact oracle이다.
각 oracle row의 `id`는 Checks 전용 boundary label이고 snapshot field가 아니다. 그 label을 제외한
lower-camel JSON field들이 위 PublicSnapshot 전체와 정확히 대응한다.

각 상태변경 함수는 다음 `CommandResult`를 반환한다.

```text
Accepted, ErrorCode|null, PublicSnapshot, BoundaryTrace[]
```

`AdvanceToNextMilestone`의 trace는 호출 시작과 public target **사이의 내부 boundary만** 순서대로
반환한다. public target은 `PublicSnapshot`에만 있다. 따라서 그 밖의 Advance trace는 비어 있고,
강변 사건 시작→복구 Advance만 `[UPS_DEPLETED]` 한 row를 가진다. 병원 utility가 유지되는 북부에는
그 경계가 없다. trace는 반환 뒤 session에 저장하지 않으며 save/replay/receipt가 아니다. 발주와
거부 결과의 trace도 비어 있다. Game과 private diagnostic은 public target snapshot만 사용하고,
Checks만 `BoundaryTrace`를 exact oracle과 대조한다.

### 5.2 상태 변경 명령

1. `OrderTownFeeder()`
   - 정확히 시작시각과 미발주 상태에서만 성공한다.
   - 비용을 즉시 한 번 차감하고 completion minute를 고정한다.
2. `OrderCorridor(RIVER_PARALLEL | NORTH_DETOUR)`
   - 정확히 회랑 선택시각, town feeder 완공 뒤와 회랑 미선택 상태에서만 성공한다.
   - 비용을 즉시 차감하고 선택한 project 하나만 Building으로 만든다.
3. `AdvanceToNextMilestone()`
   - 현재 화면의 필수 action이 완료된 경우에만 `CurrentMinute`보다 큰 첫 fixture milestone까지 진행한다.
   - 마지막 milestone 뒤에는 거부한다.

`GetSnapshot()`과 `EvaluateRemoval(design, caseId)`는 무상태 query다. `EvaluateRemoval`은 현재
Building 상태를 평가하지 않고, town feeder와 인자로 받은 design 회랑이 모두 완공되고 두 load가
active인 **고정 post-deadline reference state**를 만든 뒤 case selector 하나만 적용한다. 제거 집합은
그 reference state에서 `commissioned && selector match`인 edge뿐이다. chronological event,
내부전원 소비와 현금은 적용하지 않는다. return은
`design, caseId, removedEdgeIds, townUtilityDelivered, hospitalUtilityDelivered,
townPathId|null, hospitalPathId|null`뿐이다. `NO_BUILD`는 Checks 전용이고 Game은 River/North만
요청한다. UI는 네 prediction과 회랑 선택을 모두 입력받은 뒤 한 번의
`예측·계획 확정` handler에서 정확히 한 `OrderCorridor`를 보낸다. 성공 응답을 받은 경우에만 같은
handler가 prediction을 불변 표시 데이터로 잠그고 reveal을 연다. 명령이 거부되면 입력은 수정
가능한 채로 남는다. Core는 prediction을 채점하거나 simulation 입력으로 쓰지 않는다.

거부 code는 `WRONG_TIME`, `REQUIRED_ACTION_PENDING`, `ALREADY_ORDERED`,
`NO_NEXT_MILESTONE`로 닫는다. 유일한 fixture에서 두 발주는 모두 확정적으로 감당 가능하므로
별도 현금부족 branch와 시험용 scenario를 만들지 않는다. town/corridor 발주는 time·선행완공 → 기존발주 순서로,
Advance는 complete → pending action 순서로 검사한다. typed corridor enum 밖의 값은 adapter 입력
오류이고 Core 명령이 아니다. `FIXTURE_INVALID`는 load/capacity 검증의 fatal session 상태이지
command 거부 code가 아니다. 거부 전후의 권위 snapshot은 동일하고 trace는 비며 UI 진단문만
달라질 수 있다.

### 5.3 필수 action과 진행

- 시작: town feeder가 발주되기 전 `Advance` 거부
- 첫 이정표: Core는 회랑 발주가 성공하기 전 `Advance`를 거부한다. prediction 완결·잠금은 adapter가
  회랑 명령 호출 전에 보장하는 scene-local invariant다.
- 이후: 정해진 이정표로만 진행

prediction 순서는 반드시 `네 칸 입력 → 회랑 선택 → 한 번에 잠금·발주`다. 네 칸이 모두
선택되기 전에는 회랑 control 자체가 disabled다. 회랑을 먼저 짓거나
결과를 먼저 공개하지 않는다. 잘못된 prediction도 발주 자체는 허용하며 결과 공개 뒤 수정할 수
없다.

## 6. 시간 경계와 결정론

모든 interval은 `[current, target)`이다. `AdvanceToNextMilestone`은 다음 공개 milestone까지의
event 시작·복구와 **실제로 선택된** 내부전원 고갈시각을 모아 오름차순 boundary로 처리한다.
공개 목록에 없는 UPS 고갈시각은 강변 사건에서만 생기며, 각 boundary마다 다음 순서를 정확히
한 번 적용한다.

1. boundary 직전까지 현재 공급·의무·event 상태로 전력과 현금을 정산한다.
2. clock을 boundary로 이동한다.
3. boundary에 끝나는 project를 원자 완공하고 edge 전체를 편입한다.
4. boundary의 새 load 의무와 2회로 deadline을 적용한다.
5. boundary의 event 제거 또는 복구를 적용한다. `EventRemovedEdgeIds`는 그 순간
   `CommissionedEdgeIds ∩ selector match`이며 항상 commissioned 집합의 부분집합이다.
6. authored path를 다시 고르고 shared-edge·발전원 용량을 검증한다.
7. 병원 utility가 없으면 UPS, 그다음 diesel을 무공백으로 선택한다. utility가 돌아오면 내부전원을
   더 소비하지 않는다.
8. snapshot을 만든다. boundary가 공개 milestone일 때만 scene을 pause한다. `IsComplete`는 복구
   boundary 처리가 끝날 때만 true가 되고 그전에는 false다. 그 뒤 Advance는 `NO_NEXT_MILESTONE`이다.

따라서 town feeder는 의무 활성과 같은 분에 먼저 완공되고, 회랑은 2회로 확인과 같은 분에 먼저
완공된다. 실제 공간사건은 시작분에 edge를 제거한 뒤 UPS를 즉시 사용하고, 15분 경계에는 diesel로
넘어가며, 복구분 직전까지 outage를 정산한 뒤 edge를 복구한다. `verificationOnly`의 모든
milestone·event oracle이 exact match해야 한다.

같은 fixture와 명령열을 새 session 두 번에 적용하면 각 snapshot과 ledger가 byte-equivalent JSON
이어야 한다. stable 정렬은 fixture 배열순서가 아니라 ID ordinal을 사용한다.

## 7. 2D 화면과 상호작용

### 7.1 한 scene

- 고정 창 `1280×720`, Compatibility renderer, camera·zoom 없음
- 왼쪽 지도, 오른쪽 현재 원인·행동 panel, 아래쪽 선형 timeline
- 외부 art 없이 Godot primitive, `Line2D`, `Polygon2D`, `Label`과 표준 `Control`만 사용
- service eligible, 무전압, 공사 중, 통전, event 제거를 색과 함께 선형·pattern·문장으로 표시
- 내부 stable ID, oracle, rubric, `SAFE/RISKY`, 추천·승자·점수는 participant UI와 접근성 tree에 없음

### 7.2 화면 단계

1. **시작** — service area 안 마을과 무전압 변전소, 끊긴 상위 피더, 병원 공급, 사건 timeline.
   `마을 접속공사 발주 6 M`과 `다음 이정표`를 보여주되 pending 동안 Advance는 disabled다.
2. **회랑 결정** — 두 회랑을 같은 viewport·정보순서로 보여준다. 병원 소유 UPS 15분과 diesel
   285분, 내부전력은 utility 인도·판매가 아니고 보상경계와 별개라는 premise를 먼저 보여준다.
   두 계획 모두 기한 안에 완공되고 병원 주 회로 E1과 다른 차단 회로를 쓴다는 공통 premise와,
   fixture에서 읽은 각 계획의 비용과 `기존 강변 통로 안 / 강변과 떨어진 북부 경로`라는
   자연어 공간경로를 중립적으로 보여준다. 2×2 표의 행은
   `강변 병렬선 / 북부 우회선`, 열은 `병원 주 회로 E1만 사용불가 / 강변의 기존 회랑 전체 사용불가`,
   질문 대상은 **전력회사의 병원 utility 공급경로가 남는가/끊기는가**다. 각 cell은 exclusive
   `남음/끊김` 두 option 중 하나를 고른다. 네 cell이 모두 채워진 뒤에만 exclusive 강변/북부 계획
   control을 enable한다. 이 다섯 group이 채워져야 `예측·계획 확정`이 enabled다. 정답 선택이나
   계획별 사건결과는 미리 표시하지 않는다.
3. **공사·의무** — 선택 회랑을 Building, 완공분 뒤 Commissioned로 구분한다. prediction은
   잠금 상태로 남고 causal reveal은 확정 뒤에만 열린다. reveal은 Core의 `EvaluateRemoval`이 돌려준
   네 utility/path 결과만 보여준다. 내부전원 계산을 Game에서 복제하거나 미선택 회랑의 가상 P0
   결과를 만들지 않는다.
4. **사건** — 강변 위험대와 제거 edge, 살아 있는 utility path, 마을/병원 utility, 병원 P0 source,
   UPS→diesel 경계를 원인 panel과 지도에서 함께 갱신한다.
5. **복구·결산** — utility 복구, event-only 판매·가스비·보상·LostSales·현금변화를 분리해 보이고
   현재 현금과 내부전원 잔여를 표시한다. 승자나 총점은 표시하지 않는다.

timeline에는 현재, 발주공사 예상 완공, 병원 2회로 기한, 강변 사건 시작·복구만 표시한다.
`DAY 9 16:15`는 강변 선택 때만 Core가 한 번의 advance 안에서 자동 분할하는 내부 정산 경계다.
별도 pause나 예고사건으로 표시하지 않고, 해당 run의 복구 화면만 UPS 15분 뒤 diesel로 무공백
절체됐음을 설명한다.
폭염·공장 증설과 빈 future slot은 없다.

### 7.3 입력·접근성

모든 조작은 표준 `Button` 또는 `CheckButton`으로 한다. drag와 hidden hotspot은 없다.

- 일반 control은 visible text와 같은 `accessibility_name`을 쓴다. 반복되는 prediction option만
  `계획 행 / 사건 열 / 남음|끊김`을 합친 고유 AX name(예: `강변 병렬선 / E1 단독 / 남음`)을 쓰고,
  visible option text 자체는 `남음|끊김`으로 유지한다. description은 중립적이며 답을 누출하지 않는다.
  모든 control은 `FocusMode.All`과 명시적 focus neighbor를 가진다.
- 새 단계의 첫 action은 deferred `GrabFocus()`로 focus한다.
- mouse와 keyboard가 같은 handler를 한 번만 호출한다.
- prediction lock 전 causal·settlement reveal은 scene tree에서도 hidden이다.
- prediction 미완료는 scene-local `INPUT_INCOMPLETE`로 처리하고 Core를 호출하지 않는다. Core-bound
  disabled action을 강제로 호출한 경우에만 Core가 해당 명령을 거부하고 상태가 바뀌지 않는다.
- 접근성 tree에 보이는 화면 문구는 허용하지만 숨은 ID·정답·rubric은 금지한다.

## 8. 구현 구조와 toolchain

### 8.1 디렉터리와 의존 방향

```text
data/scope-0b-v1.json
src/Gridworks.Core/          Godot 비의존 규칙·fixture loader·state transition
tools/Gridworks.Checks/      외부 test framework 없는 deterministic console checks
game/                        project.godot, 한 scene, C# adapter와 draw/UI
playtests/scope-0b/          verifier, checkpoint, 후속 진행자 자료·공개 결과
```

의존은 `Game → Core`, `Checks → Core`뿐이다. Core의 boundary loader가 fixture envelope를 세 부분으로 분리하고
Checks는 scenario 결과와 `verificationOnly`를 대조한다. transition engine은 Godot, presentation,
filesystem path, UI string, test oracle을 참조하지 않는다. DI container, ECS, event sourcing,
repository pattern과 일반 scheduler를 쓰지 않는다.

### 8.2 동결 toolchain

- Godot Engine **4.7.1 .NET**, macOS universal
- Godot source URL:
  `https://github.com/godotengine/godot/releases/download/4.7.1-stable/Godot_v4.7.1-stable_mono_macos.universal.zip`
- archive SHA-256:
  `92cac516baa8ddc7756eeaa38a6d007778a968bfbf188db7c5d6e6ec21c5d52c`
- .NET SDK **8.0.129**, `global.json`에서 `rollForward = disable`, `allowPrerelease = false`
- Core·Checks target `net8.0`; C# nullable와 warnings-as-errors 활성
- Godot download는 `.tools/` 아래에 풀고 Git에서 제외한다. export template과 app export는 없다.

공식 macOS 페이지는 [.NET build가 C# 지원과 별도 .NET SDK를 요구함](https://godotengine.org/download/macos/)을,
[4.7.1 release](https://godotengine.org/article/maintenance-release-godot-4-7-1/)는 stable maintenance
version임을 확인한다. 구현 checkpoint에 실제 `dotnet --info`, Godot `--version`, archive hash를
기록한다.

## 9. 구현 TODO

### Fixture와 Core

- [x] strict fixture DTO·unknown-field 거부·ID/reference/enum validator
- [x] scenario/presentation/oracle을 분리해 transition engine과 Game에 oracle을 넘기지 않는 loader
- [x] authored service eligibility와 permitted-path 연속성 validator
- [x] project 세 상태, 세 명령과 query 두 개
- [x] 반개구간 정산과 고정 boundary 순서
- [x] primary 우선, 선택 backup 하나뿐인 route resolver
- [x] shared-edge·발전원 capacity validator; 초과 시 fail closed
- [x] UPS→diesel 에너지 보존과 utility/P0/internal 계량 분리
- [x] checked integer cash ledger와 진단용 LostSales
- [x] deterministic snapshot serialization

### Game

- [x] 한 scene의 지도·원인 panel·timeline과 다섯 화면단계
- [x] PresentationDefinition과 Core snapshot만 읽는 renderer, command result만 처리하는 adapter
- [x] prediction 4 cell·회랑 1개 입력, atomic lock/order, reveal 차단
- [x] 공사·무전압·통전·제거·내부전원의 color-independent 표현
- [x] 표준 control 접근성 name·description·focus order
- [x] fixed AB/BA layout argument와 session ID diagnostic header
- [x] participant에게 보이지 않는 private diagnostic JSONL log
- [x] `--smoke`에서 UI handler를 거쳐 한 valid 흐름을 실행하고 종료하는 wiring check

app diagnostic log는 save/replay나 제품 telemetry가 아니다. 각 JSONL row는 `schemaVersion`, monotonic
`sequence`, process-local `elapsedMs`, `sessionId`, `variant`, `event`, `accepted`, `snapshotHash`와
`payload`를 정확히 한 번씩 가진다. event는 다음 다섯 가지뿐이다.
`snapshotHash`는 stable property/ID order로 직렬화한 exact UTF-8 `PublicSnapshot` JSON bytes의
SHA-256 lowercase hex다.

| event | payload |
|---|---|
| `READY` | `buildHash`, `fixtureHash` |
| `COMMAND` | `commandName`, `errorCode|null` |
| `PREDICTION_LOCKED` | `RIVER_E1`, `RIVER_OLD`, `NORTH_E1`, `NORTH_OLD`의 `remains|cut`, `selectedCorridor` |
| `REVEAL_OPENED` | 빈 object |
| `FINAL` | 빈 object; row의 `snapshotHash`가 final 권위 |

`accepted`는 COMMAND에만 Core 결과이고, 나머지는 해당 scene 경계가 실제 열렸으면 true다.
실행 중 위 row만 append하고 proxy 원답과 함께 Git 제외한다.
게임 규칙은 log를 읽지 않는다. Godot engine log와 app JSONL은 반드시 다른 파일이다.

## 10. 자동검사와 preflight

### 10.1 Contract·fixture

- [x] JSON syntax, strict root/schema와 모든 unique ID·reference
- [x] Scope 0A handoff §4.4 전체와 여섯 제거행렬
- [x] authored path 연속성, primary/selected-backup 정책, 일반 BFS·역송 부재
- [x] OLD 위험 band/edge group 일치와 shared terminal 밖 polyline 교차 0
- [x] evaluation oracle의 정확한 `3 designs × 2 cases` pair 집합
- [x] 정상 shared capacity와 발전원 여유
- [x] 모든 cash numerator exact division, overflow 없음

### 10.2 Core

- [x] 시작 `town eligible=true/path=false`, hospital path=true
- [x] 발주 즉시 비용 한 번 차감, Building edge 공급불가, 완공분 원자 편입
- [x] 같은 분 `완공 → 의무`, `event → route → internal source` 순서
- [x] 공개 이정표와 강변의 숨은 15분 경계 full snapshot·interval/cumulative settlement exact oracle;
  북부 trace에는 숨은 경계 없음
- [x] UPS 15분, diesel 285분, 무공백, 사건 뒤 60분 잔여
- [x] utility delivered/unserved, internal used, P0 unserved, gas, sales와 compensation 보존
- [x] E1 counterfactual query와 snapshot query 무상태
- [x] 모든 도달 가능한 거부 code 전후 권위 snapshot 동일
- [x] 동일 command script 두 번의 snapshot·ledger exact equality
- [x] AdvanceResult는 public target을 trace에 중복하지 않고, 강변 사건 Advance에서만
  `[UPS_DEPLETED]` transient projection을 반환하며 저장하지 않음

### 10.3 Game·native smoke

- [x] managed build, Godot headless import/build/boot, 오류 0과 정확한 `READY`
- [x] 단계별 visible/enabled action과 focus order
- [x] prediction 네 cell 미완료 시 lock 불가, lock 뒤 immutable, reveal 이전 hidden
- [x] 네 prediction 전에는 회랑 control disabled; incomplete confirm은 UI-only이고 Core call 0회
- [x] UI signal 한 번이 Core command 한 번만 호출
- [x] `--smoke`가 start→order→prediction/plan→event→final을 완료
- [x] native `1280×720`에서 clipping·겹침·색 이외 상태구분 수동 QA
- [x] source tree에 save/replay, general BFS, future schema와 placeholder UI가 없음

검증 명령은 구현 checkpoint에서 exact path와 함께 동결한다. 모든 자동검사가 통과한 뒤 큰 작업단위
initial commit → 독립 review → scope-valid 수정 → 재검사 → reviewed commit을 완료해야 proxy
preflight로 넘어간다.

## 11. 실제 UI LLM proxy

### 11.1 기술 preflight

공식 session 전, 집계하지 않는 `S0B-L00` 한 번으로 다음을 확인한다. 구현 freeze checkpoint는
먼저 실제 Godot binary·project·engine log·app diagnostic JSONL·runner manifest의 절대경로를 넣은
아래 command template, app target과 두 variant를 exact text와 hash로 동결한다.

```text
<GODOT> --path <GAME> --windowed --resolution 1280x720 --position 0,40 --single-window
  --accessibility always --rendering-method gl_compatibility --log-file <ENGINE_LOG>
  -- --session-id <SESSION_ID> --variant <ab|ba> --diagnostic-log <DIAGNOSTIC_JSONL>
```

1. 새 native Godot process 하나만 실행한다.
2. `--accessibility always --windowed --resolution 1280x720 --single-window`로 부팅한다.
3. Computer Use의 `get_app_state`가 20초 안에 두 번 반환하고 title·현재상태·enabled action을
   accessibility tree 또는 screenshot에 노출한다.
4. element-index click을 우선하고, 실패할 때만 screenshot 좌표 또는 Tab/Return으로 한 full run을
   완료한다.
5. 각 click 뒤 fresh app state가 바뀐다. Core command 또는 확정 click 뒤에만 accepted-command나
   prediction-lock diagnostic이 추가된다. scene-local option click에 command log를 요구하지 않는다.

AX와 screenshot을 모두 얻지 못하거나 실제 accepted command를 만들 수 없으면
`PROXY-RUN-BLOCKED`다. 이는 게임 gate 실패가 아니며 정적 이미지·self-report로 대체하지 않는다.

### 11.2 공식 세션

아래 code block이 `S0B-PROXY-v1`의 exact participant prompt template다. `<SESSION_ID>`만 할당표의
값으로 치환할 수 있다. 구현 checkpoint의 facilitator sheet는 이를 verbatim 복사하고 template와
치환된 다섯 message hash를 동결하며, 설명·힌트·성공조건을 덧붙이지 않는다.

```text
당신은 Gridworks를 처음 플레이하는 전력회사 운영자입니다. 세션 ID는 <SESSION_ID>입니다.
측정 시작 전에 coordinator가 지정한 computer-use SKILL.md만 한 번 읽고, 그 뒤에는 이미 열린
Gridworks 창을 Computer Use(node_repl + @oai/sky)로만 조작하세요. repository, shell, web,
정적 카드와 다른 세션은 보지 마세요.

화면에서 직접 읽을 수 있는 정보만 사용해 처음 상태부터 마지막 복구·결산 화면까지 도움 없이
진행하세요. 결과가 공개되기 전에 표의 네 병원 utility 공급경로 결과를 모두 예측해 잠그고,
그다음 건설할 회랑 하나를 선택하세요. 앱이 막히거나 끝까지 갈 수 없으면 실제로 시도한 마지막
상태와 막힌 이유를 남기세요.

마지막에는 짧게 보고하세요: (1) 서비스 권역이 가능하게 하는 것과 실제 공급에 더 필요한 것,
(2) 잠갔던 네 예측과 전기회로 사고/공간 통로 사고의 차이, (3) 선택한 회랑과 실제 사건 결과,
(4) 병원 UPS·디젤이 지킨 P0와 전력회사의 병원 인도·판매가 같은지 다른지,
(5) 예상 밖이거나 이해하기 어려웠던 조작.
```

- 신규 cold session: `S0B-L01 AB`, `L02 BA`, `L03 AB`, `L04 BA`, `L05 AB`
- model: `gpt-5.6-sol`, reasoning `medium`, `fork_turns = none`
- 세션은 직렬 실행하며 매번 새 process·새 in-memory fixture로 시작한다.
- coordinator가 앱을 시작한 뒤 participant는 Computer Use의 `node_repl + @oai/sky`만 사용한다.
  측정 bootstrap 전에 환경이 제공한 exact `computer-use/SKILL.md`를 한 번 read-only로 읽는 것은
  허용하고 기록한다. 그 외 shell, repository, web, static card, oracle, rubric과 다른 session 답은
  금지한다.
- 15분 상한. coordinator는 app과 별도인 runner manifest JSONL에 `sessionId`, variant, build/fixture/
  prompt hash, `pid|null`, app target, monotonic `launchElapsedMs`, `readyElapsedMs|null`, `endElapsedMs`,
  `endReason`과 fault attribution을 남긴다. READY 미도달이면 ready 시각은 null이다.
  `endReason`은 `final | participant_stop | timeout | app_crash | runner_error`로 닫는다.
  `faultAttribution`은 `none | app | runner`로 닫고 독립 로그 근거를 함께 기록한다.
  허용 pair는 정확히 `final:none`, `participant_stop:none`, `timeout:none`, `timeout:app`,
  `app_crash:app`, `runner_error:runner`뿐이며 다른 조합의 manifest는 TechnicalValid=false다.
  `timeout:none`은 participant 미완료, `timeout:app`은 독립 로그로 입증된 frozen app hang이다.
- 독립 증거가 있는 host lock·Computer Use transport·process launch 같은 환경/runner 오류 slot만 최대
  2번 새 cold session으로 교체해 총 launch를 7로 제한한다. participant가 막히거나 timeout된 실행,
  또는 frozen app에 귀속되는 crash·무응답은 교체하지 않고 scored failure다.
- reveal 전 diagnostic의 locked 네 cell이 prediction 권위다. reveal 뒤 수정한 답은 세지 않는다.
- 최종 화면 뒤 participant는 서비스 권역/실제 공급, 두 사고축, 자신의 선택과 실제 사건결과,
  병원 내부전원/utility 인도·판매 경계와 예상 밖 행동을 짧게 보고한다.
- 원 transcript·diagnostic·screenshot은 `playtests/scope-0b/private/`에 두고 Git 제외한다. 공개에는
  SHA-256, 비식별 aggregate·오해·도구 한계만 남긴다.

`TechnicalValid`는 frozen build·fixture·prompt hash, 새 process, exact variant, 허용 bootstrap/tool과
완전한 transcript·runner manifest가 있고, (a) 정상 runner에서 `READY`와 app diagnostic prefix가
확보된 뒤 `faultAttribution != runner`이거나 (b) preflight가 통과한 동일 환경에서 frozen app 귀속
crash·무응답(`faultAttribution = app`)이 runner manifest로 입증되면 true다. 정상 runner의
participant stop·timeout과 app failure는 `InteractionCompletionPass = false`인 valid scored failure다.
host lock·Computer Use transport·process launch를 포함해 독립 증거로 `faultAttribution = runner`인
오류는 READY 전후와 무관하게 TechnicalValid=false이고 replacement 대상이다. 사후 추측만으로
app/runner 귀속을 바꾸지 않는다.
환경/runner 오류 replacement 뒤에도 valid 다섯 slot을 확보하지 못하면 `PROXY-RUN-BLOCKED`이고
판정하지 않는다.

### 11.3 사전 rubric

| Field | 같은 세션의 필수 증거 |
|---|---|
| `InteractionCompletionPass` | 무도움으로 feeder 발주, 네 prediction 잠금, 회랑 발주, 모든 이정표와 final 완료 |
| `CoverageActionPass` | 실제로 feeder를 연결하고, 권역은 접속 가능 관계이며 실제 공급은 온라인 발전원까지 usable path가 필요하다고 설명 |
| `RiskPredictionPass` | reveal 전 잠근 네 cell이 exact이고 선택한 회랑의 actual event 결과가 그 prediction과 일치함 |
| `UtilityBoundaryPass` | 병원 내부 UPS/diesel의 P0 연속성과 전력회사 병원 인도·판매 여부를 구분 |

E1 차단 회로와 강변 공간 통로의 reveal 뒤 원인 설명, 선택 회랑, 완료시간, 클릭수, 강변/북부
비율과 비용 trade-off 설명은 진단값이다. reveal 뒤 원인문을 복사한 것을 사전 인과 발견으로
점수화하지 않는다. R2가 이미 원인과 trade-off를 5/5로 통과했고 이 gate의 잔여 위험은 실제
조작 전이이므로 scored field를 다시 추가하지 않는다.

`IntegratedInteractionPass`는 위 네 field가 모두 true이고 `FacilitatorHelp = false`일 때만 true다.
revision 판정용 Core conclusion은 정확히 세 개로 따로 기록한다.

- `CoverageConclusionPass`: service area는 접속 가능 관계이고 공급에는 online source까지 path가 필요함
- `RiskConclusionPass`: reveal 전 네 utility prediction이 exact함
- `UtilityConclusionPass`: 병원 내부전원 P0와 utility 인도·판매 경계를 구분함

이 셋은 새 score 축이 아니라 복합 field 안에서 개념 회귀와 조작 결함을 분리하는 표식이다.

## 12. 판정과 한 번의 revision

`S0B-GATE-v1`은 technically valid 다섯 세션에서 다음을 모두 만족하면 `GO`다.

1. 네 field가 각각 `4/5` 이상
2. `IntegratedInteractionPass`가 `3/5` 이상

각 field `4/5`는 같은 결손이 두 번 반복되는 것을 막고, integrated `3/5`는 네 항목의 AND를 다시
`4/5`에 요구하는 과도한 false negative를 피한다. 같은-model 다섯 회는 모집단 통계가 아니라
고정 build의 반복 일관성 probe다.

`GO` 미달이어도 `CoverageConclusionPass`, `RiskConclusionPass`, `UtilityConclusionPass`가 각각
`4/5` 이상이고, 나머지
실패 전체가 답 누출·fixture·규칙·rubric 변경 없이 한 `Interaction` 또는 `InformationStructure`
family로만 고칠 수 있으면 `REVISE`다. revision budget은 **1 round**이고 새 build·prompt hash와
새 다섯 cold session을 사용하며 이전 결과와 합산하지 않는다.

technically valid 다섯 slot을 확보한 scored round에서 두 개 이상 family, 위 세 Core conclusion 중
하나라도 두 session
이상 반복 오류, 답을 보여줘야만 통과, fixture·경제·정답 의미 변경 필요, 또는 revision 소진이면
`NO-GO`다. 그 scored round에서 `GO`와 `REVISE`가 아니면 모두 `NO-GO`로 닫는다. valid 다섯
slot을 확보하지 못한 `PROXY-RUN-BLOCKED`에는 이 catch-all을 적용하지 않는다. 자동검사·tool
preflight blocker는 공식 세션 전 고치며 revision budget을 소비하지 않는다.

## 13. Parameter 정책

- `ActiveKnob = 0`
- fixture 숫자, 비용, 시간, 좌표와 사건: `FrozenFixture`
- authored path·경계순서·계량: `Structural`
- control 간격·폰트·focus·pattern: `Presentation`
- 자동 sweep, LLM 목표 튜닝, 성공률·선택률 최적화: 금지

preflight에서 clipping이나 click target 결함을 고치는 것은 Presentation bug fix다. 공식 응답을 본
뒤 결과에 맞춰 hitbox·문구를 반복 조정하면 revision이다. fixture나 rule이 바뀌면 같은 Scope 0B
결과에 합산하지 않는다.

## 14. 즉시 중단·재검토 조건

- authored path 하나를 구현하는 데 일반 power-flow/BFS framework가 필요하다고 판단한다.
- UI를 읽히게 하려면 자유 배선·정비·복수 사건이나 경제 parameter 변경이 필요하다.
- Game이 `verificationOnly`를 읽거나 scene에 권위 숫자를 독립 복제한다.
- 공사 완료 전 공급, 동일분 순서 오류, 내부전원 이중계량이나 실패 명령 state mutation이 남는다.
- Computer Use actual operation을 정적 screenshot 문답으로 대체하려 한다.
- 한 fixture와 한 scene으로 가설을 닫을 수 없다.

이 경우 기능을 추가하지 않고 blocker·증거·제거할 가정을 기록해 사용자와 범위를 다시 정한다.

## 15. 작업단위 checkpoint와 완료 산출물

### 계약 동결 — 구현 전

- [x] 이 문서와 machine fixture가 complete·strict·링크 가능하다.
- [x] R1 handoff verifier와 JSON syntax·oracle check가 통과한다.
- [x] README·docs map·Scope 0 TODO가 같은 활성상태를 가리킨다.
- [x] initial contract commit, 독립 bounded review, scope-valid 수정, 재검사, reviewed commit을 기록한다.
- [x] [`playtests/scope-0b/CHECKPOINT_0_CONTRACT_FREEZE.md`](../../playtests/scope-0b/CHECKPOINT_0_CONTRACT_FREEZE.md)에
  `SubGateDecision = PENDING`, 실제 commit과 검사 결과를 남긴다.

### 구현·자동증거 — proxy 전

- [x] exact toolchain과 archive hash를 재확인한다.
- [x] Core·Checks·Game과 automatic/native smoke가 모두 통과한다.
- [x] 허용 범위 밖 abstraction·placeholder가 없음을 감사한다.
- [x] initial implementation commit, 독립 review, 수정·재검사, reviewed build commit을 기록한다.
- [x] [`CHECKPOINT_1_IMPLEMENTATION_FREEZE.md`](../../playtests/scope-0b/CHECKPOINT_1_IMPLEMENTATION_FREEZE.md)에
  build·fixture·prompt hash와 예상 밖 기술관찰을 남긴다.

### 조작 proxy·결과

- [ ] L00 tool preflight 뒤 동일 reviewed build로 공식 다섯 valid session을 수행한다.
- [ ] 원자료 hash·독립 strict score·aggregate·판정을 기록한다.
- [ ] 결과의 큰 단위 checkpoint에서 문서 최신성과 다음 최대 미검증 위험을 다시 점검한다.
- [ ] `GO`라면 Scope 0을 `REVIEWED`로 닫되 Scope 1을 자동 구현하지 않는다.

Scope 0B `GO`와 적응형 점검이 수동 pole·`MaxSpan` Interaction을 다음 위험으로 선택하고 현재 사용자
목표가 그 준비를 계속 승인할 때만, Scope 1 후보를 실제 evidence에 맞는 활성 계약으로 다시 쓴다.
