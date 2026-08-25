# 현재 작업 범위

## 상태

**누적 4장 production-input 직접 플레이 scope가 활성화됐다.**

fresh process의 `./dev play through NORTH_BANK_PROMISE`에서 첫 3장의 실제 망·공사·자금·시계가
네 번째 장까지 이어지는지 관찰한다. Keep과 Defer를 각각 production mouse/keyboard 입력으로 결과까지
진행하고, 한 줄 사건 지평선의 약속 기한과 주변 사건 상세를 실제 화면에서 연다.

## 수정 범위

- `docs/ACTIVE_SCOPE.md`, `README.md`, `docs/NEXT_TASKS.md`, `docs/archive/COMPLETED_HISTORY.md`
- 실제 production 입력에서 재현된 결함이 있을 때만 그 결함의 가장 작은 owning R2/Core/UI 파일
- 결함 수정에 직접 필요한 가장 작은 deterministic regression

## 단일 권위

- 누적 chapter·망·공사·자금·시계와 Keep/Defer 결과: `RealtimeCampaignRun`과 `RealtimeSession`
- native 4장 route: `RealtimeNativeRouteCatalog.ThroughNativeCoverage`
- 사건·결정 marker와 상세: typed presentation, `RealtimeEventRail`과 `RealtimeUiRoot`

## 범위 밖

- `WHOSE_MARGIN` 이후 장, finale/epilogue
- save/resume, 실제 title `이어하기`
- settings, audio, 새 자산, package, 평가 실행, 배포
- direct play에서 재현되지 않은 선제 refactor나 새 fixture
- branch 통합, push, PR 또는 merge

## 완료 검사

- Keep과 Defer를 서로 독립된 fresh process에서 production 입력으로 결과까지 진행한다.
- 각 경로에서 장 전환 뒤 실제 망·공사·자금·시계가 보존됨을 화면 상태와 runtime evidence로 확인한다.
- 네 번째 장에서 6개월 달력 전환, 약속 기한 marker, 주변 사건과 선택 상세를 실제로 연다.
- 결함을 수정한 경우에만 가장 가까운 targeted check와 기본 `./dev check`를 실행한다.

이 관찰은 누적 4장 native 도달성만 증명한다. 남은 4장, save/resume, 전체 캠페인, 사람 표본의
미감·사용성 또는 공식 UX 점수의 증거로 확대하지 않는다.
