# 현재 작업 범위

## 상태

**game-design review and focused repair scope가 활성 상태다.**

## 단일 결과물

기존 8장 플레이에서 플레이어가 다가오는 문제, 선택 가능한 대응과 그 결과를 더 빠르게 연결해 읽고,
망을 보강한 이유와 다음 행동을 명확히 이해한다.

## 단일 권위

- gameplay 규칙·상태: 기존 Release V3 Core와 authored campaign을 유지한다.
- 목표·병목·행동·결과의 화면 의미와 문구: 기존 `RealtimeSession`→typed presentation→owning UI node가
  각각 한 번 소유한다.
- 장기 게임 디자인 기준: `docs/product/GAME_DESIGN_KO.md`를 유지한다.

## 범위 안

- 한 고정 LLM subagent sample로 current native 플레이의 agency, decision readability, pacing과 feedback을
  게임 디자인 관점에서 검토한다.
- 자동 증거로 확인할 수 없는 실제 플레이 관찰만 subagent review에 맡긴다.
- 재현된 고영향 결함 중 기존 mechanic·content authority 안에서 고칠 수 있는 목표, 행동 안내,
  인과 feedback과 결과 설명을 보완한다.
- 가장 작은 결정론적 contract를 먼저 추가하고 필요한 native 재관찰과 전체 회귀를 수행한다.

## 범위 밖

- 새 gameplay mechanic·chapter·event·경제·열·경로 선택 규칙·save schema·자산 제작
- 공식 점수, 사람 재미·사용성 증거, package·외부 gate·push·PR·merge·배포
- review의 취향성·저영향 지적이나 현재 권위 밖의 대규모 재설계

## 완료 검사

- subagent review의 재현 경로와 수정 우선순위를 기록한다.
- 채택한 결함을 가장 가까운 story/controller/presentation/UI contract로 고정한다.
- Debug build, relevant checkpoint/story/UI harness와 `./dev check`를 통과한다.
- 같은 고정 관점의 bounded independent re-review를 받아 scope-valid 회귀가 없음을 확인한다.
- 바뀐 current 사실의 소유 문서만 갱신하고 scope를 닫는다.

## 다음 변경을 여는 조건

- 읽기·설명·진단은 관련 질문 소유 문서를 read-only로 확인한다.
- 파일 변경은 사용자가 명시한 결과물 하나를 이 문서에 먼저 연다.
- 외부 gate 실행, push, PR, merge와 공개 배포는 각각 사용자의 명시적 권한이 있어야 한다.
- 시작 형식, 최소 검증과 종료 checklist는 [Agent 작업 안내](AGENT_GUIDE.md)를 따른다.

`NEXT_TASKS.md`, [외부 출시 gate](RELEASE_GATES.md), 준비된 코드와 과거 PASS는 자동으로
구현·평가·배포 권한을 만들지 않는다.
