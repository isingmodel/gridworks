# 현재 작업 범위

## 상태

**R2 개발 구조 단순화 scope가 활성화됐다.**

이 scope의 플레이어 결과는 현재 누적 4장과 입력·시간·망·표현 동작을 그대로 보존하면서, 개발자가
current R2 경로를 더 적은 권위·분기·파일 fan-out으로 이해하고 수정할 수 있게 하는 것이다. 새 gameplay
기능을 만드는 단계가 아니라 current R2의 구조와 개발 경로를 정리하는 단계다.

## 수정 가능한 범위

- current R2의 build/workspace와 개발 명령: root solution·개발 command, `game/Gridworks.Game.csproj`,
  current check project wiring
- current R2 application/controller/presentation 경계: `game/realtime/r2/`, 필요한
  `game/realtime/ui/` contract·publication seam
- current R2가 직접 사용하는 Core composition seam: `src/Gridworks.Core/Release/V3/`
- current deterministic checks와 bounded Godot checkpoint/smoke selector: `tools/Gridworks.RealtimeChecks/`,
  `tools/Gridworks.CommercialChecks/`, current R2 DEBUG harness
- 현재 사실과 개발 방법을 소유한 `README.md`, `INSTALL.md`, `docs/README.md`, `docs/NEXT_TASKS.md`,
  이 문서와 완료 이력

기존 Product/V1/V2 코드와 평가 도구는 current 개발 graph에서 분명히 분리하거나 historical로 이름 붙일
수 있지만, 이 scope에서 그 동작·evidence를 삭제하거나 다시 제품 권위로 승격하지 않는다.

## 범위 밖

- title, save/resume, 남은 4장, finale/epilogue, audio/settings의 구현
- gameplay 규칙·수치·작성 문장·G3 art와 native chapter coverage 변경
- current R2 package, signing/notarization, 평가 session, LLM judge 또는 공식 UX 점수
- V2 저장 migration, historical package 재생성, 과거 playtest/evidence 재작성
- branch 밖 사용자 소유 untracked 파일

## 단일 권위

- 망·공사·공급·열·시간·결과 규칙: 현재 strict-load된 Release V2 content와 Release V3 realtime
  schedule/world를 합성한 `RealtimeCampaignDefinition`/`RealtimeWorldDefinition`
- current native coverage: `NORTH_BANK_PROMISE`에서 끝나는 하나의 명시적 runtime capability
- 입력 뒤 상태: `RealtimeCampaignRun` snapshot과 canonical state hash
- 화면 의미: 같은 snapshot에서 만든 typed immutable presentation

직렬화 version은 loader/composition 경계에만 남기고, application과 Godot adapter가 여러 역사 runtime
세대를 동시에 소유하지 않게 한다. 구조 변경 중에도 위 권위를 복제하지 않는다.

## 완료 검사

1. current R2 build와 개발 명령이 legacy product graph와 혼동되지 않고 한 곳에서 발견된다.
2. release prefix 선택·resource load·chapter/story flow가 하나의 capability-driven 경로를 사용하며
   `NORTH_BANK_PROMISE` 뒤 authored chapter를 노출하지 않는다.
3. Godot host는 scene/input/render adapter에 가까워지고, 가능한 session·intent·chapter 정책은 plain C#
   application seam에서 deterministic하게 검사된다.
4. 새 intent/action이 handler 누락에서 성공 no-op이 되지 않고 fail-closed한다.
5. `dotnet build`, RealtimeChecks, CommercialChecks, 세 Python 회귀와 두 named checkpoint가 통과한다.
6. default no-argument fixture, `FIRST_LIGHT`, `SECOND_SOURCE`, `NORTH_BANK_PROMISE` route와 기존 canonical
   state/evidence 의미가 변하지 않는다.
7. bounded independent review의 scope-valid finding을 고친 뒤 같은 검사를 다시 통과한다.

## 아직 주장하지 않는 것

- 구조 정리가 새 chapter·title·save/package를 구현했다는 주장
- compile/test 시간이 줄었다는 성능 주장
- current R2가 판매·배포·공식 평가 준비를 마쳤다는 주장
- automated PASS가 사람 직접 플레이 또는 UX 품질을 대신한다는 주장
