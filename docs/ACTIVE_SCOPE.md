# 현재 작업 범위

## 상태

**활성 구현 scope가 없다.**

standalone `FIRST_LIGHT`에서 briefing을 닫고 Core command가 하나 이상 수락된 stable 진행 상태를 정상
종료하면 canonical source identity·saved minute·ordered accepted journal·final hash를 한 current R2
save에 atomic하게 기록한다. 유효한 save는 제품 title의 `이어하기`만 활성화하고, fresh process에서
같은 시각·망·공사·현금·결정을 player-paused·normal speed·no-modal 상태로 복원한다.

저장 파일이 없으면 `새 게임`만 활성화한다. 형식 손상·지원하지 않는 schema/version·route/source/hash/
replay 불일치·I/O 실패는 원본을 바꾸지 않고 `새 게임`과 `이어하기`를 모두 차단한다. Debug/Release
build, strict Core replay suite, `./dev check`, 전체 Godot UI harness와 두 독립 code review를 통과했고,
code review의 uppercase hash canonicalization과 두 독립 markdown audit의 상태 용어 finding을 반영했다.

이 완료는 standalone `FIRST_LIGHT`의 첫 stable in-progress save/Continue seam만 증명한다. 사건·장 전환·
active story·완료 save, 누적 8장 product 새 게임, 완료 후 result/chapter/replay, overwrite/recovery UI,
settings/audio, package, production-input 직접 관찰과 사람 UX 품질은 완료되지 않았다.

다음 구현을 시작하려면 [남은 작업](NEXT_TASKS.md)에서 한 단계만 선택해 결과물, 수정 범위, 범위 밖
항목과 완료 검사를 이 문서에 먼저 적는다. backlog나 준비된 코드는 구현 권한이 아니다.
