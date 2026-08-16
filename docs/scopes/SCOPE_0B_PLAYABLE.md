# Scope 0B — 강변 병원 회랑 구현 기준

> 상태: `COMPLETED`
>
> 기본 장면: `game/Main.tscn`
>
> 기계 권위: `data/scope-0b-v1.json`

이 문서는 현재 저장소에 남아 있는 고정 시나리오 구현을 설명한다. 과거 LLM 실행 절차와 승인
체크포인트는 [개발 이력](../DEVELOPMENT_HISTORY.md)으로 압축했다. 이 구현은 제품 전체의 범용
전력망 모델이 아니라 서비스 권역, 경로 위험, 병원 내부전원과 정산을 검증한 회귀 기준이다.

## 1. 플레이 흐름

플레이어는 이미 발전소, 마을·병원 변전소와 병원 주 회선이 있는 지역을 맡는다.

1. `DAY 0 08:00`에 마을 피더를 발주한다.
2. `DAY 1 08:00`에 두 병원 예비 계획의 사고 결과를 먼저 예측한다.
3. 강변 병렬선 또는 북부 우회선 하나를 선택해 발주한다.
4. `DAY 3 08:00` 완공 뒤 `DAY 9 16:00`의 강변 기존 회랑 사용불가 사건까지 진행한다.
5. `DAY 9 20:00` 복구 뒤 서비스, 병원 내부전원과 현금을 결산한다.

강변 병렬선은 더 싸지만 기존 선로와 같은 공간 위험 그룹을 사용한다. 북부 우회선은 더 비싸지만
그 공간 사건에서 병원 utility 경로를 유지한다. 화면은 승자나 추천 점수를 표시하지 않는다.

## 2. 데이터 권위

[`data/scope-0b-v1.json`](../../data/scope-0b-v1.json)이 다음 항목의 유일한 기계 권위다.

- 단위, 달력, 경제율과 초기 현금
- node, edge, project, load와 requirement
- 허용 공급 경로와 전기·공간 제거 case
- 사건, 병원 내부전원과 milestone
- 지도 bounds, 서비스 권역, 위험 polygon, 선로 polyline과 UI 배치 variant
- 자동검사용 topology·평가·경계·정산 oracle

현재 SHA-256은
`e617f7b9163294ca0e72f89bf3cb3a3be634c0de21f1d2736549863f53617e57`이다. 코드와 장면은 이 값을
별도 상수로 복제하지 않는다. 문서의 자연어·숫자와 fixture가 다르면 fixture가 우선한다.

loader는 root와 중첩 object의 허용 field, 중복 식별자, 참조, 단위, 범위와 구조를 엄격히 검사한다.
표시 기하의 위험대 포함관계와 비 node 교차 같은 fixture 전체 handoff는 Ruby contract 검사가 맡는다.

## 3. 핵심 규칙

### 서비스 권역과 공급

마을의 서비스 적격성은 authored `serviceSubstationId` 관계다. 화면의 ellipse는 그 관계를 표현할 뿐
runtime point-in-shape 계산으로 자격을 새로 만들지 않는다. 적격이어도 온라인 발전원까지 허용된
commissioned 경로가 없으면 공급되지 않는다.

### 경로

이 시나리오는 fixture의 `permittedSupplyPaths`만 사용한다. 범용 BFS, 최단경로, reverse feed와
임의 대체경로는 없다. 주 경로가 사용 가능하면 주 경로를 쓰고, 그렇지 않으면 선택해 완공한 회랑의
예비 경로 하나만 본다. 경로의 모든 edge가 commissioned·available이어야 하고 모든 정격이 활성
부하를 감당해야 한다.

전기 사고는 `electricalContingencyId`, 공간 사건은 `spatialRiskGroup`으로 edge를 고른다. 실제
사건에서 제거되는 edge는 selector와 일치하는 **commissioned edge**의 교집합이다. 발주하지 않은
회랑을 사건 제거 상태로 표시하지 않는다.

### 공사와 시간

project는 `not_ordered → building → commissioned`만 거친다. 발주는 허용 시각에만 가능하고 현금과
완료시각을 원자적으로 반영한다. building edge는 공급에 참여하지 않는다. `AdvanceToNextMilestone`
은 다음 public 경계까지 진행하며, 내부전원 고갈처럼 중간에 필요한 경계는 transient trace로
반환한다. 최종 복구 뒤 `IsComplete = true`이고 추가 진행은 거부된다.

### 병원 내부전원

병원 utility가 끊기면 병원 소유 UPS, 그다음 디젤이 P0 수요를 맡는다. 이 에너지는 전력회사의
utility delivery, 판매 또는 gas injection이 아니다. 내부전원이 P0를 지켜도 utility 미공급과
보상은 별도로 계산한다. 사용하지 않은 내부전원은 소모되지 않는다.

