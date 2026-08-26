# 현재 작업 범위

## 상태

**current R2 제품 설정 vertical slice가 활성 scope다.**

가장 구현하기 쉬운 순서로 전체 목표를 빠르게 닫기 위해, 먼저 title과 gameplay가 하나의 current R2 설정
surface와 persistence를 공유하게 만든다. 과거 V2 settings runtime은 연결하지 않고 strict codec·atomic
store 패턴만 current R2가 소유한다.

## 결과물

- current R2 전용 설정 authority 하나가 다음 값을 strict하게 소유한다.
  - 창 모드: `windowed | fullscreen`
  - UI 배율: `100 | 125 | 150 | 200`
  - `Master | Ambient | SFX` 볼륨: `0 | 25 | 50 | 75 | 100`; `0`은 mute
  - Reduce Motion
- 인자 없는 product boot만 별도 current R2 settings path를 읽고 쓴다. explicit
  `fixture | chapter | through` 경로는 설정 파일을 소유하지 않는다.
- missing 설정은 기본값을 사용한다. malformed/unsupported/read failure는 기존 bytes를 덮어쓰지 않고
  기본값과 보이는 오류를 사용한다.
- 설정 변경은 같은 directory의 temp write·flush·atomic replace가 성공한 뒤에만 runtime과 control에
  반영한다. 실패하면 이전 runtime 값과 기존 bytes를 그대로 유지한다.
- title과 gameplay에서 같은 설정 surface를 연다. 닫으면 정확한 opener focus와 열기 전 여정으로
  돌아간다.
- gameplay에서 열 때 running은 임시 pause하고 닫을 때 복구한다. 이미 player-paused이면 paused를
  유지한다. settings open/change/close는 Core snapshot/hash, journal, camera, selection과 campaign save
  ownership을 바꾸지 않는다.
- UI 배율은 기존 `RealtimeUiRoot` authority, window/audio bus는 Main의 engine seam, Reduce Motion은
  `RealtimeSession`의 기존 typed presentation source를 통해 적용한다.

## 범위 밖

- R2 ambient/SFX 재생기, cue mapping, 음원 asset, voice, music과 spatial audio
- 새 animation 또는 Reduce Motion의 사람 UX 품질 주장
- 별도 pause menu, help, key rebinding, controller와 새 input architecture
- 과거 V2 settings migration 또는 historical settings class의 current authority 승격
- campaign save/schema, Core rule/hash와 transient story cursor 변경
- package, score-bearing 평가, 사람 미감·사용성 검토와 출시 승인
- push, PR, merge

## 완료 검사

1. 임시 settings path의 실제 product scene process A에서 title pointer/keyboard 설정 열기, 모든 typed 값
   저장·적용과 opener focus 복귀를 확인한다.
2. 같은 path의 fresh process B가 title을 표시하기 전에 exact 값을 복원하고 UI 배율, audio bus,
   Reduce Motion과 window mode runtime projection을 확인한다.
3. gameplay 설정 open/close가 running과 player-paused를 각각 복구하며 Core hash·journal·camera·selection을
   바꾸지 않는지 확인한다.
4. malformed/read/write failure에서 원본 bytes와 이전 runtime 값이 보존되고 기본값 또는 보이는 오류로
   fail-closed하는지 확인한다.
5. 기존 UI layout harness로 FHD/UHD의 100/125/150/200% 회귀와 설정 surface의 FHD 100/200% bounds·focus를
   확인한다.
6. `./dev check`를 통과하고 bounded independent review 두 건에서 나온 scope-valid finding을 수정한 뒤
   current-state 문서와 완료 이력을 갱신한다.

자동 PASS는 실제 audio coverage, Reduce Motion의 사람 관찰, packaged 전체 여정, 공식 UX 품질 또는 출시
승인의 증거가 아니다.
