# 예고된 폭염과 예방정비 — 완료된 구현 기준

> 상태: `COMPLETED`
>
> 사람 검증: `NOT_COLLECTED`

이 완료는 다음 캠페인 골격·저장·기본 설정 단계의 구현을 승인하지 않는다.

이 단계는 완료된 공장 결산 뒤에 읽기 전용 예고와 예방정비 선택, 고정 폭염 사건만 추가한다.
별도 장면, 연속 시계, 작업 일정표, 확률 고장, 위기 중 수요감축, 상세 수리와 저장은 열지 않는다.

## 1. 플레이어 결과

기본 실행은 이전 흐름을 그대로 지나 다음까지 이어진다.

```text
공장 공급·결산 성공
→ 폭염 시작과 복구 시각, 수요·정격 변화를 미리 확인
→ 기존 공장 노후 feeder를 예방정비하거나 정비 없이 진행
→ 폭염 시작: 마을 냉방 수요 증가·노후 feeder 유효정격 감소
→ 정비하지 않았다면 노후 feeder 사용불가
→ 위기 평가 경계까지 진행
→ 회선 복구와 공급·현금 결산 확인
```

정비는 공장 공급을 지키지만 비용이 든다. 정비를 건너뛰면 현금을 아끼지만 폭염 동안 공장이
끊긴다. 병원과 마을은 두 선택 모두 공급되므로, 화면은 서비스와 현금 결과를 나란히 보여주고
점수나 추천을 만들지 않는다.

## 2. 단일 데이터 권위

[`data/product-heatwave-v1.json`](../../data/product-heatwave-v1.json)이 현재 제품 실행값의 유일한
권위다. 완료된 공장 fixture를 누적하고 다음 항목만 더한다.

- 폭염 예고시간과 고정 지속시간
- 폭염 중 마을 수요와 이름 붙은 기존 공장 feeder의 유효정격
- 기존 공장 feeder의 stable asset ID
- 예방정비 대상, 비용과 공기

병원·공장 수요, 발전비·판매·보상률과 기존 설비는 누적 fixture의 값을 그대로 사용한다. 정답
선택, reference 기말현금과 추천 문구는 fixture에 넣지 않는다. Loader는 exact field와 type, 기존
식별자 일치, 양의 시간·정격·비용, `heatwave.townDemandKw > town.demandKw`,
`0 < agedFactoryFeederHeatwaveRatingKw < factory.feederRatingKw`, 정비 공기가 예고시간 안에
끝나는지와 checked 산술을 검사한다.

## 3. 상태와 명령

공장 결산이 성공한 누적 fixture만 다음 상태로 이어진다.

```text
MaintenanceDecision
├─ 정비 발주 → MaintenanceBuilding → HeatwaveReady
└─ 정비 생략 ───────────────────────→ HeatwaveReady
HeatwaveReady → HeatwaveActive → Complete
```

`MaintenanceDecision` 진입 시 폭염 시작시각을 현재 시각과 예고시간으로 한 번 고정한다.
`OrderPreventiveMaintenance`는 비용을 한 번 차감하고 완공시각을 고정한다. 기존
`AdvanceToConstructionCompletion`이 정비 완공까지 진행하며, 정비 공기 동안 다른 공사는 없다.
`SkipPreventiveMaintenance`는 비용과 시간을 쓰지 않고 선택을 고정한다.

`AdvanceToHeatwave`는 고정 시작시각으로 이동해 폭염 수요·정격을 적용한다. 정비를 생략했다면
노후 회선 전체가 사용불가가 된다. `AdvanceToHeatwaveSettlement`은 고정 지속시간 뒤로 이동해
사건 기간을 한 번 결산하고 회선을 복구한다. preview·거부는 상태를 바꾸지 않으며
`RestartMission`은 어느 상태에서든 전체 최초 상태로 돌아간다.

## 4. 공급과 사건

폭염 동안 기존 고정 급전 순서와 전량공급 규칙을 그대로 사용한다. 병원, 마을, 공장을
`(priority, stable ID)` 순서로 처리하고, 기존 발전원 다음 새 가스발전소를 본다.

- 병원과 공장의 수요는 이전 단계와 같다.
- 마을 수요는 authored 폭염값으로 증가한다.
- 기존 공장 feeder의 유효정격은 authored 폭염값으로 감소한다.
- 정비하지 않은 노후 feeder는 사건 동안 공장 공급 경로에서 제외한다.
- 정비를 완료한 feeder는 감소한 유효정격 안에서 사용 가능 상태를 유지한다.

사용불가 판정은 이름 붙은 회선 하나에만 적용한다. 다른 회선, 위험 사각형, 발전원과 내부전원을
함께 제거하지 않는다. 확률, 부분공급, 최적급전과 연속 열 상태는 없다.

