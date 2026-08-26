# 현재 작업 범위

## 상태

**current R2 basic product audio vertical slice가 활성 scope다.**

가장 구현하기 쉬운 다음 단위로, 외부 음원 자산이나 새 subsystem 없이 title/gameplay에 하나의
deterministic ambient loop와 세 typed live SFX를 연결한다. cue 의미는 existing `RealtimeSession`, 실제
Godot playback은 Main 아래의 audio node 하나가 소유한다.

## 결과물

- 새 `RealtimeAudio` node 하나가 고정 생성한 22,050Hz mono PCM16 ambient loop와
  `Breaker | Energize | Outage` non-loop SFX를 재생한다. 별도 audio asset/scene/manifest는 만들지 않는다.
- title부터 ambient를 한 번 시작하고 session 교체와 settings open/close에서 중복 시작하지 않는다.
- `RealtimeSession`이 한 public live operation당 cue를 최대 하나만 선택한다.
  1. `ThermalEmergencyEntered | ThermalProtectiveTrip` → `Outage`
  2. 그 외 `ConstructionCompleted | ThermalEmergencyCleared | ThermalRecovered` → `Energize`
  3. 그 외 accepted `OrderNode | OrderLine` → `Breaker`
  4. 나머지와 rejected command → cue 없음
- 내부 minute chunk 여러 개를 처리해도 aggregate transition batch에서 `Outage > Energize > Breaker` 우선순위로
  한 번만 발행한다. initial bootstrap, save replay와 Resume history는 과거 cue를 발행하지 않는다.
- audio node는 `Ambient | SFX` bus만 사용한다. volume/mute는 이미 구현된 Main의
  `Master | Ambient | SFX` settings projection을 그대로 소비하고 새 volume authority를 만들지 않는다.
- cue 요청과 playback은 Core state/hash, journal, presentation, interaction과 save/settings bytes를 바꾸지
  않는다.

## 범위 밖

- 녹음·third-party 음원, weather별 ambience, music, voice, spatial audio, polyphony와 crossfade
- historical V2 audio runtime 연결 또는 과거 audio/settings class의 current authority 승격
- 새 Core transition, gameplay rule, settings field/schema와 persistence 변경
- speaker/device 출력, loudness·음질·loop seam의 사람 청감 평가와 accessibility 품질 주장
- package, fresh-install candidate, score-bearing 평가와 출시 승인
- push, PR, merge

## 완료 검사

1. pure cue selector가 accepted/rejected order, transition 조합과 `Outage > Energize > Breaker` 우선순위를
   고정하고 operation당 최대 한 cue인지 확인한다.
2. actual `RealtimeSliceMain` scene에서 audio node, PCM16/22,050Hz/mono/loop shape와 Ambient/SFX bus,
   ambient one-start를 확인한다.
3. pure selector가 단독 construction completion의 `Energize`를 확인하고, 실제 construction checkpoint는
   accepted order의 `Breaker` 뒤 completion과 emergency가 섞인 batch의 최고 우선순위 `Outage`를 한 번만
   받으며 canonical Core/journal이 audio 때문에 달라지지 않는지 확인한다.
4. fresh product Resume가 replay history SFX를 발행하지 않고 기존 settings restore의 bus mute/linear 값을
   그대로 소비하는지 확인한다.
5. `./dev check`를 통과하고 bounded independent review 두 건의 scope-valid finding을 수정한 뒤 current
   문서와 완료 이력을 갱신한다.

headless PASS는 stream shape·routing·play request의 증거일 뿐 실제 speaker 출력, 음질, 사람 UX 또는
출시 품질의 증거가 아니다.
