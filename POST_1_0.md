# Gridworks — 1.0 이후 제품 방향

> 상태: 장기 후보, 현재 구현 금지

이 문서는 전력망 건설·신뢰도 루프가 사람 플레이에서 검증된 뒤 검토할 차별화 방향을 정의한다.
확정 roadmap이 아니며, 관련 gate를 열기 전에 interface, schema, 명령, 저장 필드와 빈 UI를
미리 만들지 않는다.

## 1. 차별화 가설

후속 제품의 우선 후보는 발전기 종류를 넓게 수집하는 게임이 아니라 **원전, 외부 데이터센터와
하나의 공유 냉각수 권역**이다.

원전은 큰 전력을 낮은 변동비로 만들지만 건설비·공기·송출망과 냉각수가 필요하다. 데이터센터는
큰 전력 판매를 제공하지만 높은 신뢰도와 냉각을 요구한다. 폭염·가뭄 때 물을 아끼려고 공랭으로
바꾸면 전력 피크가 커지고, 전력을 아끼려고 수랭을 쓰면 원전과 같은 냉각수 여유를 사용한다.

이 확장이 답할 제품 질문은 다음이다.

> 제한된 물과 전력 여유가 서로 반대 방향으로 압박할 때, 플레이어가 병원과 계약 부하를
> 지키기 위해 입지·접속·운전 상태를 미리 설계하는가?

물 관리나 데이터센터 경영 자체가 목적은 아니다. 기존의 공간 건설과 신뢰도 계획을 더 날카롭게
만들지 못하면 이 방향을 폐기한다.

## 2. 세 단계 prototype

운영 교환관계, 입지·접속과 경제·공사는 서로 다른 가설이다. 한 번에 만들지 않는다.

| 단계 | 검증할 것 | 새로 여는 것 | 제외하는 것 |
|---|---|---|---|
| `P-A 운영` | 물·전력 인과가 읽히는가 | 이산 운전상태, 상태별 총량, 물 용량, 자원부족 정지 | 입지·거리·송전건설·NIMBY·돈 |
| `P-B 입지` | 입지가 전력망 결정을 깊게 하는가 | 후보 geometry, 거리 hard cut, 선형 펌프, 송전 옵션, 비교용 입지비 | 공사 queue·현금·계약정산 |
| `P-C 경제` | 경제가 물·전력 선택을 강화하는가 | 실제 건설비·공기·현금, 판매·변동비·위약 | 금융·주민 정치·연료시장 |

각 단계의 통과는 적응형 점검을 열 뿐 다음 단계 구현을 자동 승인하지 않는다. 아래 규칙은
표시된 단계부터만 적용한다.

## 3. 완성 방향의 두 프로젝트

### 원전 프로젝트

완성 방향에서 플레이어는 유효 후보지에 원전을 발주하고 송출망을 연결한다. 원전은 매우 높은
출력과 낮은 변동비를 제공하지만 건설비가 크고 오래 걸리며, 수원지 hard cut 안에서만 건설할
수 있다. 냉각수가 부족하면 출력을 낮춰야 한다.

단계별 플레이어 결정은 다음으로 제한한다.

- `P-A`: 출력상태 `FULL / REDUCED`
- `P-B`: 원전 후보지와 기존 계통까지 송출 회랑
- `P-C`: 실제 발주, 공사와 현금 지출

원전은 수랭 전용이다. 원자로 내부, 연료주기, 폐기물, 상세 안전계통, 열역학과 다단계 인허가를
조작하지 않는다. `REDUCED`는 authored 비율 하나이며 연속 출력 slider가 아니다. 첫 prototype에는
`OFF`, 재기동시간과 최소운전시간도 없다.

### 데이터센터 프로젝트

데이터센터는 전력회사가 서버 사업을 소유하는 건물이 아니라 외부 사업자의 앵커 수요 계약이다.
플레이어는 서버·AI workload를 경영하지 않고 다음만 결정한다.

- `P-A`: 서비스상태 `FULL / REDUCED / OFF`
- `P-A`: 냉각상태 `WATER / AIR`
- `P-B`: 후보지와 계통 접속
- `P-C`: 계약경제와 공사

`REDUCED` 비율은 계약의 authored 값 하나다. P-A UI는 서비스·냉각상태, 최종 계통수요 MW와
냉각수 사용량만 표시한다. P-B에서 거리·펌프전력과 `AIR ONLY`, P-C에서 판매와 위약을 추가한다.

## 4. 냉각상태와 전환

### `WATER`

