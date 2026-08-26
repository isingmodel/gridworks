# 현재 작업 범위

## 상태

**활성 구현 scope가 없다.**

current R2 기본 제품 오디오 vertical slice는 완료·검증·문서화됐다. 결과와 증거 상한은
[완료 이력](archive/COMPLETED_HISTORY.md)의 `UX-R2.21`이 소유한다.

다음 구현 전에 이 파일에 하나의 결과물, 범위 밖 항목과 결정론적 완료 검사를 먼저 적어 active scope를
명시적으로 열어야 한다. [남은 작업](NEXT_TASKS.md)은 후보와 순서를 설명하지만 그 자체로 구현 권한을
만들지 않는다.

## 닫힌 범위의 경계

- source-tree current R2에는 generated ambient와 live Breaker/Energize/Outage cue가 연결됐다.
- cue 의미는 Session, PCM/playback은 `RealtimeAudio`, volume/mute는 기존 Main settings projection이 소유하고
  Main은 typed C# event만 전달한다.
- bootstrap·save replay·fresh Continue는 과거 SFX를 발행하지 않고 ambient는 title부터 한 번만 시작한다.
- `./dev check`, 두 실제 checkpoint와 두 독립 review 뒤 scope-valid finding을 모두 닫았다.
- headless PASS는 stream·routing·play request의 증거이며 speaker 출력, 음질·loop seam, 사람 UX,
  packaged coverage 또는 출시 품질의 증거가 아니다.
- push, PR, merge는 수행하지 않았다.
