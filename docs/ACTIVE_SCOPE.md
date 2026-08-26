# 현재 작업 범위

## 상태

**8장 finale 뒤의 3-card epilogue scope가 활성화됐다.**

`LONGEST_NIGHT`의 기존 authored standard result를 별도 카드로 복제하지 않고 finale로 유지한다. 플레이어가
그 결과를 닫으면 `BaseCampaign.Epilogue`의 city report → medical witness → closing 세 카드만 순서대로
확인하고, 마지막 카드를 닫은 뒤 완료된 읽기 전용 망으로 돌아간다.

## 수정 범위

- final chapter result와 epilogue 사이만 소유하는 작은 `RealtimeEpilogueFlow`
- `RealtimeSession`의 final-result close handoff와 기존 modal interaction 재사용
- completion request를 받는 `RealtimePresentationSource`, `RealtimeModalPresenter`, stable R2 modal/action ID
- 기존 8장 누적 smoke의 finale → 세 epilogue card 완료 검사
- 이 단계의 현재 구현 사실을 소유하는 current 문서와 완료 이력

## 단일 권위

- finale: `LONGEST_NIGHT`의 기존 authored standard result
- epilogue 카드·promise line: strict `BaseCampaign.Epilogue`
- 완료 장·Keep/Defer·남은 자금·최종 망: `RealtimeCampaignRun` snapshot
- 카드 순서와 완료 상태: `RealtimeEpilogueFlow`
- 화면 의미: typed completion request → `RealtimeModalPresenter` → 기존 `RealtimeModalHost`

## 범위 밖

- save/resume, title `이어하기`, 완료 저장과 result/chapter/replay 선택
- final result를 복제한 별도 finale card, epilogue data 또는 chapter 결과 문구 재작성
- 기존 `RealtimeChapterStoryFlow`에 epilogue purpose를 추가하는 nullable 분기
- settings, audio, 새 자산·mechanic·UI abstraction
- package, 평가 실행, 사람 미감·사용성 판정, 배포
- branch 통합, push, PR 또는 merge

## 완료 검사

- 전체 8장 cumulative route에서 exact `LONGEST_NIGHT` standard result가 먼저 표시되고, 닫은 뒤 epilogue가
  city report → medical witness → closing 정확히 세 카드로 한 번만 열린다. standalone 1장과 tutorial
  3장 prefix는 epilogue를 열지 않는다.
- 세 카드의 speaker·title·기본 body는 `BaseCampaign.Epilogue`를 그대로 읽는다. city report에는 완료된
  chapter outcome을 generic join해 세 promise line의 exact Keep/Defer 문장과 남은 운영 자금을 표시한다.
  chapter ID나 고유 수요처 문구로 분기하지 않는다.
- finale close부터 epilogue close까지 Core canonical hash, minute, command count, cash, 망과 chapter outcome이
  변하지 않는다. 모든 modal은 기존 ended/read-only pause와 focus 경계를 유지한다.
- 마지막 카드 close 뒤 completion flow가 완료되고 같은 카드를 다시 열지 않으며, 완료된 망 화면을
  읽기 전용으로 유지한다. 저장·재개나 replay가 완료된 것처럼 표시하지 않는다.
- 기존 full-route smoke, 전체 Godot UI harness, Release build와 기본 `./dev check`가 통과한다.

이 단계는 현재 R2의 campaign completion presentation만 증명한다. save/resume, 완료 후 선택 화면,
fresh-process 제품 여정, 사람 UX 품질, package 또는 공식 점수의 증거로 확대하지 않는다.