- 공랭보다 데이터센터 계통수요가 낮다.
- 하나의 `CoolingWaterZone` 용량을 사용한다.
- P-B부터 수원지까지의 거리와 펌프 보조전력을 반영한다.
- 수원지 hard cut 밖에서는 선택할 수 없다.

### `AIR`

- 냉각수 사용량은 0이다.
- 평시에도 수랭보다 계통수요가 높다.
- 폭염에는 authored 공랭 추가수요가 붙는다.
- 수원지 거리와 냉각수 부족의 직접 영향을 받지 않는다.

수원지 hard cut 안에 있는 데이터센터는 건설 때 냉각방식이 영구 고정되지 않는다. 운영 중
`WATER → AIR`, `AIR → WATER`를 모두 전환할 수 있다. 다만 임의의 매분 threshold를 맞추는
미세조작을 막기 위해 다음 경계에서만 변경한다.

- scenario가 정한 `OperatingDecisionMarker`
- 다음 틱의 냉각수 요구가 가용량을 넘어 자동 정지한 경계

허용 경계의 전환은 다음 자원검증 전에 적용되며 첫 prototype에는 전환비용·전환시간·중간상태가
없다. 실제 플레이에서 너무 관대하거나 교착을 만든다는 증거가 생기면 비용 또는 시간 중 하나만
새 가설로 연다.

원전 출력상태와 데이터센터 서비스상태도 같은 경계에서만 바꾼다.

## 5. 하나의 냉각수 권역

지도에는 강이나 호수를 추상화한 `CoolingWaterZone` 하나만 둔다. 이는 배관망이 아니라 원전과
수랭 데이터센터가 공유하는 단일 용량 제약이다. `CW/h`는 게임용 단위이며 현실의 취수·순소비·
방류를 예측한다고 주장하지 않는다.

### P-A의 총량 모델

P-A는 위치와 거리를 계산하지 않고 세 authored 표만 읽는다.

- `AvailableCoolingWater[CwPerHour]`
- `NuclearOperationTable[OutputState]`
- `DataCenterOperationTable[ServiceState, CoolingMode, HeatwaveState]`

```text
TotalCoolingWater = NuclearWater + DataCenterWater
CoolingWaterMargin = AvailableCoolingWater - TotalCoolingWater
```

### P-B의 거리 모델

P-B에서만 다음 geometry 입력을 연다.

- 수원지 geometry
- `MaxWaterCoolingDistanceKm`
- 공통 `WaterCoolingPumpMwPerKm`

프로젝트 냉각 접속점에서 수원지 geometry의 가장 가까운 점까지 수평 유클리드 거리 `d`를 쓴다.
도로, 경사, 관경, 고저차와 실제 배관경로는 계산하지 않는다.

```text
WaterCoolingAllowed = d <= MaxWaterCoolingDistanceKm
PumpAuxMW = BasePumpMW + d × WaterCoolingPumpMwPerKm
```

거리효과는 물이 사라지는 비물리적 손실이 아니라 계통이 부담하는 펌프 보조전력이다. hard cut과
선형계수는 각각 하나이며 프로젝트별 override를 두지 않는다.

## 6. 상태 계산

P-A는 아래 두 상태표를 원자적으로 조회한다. 한 셀씩 숨은 knob로 취급하지 않는다.

```text
NuclearOperationTable[OutputState]
  → GrossMW, PumpMW, NetInjectionMW, Water

DataCenterOperationTable[ServiceState, CoolingMode, HeatwaveState]
  → ITMW, AuxMW, GridDemandMW, Water
```

P-B부터 거리 성분을 다음처럼 파생한다.

```text
NuclearOutputFraction = FULL ? 1.0 : NuclearReducedFraction
NuclearGrossMW = NuclearNameplateMW × NuclearOutputFraction
NuclearWater = NuclearWaterAtNameplate × NuclearOutputFraction
NuclearPumpMW = NuclearOutputFraction
              × (BaseNuclearPumpMW + NuclearDistanceKm × WaterCoolingPumpMwPerKm)
NuclearNetInjectionMW = NuclearGrossMW - NuclearPumpMW

DataCenterServiceFraction = FULL ? 1.0
                          : REDUCED ? DataCenterReducedFraction
                          : 0.0
DataCenterITMW = ContractITMW × DataCenterServiceFraction

WATER:
  DataCenterWater = WaterAtFullService × DataCenterServiceFraction
  DataCenterAuxMW = DataCenterServiceFraction
                  × (BaseWaterCoolingAuxMW
                     + DataCenterDistanceKm × WaterCoolingPumpMwPerKm)

AIR:
  DataCenterWater = 0
  DataCenterAuxMW = DataCenterServiceFraction
                  × (BaseAirCoolingAuxMW + HeatwaveAirAdderMW)

DataCenterGridDemandMW = DataCenterITMW + DataCenterAuxMW
```