### 정산

정수 `kW·minute`와 `CashUnit`만 사용한다. 각 진행 명령의 interval과 시작 이후 cumulative 정산은
다음을 분리한다.

- load별 utility delivered·unserved energy
- gas injection과 병원 내부전원 사용량
- 병원 P0 미공급
- 매출, 가스비, 미공급 보상과 LostSales

LostSales는 진단값이고 현금에 다시 차감하지 않는다. 내부 고갈 경계를 포함한 한 public advance의
interval은 그 명령 전체 구간을 나타낸다.

### 반사실 평가

`EvaluateRemoval`은 실제 시각·현금·내부전원을 바꾸지 않는 순수 query다. 지정한 설계가 완공된
고정 기준 topology에 한 case를 적용하고 각 load의 utility 경로를 반환한다. 실제 chronology의
building 상태나 선택하지 않은 설계에 의존하지 않는다.

## 4. Core 경계

주요 public API는 다음과 같다.

```text
FixtureLoader.Load(...)
GridworksSession.GetSnapshot()
GridworksSession.OrderTownFeeder()
GridworksSession.OrderCorridor(design)
GridworksSession.AdvanceToNextMilestone()
GridworksSession.EvaluateRemoval(design, caseId)
SnapshotJson.Serialize(...)
SnapshotJson.Sha256Hex(...)
```

명령은 `CommandResult`와 새 `PublicSnapshot`을 반환한다. 거부 code는 잘못된 시각, 선행 행동 미완료,
이미 발주됨과 다음 milestone 없음이다. 거부된 명령은 상태·현금·trace를 바꾸지 않는다. snapshot
JSON은 고정 field 순서와 결정론적 collection 순서를 가진다.

`LoadedFixture`에는 scenario, presentation과 검사 oracle이 함께 있으나, `GridworksSession`과 Game은
scenario만 사용한다. oracle은 자동검사 밖으로 전달하지 않는다.

## 5. Game 경계

`game/Main.cs`가 UI 흐름, 명령 adapter와 상태 표현을 맡고 `GridMapView.cs`와 `TimelineView.cs`는
반환 상태와 presentation data만 그린다. Game은 경로, 위험, 내부전원, 현금과 정답을 다시 계산하지
않는다.

화면은 다섯 단계다.

1. 마을 피더 발주
2. 네 개의 사고 결과 예측과 회랑 선택
3. 반사실 검증 결과 공개
4. 완공과 사건 진행
5. 복구·결산

예측은 Game의 임시 UI 상태이며 Core scenario state가 아니다. 네 예측을 모두 고르기 전에는 회랑
선택이 비활성이고, 회랑을 고른 뒤에만 확정할 수 있다. 표준 Godot button과 고유 접근성 이름을
사용한다. 지도는 서비스 권역과 공간 위험대를 구분하며, 선로 교차가 전기 접속을 뜻하지 않는다.

진단 JSONL은 `READY`, accepted command, 예측 잠금, 공개와 `FINAL`의 최소 실행 증거를 남긴다. 이는
제품 save나 replay가 아니다.

## 6. 현재 검사

```sh
ruby playtests/scope-0b/verify_contract.rb
dotnet run --project tools/Gridworks.Checks/Gridworks.Checks.csproj -c Release
dotnet build game/Gridworks.Game.csproj -c Debug -t:Rebuild
```

Ruby 검사는 fixture root, topology, authored path, presentation geometry, 사건, 에너지·현금과 경계
oracle을 독립 계산한다. C# 검사는 strict loader, 두 회랑 전체 경계, 순수 query, public 거부 상태,
보존식, 결정론과 reverse-feed 금지를 검사한다.

Godot 회귀는 기본 `Main.tscn`을 AB와 BA layout으로 각각 fresh diagnostic path에서 headless smoke한다.
두 variant는 선택지 표시 순서만 바꾸며 규칙과 결과는 같다. native 화면·접근성 검토는 과거 완료
증거이고 매 빌드의 자동검사를 대신하지 않는다.

## 7. 명시적 한계

이 구현에는 자유 선로 배치, 발전소·변전소 건설, 철거, 일반 graph 탐색, 공장, 폭염, 정비, 저장,
캠페인과 최종 아트가 없다. 경로·부하·사건 수를 늘려 범용 게임으로 쓰지 않는다.

과거 동일 계열 LLM 다섯 세션은 이 화면을 완료했지만 사람 검증은 수집하지 않았다. 결과는
사람 사용성·재미·밸런스·접근성 또는 전략 다양성을 지지하지 않는다. 자세한 결정과 증거 식별값은
[개발 이력](../DEVELOPMENT_HISTORY.md)에 있다.
