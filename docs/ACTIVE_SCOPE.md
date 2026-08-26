# 현재 작업 범위

## 상태

**`WHOSE_MARGIN` 누적 5장 native 구현 scope가 활성화됐다.**

플레이어가 `./dev play through WHOSE_MARGIN`에서 앞선 4장의 실제 망·공사·자금·시계와 북안 약속 결과를
그대로 이어받는다. 다섯 번째 장의 briefing, 두 decision-window story, 세 실시간 사건과 산업 야간 증산
Keep/Defer를 generic chapter flow로 진행하고 authored 결과까지 도달한다.

## 수정 범위

- `RealtimeNativeRouteCatalog`의 단일 native endpoint와 이를 고정하는 R2 cumulative smoke
- 실제 누적 경로에서 재현된 결함이 있을 때만 가장 작은 owning Core/Session/presentation/UI 파일
- 이 단계의 현재 구현 사실을 소유하는 README, INSTALL, ARCHITECTURE, NEXT_TASKS, 평가 경계와 완료 이력

## 단일 권위

- authored briefing·window·phase·result: strict Release V2 campaign
- realtime 준비시간·forecast·사건·약속 기한: Release V3 overlay
- 망·공사·자금·시계·열·약속 결과: `RealtimeCampaignRun`
- cumulative story와 native route: `RealtimeChapterStoryFlow`, `RealtimeSession`,
  `RealtimeNativeRouteCatalog.ThroughNativeCoverage`
- 화면 의미: 기존 typed presentation과 owning Godot UI

## 범위 밖

- `BEFORE_WATER_RISE` 이후 장, finale/epilogue
- save/resume, title `이어하기`, settings, audio, 새 자산
- package, 평가 실행, 사람 미감·사용성 판정, 배포
- V2/V3의 authored 시각·story 문구 재작성
- chapter ID별 loader/Session/Main/UI 분기 또는 선제 refactor
- branch 통합, push, PR 또는 merge

## 완료 검사

- native catalog가 정확히 `WHOSE_MARGIN`까지 5장을 허용하고 다음 장과 위조 route를 fail-closed한다.
- 앞선 4장의 성공 상태가 다섯 번째 장으로 이어진다. authored reveal 순서에 따라 modal FIFO는
  briefing→`HOT_EVENING_PLANNING_WINDOW`→`LATE_NIGHT_RECOVERY_WINDOW`→`NIGHT_SHIFT` event story→
  result이며, event FIFO는 `HOT_BASE`→`NIGHT_SHIFT`→`LATE_NIGHT`로 Core transition과 일치한다.
- 약속 정보는 실제 city-promise 수요가 있는 `NIGHT_SHIFT`에만 나타난다. 보강 회랑 Keep의 실제 공급·열
  상태와 exact kept result, 명시적 Defer의 수요 제외와 exact deferred result를 검증한다. Keep만 5장
  full-flow evidence를 만들고 Defer는 만들지 않는다.
- 일반 회랑의 비상 노출·보호정지·복귀와 약속 미공급이 Core truth 및 사건 지평선/detail에서 사라지거나
  authored 성공 결과로 위조되지 않는다.
- WHOSE_MARGIN story selector, 누적 R2 harness와 기본 `./dev check`가 통과한다.

이 단계는 누적 5장 native 도달성과 결정론적 wiring만 증명한다. production-input 직접 플레이, 남은 3장,
전체 캠페인, 사람 UX 품질, save/package 또는 공식 점수의 증거로 확대하지 않는다.