idle 전력과 idle 물은 첫 prototype에서 0이다. 여러 데이터센터 유형, 비선형 효율곡선, 연속
service/output slider와 개별 펌프는 없다.

## 7. 자원 부족 처리

다음 틱의 냉각수 요구가 가용량을 넘으면 그 틱을 확정하지 않고 자동 정지한다. UI는 초과량과
세 조치의 전력·물·서비스 결과를 나란히 보여준다.

1. 원전을 `FULL → REDUCED`로 낮춘다.
2. 수랭 가능한 데이터센터를 `WATER → AIR`로 바꾼다.
3. 데이터센터 서비스를 `FULL → REDUCED → OFF`로 낮춘다.

플레이어가 정지 경계에서 명령을 적용하고 총 요구가 가용량 이하가 되어야 다음 틱으로 진행한다.
엔진은 최적해나 일부 냉각수 배분을 자동 선택하지 않는다. P-A/B는 병원 P0, 계통 한계,
전력·물과 서비스만 계산하고 P-C에서만 현금 결과를 추가한다.

fixture validator는 모든 부족 경계에 합법적인 해소 조합이 하나 이상 있는지 검사한다. 없으면
플레이어 실패가 아니라 저작 오류다.

## 8. 원전 입지 부담

주거지 인접 대형 열발전의 NIMBY 효과는 정치 simulation이 아니라 비용으로만 표현한다.

| 등급 | 의미 | P-B | P-C |
|---|---|---|---|
| `LOW` | 주요 주거지에서 충분히 떨어짐 | 기준 비교비 | 기준 건설비 |
| `HIGH` | 주거지 인접 | 고정 `SitingCostDelta` | 실제 추가 건설비 |

등급은 authored 후보지와 인구권의 공간 관계로 정한다. 런타임 주민, 시위, 여론·신뢰, 연속
거리곡선과 허가기간은 없다. P-B의 delta는 현금을 차감하거나 지급능력을 판정하지 않는다.
P-C에서만 실제 CashUnit에 편입한다. 데이터센터에는 NIMBY를 적용하지 않는다.

향후 석탄·LNG를 별도 발전 포트폴리오 gate에서 열 경우에도 같은 `LOW/HIGH` 비용 규칙부터
재사용한다.

## 9. 단계별 검증

### P-A — 운영 교환관계

이미 건설·접속된 원전 하나, 데이터센터 하나와 수원지 하나를 사용한다. 고정 폭염·가뭄에서
세 이산 상태명령만 조작한다. 입지, 거리, 송전건설, NIMBY, 판매·위약과 현금은 없다.

검증 질문은 하나다.

> 플레이어가 수랭↔공랭, 원전 감발과 서비스 제한의 물·전력 인과를 사고 전에 예측하고
> 결과를 설명하는가?

코드 전에 `2 원전상태 × 3 서비스상태 × 2 냉각상태 = 12`개 조합을 정상·사건 snapshot에
대입한 표를 손검산한다. 물 초과, 계통 초과와 병원 P0 실패를 별도 열로 둔다. 이 12행으로
충분하면 범용 simulator를 만들지 않는다.

### P-B — 입지·접속

P-A가 신규 참가자 검증을 통과한 뒤에만 authored 후보지, 송출 회랑, hard cut, 선형 펌프전력과
원전 `LOW/HIGH` 비교 delta를 연다. 현금, queue, 허가기간, 계약경제는 없다.

입지와 접속이 물·전력 선택을 실제로 더 깊게 만들지 못하거나 한 후보가 모든 면에서 지배하면
P-B를 폐기한다.

### P-C — 경제·공사

P-B가 통과한 뒤에만 실제 건설비·공기·현금, 접속 분담금, 전력 판매, 변동비와 위약을 연다.
프로젝트 금융, 주민 정치, 연료·탄소시장은 없다. 경제 정보가 물·전력 인과를 가리면 수치를
더 추가하지 않고 P-B로 돌아간다.

## 10. 파라미터 예산

현재 모든 밸런스 숫자는 `TBD`다. 각 단계는 실제로 쓰는 값만 정의한다.

- 한 단계의 `FrozenFixture + ActiveKnob`는 최대 6개 family다.
- active knob는 최대 3개이며 첫 사람 round 전에는 0개다.
- 표를 한 family로 묶으면 scalar와 행 수를 기록한다.
- 셀 하나를 독립 변경하면 새 family다.
- 여섯 family로 가설을 닫지 못하면 단계를 더 작게 나눈다.
- 앞 단계의 결과는 승인된 option/envelope 표 하나로 동결한다.
- 그 표의 셀을 다시 조정하려면 앞 gate를 재개방한다.

