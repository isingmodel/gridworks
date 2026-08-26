# 현재 작업 범위

## 상태

**`BEFORE_WATER_RISE` 누적 6장 native 구현 scope가 활성화됐다.**

플레이어가 `./dev play through BEFORE_WATER_RISE`에서 앞선 5장의 실제 망·공사·자금·시계와 약속 결과를
그대로 이어받는다. 여섯 번째 장의 briefing, 범람 전 planning window, `FLOOD_ARRIVAL` 사건과 동부
생활권 공급 Keep/Defer를 기존 generic chapter flow로 진행하고 authored 결과까지 도달한다.

## 수정 범위

- `RealtimeNativeRouteCatalog`의 frontier endpoint와 이를 고정하는 기존 R2 cumulative smoke
- 실제 누적 경로에서 재현된 결함이 있을 때만 가장 작은 owning Core/Session/presentation/UI 파일
- 이 단계의 현재 구현 사실을 소유하는 README, INSTALL, ARCHITECTURE, NEXT_TASKS, 평가 경계와 완료 이력

## 단일 권위

- authored briefing·window·phase·result: strict Release V2 campaign
- realtime 준비시간·forecast·사건·약속 기한: Release V3 overlay
- 망·공사·자금·시계·범람·약속 결과: `RealtimeCampaignRun`
- cumulative story와 native route: `RealtimeChapterStoryFlow`, `RealtimeSession`,
  `RealtimeNativeRouteCatalog.ThroughNativeCoverage`
- 화면 의미: 기존 typed presentation과 owning Godot UI

## 범위 밖

- `SWITCH_OFF_TO_PROTECT` 이후 장, finale/epilogue
- save/resume, title `이어하기`, settings, audio, 새 자산
- package, 평가 실행, 사람 미감·사용성 판정, 배포
- V2/V3의 authored 시각·story 문구 재작성
- chapter ID별 loader/Session/Main/UI 분기 또는 선제 refactor
- 별도 표준/실패 replay나 새 UI abstraction
- branch 통합, push, PR 또는 merge

## 완료 검사

- native catalog가 정확히 `BEFORE_WATER_RISE`까지 6장을 허용하고 이전 frontier, 다음 장과 위조 route를
  fail-closed한다. canonical route 수는 3개를 유지한다.
- 앞선 5장의 성공 상태가 266850분의 여섯 번째 장으로 이어진다. modal FIFO는 briefing→
  `BEFORE_FLOOD_WINDOW`→`FLOOD_ARRIVAL` event story→result이고, 사건은 267150–267270분의
  `FLOOD_ARRIVAL` 하나다.
- 누적 망이 이미 가진 동부 접속 2회는 계획 선행의 상속으로 검증하며 이번 장의 새 공사로 위조하지 않는다.
  범람 구역과 겹치지 않는 남부 고지대 보완 회랑을 실제로 완공하고 quote의 risk exposure가 없음을
  확인한다.
- 명시적 Keep에서 범람 중 의료원·정수장·동부 생활권 공급과 exact kept result를, 명시적 Defer에서
  동부 수요 의무 제외·필수시설 공급과 exact deferred result를 검증한다. forecast/active state는
  `RIVER_FLOOD_ZONE`과 `WEST_SOURCE_NODE` 사용 불가를 같은 Core truth에서 보인다. Keep만 6장
  full-flow evidence를 만들고 Defer는 만들지 않는다.
- BEFORE_WATER_RISE의 기존 story selector, 누적 R2 harness와 기본 `./dev check`가 통과한다.

이 단계는 누적 6장 native 도달성과 결정론적 wiring만 증명한다. production-input 직접 플레이의 관찰
상한은 여전히 `NORTH_BANK_PROMISE`까지 4장이다. 남은 2장, 전체 캠페인, 사람 UX 품질, save/package
또는 공식 점수의 증거로 확대하지 않는다.
