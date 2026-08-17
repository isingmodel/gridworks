# 공장 수요와 발전소 용량 — 완료된 구현 기준

> 상태: `COMPLETED`
>
> 사람 검증: `NOT_COLLECTED`

이 완료는 다음 예고된 폭염·정비 단계의 구현을 승인하지 않는다.

이 단계는 완료된 병원 사건 결산 뒤에 공장 수요와 가스발전소 직접 건설만 추가한다. 별도 장면,
범용 발전소 시스템, 타임라인, 폭염, 정비, 저장과 최종 아트는 열지 않는다.

## 1. 플레이어 결과

기본 실행은 이전 흐름을 그대로 지나 다음까지 이어진다.

```text
병원 사건 복구·결산 성공
→ 이미 발효된 공장 증설과 기존 발전용량 부족 확인
→ 두 허용 부지 중 가스발전소 위치 선택·발주·완공
→ 발전소에서 기존 계통 접속점까지 지지물·접속선 건설
→ 고정 merit order 급전과 공장 공급 확인
→ 한 공급기간 결산
```

가까운 부지는 부지비가 높지만 접속선이 짧고, 먼 부지는 부지비가 낮지만 접속선이 길다. 화면은
두 부지를 같은 중립 표현으로 보여주며 추천·총점·자동 경로를 제공하지 않는다.

## 2. 단일 데이터 권위

[`data/product-factory-v1.json`](../../data/product-factory-v1.json)이 현재 제품 실행값의 유일한
권위다. 완료된 병원 fixture를 누적하고 다음 항목을 더한다. 전체 흐름을 한 번에 끝낼 수 있도록
초기 현금도 `18 M`으로 올린다. 이는 이 누적 임무의 동결된 시작값이며, 완료된 이전 fixture의
기록이나 규칙을 바꾸지 않는다.

- authored 공장 위치·수요·우선순위와 기존 고정 feeder 정격
- 가스발전소 한 종류의 정격·기본비·공기·변동비
- 정확히 두 개의 허용 부지 위치와 고정 부지비
- 가스발전소 접속 `LineProject`의 정격·거리·비용·공기
- 마지막 공급기간 길이

추천 support, 정답 부지, reference 기말현금은 fixture에 넣지 않는다. Loader는 exact field와 type,
식별자·terminal 참조, 두 부지의 고유한 빈 cell, 양의 정격·비용·시간, checked 산술을 검사한다.
두 발전소 부지 cell은 누적 임무 시작부터 line support가 점유할 수 없는 예약 부지로 취급하지만,
발전소 초안은 해당 부지에 놓을 수 있다. 별도 예약 schema나 범용 토지 시스템은 만들지 않는다.

## 3. 상태와 명령

병원 결과가 성공한 누적 fixture만 다음 상태로 이어진다.

```text
PlantPlanning → PlantBuilding
→ PlantConnectionPlanning → PlantConnectionBuilding
→ FactorySettlementReady → Complete
```

`PlantPlanning`에서 두 authored site anchor 중 하나만 초안으로 선택·이동·취소할 수 있다. 발전소
발주는 기본비와 해당 부지비를 함께 차감하고 완공시각과 `Building` 상태를 원자적으로 고정한다.
완공 전 출력은 0이다.

발전소 완공 뒤 기존 line support preview·추가·되돌리기·초안 취소·발주 명령을 접속선에 재사용한다.
접속선은 선택한 발전소 terminal에서 기존 계통 접속점까지 이어지며, support 공유와 교차 접속은
없다. 완공 전에는 발전소가 계통에 편입되지 않는다. 모든 preview와 거부는 상태를 바꾸지 않는다.
`RestartMission`은 어느 상태에서든 전체 최초 상태로 돌아간다.

## 4. 공급과 급전

공장 증설은 시작부터 발효돼 있다. 병원, 마을, 공장을 `(priority, stable ID)` 순서로 처리한다.
각 부하는 한 공급원과 한 고정 경로로 전량 공급되거나 0을 받으며 여러 발전원에 분할하지 않는다.

공급원은 기존 발전원, 새 가스발전소 순서의 고정 merit order다. 각 부하마다 순서대로 남은 정격과
사용 가능한 경로가 수요 전량을 감당하는 첫 공급원을 선택한다. 선택된 공급원·회선 용량은 다음
부하를 위해 예약한다. 새 발전소는 `Commissioned`이고 접속선도 `Commissioned`일 때만 후보가 된다.
공장 feeder는 authored 완공 설비이며 정격만 검사한다. 최적급전, 부분공급, 수동출력과 기동제약은
없다.