첫 family inventory는 다음과 같다.

| 단계 | 독립 family 6개 이하 |
|---|---|
| P-A | `NuclearOperationTable`, `DataCenterOperationTable`, `CoolingWaterAvailabilityTimeline`, `NonProjectDemandTimeline`, `GridTransferLimitTimeline`, `OperatingDecisionTimeline` |
| P-B | `ValidatedOperatingEnvelope`, `CandidateSiteGeometry`, `MaxWaterCoolingDistance`, `WaterCoolingPumpMwPerKm`, `TransmissionOptionTable`, `NuclearSitingCostDeltaTable` |
| P-C | `ValidatedProjectOptionTable`, `ConstructionCostTable`, `ConstructionDurationTable`, `ContractSettlementTable`, `GenerationVariableCostTable`, `DemandAndEventTimeline` |

인과는 이해되지만 세 개 이상의 유효 전략 비교가 손검산을 막을 때만
[Static Balance Lab](BALANCING_STATIC_SIM.md)을 별도 승인한다.

## 11. 별도 후보로 보류한 것

### 발전원 포트폴리오

태양광·풍력, 원전, LNG와 석탄의 동시 포트폴리오는 이 확장과 함께 만들지 않는다. 별도 가설이
생기면 한 번에 기술 둘만 비교한다.

- 태양광·풍력: 높은 초기비, 낮은 변동비, authored 날씨 출력
- 원전: 매우 높은 초기비·긴 공기, 높은 출력, 낮은 변동비
- LNG: 높은 변동비, 빠른 기동과 조절
- 석탄: 높은 변동비, LNG보다 느린 기저 역할 후보
- 대형 열발전: 주거지 인접 `HIGH` 입지 추가비

연료시장, 탄소가격, 보조금과 주민 정치를 함께 넣지 않는다. 발전원 차이가 송전망 입지·여유
선택을 더 깊게 만든다는 사람 증거가 없으면 열지 않는다.

### 프로젝트 금융

채권, 대출, 공동투자, 신용, 이자·원금, 단계지급과 연환산 자본비용은 P-C에도 없다. 대형
프로젝트 자금조달 자체가 의미 있는 선택을 만든다는 별도 증거가 생길 때 독립 prototype으로
검토한다.

## 12. 명시적 비범위

- 상수도·하수도·냉각수 배관망
- 여러 수원, 수질, 지하수·재이용수와 물시장
- 취수·순소비·열방류·수문학과 물 가격
- 펌프 개체, 관경, 고저차와 냉각설비 내부 구성
- 서버·랙·AI workload와 cloud 사업 경영
- 여러 데이터센터 유형과 냉각 기술 tree
- 원자로 내부·연료주기·상세 안전계통 조작
- 주민 신뢰·시위·선거와 개별 인구 정치
- 폐열 지역난방
- 여러 발전원의 동시 포트폴리오

## 13. 개방·중단 조건

각 단계는 다음 질문을 해당 범위에서만 검증한다.

1. P-A에서 물·전력 인과를 사고 전에 예측하고 설명하는가?
2. 세 이산 상태명령으로 유효 대응이 둘 이상 생기며 미세조작이 없는가?
3. P-B에서 입지가 송전망 결정을 실제로 더 깊게 만드는가?
4. 단일 물 용량, 선형 펌프와 hard cut만으로 유효 설계가 둘 이상 생기는가?
5. `LOW/HIGH` 비교 delta가 정치 simulation 없이 이해되는가?
6. P-C의 경제가 물 UI와 전력 병목을 가리지 않는가?
7. 평가기간 안에 원전·데이터센터가 지배적 무선택이나 필수 함정이 되지 않는가?

어느 단계든 핵심 질문에 부정적이면 숫자나 하위 시스템을 더하지 않고 중단하거나 직전 단계로
돌아간다.

## 14. 완성 방향의 한 장면

폭염과 가뭄이 동시에 온다. 마을 냉방 수요는 늘고, 냉각수는 줄며, 공랭 데이터센터의 전력
수요는 오른다. 플레이어는 원전을 감발해 물을 아낄지, 데이터센터를 공랭으로 바꿔 전력 피크를
감수할지, 서비스 수준을 낮춰 계약 결과를 받아들일지 선택한다. 그 결과는 병원 P0, 계통 MW,
냉각수, 데이터센터 서비스와 현금으로 분리되어 보고된다.

이 장면이 기존 전력망 설계를 더 중요하게 만들 때만 후속 방향은 성공이다.
