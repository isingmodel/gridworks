# 현재 작업 범위

## 상태

**제품 title과 새 게임 경로 scope가 활성화됐다.**

플레이어가 인자 없이 실행하면 제품 title을 보고, `새 게임`의 production UI 입력으로 authored
`FIRST_LIGHT` briefing에 진입한다. 저장 권위가 아직 없으므로 `이어하기`는 보이되 이유가 명확한
비활성 상태다. 개발 fixture와 checkpoint/native 개발 route는 명시적 인자로만 접근한다.

## 수정 범위

- `game/realtime/r2/`: launch 선택, default scene bootstrap, 작은 title smoke runner
- `game/realtime/ui/`: product title surface와 R2 UI root wiring
- `dev`, default-entry 회귀와 이 단계에 필요한 최소 smoke seam
- 이 단계가 바꾼 현재 사실을 소유하는 README, INSTALL, ARCHITECTURE, NEXT_TASKS, 완료 이력

## 단일 권위

- no-arg/title, explicit fixture와 native route의 구분: `RealtimeLaunchCatalog`
- native chapter/prefix capability: `RealtimeNativeRouteCatalog`
- FIRST_LIGHT world·chapter·briefing: strict V2 content, V3 overlay와 `RealtimeSliceResources`
- title 표시·focus·입력 차단: `RealtimeProductTitle`과 `RealtimeUiRoot`

## 범위 밖

- R2 save/resume와 실제 `이어하기`
- 과거 V2 save/title/settings/audio 연결
- `NORTH_BANK_PROMISE` 이후 장, finale/epilogue
- settings, audio, 새 자산, package, 평가 실행, 배포
- branch 통합, push, PR 또는 merge

## 완료 검사

- no-arg default scene에서 title, 활성 `새 게임`, 비활성 `이어하기`와 이유를 확인한다.
- production button 입력으로 title→authored FIRST_LIGHT briefing을 확인한다.
- `./dev play fixture`, 두 named checkpoint와 세 native route가 계속 명시적 인자로만 동작한다.
- `./dev build`, title smoke와 기본 `./dev check`가 통과한다.

이 자동 검사는 production UI signal과 실제 R2 session/modal wiring을 증명하지만 사람 미감·사용성,
save/resume 또는 전체 캠페인 완결성의 증거로 확대하지 않는다.
