# 현재 작업 범위

## 상태

**native end-to-end 품질 보완 scope가 활성 상태다.**

## 단일 결과물

고정된 `gpt-5.6-sol` xhigh native 전체 캠페인 관찰에서 재현된 고영향 품질 결함을 기존 current R2
구조 안에서 보완해, 플레이어가 title부터 finale·epilogue까지 더 명확하고 응집력 있게 진행할 수 있다.

## 단일 권위

- 규칙·시간·결과: 기존 `RealtimeCampaignRun`과 Release V3 Core를 유지한다.
- application 상호작용: `RealtimeSession`과 해당 flow가 한 번 소유한다.
- 화면 의미: typed presentation과 owning presenter가 소유하고 UI node는 layout·render·focus만 맡는다.
- LLM playtest는 고정된 한 native sample의 비공식 관찰이며 사람 증거나 공식 상용 UX 점수가 아니다.

## 범위 안

- `./dev play through LONGEST_NIGHT`의 title 이후 누적 8장·finale·epilogue를 native 입력으로 관찰한다.
- 재미·pacing·학습성, game UI, 시각·상호작용 design 관점에서 구체적 재현 절차와 우선순위를 기록한다.
- 자동화로 재현되거나 고정 playtest trace가 명확히 보여 주는 고영향 결함만 기존 authority에서 보완한다.
- 가장 작은 unit/story/checkpoint 검사를 먼저 실행하고, 바뀐 경계에 필요한 current R2 통합 회귀와
  고정 native 재관찰을 수행한다.

## 범위 밖

- 새 장·사건·mechanic, 전면적인 Core 밸런스 재설계, save schema 변경
- 새 art·audio 제작, 기존 visual direction 교체, 대규모 UI 재작성
- 공식 `CommercialUXProxy`, 사람 QA, 실제 speaker·hardware·접근성 승인
- package 재생성, 외부 release gate, push·PR·merge·배포

## 완료 검사

- `gpt-5.6-sol` xhigh subagent의 고정 native 전체 캠페인 관찰과 우선순위 review를 남긴다.
- 채택한 finding마다 가장 작은 결정론적 재현·회귀 검사를 통과한다.
- presentation·UI·input 변경은 필요한 named checkpoint와 `./dev check`를 통과하고, 고정 native 경로에서
  해당 장면을 다시 관찰한다.
- 실제로 바뀐 current 사실의 소유 문서만 갱신하고 이 scope를 닫는다.

## 직전 완료

`FIRST_LIGHT` 학습 루프 보완을 완료했다. native release 경로에는 공사 완료·첫 공급 시험·결과까지의
문맥형 진행 action, 종결 결과의 다시 하기·임무 나가기, 실제 다음 행동을 보여 주는 더 크고 밝은 하단
안내, 올바른 원/만 원 표기, 부분 진행 실패와 성공 payoff 요약이 있다. Core 규칙, 시간, 결과 판정과
save schema는 바꾸지 않았다.

Debug build와 누적 full realtime layout/controller harness가 PASS했다. 이후 native window에서 발견한
single-modal replay lifecycle을 보완하고 final build와 실제 재시작을 다시 확인했다. clean success,
invalid-span recovery, partial-build failure의 native LLM playtest 3회는 각각 성공 payoff, 빨간 수치 오류의
즉시 회복, `진행 1/3` 실패와 17:00 briefing 재시작을 관찰했다. 이는 human evidence가 아니다.

저장소가 소유하는 제품 목표는 실시간 8장·finale/epilogue, product save/settings/audio wiring,
internal macOS package identity와 combined 2B를 포함하는 **current R2 내부 후보**다.
[남은 구현 작업](NEXT_TASKS.md)은 현재 비어 있다.

## 다음 변경을 여는 조건

- 읽기·설명·진단은 관련 질문 소유 문서를 read-only로 확인한다.
- 파일 변경은 사용자가 명시한 결과물 하나를 이 문서에 먼저 연다.
- 외부 gate 실행, push, PR, merge와 공개 배포는 각각 사용자의 명시적 권한이 있어야 한다.
- 시작 형식, 최소 검증과 종료 checklist는 [Agent 작업 안내](AGENT_GUIDE.md)를 따른다.

`NEXT_TASKS.md`, [외부 출시 gate](RELEASE_GATES.md), 준비된 코드와 과거 PASS는 자동으로
구현·평가·배포 권한을 만들지 않는다.
