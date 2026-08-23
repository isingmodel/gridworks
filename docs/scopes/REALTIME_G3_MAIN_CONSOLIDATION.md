# Gridworks — realtime G3 canonicalization과 main 통합 범위

> 문서 상태: **활성 — 사용자 명시 지시**
> 제품 방향: **live-streaming R2를 제품 경로로 유지하고, 과거 full G3 표현을 그 경로에 복원한다.**

## 1. 문제와 결정

이 scope를 열 당시 기본 실행은 `CommercialMain`/V2 renderer였고, 과거 full G3 표현은 local `main`의
`CommercialMapView`에만 남아 있었다. 당시 R2에는 그중 35개 asset만 `RealtimePlaceholderMap`으로
부분 이식됐으므로 기본 화면에서는 기존 이미지 스타일이 보이지 않았다.

사용자는 이 분기를 해소하고, 검증된 결과를 `main` 하나로 정리하도록 명시했다. 이 scope는 V2의
turn-based game loop를 되살리거나 병합하지 않는다. full G3의 **시각 자산·배치 recipe·재질·UI chrome**만
live R2 surface로 이식하고 R2를 기본 제품 진입점으로 만든다.

## 2. 단일 권위와 허용 변경

- 시각 source authority: local `main`의 `cf5da56` G3 tree와 그 renderer recipe
  (`game/CommercialMapView.cs`, `.tscn`, `CommercialMain.tscn`)다.
- 게임 rule/input/event authority: 현재 branch의 Release.V3/R2
  (`RealtimeSliceMain`, `IRealtimeWorldView`, `RealtimeUiRoot`)다.
- runtime art authority: exact 57 PNG + matching `.import`의 새 SHA-256 manifest이며, source bytes는
  `cf5da56`과 byte-identical해야 한다.
- 현재 R2 `RealtimePlaceholderMap`과 `RealtimeUiRoot`/`RealtimeTheme`만 G3 presentation을 소유한다.
  미래 사건 status rail은 한 줄 compact marker + custom hover detail 계약을 유지한다.

이 scope에서만 다음을 수정할 수 있다.

- `game/art/commercial/g3/**`의 exact accepted G3 assets 및 `.import`, SHA manifest/provenance test
- `game/realtime/r2/RealtimePlaceholderMap*`, `game/realtime/r2/RealtimeSliceMain*`
- `game/realtime/ui/**`, `game/RealtimeTheme.tres`, `game/project.godot`
- 해당 deterministic smoke/UI harness, default-entry validator와 current-state docs

다음은 금지한다.

- V2 `CommercialMain`, `CommercialMapView`, V2 campaign/data/Core의 gameplay를 realtime으로 merge
- Release.V3 rules, authored story truth, event rail semantics, selection/hit/focus/accessibility ownership 변경
- 새 이미지 생성, root `assets/01`–`04`를 runtime plate로 복사, default/package/export/remote release
- score-bearing LLM judge, native human evidence의 주장

## 3. 완료 조건

1. default scene이 `RealtimeSliceMain`이며, V2 `CommercialMain`이 runtime entry가 아니다.
2. R2 runtime의 full G3 union은 exact 57 path/source hash다. 이 중 map 50개는
   clear/heat/rain/storm의 terrain·river·road·city·grid presentation에서 draw-only로 확인되고,
   UI 7개는 live R2 chrome에 style resource로 연결된다.
3. R2 UI는 G3 UI chrome을 사용하되 TopHud, 한 줄 EventRail, ContextDock, Build/Action dock와 modal의
   geometry, keyboard/pointer/AX contract를 보존한다.
4. Debug build, G3 provenance test, Realtime/Commercial deterministic suites, text-plan tool, full R2 UI harness와
   default-entry smoke가 PASS한다.
5. independent review가 P0 0/P1 0이며 current-state docs가 actual default/R2/G3 authority를 기록한다.
6. 검증 후 local `main`이 canonical head가 되고, local working branches
   `codex/commercial-ux-87-realtime`와 `codex/commercial-ux-87`은 삭제된다. remote push/remote branch deletion은
   이 scope가 자동으로 수행하지 않는다.

## 4. branch 통합 절차

현재 realtime branch에서 source-preserving port와 검증을 완료한다. 이후 local `main`에 merge하고,
main에서 동일 검증을 다시 실행한 뒤에만 작업 branch를 삭제한다. branch 삭제 전에는 `main`이 모든 relevant
commit을 ancestry로 갖는지 확인한다. 이 절차는 Git history를 rewrite하지 않으며, `origin/main`에는 쓰지 않는다.

## 5. 현재 진행

- commit `1af2b33`에서 `RealtimeSliceMain`을 default entry로 전환했고, exact G3 57개(지도 50/UI 7)를
  live R2 draw/theme surface에 연결했다.
- G3 provenance/default-entry/text-plan, Debug build, Realtime 25/1,077, Commercial 31/7,084, full R2 UI
  matrix, 두 targeted checkpoint와 headless default boot가 통과했다.
- independent review의 current-state documentation follow-up은 P0 0/P1 0으로 닫았다. 남은 작업은
  local `main`의 history-only consolidation과 그 final verification뿐이다.
