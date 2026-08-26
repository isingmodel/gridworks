# 현재 작업 범위

## 상태

**terminal completed save·Continue scope가 활성화됐다.**

현재 product-owned 누적 8장은 성공한 마지막 result를 닫은 뒤 city report→medical witness→closing을
보여 주고 완료된 망의 read-only 화면에 남는다. 그러나 `CampaignComplete`와 시작된 epilogue는 저장에서
차단되므로 정상 종료해도 직전 진행 저장이 남는다. 다음 실행의 `이어하기`가 완료 상태가 아니라 과거
진행으로 돌아가는 것이 이번 scope의 제품 손실 seam이다.

## 결과물

- current v3 wire를 바꾸지 않고 canonical full `ProductCampaign`의 exact terminal 완료 상태를 저장한다.
- terminal 완료는 모든 chapter story가 닫혔고, 성공한 마지막 결과라면 세 epilogue card도 모두 닫힌
  `Ended`·no-modal 상태다. 실패한 마지막 결과는 result를 닫았고 epilogue가 시작되지 않은 상태만
  허용한다.
- current v3 journal·minute·canonical hash·`closedStoryCount`로 completed Core를 strict replay하고,
  restore 결과에 원본 schema identity를 보존해 crafted v1/v2 completion은 거부한다.
- 제품 title은 완료 저장임을 설명하고 `이어하기`로 동일한 Core/world/outcome을 epilogue 재생 없이
  `Ended` read-only 화면에 복원한다.
- 정상 product-owned tree exit가 직전 in-progress 저장을 terminal completed bytes로 교체할 수 있는 기존
  단일 write lifecycle을 유지한다.

## 구현 범위

- `RealtimeCampaignSaveCodec`의 completed replay 증거와 restore schema identity
- `RealtimeChapterStoryFlow`의 all-candidates-closed completed matcher
- `RealtimeEpilogueFlow`의 exact terminal completed restore
- `RealtimeSession`의 분리된 in-progress/terminal capture predicate와 typed resume plan
- `RealtimeSliceMain`의 completed title/Continue routing
- 기존 Realtime save suite, R2 cumulative smoke와 product-entry fresh-process smoke의 최소 확장
- 실제 변경 사실을 소유하는 current Markdown 문서

## 범위 밖

- active final result와 city report/medical witness/closing 중간 저장·cursor
- undelivered Core pending transition과 general queued story suffix
- 완료 후 result/chapter/replay 선택과 journal suffix 폐기 정책
- 유효한 저장 위 `새 게임` 확인·덮어쓰기, 손상/구버전 recovery·폐기 UI
- explicit chapter/through/fixture의 product save ownership
- settings/audio, package, 공식 평가와 사람 UX 판정
- push, PR, merge

## 완료 검사

- 성공 경로의 final result와 세 epilogue card 중에는 capture가 계속 거부되고 closing 종료 뒤에만 terminal
  capture가 성공한다.
- terminal current v3 serialize→deserialize→restore가 snapshot/hash/journal/transition history와
  `closedStoryCount`를 exact 유지한다.
- completed Continue가 `Ended`·no-modal·no-frame-debt read-only world를 열고 epilogue/story transition을
  재생하지 않는다.
- prior v1/v2 completion, partial/standalone route, pending/draft와 nonterminal completed cursor는
  fail-closed한다.
- 기존 initial c0/c1, v2 `+1`, v1 all-closed, active event/duty와 bounded result→briefing 회귀를 유지한다.
- 새 test suite를 만들지 않고 기존 focused save suite, cumulative R2 smoke와 product-entry
  save-create→fresh Continue 경로를 확장한다.
- `./dev check`, root Release build, 전체 Godot UI harness와 독립 review를 통과한다.
