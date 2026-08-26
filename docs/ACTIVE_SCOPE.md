# 현재 작업 범위

## 상태

**누적 8장 product 진입·stable save/Continue scope가 활성화됐다.**

저장 파일이 없는 제품 title의 `새 게임`은 canonical `FIRST_LIGHT`→`LONGEST_NIGHT` 누적 8장 route를
시작한다. title에서 시작하거나 유효 save로 Continue한 product-owned session만 기존 v1 accepted-journal
한 파일을 갱신하고, 모든 장의 stable in-progress 상태를 exact replay한 뒤 player-paused·normal speed·
no-modal로 복원한다.

## 수정 범위

- `RealtimeNativeRouteCatalog`의 누적 8장 route를 `ProductCampaign` 단일 제품 권위로 명명
- route와 별개인 typed product-save ownership; 명시적 chapter/through/fixture 개발 실행은 product save를
  읽거나 쓰지 않음
- native data가 자신의 canonical save source identity를 한 곳에서 조립
- active와 pending story가 모두 없는 `RealtimeChapterStoryFlow.IsIdle`을 stable capture 계약에 포함
- 제품 title·fresh-process save-create→Continue와 한 chapter handoff 뒤 resume의 최소 결정론적 검사
- 이 단계의 current fact 문서와 완료 이력

## 단일 권위

- 제품 누적 route와 허용 continuation route: `RealtimeNativeRouteCatalog`
- gameplay 상태·accepted journal·canonical hash: `RealtimeCampaignRun`
- save schema·strict decode·deterministic replay: `RealtimeCampaignSaveCodec`
- native source identity: `RealtimeSliceData`
- stable capture·paused resume와 story-idle 정책: `RealtimeSession`
- 파일 상태·atomic write: `RealtimeCampaignSaveStore`
- product-save ownership과 title/Godot lifecycle: `RealtimeSliceMain`; title view는 표시와 signal만 소유

## 호환 정책

- 이 scope에서 새로 만드는 save는 누적 8장 `ProductCampaign` source에 결속한다.
- 직전 단계의 exact-current standalone `FIRST_LIGHT` v1 save는 원 route 그대로 `이어하기`만 허용하고
  누적 save로 migration하지 않는다.
- tutorial prefix 같은 다른 개발 route save, source/hash/replay 불일치, 형식 손상, 지원하지 않는 schema/
  version과 I/O 실패는 두 title action을 차단하고 원본을 보존한다.

## 범위 밖

- 사건 중·active duty·pending transition, queued/active story, chapter result·handoff 저장
- 완료 run, finale/epilogue cursor와 완료 후 result/chapter/replay 선택
- snapshot/application cursor 신규 schema, 유효 save 덮어쓰기 확인, 삭제·migration/recovery UI
- 비정상 종료 journal, 다중 slot, cloud save
- settings/audio, package, 공식 평가, 사람 UX 판정, push/PR/merge
- frozen V2 persistence나 historical Product/Commercial main의 재사용·수정

## 완료 검사

- 저장 파일 없는 제품 title의 `새 게임`이 exact 누적 8장 `ProductCampaign`과 authored `FIRST_LIGHT`
  briefing을 연다.
- product title New Game/Continue가 만든 session만 stable progress를 저장한다. 명시적 chapter/through/fixture
  실행은 같은 route여도 product save lifecycle을 소유하지 않는다.
- Core command 하나 이상, incomplete active chapter, no event/duty/pending transition/draft, story idle,
  no epilogue/frame debt인 상태만 capture한다.
- 각 장의 stable progress는 source·snapshot/hash/journal/transition history를 exact replay하고, 한 chapter
  handoff 뒤 resume는 과거 story를 다시 열지 않으며 다음 authored story만 한 번 연다.
- 별도 fresh process의 save-create→Continue는 누적 product source와 exact clock·cash·world·construction·
  journal/hash를 복원하고 paused·normal speed·no-modal 정책을 적용한다.
- exact standalone `FIRST_LIGHT` v1 Continue는 유지하고, 그 밖의 허용되지 않은 route와 invalid/unsupported/
  I/O save는 원본을 바꾸지 않은 채 두 action을 차단한다.
- focused replay/session 검사, Debug/Release build, `./dev check`, 전체 Godot UI harness와 독립 review가
  통과한다.

이 단계는 누적 8장 product route의 stable in-progress save/Continue만 증명한다. 사건·story·장 전환·완료
저장, 전체 8장 production-input journey, 완료 후 선택, package 또는 사람 UX 품질의 증거로 확대하지 않는다.
