# 현재 작업 범위

## 상태

**standalone `FIRST_LIGHT` 안정 체크포인트 저장·Continue scope가 활성화됐다.**

briefing을 닫고 Core command가 수락된 진행 중 `FIRST_LIGHT`를 정상 종료할 때 한 R2 save를 atomic하게
기록한다. fresh process의 제품 title은 exact current source와 결정론적으로 replay되는 유효 save만
`이어하기`로 열고, 복원된 session은 같은 시각·망·공사·현금·결정을 player-paused 상태로 보여 준다.

## 수정 범위

- inactive V3 persistence 초안을 source identity·minute·accepted journal·canonical hash의 작은 current codec과
  replay result로 교체하고 current Core compile graph에 포함
- R2 전용 한 파일의 load status와 atomic store; `RealtimeSliceMain`만 `user://` 절대 경로를 결정
- restored run·transition history를 받는 `RealtimeSession` factory와 paused/no-modal resume policy
- title의 typed Continue/New Game availability, `ContinueRequested`, Main의 load/restore/save lifecycle
- standalone `FIRST_LIGHT`의 stable mid-construction 정상 종료→fresh process Continue smoke와 가장 작은 Core 검사
- 이 단계의 현재 사실을 소유하는 문서와 완료 이력

## 단일 권위

- gameplay 상태·accepted journal·canonical hash: `RealtimeCampaignRun`
- save schema·strict decode·deterministic replay: current `RealtimeCampaignSaveCodec`
- route와 bundled source identity: canonical `RealtimeNativeRouteCatalog`와 `RealtimeSliceData`
- 파일 상태·atomic write: `RealtimeCampaignSaveStore`
- title/session/Godot lifecycle: `RealtimeSliceMain`; title view는 표시와 signal만 소유

## 범위 밖

- 누적 8장 product `새 게임`, 사건 중·chapter 전환 직전·active story modal 저장
- 완료 save, finale/epilogue 재개와 result/chapter/replay 선택
- 유효 save의 새 게임 덮어쓰기 확인, 손상·구버전 migration/recovery UI
- 비정상 종료 journal, 다중 slot, cloud save
- settings/audio, package, 평가, 사람 UX 판정, push/PR/merge
- frozen V2 persistence나 historical Product/Commercial main의 재사용·수정

## 완료 검사

- save는 schema, canonical route, base world/campaign, realtime world/overlay, selected/full composed campaign hash,
  saved minute, ordered accepted commands와 final canonical hash를 결속한다. snapshot bytes나 immutable seed를
  중복 저장하지 않는다.
- capture→serialize→strict deserialize→replay가 동일 snapshot/hash/journal과 ordered transition history를
  만들고, 다음 advance/command도 uninterrupted run과 동일하다. 알 수 없는 schema/field, duplicate field,
  source/hash/sequence/minute/command shape 변조는 fail-closed한다.
- briefing/story modal, 완료 run 또는 불안정 application state는 저장하지 않는다. stable 진행을 복원할 때
  fractional frame debt, pointer/camera/selection을 되살리지 않고 paused·normal speed·no modal 정책을 쓴다.
- 빈 저장은 Continue disabled/New Game enabled다. 유효 save는 Continue enabled이며 이 scope에서는
  overwrite 방지를 위해 New Game disabled다. 손상·구버전·I/O 실패는 두 action을 비활성하고 원본 bytes를
  바꾸지 않은 채 이유를 표시한다.
- FIRST_LIGHT에서 command를 수락하고 공사 진행 중 정상 종료한 뒤 별도 fresh process의 title Continue가
  exact clock/cash/world/construction/hash를 paused 상태로 복원하며 briefing을 다시 열지 않는다.
- Debug/Release build, `./dev check`, 전체 Godot UI harness와 독립 review가 통과한다.

이 단계는 첫 stable in-progress save/Continue seam만 증명한다. 사건·장 전환·완료 저장, 전체 제품 여정,
fresh-install package, 사람 UX 품질 또는 공식 점수의 증거로 확대하지 않는다.
