# 현재 작업 범위

## 상태

**current source ownership 정렬 scope가 활성 상태다.**

## 단일 결과물

개발자는 source tree에서 current product, historical implementation, deterministic evidence의 소유권을
즉시 구분하고 current world/controller/UI 변경을 한 feature slice 안에서 추적할 수 있다.

## 단일 권위

- current와 historical Core의 물리 경계: `Gridworks.Core`와 `Gridworks.LegacyCore` project root
- current world surface의 contract·renderer·scene 경계: `game/realtime/r2/world/`
- Godot controller/UI evidence의 source grouping: 기존 runner type과 feature별 partial file
- current 변경 경로: `docs/ARCHITECTURE.md`

## 범위 안

- current project에서 이미 제외된 prototype·Product·Release V1 Core source를 owning LegacyCore tree로 옮긴다.
- compile되지 않고 참조도 없는 옛 realtime world 구현을 제거하고 current world contract와 renderer를
  하나의 feature folder와 canonical 이름으로 정렬한다.
- controller와 UI evidence의 기존 registry·phase·method body를 feature별 partial file로 나눈다.
- project include, Godot scene path, DEBUG 경계와 architecture map을 새 물리 ownership에 맞춘다.

## 범위 밖

- gameplay 규칙, authored data, presentation 의미, UI layout·input, save/settings/audio와 product scene 동작
- namespace·assembly·schema·public contract 변경, 새 abstraction이나 test framework 도입
- assertion·case·phase 삭제 또는 순서·출력 marker 변경
- package·qualification identity, 외부 device·사람 gate, push·PR·merge·배포

## 완료 검사

- current Core tree에는 V2 base와 V3 realtime source만 남고 모든 legacy checker가 같은 결과로 PASS한다.
- current world scene은 단일 canonical contract/renderer slice만 참조하며 obsolete implementation reference가 없다.
- controller 31-case 순서와 UI phase 순서가 refactor 전과 같고 exact/unknown/controller/UI 검사가 PASS한다.
- Debug·Release build와 인자 없는 `./dev check`가 terminal signature를 포함한 기존 회귀를 통과한다.
- `ARCHITECTURE.md`, source-path 검사, independent review와 `git diff --check`가 새 ownership과 일치한다.
