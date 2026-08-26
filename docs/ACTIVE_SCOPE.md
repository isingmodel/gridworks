# 현재 작업 범위

## 상태

**`SWITCH_OFF_TO_PROTECT` 누적 7장 native 구현 scope가 활성화됐다.**

플레이어가 `./dev play through SWITCH_OFF_TO_PROTECT`에서 앞선 6장의 실제 망·공사·자금·시계와 약속
결과를 그대로 이어받는다. 일곱 번째 장의 briefing, 계획정지 전 planning window, 서부 전원 계획정지와
복귀 사건, standard authored 결과를 기존 generic chapter flow로 진행한다.

## 수정 범위

- `RealtimeNativeRouteCatalog`의 frontier endpoint와 이를 고정하는 기존 R2 cumulative smoke
- 실제 누적 경로에서 재현된 결함이 있을 때만 가장 작은 owning Core/Session/presentation/UI 파일
- 이 단계의 현재 구현 사실을 소유하는 README, INSTALL, ARCHITECTURE, NEXT_TASKS, 평가 경계와 완료 이력

## 단일 권위

- authored briefing·window·phase·result: strict Release V2 campaign
- realtime 준비시간·forecast·사건: Release V3 overlay
- 망·공사·자금·시계·계획정지·열 상태·결과: `RealtimeCampaignRun`
- cumulative story와 native route: `RealtimeChapterStoryFlow`, `RealtimeSession`,
  `RealtimeNativeRouteCatalog.ThroughNativeCoverage`
- 화면 의미: 기존 typed presentation과 owning Godot UI

## 범위 밖

- `LONGEST_NIGHT`, finale/epilogue
- save/resume, title `이어하기`, settings, audio, 새 자산
- package, 평가 실행, 사람 미감·사용성 판정, 배포
- V2/V3의 authored 시각·story 문구 재작성
- chapter ID별 loader/Session/Main/UI 분기 또는 선제 refactor
- 값싼 공유 회랑의 emergency/trip 실패 replay, 별도 대안 replay나 새 UI abstraction
- branch 통합, push, PR 또는 merge

## 완료 검사

- native catalog가 정확히 `SWITCH_OFF_TO_PROTECT`까지 7장을 허용하고 이전 frontier, 다음 장과 위조
  route를 fail-closed한다. canonical route 수는 3개를 유지한다.
- 앞선 6장의 성공 상태가 267270분의 일곱 번째 장으로 이어지고 이전 종료 현금에 1,600,000원이
  더해진다. modal FIFO는 briefing→`BEFORE_PLANNED_OUTAGE_WINDOW`→
  `WEST_SOURCE_PLANNED_OUTAGE` event story→`WEST_SOURCE_RETURN_SERVICE` event story→standard result다.
- 북안 장에서 만든 정수장 player corridor 1/2를 상속한다. 범람 장의 기존 `(1950, 850)` 보강
  전신주에서 새 소형 변전소 `(2300, 900)`를 거쳐 정수장으로 가는 보강 회선 하나만 267417분까지
  완공하고, 첫 사건 시작에 2/2로 freeze되는지 확인한다.
- 계획정지는 267690–267810분에 `WEST_SOURCE_NODE`를 실제 공급에서 제외하고 의료원 1,800 kW,
  정수장 1,400 kW와 동부 700 kW를 연속 한계 안에서 공급해 비상·보호정지를 만들지 않는다.
  복귀는 267870–267990분에 서부 전원을 다시 사용하고 의료원·정수장 각 900 kW와 동부 700 kW를
  연속 한계 안에서 공급한다.
- 두 사건과 접속 의무가 성공한 exact standard authored result를 표시한다. 결과를 닫으면 7장 ordered
  result와 full-flow evidence를 한 번만 만들고 prefix campaign을 종료한다.
- SWITCH_OFF_TO_PROTECT의 기존 story selector, 누적 R2 harness, Release build와 기본 `./dev check`가
  통과한다.

이 단계는 누적 7장 native 도달성과 결정론적 wiring만 증명한다. production-input 직접 플레이의 관찰
상한은 여전히 `NORTH_BANK_PROMISE`까지 4장이다. 남은 1장, 전체 캠페인, 사람 UX 품질, save/package
또는 공식 점수의 증거로 확대하지 않는다.