## 5. 경제와 완료

마지막 고정 공급기간에는 실제로 인도한 load energy만 매출로 계산한다. 공급원별 실제 발전 energy에
각 변동비를 적용하고, utility 미공급은 기존 보상률과 LostSales 진단값을 사용한다. LostSales는
현금에서 다시 빼지 않는다. 모든 rate 계산은 `60_000_000 kWMinute/GWh` 정수 연산이다.

최종 성공은 이전 병원 hard condition을 모두 지킨 상태에서 마지막 기간의 병원·마을·공장을 모두
전량 공급하는 것이다. 선택 부지, 완공시각, 공급원별 발전량과 기말 현금은 병렬 결과이며 점수가
아니다.

## 6. 화면과 진단

기존 `ProductMain` 하나를 연장한다. 지도에 공장, 고정 feeder, 두 중립 부지, 발전소와 접속선을
추가한다. 발전소는 초안 점선, 공사 사선, 완공·미접속 중립 실선, 계통접속 뒤 청백 실선과 출력
문장으로 구분한다. 해결된 병원 위험 사각형은 이 단계에서 흐리게 표시한다.

기존 지도 click·keyboard cursor와 Cancel·Undo·Order·Advance·Settle·Restart button을 재사용한다.
Game은 부지, 비용, 급전, 공급과 정산을 계산하지 않는다. 진단은 기존 `READY / COMMAND / FINAL`에
선택 site ID, 발전소 online 시각, 공장 인도, 공급원별 급전과 결산만 더하고 support 좌표나 정답을
기록하지 않는다.

## 7. 느슨한 기술 완료조건

- 새 fixture shape·참조·대표 산술 검사
- 두 부지와 충돌, 미완공·미접속 출력 0, 원자 접속, 고정 merit order, 전량공급·용량 경계와 현금
  보존의 작은 Core 사례
- 두 부지가 모두 가능하고 reference 흐름에서 가까운 부지는 빠르지만 비싸며 먼 부지는 느리지만
  싼지 확인
- 누적 ProductChecks와 Game build
- 실제 viewport 입력과 표준 button으로 시작부터 가까운 부지 공장 결산까지 가는 native smoke 한 번
- 미해결 critical·core-flow major 0과 짧은 독립 검토 한 번

두 번째 해상도, 전체 접근성, LLM·사람 플레이, 밸런스 조정과 다음 단계 placeholder는 만들지 않는다.
`HumanValidationStatus = NOT_COLLECTED`를 유지한다.

## 8. 현재 검사와 종료 기록

현재 제품 검사는 다음 한 명령으로 첫 점등·병원·공장 규칙을 함께 확인한다.

```sh
dotnet run --project tools/Gridworks.ProductChecks/Gridworks.ProductChecks.csproj -c Release -- data/product-factory-v1.json
```

- 첫 점등 회귀: 10 suites / 664 assertions 통과
- 두 번째 심장 회귀: 5 suites / 124 assertions 통과
- 공장 용량 확장: 5 suites / 378 assertions 통과
- Scope 0B·Scope 1 회귀와 Core·Game Debug·Release build: warning 0, error 0
- 실제 viewport 입력과 표준 button을 사용한 1280×720 대표 흐름: `Success`, 기말 현금 `5.820 M`,
  종료 시각 `1425분`
- 발전소 완공·미접속 상태에서 출력 `0`, 접속선 원자 완공 뒤 공장 `2 MW` 공급 확인
- 진단: `READY → COMMAND × 30 → FINAL`, 선택 부지·온라인 시각·공급원별 급전·현금 ledger 기록,
  support 좌표 미기록
- fixture SHA-256: `5c706a17ab45d33118f70b8f0bac6f4fd1bb59a33f13c07bb21bde8c5eae5d14`
- 대표 실행 build hash: `3d89edf59dd4892a64b2b7d369c458ec1ac1a3d72ef035413aca3255182ee252`
- 짧은 독립 Core·Game 검토: P0 0, P1 0

대표 smoke는 가까운 부지 하나와 접속 support 하나를 `--smoke-*` 인자로 주입한다. 먼 부지의
비우월 관계와 실패 경계는 Core 검사만 소유한다. 사람·LLM 플레이는 수행하지 않았으며, 이 단계의
완료 자체는 당시 후속 단계를 자동으로 승인하지 않았다.
