# 두 번째 심장 — 완료된 구현 기준

> 상태: `COMPLETED`
>
> 사람 검증: `NOT_COLLECTED`

이 완료는 다음 공장 수요·발전소 용량 단계의 구현을 승인하지 않는다.

이 단계는 완료된 첫 점등 제품 흐름을 병원 사건 결산까지 연장한다. 별도 병원 prototype이나 범용
전력조류 엔진을 만들지 않는다. 공장, 신규 발전소, 연속 타임라인, 저장과 최종 아트는 열지 않는다.

## 1. 플레이어 결과

기본 실행 한 번에서 다음을 끝낸다.

```text
첫 점등: 변전소와 마을 선로 건설·첫 결산
→ 병원 주 회선 직접 건설
→ 병원 예비 회선 직접 건설
→ 전기 단일회선 사용불가와 공간 공통사건 결과 확인
→ 고정 공간사건 진행
→ 병원 utility·내부전원·마을 서비스·현금 결산
```

주·예비 회선은 같은 출발 설비를 사용하지만 서로 다른 `LineProject`다. 공간에서 겹쳐 지나갈 수는
있어도 support를 공유하거나 교차점에서 접속하지 않는다. 플레이어는 더 싼 공유 위험 경로와 더 긴
공간 우회 경로의 차이를 지도에서 만든다. 화면은 별점·가중 총점이나 추천 경로를 표시하지 않는다.

## 2. 단일 데이터 권위

[`data/product-second-heart-v1.json`](../../data/product-second-heart-v1.json)이 현재 제품 실행값의
유일한 권위다. 첫 점등 수치와 다음 병원 항목을 함께 소유한다.

- 병원 위치·수요·처리 우선순위와 두 terminal
- 주·예비 `LineProject`의 식별자·경로 우선순위·정격·거리·비용·공기
- 닫힌 축정렬 공간 위험 사각형과 사건 시작 여유·지속시간
- UPS·디젤 지속시간
- 발전 변동비, utility 미공급 보상과 현금 미반영 LostSales rate

완료된 [`product-first-light-v1.json`](../../data/product-first-light-v1.json)은 첫 단계 회귀용으로
동결한다. 새 fixture에는 추천 support, 정답 경로, 예상 기말현금이나 검사 witness를 넣지 않는다.

Loader는 두 schema를 식별하고 각각의 exact field·type을 검사한다. 현재 schema에서는 식별자와
terminal 참조의 고유성, 두 hospital line, 증가하는 map/risk bounds, 위치·우선순위·정격·시간·rate,
checked 산술 범위를 검사한다. 성공 가능성은 검사 코드의 한 reference 흐름이 증명한다.

## 3. 건설과 상태

첫 점등 명령과 원자 공사 규칙은 그대로 유지한다. 첫 결산이 성공하면 현재 session이 병원 주 회선
계획으로 이어진다. 첫 점등이 실패하면 더 진행하지 않고 전체 임무를 다시 시작할 수 있다.

병원 두 회선은 다음 상태를 순서대로 지난다.

```text
PrimaryPlanning → PrimaryBuilding
→ BackupPlanning → BackupBuilding
→ IncidentReady → IncidentActive → Complete
```

현재 planning 회선에 기존 `PreviewLineSupport`, `AddLineSupport`, `UndoLineSupport`,
`CancelLineDraft`, `PreviewLineOrder`, `OrderLine`을 사용한다. 완공도 기존
`AdvanceToConstructionCompletion`을 사용한다. 별도 route 선택기나 범용 project queue는 없다.

support는 map 안의 건설 가능 빈 cell에만 놓는다. 기존 자산, 첫 점등 support와 다른 병원 회선의
support를 공유할 수 없다. 모든 span은 해당 회선의 최대 제곱거리 이하여야 하며 경계는 허용한다.
발주 전 전체 비용·공기·공간사건 노출 여부를 보여준다. 발주 성공은 현금 차감과 `Building` 전환,
완공시각 설정을 한 번에 수행하고, 모든 거부·preview는 상태를 바꾸지 않는다.

`RestartMission`은 어느 phase에서든 전체 흐름의 최초 상태로 돌아간다. 장 checkpoint와 부분
restart는 저장 단계 전에는 만들지 않는다.

## 4. 공급과 신뢰도

사용 가능한 완공 설비만 공급에 참여한다. 부하는 fixture의 `priority`, 이어서 stable load ID 순서로
처리한다. 병원이 마을보다 먼저다. 한 부하는 한 경로로 수요 전량을 받거나 0을 받으며 분할·부분공급은
없다. 선택 경로가 source와 line의 남은 정격을 예약한다.

병원 후보 경로는 `(routePriority, projectId)` 순서로 선택한다. 숨은 최단경로·최저비용 최적화는
없다. 마을은 첫 점등 선로 하나만 사용한다. 이 작은 고정 topology 밖의 임의 graph 편집, reverse
feed, switch와 max-flow는 만들지 않는다.

순수 reliability query는 두 hospital line을 각각 하나씩 제거해 반대 회선으로 병원 utility가
유지되는지 계산한다. 실제 사건은 별개다. line의 span 하나라도 닫힌 위험 사각형의 내부 또는 경계와
교차하면 whole `LineProject`가 사건 동안 사용불가다. 선의 단순 교차는 전기 접속이 아니다.
첫 점등에서 만든 마을 선로도 같은 공간 규칙에 참여하므로 위험 사각형을 지나면 사건 중 마을도
미공급될 수 있다.

