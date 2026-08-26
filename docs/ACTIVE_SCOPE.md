# 현재 작업 범위

## 상태

**`LONGEST_NIGHT` 누적 8장 native 구현 scope가 활성화됐다.**

플레이어가 `./dev play through LONGEST_NIGHT`에서 앞선 7장의 실제 망·공사·자금·시계와 결과를 그대로
이어받는다. 마지막 장의 briefing, 최종 운영안 window, 최대수요·폭염 정점·보호정지와 범람 사건,
standard authored 결과를 기존 generic chapter flow로 진행한다.

## 수정 범위

- `RealtimeNativeRouteCatalog`의 frontier endpoint와 이를 고정하는 기존 R2 cumulative smoke
- 실제 누적 경로에서 재현된 결함이 있을 때만 가장 작은 owning Core/Session/presentation/UI 파일
- 이 단계의 현재 구현 사실을 소유하는 README, INSTALL, ARCHITECTURE, NEXT_TASKS, 평가 경계와 완료 이력

## 단일 권위

- authored briefing·window·phase·result: strict Release V2 campaign
- realtime 준비시간·forecast·사건: Release V3 overlay
- 망·공사·자금·시계·위험·열 상태·결과: `RealtimeCampaignRun`
- cumulative story와 native route: `RealtimeChapterStoryFlow`, `RealtimeSession`,
  `RealtimeNativeRouteCatalog.ThroughNativeCoverage`
- 화면 의미: 기존 typed presentation과 owning Godot UI

## 범위 밖

- finale, epilogue card·promise line, save/resume·replay와 title `이어하기`
- settings, audio, 새 자산, 새 mechanic이나 UI abstraction
- package, 평가 실행, 사람 미감·사용성 판정, 배포
- V2/V3의 authored 시각·story 문구 재작성
- chapter ID별 loader/Session/Main/UI 분기 또는 선제 refactor
- 새 대형/소형 refuge 대안, 실패 replay나 별도 route branch
- branch 통합, push, PR 또는 merge

## 완료 검사

- native catalog가 정확히 `LONGEST_NIGHT`까지 8장을 허용하고 이전 frontier와 위조 route를
  fail-closed한다. canonical route 수는 3개를 유지하고 full composed campaign identity를 사용한다.
- 앞선 7장의 성공 상태가 267990분의 마지막 장으로 이어지고 이전 종료 현금에 1,800,000원이 더해진다.
  이 장에는 promise와 connection gate가 없다. modal FIFO는 briefing→`FINAL_OPERATING_PLAN_WINDOW`→
  `HEATWAVE_PEAK` event story→`PROTECTIVE_STOP_FLOOD` event story→standard result이며 story가 없는
  `MAX_DEMAND`는 modal을 만들지 않는다.
- SWITCH에서 만든 기존 `(2300, 900)` 소형 변전소를 재사용한다. 서부 전원에서 `(650, 450)`,
  `(990, 400)`, `(1570, 400)`, `(1800, 850)`, `(1950, 1000)`을 거치는 범람 비노출 보강 feed와 그
  변전소에서 의료원으로 가는 보강 직결 회선만 추가해 268230분에 완공한다.
- forecast reveal은 267990/268170/268350분이고, 사건은 `MAX_DEMAND` 268590–268710,
  `HEATWAVE_PEAK` 268770–268890, `PROTECTIVE_STOP_FLOOD` 268950–269070분이다. 각 forecast와 actual은
  authored 5개 수요와 thermal policy를 유지한다.
- 최대수요에서 의료원·정수장 각 900 kW를 연속 공급한다. 폭염 정점에서 의료원 1,600 kW와 정수장
  1,400 kW를 공급하고 실제 비상 운전과 보호정지 전이를 남긴다. 마지막 범람 국면은
  `RIVER_FLOOD_ZONE`과 실제 보호정지·복귀를 반영하면서 모든 duty segment에서 의료원·정수장 각
  900 kW를 공급한다. 일반 수요는 operating record이며 성공 hard gate로 위조하지 않는다.
- 세 사건의 safety duty가 성공한 exact standard authored result를 표시한다. 결과를 닫으면 8장 ordered
  result와 full-flow evidence를 한 번만 만들고 `CampaignComplete=true`인 native route를 종료한다.
  이를 epilogue·save·전체 제품 완결성 증거로 확대하지 않는다.
- LONGEST_NIGHT의 기존 story selector, 누적 R2 harness, Release build와 기본 `./dev check`가 통과한다.

이 단계는 누적 8장 native 도달성과 결정론적 wiring만 증명한다. production-input 직접 플레이의 관찰
상한은 여전히 `NORTH_BANK_PROMISE`까지 4장이다. finale/epilogue, 사람 UX 품질, save/package 또는 공식
점수의 증거로 확대하지 않는다.
