# 현재 작업 범위

## 상태

**current R2 packaged lifecycle InputEvent qualification 2B2가 활성 scope다.**

[남은 작업](NEXT_TASKS.md)의 큰 packaged E2E를 가장 구현하기 쉬운 제품 lifecycle seam으로 줄인다. 동일
source의 8장 규칙·상태 진행은 기존 deterministic Core/actual-scene 검사가 소유한다. 이번 2B2는 exact
package에서만 달라질 수 있는 default scene의 title, Continue, reset, settings와 generated audio wiring을
engine `Viewport.PushInput`으로 확인한다. authored 8장을 release test harness로 복제하지 않는다.

## 결과물

- `RealtimeSliceMain.Qualification.cs` 한 release-safe dormant partial이 명시적 qualification env scenario에서만
  default scene의 실제 control에 pointer/key InputEvent를 넣고 machine-readable 결과 하나를 출력한다.
- empty New Game, in-progress Continue, completed Continue/New Game, readable-save reset confirm, settings
  apply→fresh restore 시나리오가 기존 2B1 app-owned root와 source actual-scene fixture를 재사용한다.
- 기존 `tools/r2_qualification.py`와 canonical record를 v2로 확장해 2B1 data stages와 2B2 lifecycle stages를
  한 fresh reconstruction에 결속한다. 새 packager·runner scene·별도 tool 계층은 만들지 않는다.

## 범위 밖

- authored 8장을 action-by-action으로 다시 재생하는 release test harness와 새 gameplay
- OS hardware keyboard/mouse, 전체 Godot engine `user://`, 실제 window/display와 CoreAudio device·speaker
- 모든 gameplay 상태의 live cue coverage, 사람 UX·미감·접근성, score-bearing 평가
- Developer ID·공증·공개 배포, push·PR·merge

## 완료 검사

1. qualification env가 없으면 기존 product boot에 새 marker·입력·상태 전이가 없고 invalid scenario/root는
   title bootstrap 전에 fail-closed한다.
2. strict exact candidate의 default scene을 app user argument 없이 fresh process로 실행하고, 각 lifecycle
   scenario가 실제 `Viewport.PushInput` 횟수와 기대 title/session/settings/save/audio 분류를 exact marker로
   출력한다.
3. 각 stage의 expected app-owned files/bytes, 실제 account home 두 파일, package app tree와 pinned candidate
   identity가 실행 전후 보존된다. reset stage의 한 raw sibling backup만 명시적으로 허용한다.
4. record v2가 source/package/tool/data/lifecycle identity를 strict canonical bytes로 결속하고 fresh verify와
   type/key/canonical/identity mutation을 fail-closed한다.
5. targeted build·qualification run/verify, `./dev check`와 두 bounded independent review를 통과한다.