`AdvanceToIncident`는 두 회선 완공 뒤 사건 시작 경계로 진행해 제거 project와 사건 중 공급을
고정한다. `AdvanceToRecoveryAndSettlement`는 고정 지속시간을 적분하고 복구·결산해 종료한다. 범용
event scheduler, 예고 타임라인, 수리와 확률 고장은 만들지 않는다.

## 5. 병원 내부전원과 경제

병원 utility가 0인 분에만 병원 소유 내부전원이 `UPS → 디젤` 순서로 P0 수요를 맡는다. 내부전원은
전력회사 utility 인도·판매나 발전소 가스 투입이 아니다. 내부전원이 P0를 유지해도 utility
미공급 보상과 LostSales는 그대로 계산한다.

사건 결산은 load별 실제 utility 인도량에서 다음을 산출한다.

```text
utility revenue = delivered utility energy × sale rate
generation cost = actual utility generation × variable cost rate
unserved compensation = utility-unserved energy × compensation rate
cash change = revenue - generation cost - compensation
```

모든 rate 계산은 `60_000_000 kWMinute/GWh`로 정확히 나뉘는 정수 연산이다. LostSales는 같은
미공급량의 진단값이지만 현금에 다시 빼지 않는다. 건설 발주 전에는 현금 부족을 거부하고, 최종
결산은 음수 현금을 숨기지 않고 그대로 표시한다.

최종 결과는 다음 세 hard condition을 각각 보여준다.

- 선언된 두 전기 단일회선 제거에서 병원 utility 유지
- 실제 공간사건 중 병원 utility 유지
- 사건 전체에서 병원 P0 미공급 0

모두 참이면 `Success`, 아니면 `Failure`다. 현금은 별도 결과이며 안전과 합산한 점수가 아니다.

## 6. 화면과 진단

기존 `ProductMain`과 제품 session을 연장한다. 지도에는 첫 점등 망, 병원, 닫힌 위험 사각형과 두
병원 회선을 함께 그린다. 활성 draft는 점선, 공사는 반복 사선, 완공은 실선, 사건 중 사용불가는
끊긴 파선·해칭과 문장으로 구분한다.

기존 map click·keyboard cursor와 표준 button을 재사용한다. 패널은 현재 phase, 견적·공기,
공간사건 노출, 병원 utility/P0, 첫 실패 이유와 최종 ledger만 표시한다. Game은 경로·위험 교차·용량·
내부전원·경제를 계산하지 않는다.

진단은 기존 `READY / COMMAND / FINAL` JSONL만 사용한다. `COMMAND`는 명령, 오류, phase와 활성
project ID를 기록하고 `FINAL`은 hard condition, 제거 project, utility/P0, 에너지·현금 ledger를
기록한다. hover, support 좌표, 추천 경로와 정답은 기록하지 않는다. save·replay·manifest 체계는
만들지 않는다.

## 7. 느슨한 기술 완료조건

이 단계에서는 다음만 요구한다.

- 새 fixture의 shape·참조와 대표 산술 검사
- 공유 source 용량·경로 우선순위, 전기 제거와 공간 제거, UPS→디젤, utility/internal 계량과 현금
  보존을 다루는 작은 Core 사례
- 공간 우회 성공 reference와 같은 위험대 실패 counterexample
- 기존 첫 점등 공사·restart 회귀와 Game build
- 실제 viewport 입력과 표준 button으로 첫 점등부터 안전한 병원 사건 결산까지 가는 native smoke 한 번
- crash·softlock·다음 단계가 의존하는 core-flow major 0, 한 번의 짧은 독립 검토

두 해상도 전체 수동 QA, 세부 접근성 매트릭스, LLM·사람 플레이와 밸런스 조정은 이 단계 종료조건이
아니다. 눈에 띄는 clipping과 기본 keyboard 접근만 smoke에서 확인하고, 전체 화면·접근성·패키지
점검은 마지막 통합 단계에서 수행한다. `HumanValidationStatus = NOT_COLLECTED`를 유지한다.

## 8. 현재 검사와 종료 기록

현재 제품 검사는 다음 한 명령으로 첫 점등 회귀와 병원 규칙을 함께 확인한다.

```sh
dotnet run --project tools/Gridworks.ProductChecks/Gridworks.ProductChecks.csproj -c Release -- data/product-second-heart-v1.json
```

- 첫 점등 회귀: 10 suites / 664 assertions 통과
- 두 번째 심장: 5 suites / 124 assertions 통과
- Core와 Game Debug·Release build: warning 0, error 0
- 실제 viewport 입력과 표준 button을 사용한 대표 흐름: `Success`, 기말 현금 `2.040 M`
- 진단: `READY → COMMAND × 23 → FINAL`, hard condition 3개 모두 참, 좌표·정답 경로 미기록
- 짧은 독립 Core·Game 검토: 미해결 critical·core-flow major 0

대표 smoke는 두 변전소 초안 위치, 마을 support, 위험대에 노출된 주회선 support와 안전한 예비회선
support를 각각 `--smoke-*` 인자로 주입한다. 이 값은 검사 입력일 뿐 fixture나 참가자 UI의 정답이
아니다. 사람·LLM 플레이는 수행하지 않았고 다음 단계는 여전히 미승인이다.