## 5. 경제와 완료

폭염 지속시간 동안 실제 인도한 load energy만 매출로 계산한다. 공급원별 실제 발전 energy에 기존
변동비를 적용하고, utility 미공급에는 기존 보상률과 LostSales 진단값을 사용한다. LostSales는
현금에서 다시 빼지 않는다. 정비비는 발주 시 한 번만 차감한다.

최종 성공은 이전 병원 hard condition을 지키고 폭염 동안 병원·마을·공장을 모두 전량 공급하는
것이다. 정비를 생략해 공장이 끊기면 사건 후 feeder가 복구돼도 결과는 실패다. 선택, 공급원별 발전,
미공급, 사건 현금변화와 기말현금은 설명용 결과이며 가중 점수가 아니다.

## 6. 화면과 진단

기존 `ProductMain` 하나를 연장한다. 별도 타임라인 화면 대신 기존 작업 패널에 `현재 / 폭염 시작 /
복구·결산` 세 이정표와 예상 수요·정격 변화를 읽기 전용으로 표시한다. 기존 `Order` button은 정비
발주, `Advance` button은 정비 완공, `Settle` button은 정비 생략·폭염 시작·복구 결산에 상태별로
재사용한다. `Restart`는 항상 보인다.

지도는 노후 회선의 정상, 정비 중, 정비 완료, 사건 사용불가를 색 외 점선·반복 사선·상태 문장으로
구분한다. Game은 사건 순서, 공급, 비용과 정산을 계산하지 않는다. 진단은 기존
`READY / COMMAND / FINAL`에 정비 선택, 폭염 시작·복구, 유효정격, 사용불가 회선, 수요처별 인도,
공급원별 발전과 결산만 더한다.

## 7. 느슨한 기술 완료조건

- 새 fixture shape·참조와 대표 산술 검사
- 정비 완료 성공, 정비 생략 공장 미공급, 수요·정격 경계, feeder 복구, 현금 보존과 restart의 작은
  Core 사례
- 누적 ProductChecks와 Game build
- 실제 viewport 입력과 표준 button으로 시작부터 정비 완료·폭염 복구 결산까지 가는 1280×720
  native smoke 한 번
- 눈에 띄는 clipping·상태 표현 확인, 미해결 critical·core-flow major 0, 짧은 독립 검토 한 번

정비 생략 native 분기, 두 번째 해상도, 전체 접근성, LLM·사람 플레이, 밸런스 조정과 다음 단계
placeholder는 만들지 않는다. 사람 관찰은 전체 개발 뒤 테스트 단계로 미루고
`HumanValidationStatus = NOT_COLLECTED`를 유지한다.

## 8. 현재 검사와 종료 기록

현재 제품 검사는 다음 한 명령으로 첫 점등·병원·공장·폭염 규칙을 함께 확인한다.

```sh
dotnet run --project tools/Gridworks.ProductChecks/Gridworks.ProductChecks.csproj -c Release -- data/product-heatwave-v1.json
```

- 첫 점등 회귀: 10 suites / 664 assertions 통과
- 두 번째 심장 회귀: 5 suites / 124 assertions 통과
- 공장 용량 확장 회귀: 5 suites / 378 assertions 통과
- 폭염·예방정비: 5 suites / 243 assertions 통과
- Core·Game Debug·Release build: warning 0, error 0
- 실제 viewport 입력과 표준 button을 사용한 1280×720 정비 분기: `Success`, 기말 현금 `4.660 M`,
  종료 시각 `1845분`
- 폭염 시작 `1605분`, 복구·결산 `1845분`, 폭염 중 마을 `1.5 MW`, 공장 feeder 유효정격
  `2 MW`, 병원·마을·공장 전량공급 확인
- 정비 생략 분기는 Core 검사에서 공장 `2 MW` 미공급, 사건 후 feeder 복구와 실패 결산 확인
- 진단: `READY → COMMAND × 34 → FINAL`, 정비 선택·사건 시각·인도·급전·현금 ledger 기록,
  support 좌표 미기록
- fixture SHA-256: `b00b7fc9d657fd355b8741e4326d9a5297ae749de629c1763334bcca4df83f9c`
- 대표 실행 build hash: `e682ff9f32c1e70d7bca82c0fa1e1a7c4250b1b8741d8101ab8091cc8a553512`
- 짧은 독립 Core·Game 검토와 두 P1 수정 후 재검토: P0 0, P1 0

두 번째 해상도, 전체 접근성, LLM·사람 플레이와 밸런스 조정은 수행하지 않았다. 최종 결과 화면은
폭염 당시 수요·인도·feeder 상태와 복구 후 현재 feeder 상태를 구분한다.
