# Gridworks — 실시간 물리 세계 전면 개편 구현 계약

> 문서 상태: **사용자가 승인한 전면 개편의 구현 권위 · R0 완료 · R1 활성**
>
> 기준일: **2026-08-19**
>
> 대상 브랜치: **`codex/total-revision`**
>
> 완료 판정: §15.1의 기계적·독립 검토 gate를 모두 충족해야
> `TotalRevisionImplementationComplete`라고 부른다. §15.2의 사람·외부·서명 gate는 별도의
> `HumanValidatedInternalCandidate`/출시 판정이며, 수집되지 않은 사람 증거를 구현 실패나 성공으로
> 바꾸어 말하지 않는다.

이 문서는 단계 G에서 동결된 상용 v2를 물리적 전력설비가 보이는 **실시간 2D 운영 게임**으로
전면 개편하는 새 구현 계약이다. 현재 사용자 지시가 이전의 `결정 경계형 진행`, 오른쪽 고정 작업
패널과 720p 기준을 명시적으로 대체한다. 기존
[상용 v2 구현 계약](COMMERCIAL_2D_IMPLEMENTATION.md)은 회귀와 저장 이관의 입력이지 새 제품의
권위가 아니다.

한 번에 이 문서의 단계 하나만 연다. 각 단계는 코드·데이터·자동검사·native 증거·독립 검토와
현재 문서 갱신을 닫은 뒤 다음 단계로 간다. 계획에 적힌 다음 단계의 schema, 빈 UI나 placeholder
system을 미리 만들지 않는다.

---

## 1. 사용자 요구와 최종 제품 결과

이번 개편은 다음 일곱 요구를 하나의 제품 결과로 묶는다.

1. 두 시각 비평 package의 Markdown과 모든 이미지를 근거로 아트를 전면 재설계한다.
2. modal, button, click hierarchy와 스크롤 중심 오른쪽 패널을 바닥부터 다시 설계한다.
3. 다가올 사건과 공사 완료·보호정지를 확인하는 **수평 사건 지평선**을 제공한다.
4. 1280×720을 품질 기준으로 삼지 않고 **1920×1080과 3840×2160**을 정식 지원한다.
5. 선로 도체뿐 아니라 **변전소 주기기와 전신주 접속부의 과부하·보호정지**가 실시간으로
   발생하고 설명돼야 한다.
6. `운영안 승인`으로 여러 국면을 한 번에 넘기는 turn형 게임을 폐기하고, 일시정지·배속 가능한
   결정론적 실시간 simulation으로 전환한다.
7. 이 전환에 필요한 접근성, 저장 이관, 성능, 밸런스와 제작 pipeline을 함께 고친다.

최종 화면에서 플레이어는 원형 node와 발광 직선이 아니라 도로·강·건물 사이에 놓인 실제 전신주,
변전소 compound와 가공 도체를 본다. 시간은 일시정지하지 않는 한 계속 흐르고, 작성된 수요 변화,
기상, 계획정지와 재난이 수평 시간축에서 현재 망을 향해 다가온다. 플레이어는 언제든 멈춰 망을
감사하고 공사를 계획할 수 있지만, `다음 국면` 버튼으로 세계를 진행시키지는 않는다.

다음은 유지한다.

- 한 도시와 같은 망·자금·약속이 이어지는 여덟 임무
- 보이는 격자 없는 고정소수점 자유 배치와 비접속 교차
- 안전 의무, 선택 가능한 도시 약속과 사실 기반 결과
- 발전원→전체 경로→첫 병목의 설명 가능성
- Core 규칙과 Godot 표현의 분리

다음은 이번 범위에서 교체한다.

- 결정 창구와 `ApproveDecisionWindow`
- 원형 node와 graph edge가 기본 세계인 지도
- 모든 정보와 조작을 한 세로 scroll에 쌓는 오른쪽 panel
- 동적으로 늘어나는 비슷한 사각 button과 중첩 확인창
- 1280×720 중심 layout·asset 기준

---

## 2. 비평 종합: 채택·거부·사용자 요구 추가

두 report는 같은 핵심 논지를 반복하므로 독립된 두 표의 통계적 합의로 세지 않는다. photoreal
mockup과 기술 도식은 방향 참고이며 production asset이나 실제 UI 증거가 아니다.

| 구분 | 항목 | 이 계약의 판정 |
|---|---|---|
| 채택 | 기본 세계는 물리 설비, graph는 선택 기반 분석 도구 | **채택** |
| 채택 | 고정 북향 oblique 2.5D, authored sprite와 procedural conductor의 혼합 | **채택** |
| 채택 | 설비 class를 색이 아니라 silhouette·footprint·hardware로 구분 | **채택** |
| 채택 | 색+형태+pattern+icon+문장으로 상태 표현 | **채택** |
| 채택 | 자유좌표·선택 ID·배치 적법성·급전·열 계산은 Core 권위 | **채택** |
| 채택 | 한 mission vertical slice 뒤 전체 campaign rollout | **채택** |
| 조건부 채택 | 기존 `CommercialMapView`를 helper로 나누는 저위험 이행 | 첫 art slice에만 사용하고, 새 world renderer gate 뒤 교체 여부 결정 |
| 거부 | photoreal concept image를 production fidelity나 asset geometry로 사용 | **거부** — 생성 artifact와 잘못된 설비 형태가 있음 |
| 거부 | 기존 오른쪽 panel·200 px scroll·고정 승인 CTA를 그대로 보존 | **거부** — 정보 보존 원칙만 남기고 구조는 교체 |
| 거부 | 구현되지 않은 송전탑·개폐기·철거·손상 상태를 아트가 암시 | **거부** |
| 거부 | 분석선·비·위험 pattern이 HUD와 panel 위를 가로지르는 compositing | **거부** |
| 거부 | Factorio·Frostpunk image, silhouette, UI frame 또는 icon의 직접 재사용 | **거부** |
| 사용자 추가 | 수평 미래 사건 지평선 | **필수** |
| 사용자 추가 | 결정론적 실시간 clock, pause, 1×/2×/4× | **필수** |
| 사용자 추가 | 전신주 접속부·변전소·선로의 시간 기반 과부하와 보호정지 | **필수** |
| 사용자 추가 | FHD·4K native layout, density asset, performance gate | **필수** |
| 사용자 추가 | modal/button/click hierarchy와 scroll의 전면 재설계 | **필수** |

비평 이미지 가운데 물리 세계 구도와 상태 도식은 방향만 채택한다. 분석 overlay가 작업 panel을
침범하거나 비가 UI 위까지 이어지는 mockup, 작은 text, 겹치는 tooltip, 출처가 불명확한 사진과
상표가 보이는 reference board는 오히려 금지 사례다. `temp/` 또는 `critics/`의 어느 이미지도
runtime·package·marketing 산출물에 들어가지 않는다.

---

## 3. 비협상 제품 원칙

1. label과 분석 overlay를 감춰도 발전원·전신주·변전소·도체·수요시설과 실제 route가 보인다.
2. 정보 위계는 `물리 세계 → 선택 대상 → 분석` 순서다.
3. simulation은 frame rate, renderer, UI focus와 wall-clock 시간에 의존하지 않는다.
4. 예정 사건은 숨은 주사위가 아니다. 현재 공개된 사건의 시각·범위·영향은 사건 지평선과 지도에서
   같은 typed forecast를 사용한다.
5. 정상 설비는 재질색을 유지한다. cyan은 통전·선택 분석, amber는 계획·결정, red는 실제
   비상·실패에 제한한다.
6. 과부하는 실제 섭씨나 상세 보호계전을 가장하지 않는다. 선로는 도체, 변전소는 주기기,
   전신주는 접속부의 **계획상 운전 한계**를 나타낸다.
7. 한 click 또는 key action은 command 하나와 accepted/rejected 결과 하나만 만든다.
8. 중요한 정보와 주 행동은 scroll 위치에 의존하지 않는다.
9. modal은 시간을 막는 드문 surface다. 정보 열람이나 정상 운영 갱신은 modal이 아니다.
10. 장식 asset의 보이는 점유와 authoritative collision이 모순되면 안 된다.
11. 상태는 색만으로, 사건은 소리만으로, 진행은 animation 속도만으로 전달하지 않는다.
12. 보고서 mockup의 아름다움이나 screenshot pixel equality를 자동 완료 기준으로 만들지 않는다.

---

## 4. 실시간 simulation 계약

### 4.1 시간의 단일 권위

새 Core는 **정수 simulation minute**를 사용한다. 한 tick은 정확히 게임 시간 1분이다. Core API는
`AdvanceTicks(count)`처럼 정수 tick 수만 받고 Godot의 `_Process(delta)`, 실제 시계, 화면 refresh
rate를 참조하지 않는다. Game은 설정된 배속의 accumulator에서 완성된 tick만 Core에 전달한다.

화면 배속은 다음 네 상태다.

- `일시정지`
- `1×`
- `2×`
- `4×`

각 배속의 real-second당 tick 수는 한 runtime tuning authority가 소유하며 문구·scene에 복제하지
않는다. 배속은 세계 규칙이 아니라 tick 전달 속도다. 같은 timestamp에 같은 command를 적용한
실행은 배속과 frame chunking이 달라도 같은 canonical state hash를 만든다.

### 4.2 한 tick의 고정 순서

simulation minute `M`의 경계는 항상 다음 순서로 한 번 처리한다.

1. `M`에 완공되는 공사를 commissioning한다.
2. `M`에 시작·종료되는 authored 사건, 수요, 기상, 사용불가를 적용한다.
3. 현재 가용 설비와 공개 우선순위로 공급 경로를 결정한다.
4. 선로 도체·변전소 주기기·전신주 접속부의 사용량을 합산한다.
5. 비상 노출, 보호정지와 냉각 상태를 갱신한다.
6. 수요 공급, 첫 병목, 의무·약속, 경고와 결과 event를 확정한다.
7. immutable presentation snapshot과 canonical hash를 만든다.

같은 시각의 공사 완공은 사건보다 먼저 적용한다. countdown이 0이 된 사건은 이미 처리된 것이므로
그 뒤 입력으로 소급 회피할 수 없다. 자동 경계 처리는 `AdvanceTo(targetMinute)`만 소유한다.
플레이어 입력은 `ApplyCommand(currentMinute, sequence, command)`로 현재 minute에 **동기적으로**
적용되며, 시간을 전진시키지 않고 accepted/rejected 결과와 새 immutable snapshot/canonical hash를
한 번에 반환한다. `sequence`는 같은 minute 안에서 단조 증가하고 command journal의 total order를
고정한다. pause 중에도 같은 API를 사용하므로 명령을 숨은 다음 tick까지 보류하지 않는다. minute
`M`의 자동 경계가 이미 처리된 뒤 들어온 command는 그 경계를 되돌리지 않고 `M`의 사후 상태에
적용된다.

### 4.3 pause와 자동 pause

- 플레이어는 언제든 pause하고 건설·분석·timeline을 조작할 수 있다.
- inspector, analysis overlay와 build palette를 여는 것만으로 자동 pause하지 않는다.
- chapter briefing, 처음 발생한 critical incident, 되돌릴 수 없는 복구와 campaign result만
  명시적으로 auto-pause할 수 있다.
- auto-pause surface에는 `왜 멈췄는지`, `현재 시각`, `재개 후 다음 사건`을 표시한다.
- 같은 event는 한 번만 auto-pause한다. 이미 읽은 반복 경고는 banner·timeline marker로 갱신한다.
- blocking surface가 열려 있으면 clock이 멈췄다는 사실을 화면과 접근성 이름으로 알린다.

### 4.4 campaign과 사건

여덟 임무와 작성 순서는 유지하되 `decisionWindow`와 `operatingPhase 승인`을 실시간 event schedule로
바꾼다. 한 chapter는 다음을 소유한다.

- 시작·종료 simulation minute
- 알려진 수요 변화와 사건의 공개 시각·발생 시각·종료 시각
- construction deadline과 promise decision deadline
- authored unavailable asset/risk area
- 안전 의무, 도시 약속과 결과 fact template
- story trigger와 auto-pause 중요도

사건은 정확한 ID, 종류, 공개 범위, 관련 설비·수요, severity와 정렬 priority를 가진다. 같은 minute
event는 데이터 순서가 아니라 명시 priority와 안정 ID로 정렬한다. 숨은 확률 고장은 이번 범위에
넣지 않는다.

기존 `운영안 승인`은 제거한다. 안전 의무는 실제 시간에 평가되고 미공급이 되면 incident가 된다.
campaign 데이터가 정한 grace 또는 deadline이 끝나면 실패·결과가 확정된다. 예측이 나쁘다는 이유로
시간 진행을 막는 대신, pause·forecast·복구 수단으로 미리 고칠 기회를 준다.

### 4.5 건설

한 공사반과 동시에 active인 project 한 개라는 bounded 범위는 유지한다.

- 초안 작성은 pause와 running 양쪽에서 가능하다.
- `공사 시작`은 비용을 지불하고 현재 minute에서 공사를 시작한다.
- 공사 진행은 simulation tick에 따라 이어지며 별도 `AdvanceConstruction` 버튼을 사용하지 않는다.
- active project의 완료 시각과 관련 사건까지의 여유를 사건 지평선에 표시한다.
- active project가 있는 동안 다음 초안은 비교용으로 보관할 수 있지만 두 번째 공사를 발주하거나
  숨은 queue를 만들 수 없다.
- 완료 minute에 원자적으로 commissioning되기 전에는 공급에 참여하지 않는다.
- 최근 공사 복구는 해당 공사 이후 진행된 시간·event 결과까지 되돌릴 수 있으므로 확인 surface가
  사라질 세계·자금·시각·사건·열 상태를 정확히 제시해야 한다.

여러 공사반, 자재 inventory, worker pathfinding과 construction vehicle simulation은 제외한다.

---

## 5. 과부하·보호정지와 설비 밸런스

### 5.1 모든 제한 설비에 적용

다음 세 종류가 동일한 typed 부하 결과에 참여한다.

| 게임 설비 | 사용자 용어 | 실제로 대표하는 것 |
|---|---|---|
| 선로 구간 | 선로 열여유 | 도체의 계획상 열 한계 |
| 배전 변전소 | 변전소 주기기 열여유 | 변압기·모선·인출부 중 제한 요소 |
| 전신주 | 전신주 접속부 열여유 | 단자·퓨즈·개폐기·분기 접속 장치 |

각 class는 `continuousLimitKw`, `emergencyLimitKw`, `emergencyAllowanceMinutes`와
`cooldownMinutes`를 가진다. 정확한 실행값은 world v3 한 곳이 소유한다.

### 5.2 실시간 상태 전이

공급 경로 선택은 단순히 먼저 찾은 경로의 사용량을 나중에 버리는 방식이 아니다. Core는 매 경계마다
load를 `obligation priority → authored dispatch priority → stable load ID` 순으로 정렬한다. 한 load의
후보 경로는 기존 결정론적 cost tuple과 전체 edge/node ID tie-break로 정렬한다. 후보 하나를
**tentative allocation**하고 모든 선로·중간 전신주·변전소의 현재 가용성과 비상 한계를 함께
검사한다. 초과하면 그 후보가 더한 사용량을 전부 rollback한 뒤 다음 후보를 시도한다. 채택된 경로만
사용량·노출·진단에 남는다. 어떤 후보도 못 쓰면 사용량을 남기지 않고 exact first-blocker와 부족량을
반환한다. 이 순서와 rollback은 forecast와 실제 실행이 같은 helper를 사용한다.

- `used <= continuous`: 연속 운전. 남아 있는 비상 노출은 데이터가 정한 정수 속도로 회복한다.
- `continuous < used <= emergency`: 비상 운전. 매 tick 비상 허용시간을 소비한다.
- `used > emergency`: 해당 경로는 부적격이며 첫 병목·부족량을 반환한다.
- minute `M`의 공급으로 비상 허용시간을 모두 소비한 설비는 `M` 결과에는 비상 운전으로 기록하고,
  `M+1`의 **공급 경로 선택 전**에 `ProtectiveOutage`가 되어 경로에서 제외된다. 0분 상태가 공급에
  한 번 더 참여하거나 같은 minute 결과를 소급 변경하지 않는다.
- 보호정지 동안 공급에 참여하지 않고 정해진 냉각시간 뒤 자동 복귀한다.
- authored outage와 보호정지가 겹치면 더 늦은 가용 시각을 사용하고 원인 둘을 모두 보존한다.

UI는 `접속부 2.8 / 연속 2.5 / 비상 3.2 MW · 비상 18분 남음 · 이후 40분 보호정지`처럼
계획 abstraction을 말한다. 섭씨온도, 실제 relay curve, 풍속별 동적 정격, 화재와 영구 손상을
표시하지 않는다.

### 5.3 forecast

Core는 현재 world, active construction과 공개 schedule만 사용해 bounded event horizon까지 순수
forecast를 만든다. forecast는 다음을 반환한다.

- 사건별 예상 공급원과 route
- 첫 병목과 부족량
- 각 제한 설비의 연속/비상/보호정지 예상 구간
- 공사 완료가 사건 전인지 후인지
- 안전 의무·도시 약속의 예상 상태

renderer와 timeline은 이 값을 재계산하지 않는다. 초안 비교는 초안을 가상 commissioning한 같은
evaluator를 사용하고 `예상`임을 계속 표시한다.

### 5.4 전면 우월 asset 금지

과부하 표현만 추가하고 큰 설비가 항상 정답인 상태를 남기지 않는다.

- 일반 전신주: 작고 싸고 빠르지만 접속 수와 연속·비상 한계가 작다.
- 보강 전신주: 큰 분기에는 유리하지만 더 비싸고 느리며 footprint가 크다.
- 소형 변전소: 빠르고 공간 효율이 좋지만 service·connection·열 한계가 작다.
- 대형 변전소: 큰 결절에 유리하지만 비용·공기·점유와 단일고장 영향이 크다.
- 일반선과 보강선도 비용·공기·경간·열여유 가운데 하나가 모든 면에서 우월하면 안 된다.

정적 dominance check는 같은 category의 두 class를 비용, 공기, footprint, 연결 수, service radius,
연속·비상 한계와 최대 경간의 방향에 맞춰 비교한다. 한 class가 모든 이점에서 같거나 크고 모든
비용에서 같거나 작으며 적어도 하나가 엄격히 좋으면 fixture를 거부한다.

campaign witness는 다음을 각각 적어도 한 번 포함한다.

- 선로 도체가 첫 병목인 경우
- 변전소 주기기가 첫 병목인 경우
- 전신주 접속부가 첫 병목인 경우
- 작은 설비가 비용·기한 때문에 유리한 유효안
- 큰 설비가 접속·열여유 때문에 유리한 유효안
- 큰 단일 결절보다 작은 독립 회랑이 사고에서 유리한 유효안

수치는 LLM 목표점수 맞춤이나 무제한 탐색으로 자동 튜닝하지 않는다. 구현 gate에서는 bounded
authored witness와 frontier 검사를 사용하고, 전면 우월하게 느껴지는지는 §15.2 사람 플레이에서
별도로 확인한다.

---

## 6. UX를 바닥부터 다시 만드는 계약

### 6.1 interaction state

화면은 다음 세 축을 섞지 않는다.

```text
Simulation: Running | PlayerPaused | AutoPaused | Ended
Tool: Inspect | BuildNode | BuildLine | MoveDraft | Analysis
Surface: World | Inspector | Timeline | Drawer | BlockingModal
```

한 번에 tool 하나, selection 하나, blocking modal 하나만 존재한다. tool을 바꿔도 simulation이
암묵적으로 재개·정지하지 않는다. view state는 save command journal과 simulation hash에 들어가지
않지만 active draft와 발주 결과는 Core state다.

### 6.2 click 우선순위

pointer hit는 다음 순서로 한 owner가 해결한다.

1. blocking modal과 shell
2. HUD·timeline·inspector·command dock
3. 현재 draft handle과 placement ghost
4. 선택 설비의 명시 action handle
5. world의 설비·도체 candidate
6. 빈 지형의 cursor·선택 해제

겹친 world candidate는 안정 ID 순서와 화면 거리로 정렬하고 기존처럼 순환 가능해야 한다. overlay,
weather와 decorative sprite는 hit target이 아니다. 투명 pixel을 hit test로 쓰지 않는다.

- 왼쪽 click/`Enter`: 보이는 후보 하나를 선택 또는 확정
- 오른쪽 click/`Backspace`: 현재 초안의 마지막 단계 되돌리기
- `Esc`: blocking subpage 닫기 → active draft 취소 확인 → tool을 Inspect로 → pause menu 순서
- `Tab / Shift+Tab`: 표준 UI focus
- `Q / E`: 겹친 world candidate 순환
- `Space`: pan modifier이며 text 입력 중에는 simulation control로 쓰지 않음

### 6.3 기본 화면 위계

```text
┌──────────────────────────────────────────────────────────────┐
│ 도시 시각 · 공급 · 자금                  pause / 1× / 2× / 4× │
├──────────────────── 수평 사건 지평선 ─────────────────────────┤
│                                                        ┌─────┐│
│                   물리적 청류시 세계                   │선택 ││
│                                                        │검사 ││
│                                                        └─────┘│
├──────────── build / analysis / context command dock ──────────┤
└──────────────────────────────────────────────────────────────┘
```

- 지도는 기본 작업면이며 permanent right rail을 강제하지 않는다.
- inspector는 선택이 있을 때만 열리는 고정 폭 drawer다. 닫아도 selection은 유지할 수 있다.
- build tool과 현재 맥락의 주 action은 수평 command dock에 둔다.
- objective·현재 critical warning·다음 사건은 HUD의 bounded 영역에 두고 긴 본문을 밀어내지 않는다.
- 긴 수치 비교는 `망 분석` drawer의 tabbed table로 열며 world와 주 action을 동시에 축소하지 않는다.

### 6.4 scroll 정책

기존처럼 서로 다른 목적의 정보와 button을 하나의 긴 세로 scroll에 넣지 않는다.

- inspector 첫 화면은 identity, 현재 상태, 핵심 수치, 첫 병목과 context action만 담고 scroll하지 않는다.
- 상세 `경로`, `열`, `예측`, `이력`은 tab으로 분리한다.
- 표가 viewport를 넘을 때만 표 자체가 독립 scroll을 가진다. header·row label·현재 selection은 고정한다.
- story는 한 화면 분량의 page로 편집한다. 긴 내용을 작은 상자 안에서 읽히지 않는다.
- horizontal event navigation은 timeline에서만 사용하고 mouse wheel 기본 방향을 훔치지 않는다.
- focus-follow는 보조 안전망이지 숨은 button을 찾게 하는 기본 탐색법이 아니다.

### 6.5 button system

모든 button은 다음 다섯 종류 중 하나다.

| 종류 | 용도 | 표현 |
|---|---|---|
| Primary | 현재 surface의 되돌리기 어려운 주 행동 하나 | 채운 accent, 동사+대상 |
| Secondary | 열기·비교·보조 행동 | 낮은 대비 outline |
| Tool toggle | 배치·분석 mode | 선택 상태가 형태와 문장으로 유지 |
| Destructive | 삭제·복구·새 게임 | 위험색, 결과 preview 뒤 확인 |
| Icon utility | pause·배속·닫기처럼 보편적 짧은 조작 | tooltip·접근성 이름 필수 |

- 한 surface에 Primary는 하나만 둔다.
- `확인`, `진행`, `예`처럼 대상 없는 label을 금지한다.
- disabled button은 opacity만 낮추지 않고 바로 옆에 이유와 필요한 다음 행동을 표시한다.
- hover, pressed, focus, selected, disabled가 색 외 outline·shape로 구분돼야 한다.
- FHD 논리 좌표에서 pointer target은 최소 40×40, 중요한 action은 최소 44×44를 사용한다.
- icon-only construction tool은 text label을 함께 보이거나 사용자가 항상 펼칠 수 있어야 한다.

### 6.6 modal 정책

modal을 허용하는 경우는 네 가지뿐이다.

1. chapter 시작/종료의 새로운 사람·도시 변화
2. 저장을 폐기하는 새 게임
3. 최근 공사·chapter 복구처럼 되돌릴 수 없는 상태 변경
4. fatal save/load 오류

일반 사건 경고, 공급 변화, 공사 완료, 과부하 시작·복귀와 선택 상세는 banner, timeline marker,
world cue와 inspector를 사용한다. nested modal은 금지한다. modal을 닫으면 이전 focus와 simulation
pause 이유를 복원한다.

### 6.7 접근성

- 모든 world mode와 timeline은 keyboard만으로 도달 가능해야 한다.
- timeline은 동일 내용을 시간순 linear list로 읽는 접근성 view를 제공한다.
- screen reader 이름은 현재 시각, simulation 상태, 선택 설비, 후보 수, 첫 거부 이유와 다음 사건을
  포함한다.
- 상태는 색+선형+pattern+icon+text 가운데 최소 세 channel을 사용한다.
- reduce-motion에서는 rain, heat shimmer, energized flow, warning pulse를 정지 cue로 바꾼다.
- 일시정지·배속·auto-pause는 소리 없이도 보인다.

---

## 7. 수평 사건 지평선

수평 bar의 제품 이름은 **사건 지평선**이다. 단순 chapter tab이나 turn 목록이 아니라 현재 시각을
기준으로 미래 사건과 진행 중 공사를 함께 보여주는 시간축이다.

### 7.1 표시 lane

1. 수요·도시 약속·deadline
2. 날씨·범람·계획정지·설비 사용불가
3. active construction와 예상 commissioning
4. 비상 노출·예상 보호정지·복귀

각 marker는 `종류 icon + 이름 + 발생 시각/남은 시간 + severity`를 가진다. 현재 시각 cursor는
고정되고 시간이 흐르면 marker가 왼쪽으로 이동한다. 겹친 marker는 숨기지 않고 cluster를 만들며
cluster에 포함된 수를 표시한다.

### 7.2 조작

- click/keyboard 선택은 simulation을 바꾸지 않고 해당 event의 map asset·load와 inspector를 연다.
- double click은 필수 조작으로 사용하지 않는다.
- drag 또는 명시적 이전/다음 button으로 horizon을 탐색한다.
- horizon preset은 `다가오는 6시간 / 24시간 / 7일`처럼 데이터 범위 안에서 제공한다.
- `Home`은 현재 시각, `[`/`]` 또는 접근 가능한 button은 이전/다음 event로 이동한다.
- draft가 바뀌면 forecast marker를 같은 Core evaluator에서 다시 만들고 `현재 초안 기준 예상`이라고
  표시한다.

### 7.3 정보 규칙

- exact time이 공개되지 않은 authored event는 공개 범위만큼 window로 표시한다.
- 이미 발생한 사건은 짧은 history 영역으로 이동하고 현재 화면을 계속 차지하지 않는다.
- safety-critical event, construction deadline과 forecast protective trip은 다른 icon·shape를 쓴다.
- timeline 자체가 modal이나 긴 tooltip을 띄우지 않는다.
- FHD에서 최소 두 lane과 가장 가까운 사건 label이 항상 보이고, 나머지는 expand할 수 있다.

---

## 8. FHD·4K와 responsive layout

### 8.1 지원 기준

정식 검수 해상도는 다음이다.

- `1920×1080` Full HD
- `2560×1440` 중간 density 회귀
- `3840×2160` 4K UHD

1280×720은 필요하면 crash·최소 fallback 회귀로 남길 수 있으나 visual quality나 정보 위계 완료
증거로 사용하지 않는다. 정식 화면비는 16:9다. 다른 화면비는 letterbox가 아니라 responsive map
확장으로 처리하되 별도 지원 주장은 실제 확인 뒤에만 한다.

### 8.2 density와 UI scale

- layout은 FHD logical unit으로 작성하고 출력 density와 사용자 UI scale을 분리한다.
- 4K는 FHD layout을 절반 크기로 그리는 것이 아니라 동일한 논리 정보량을 2× density로 선명하게
  렌더링한다.
- settings v4는 `100 / 125 / 150 / 200%` UI scale을 지원한다.
- text, focus outline, hit target과 status marker는 density-aware screen size를 유지한다.
- world sprite는 overview/mid/close LOD와 1×/2× density resource 또는 충분한 고해상도 source를
  가진다.
- UI icon은 SVG/SDF 또는 lossless density variant를 사용한다. 저해상도 raster 확대를 금지한다.
- texture filtering, mipmap과 pixel snapping은 asset category별 import preset이 소유한다.

노출하는 조합은 일부만 편의상 지원하지 않는다. 자동 layout gate는 아래 **여덟 조합 전부**를
검사한다.

| 출력 | UI scale |
|---|---|
| 1920×1080 | 100 / 125 / 150 / 200% |
| 3840×2160 | 100 / 125 / 150 / 200% |

각 조합에서 HUD·사건 지평선·지도·inspector·command dock bounds, 40/44 px target, label clipping,
focus reachability와 한 primary-action 규칙을 확인한다. 2560×1440은 100/200% 중간 density 회귀다.
FHD↔4K fullscreen/windowed 전환은 100%와 200%에서 각각 왕복해 focus, camera center, 선택과 scale을
확인한다. 구현이 이 matrix를 통과하지 못하는 scale을 settings에 노출하는 것은 금지한다.

### 8.3 layout 불변식

- 사건 지평선, speed control, current critical warning과 command dock은 어떤 지원 조합에서도
  서로 겹치지 않는다.
- inspector가 열려도 지도 interaction 영역은 FHD 너비의 절반 아래로 줄지 않는다.
- story, setting과 confirmation은 화면 밖으로 나가지 않고 nested scroll을 만들지 않는다.
- 4K에서 text가 물리적으로 작아지지 않고 FHD에서 button label이 잘리지 않는다.
- OS fullscreen·windowed 전환 뒤 camera center, UI focus와 scale을 복원한다.

### 8.4 성능 gate

지원 기준 Mac model, OS, GPU와 display mode를 증거에 기록한다. 최대 도시 density, storm,
analysis overlay, inspector와 사건 지평선을 함께 켠 worst-case에서 FHD와 4K 각각 안정적인 60 fps를
목표로 하고 p95 frame time 16.7 ms 이하를 gate로 삼는다. 환경이 이 기준을 못 만족하면 원인을
기록하고 asset batching, LOD, effect density를 먼저 줄인다. simulation tick 결과를 생략하거나
규칙을 renderer frame에 맞추지 않는다.

---

## 9. 아트 방향과 production pipeline

### 9.1 최종 정체성

시각 정체성은 **Operational Industrial Realism**으로 고정한다.

> 차갑고 습한 지방도시의 낡았지만 작동하는 22.9 kV급 지역 기반시설. 기능 중인 설비와 사람의
> 공간에서만 절제된 따뜻한 빛이 보인다.

Factorio에서는 entity silhouette와 현장 interaction 원칙만, Frostpunk에서는 환경·조명·UI가 한
주제를 말하는 coherence만 참고한다. 두 게임의 palette, icon, frame, building, screenshot과
composition을 복제하지 않는다.

### 9.2 camera·빛·재질

- north-up, 회전 없는 near-orthographic oblique 2.5D
- 고정 camera angle, 고정 northwest key light, southeast authored shadow
- concrete, galvanized/painted steel, dark conductor, gravel yard, wet asphalt, low-contrast vegetation
- powered window·yard light·warning beacon만 국소 warm light
- normal equipment body는 cyan이 아니라 재질색
- photoreal texture 혼합과 과도한 rust·bloom·screen darkness 금지

### 9.3 asset 제작

설비는 가능한 한 동일 Blender render rig 또는 하나의 hand-authored 2D 규칙에서 만든다.

1. class별 scale sheet와 silhouette 승인
2. fixed orthographic camera·light로 2× working resolution render
3. transparent sprite, shadow, selection/status mask 분리
4. overview/mid/close LOD와 1×/2× density export
5. Godot import preset 적용
6. visual catalog 등록
7. source·tool·author·license·SHA-256을 `ASSET_MANIFEST.md`에 기록

AI 생성 이미지는 단순 style exploration뿐 아니라 이번 내부 후보의 **원본 runtime source**로도
채택할 수 있다. 단, named artist·특정 게임 style 모사를 prompt에 넣지 않고, asset마다 exact prompt,
도구/모델, 생성일, 원본 경로, 원본·파생 SHA-256과 편집 이력을 manifest에 남긴다. 투명 배경,
silhouette, footprint, conductor attachment, 잘못된 전기설비·문자·상표·사람·artifact를 asset QA와
독립 art review에서 검사한다. 1×/2×·세 LOD·상태 mask는 이 승인된 원본에서 결정론적으로 파생하고
부모 hash를 기록한다. reference report/mockup과 제3자 asset은 입력 이미지나 runtime source로 쓰지
않는다. 재배포 권리와 외부 사람의 미감·전기설비 검토가 수집되지 않은 내부 후보는 그 사실을
명시하며 공개 release-ready라고 부르지 않는다.

### 9.4 최소 runtime asset 목록

#### 전력설비

- 일반 전신주, 보강 전신주
- 소형·대형 배전 변전소 compound
- 발전 접속 yard
- 의료원·정수장·산업단지·주거지 수전설비
- 일반·보강선의 conductor material
- 계획·공사·연속·비상·보호정지 cue와 mask

#### 도시

- 주거·상업 building 8~12종
- 의료원, 정수장, 산업시설 각 3~5개 조립 asset
- arterial/local/service road, bridge, riverbank, fence, retaining wall
- streetlight, utility cabinet, sign, vegetation·ground decal 8~12종

#### 효과와 UI

- rain, wet-ground, heat, storm grade와 warning beacon
- selection bracket, route halo, footprint, hazard pattern
- build, inspect, analysis, event category와 상태 icon set
- button·panel 9-slice 또는 vector style, focus ring와 cursor

### 9.5 visual catalog 계약

각 equipment class mapping은 최소 다음 값을 가진다.

```text
classId
overview / operating / construction visual
density variants
groundAnchor / visualBounds / selectionHull
shadowOffset / depthExtent
orientationPolicy
conductorAttachmentPoints A/B/C
labelAnchor / tooltipAnchor
stateMasks
sourceManifestId
```

자유 방향 분기 때문에 고정 pole sprite 하나에 anchor 세 개만 두지 않는다. class별로 8/16방향
variant를 만들거나 pole body와 회전 가능한 crossarm·terminal overlay를 결합한다. local attachment는
orientation과 branch assignment를 거친 뒤 world에 투영한다.

### 9.6 LOD

| LOD | 목적 | 보여줄 것 |
|---|---|---|
| Regional | 계획 | 시설 compound, route corridor, 단순 dark wire bundle, 중요 사건·시설 label |
| Operating | 선택·운영 | 개별 pole, 3상 도체, 변전소 구성 silhouette, 국소 warning·hover |
| Construction | 정밀 배치 | A/B/C anchor, span·clearance, footprint, ghost, cost·완공시각 |

LOD는 zoom index로 결정하고 output resolution에 따라 gameplay 정보가 달라지지 않는다. selection
outline, 중요 conductor와 status marker는 screen-space 최소 굵기를 가진다.

### 9.7 rendering layer

아래 순서를 compositor 불변식으로 둔다.

1. terrain·water
2. roads·parcels·hazard material
3. decorative city prop
4. equipment shadow
5. facility·pole·substation sprite
6. depth-sorted conductor와 conductor shadow
7. localized operation·construction·damage effect
8. weather·lighting — world viewport 안에서만 clip
9. analysis overlay
10. selection·construction interaction overlay
11. world label·tooltip
12. HUD·사건 지평선·command dock·inspector
13. toast·drawer
14. blocking modal·story

전선을 하나의 평면 layer에 모두 그려 건물과 교차 전선 위로 떠 보이게 하지 않는다. span은 endpoint
elevation과 depth를 사용하고 비접속 교차는 한 bundle의 bridge/gap으로 표현한다. 선택 route는 다른
교차선으로 번지지 않는다.

---

## 10. 코드·데이터 권위와 이행 architecture

### 10.1 동결 경계

다음 v2 실행물은 수정 대신 회귀로 보존한다.

- `data/release-world-v2.json`, `data/release-campaign-v2.json`
- `src/Gridworks.Core/Release/V2/`
- `CommercialMain`과 v3 campaign save codec
- 단계 G 내부 package identity와 완료 증거

실시간 규칙을 v2 공개 type에 조건문으로 넣지 않는다.

### 10.2 새 단일 권위

| 책임 | 새 권위 |
|---|---|
| 물리 공간·class·실시간 열·공사 숫자 | `data/release-world-v3.json` |
| 여덟 임무·event schedule·의무·약속·문구 | `data/release-campaign-v3.json` |
| clock·event·construction·supply·overload·save 규칙 | `src/Gridworks.Core/Release/V3/` |
| 새 제품 장면 | 별도 realtime scene tree, 완료 gate 전까지 `CommercialMain` 대체 금지 |
| asset geometry·LOD·manifest link | Game presentation visual catalog |
| 결정론 검사 | 별도 realtime checks executable |

world/campaign의 실행 숫자와 문구를 문서·scene·Game code에 복제하지 않는다. asset pixel geometry는
Core에 넣지 않는다.

### 10.3 typed presentation boundary

Core가 Game에 최소 다음 immutable model을 반환한다.

- `RealtimeWorldSnapshot`
- `RealtimeClockSnapshot`
- `RealtimeEventHorizon`
- `RealtimeSupplyDiagnostic`
- `RealtimeOverloadForecast`
- `RealtimeConstructionForecast`
- `RealtimeObligationSnapshot`

Game은 이를 다음 surface model로만 변환한다.

- world renderer
- analysis overlay
- event horizon
- selection inspector
- command dock
- banner/modal/story shell

timeline, renderer와 inspector가 별도 graph 탐색이나 미래 simulation을 하지 않는다.

### 10.4 renderer 이행

첫 vertical slice에서는 현재 `CommercialMapTransform`과 검증된 hit envelope를 재사용할 수 있다.
그러나 1,800줄 map draw와 3,600줄 main owner에 새 기능을 계속 추가하지 않는다. terrain, city,
equipment, conductor, weather, analysis와 interaction renderer를 분리하고 한 coordinator가 layer order를
소유한다.

새 scene graph로 이동할지는 다음 gate에서 결정한다.

- 4K의 draw/update profile
- conductor depth·Y-sort 정확성
- sprite batch와 selection hit testing
- 세 LOD 전환의 안정성

scene migration 여부와 관계없이 Core state, input command ID와 world-to-canvas transform의 결정론은
같아야 한다.

### 10.5 realtime loop

Godot owner는 elapsed time을 tick accumulator에 넣고 bounded batch로 Core를 전진시킨다. frame이
늦어도 tick을 조용히 버리지 않는다. catch-up ceiling을 넘으면 simulation을 자동 pause하고 성능
경고를 남긴다. renderer는 마지막 immutable snapshot을 그리며 매 frame graph·forecast를 다시
계산하지 않는다.

---

## 11. 저장·설정과 v3 이관

### 11.1 새 저장

새 URI는 기존 save를 덮지 않는 별도 v4를 사용한다. schema 이름은
`gridworks.realtime.campaign-save.v4`로 고정한다.

v4는 최소 다음을 exact하게 묶는다.

- schema version
- world/campaign ID와 SHA-256
- 현재 simulation minute와 다음 event index
- timestamp와 sequence가 있는 player command journal
- chapter·promise·construction·thermal checkpoint identity
- replay의 immutable `baseStateSeed` — seed schema/transform version, 원본
  `gridworks.commercial.campaign-save.v3` file bytes SHA-256, 그 save가 참조한 동결
  `release-world-v2` SHA-256과 `release-campaign-v2` SHA-256, source checkpoint identity, realtime
  anchor minute, canonical seed payload와 seed hash. 대상 realtime world/campaign v3 hash는 v4 top-level
  identity만 소유한다.
- migration origin과 source hash, 해당하는 경우

pause, 배속, camera, hover, inspector tab과 timeline scroll은 simulation journal에 넣지 않는다. load는
항상 현재 minute의 **paused** 상태로 열고 가장 가까운 다음 사건을 보여준다.

tick마다 command를 저장하지 않는다. initial seed, authored schedule, timestamped command를 replay해
현재 minute를 복원하고 canonical hash를 확인한다. 성능을 위한 checkpoint를 넣어도 command replay와
값이 같아야 한다.

### 11.2 v3 one-way migration

기존 `release-campaign-save-v3.json`은 다음 절차로만 이관한다.

1. strict v3 codec과 동결 v2 runner로 exact campaign/world hash를 검증하고 replay한다.
2. 원본 bytes와 SHA-256을 그대로 보존한다.
3. construction이 `Ready`이고 chapter/window checkpoint가 명확한 안정 상태만 자동 변환한다.
4. 완공 world, cash, 완료 chapter, promise, 결과 fact와 열 상태를 exact canonical
   `baseStateSeed` payload로 옮긴다. seed에는 transform version, 원본 commercial save-v3 bytes hash,
   그 save가 참조한 frozen world-v2/campaign-v2 hash, source checkpoint, realtime anchor minute와
   payload hash가 있어야 하며 opaque in-memory snapshot이나 실행 시각에 의존하지 않는다. target
   realtime world-v3/campaign-v3 hash를 이 세 source hash와 섞지 않는다.
5. 새 campaign schedule은 대응 chapter의 authored realtime anchor에서 시작한다.
6. v4를 same-directory temp→flush→replace로 쓴 뒤 clean process가 동일 base seed와 journal만으로
   strict restore하고 canonical seed/state hash를 확인한다. transform version이나 source hash를
   모르면 이관하지 않는다.

active draft, active construction 또는 window 중간의 의미를 손실 없이 옮길 수 없으면 자동 변환하지
않는다. 사용자는 `동결 v2로 계속`, `마지막 안정 checkpoint에서 개편판 시작`, `새 게임` 중 하나를
고른다. 어떤 경우도 v3를 삭제·수정하거나 말없이 초안을 버리지 않는다.

완료 save는 완료 기록과 chapter replay option을 이관하되, 개편판의 실시간 completion을 이미
달성했다고 표시하지 않는다.

### 11.3 settings v4

settings v4는 v3의 fullscreen, 세 음량과 reduce-motion을 보존하고 다음을 추가한다.

- display mode와 선택 가능한 FHD/4K resolution
- UI scale 100/125/150/200
- critical-event auto-pause
- graphics density 상태 — 자동/native가 기본

v3를 한 번만 import하고 write는 v4만 한다. invalid document는 임의 보정하지 않고 안전 기본값과
사용자 문구를 반환한다.

### 11.4 저장 gate

- Save & Quit은 write와 fresh restore 성공 뒤에만 title로 간다.
- autosave는 chapter start, critical event 직전, construction commissioning과 migration 직후의
  bounded checkpoint에서만 쓴다.
- save 중 simulation tick을 진행하지 않는다.
- disk full, permission, corrupt temp와 기존 파일 변경 race를 대표 반례로 검사한다.

---

## 12. 결정론적 자동검사

새 checks는 검사 수가 아니라 아래 불변식을 대표 성공·경계·반례로 고정한다.

### clock·event

- 1 tick×N과 `AdvanceTicks(N)`의 snapshot/hash 동등성
- 30/60/120/144 Hz frame chunk와 1×/2×/4×의 결과 동등성
- pause 중 세계·minute·journal 불변
- 같은-minute construction→event→supply→thermal 순서
- stable event priority와 exact horizon ordering
- catch-up ceiling에서 tick loss 없이 pause

### network·overload

- 선로·변전소·전신주 접속부의 연속 경계, 비상 경계와 초과 경계
- emergency allowance 소비·회복, protective outage와 cooldown 복귀
- authored outage와 protective outage 중첩
- 한 경로 후보 rollback 뒤 대체 경로가 선택되고 거부 후보 사용량이 0으로 남는지
- minute `M` 노출 소진→`M+1` pre-supply 차단의 exact 경계
- 공유 분기 사용량 합산과 first bottleneck 종류
- 비접속 교차와 선택 route 격리
- forecast와 실제 동일 command run의 사건 결과 동등성

### construction·balance

- pause/running 중 같은 배치 command 결과 동등성
- 사건과 같은 minute의 just-in-time commissioning
- active project 하나와 숨은 queue 부재
- class dominance 검사
- 작은/큰 설비, 일반/보강 asset의 bounded 유효 witness

### UX presentation

- Core snapshot 하나가 world, inspector와 timeline에서 같은 ID·수치·시각을 표시
- 한 input→한 command→한 result
- overlay/weather가 HUD hit 영역을 가로채지 않음
- modal focus 복원과 명시 pause reason
- timeline keyboard order와 linear accessibility view 동등성

### persistence

- v4 serialize→deserialize→replay exact equality
- v3 안정 checkpoint migration과 원본 보존
- 불안정 v3의 자동 migration 거부
- atomic write failure에서 기존 save bytes 보존
- FHD/4K, UI scale과 simulation state 독립

v2 CommercialChecks와 동결 Release/Product checks는 변경이 닿는 동안 계속 통과해야 한다.

---

## 13. native·시각·사람 gate

### 13.1 단계별 native gate

Game을 바꾼 모든 단계는 실제 Godot binary에서 다음을 한 흐름으로 확인한다.

- pause→1×→2×→4×→pause와 같은 사건 결과
- timeline event 선택→map focus→inspector 원인
- 공사 초안→발주→실시간 진행→commissioning
- 전신주 접속부 또는 변전소의 비상 운전→보호정지→복귀
- save→process 종료→fresh process restore
- keyboard-only tool, candidate, timeline, modal과 focus 복원

### 13.2 layout·art matrix

최소 matrix는 다음이다.

- FHD와 4K
- 두 해상도 각각 UI 100/125/150/200 전 조합
- 세 LOD
- clear/heat/rain/storm
- planned/construction/continuous/emergency/outage
- inspector closed/open, analysis off/on
- dense branch와 비접속 crossing
- reduce-motion off/on

모든 조합을 screenshot pixel equality로 검사하지 않는다. layout invariant는 control bounds와 semantics로,
대표 시각은 native capture와 사람 review로 확인한다.

### 13.3 후속 사람 validation gate

아트·click hierarchy·실시간 pacing의 품질 주장은 자동검사로 완료할 수 없으므로 이 scope는
`HumanValidatedInternalCandidate`를 주장하기 전에 bounded 사람 관찰을 요구한다. 이 절은 §15.1의
repository 구현 완료를 막는 gate가 아니며, 수집 전 상태를 `NOT_COLLECTED`로 정직하게 유지한다.

#### Vertical slice 관찰

처음 보는 한국어 desktop player가 label·analysis를 끈 첫 화면에서 3초 안에 발전원, 전신주,
변전소, 수요시설과 route를 가리킬 수 있는지 본다. 이어 다음을 관찰한다.

- pause와 배속을 발견하는가
- 사건 지평선에서 다음 위기와 공사 완료 순서를 설명하는가
- 전신주 접속부와 변전소의 과부하 원인을 구분하는가
- scroll 없이 선택→분석→공사 발주를 끝내는가
- modal이 왜 시간을 멈췄는지 이해하는가

#### 전체 campaign 관찰

- turn button을 찾지 않고 real-time flow를 이해하는가
- standard/reinforced, small/large 선택이 실제 trade-off로 느껴지는가
- FHD와 4K에서 text·target·map density가 편안한가
- 반복 modal·button hunt·scroll hunt가 다시 나타나는가
- 마지막 밤의 overload와 사건 순서를 자기 말로 설명하는가

한 명의 LLM play는 사람 증거가 아니며 beauty·fun·balance를 승인하지 않는다. 관찰에서 새로운 major가
나오면 범위 안 수정 후 같은 핵심 질문만 재검증한다.

---

## 14. 단계별 구현 순서

### R0 — 계약·기준선

범위:

- 이 계약, 두 비평 package의 채택/거부와 asset provenance 경계
- 기존 v2 state·save·layout·performance baseline capture
- 실시간 command/event/overload와 UX state machine의 public contract review

동결 기준선은 `4569c35cf54218faa9407d67319113ebf52adcf5`의 상용 v2다. 이 revision과 분리한
clean source에서 CommercialChecks 30 suites / 5,739 assertions와 Game
Debug·Release·ExportRelease 0 warnings / 0 errors를 재확인해 기록한다. R1의 단일 vertical slice는
`FIRST_LIGHT`의 별도 V3 schedule이다. 공사 한 건의 자동 완공, 세 수요 경로의 line/pole/substation
각 첫 병목, 비상 노출→차단→복귀와 forecast=actual을 한 bounded fixture에서 증명한다. 전체 여덟 장
전환은 R5 전에는 제품 기본 path가 아니다.

종료 gate:

- R1 schema나 UI placeholder를 stage하지 않은 문서·fixture 설계 commit을 독립 P0/P1 검토
- v2 exact regression baseline 기록
- 다음 단계가 사용할 `FIRST_LIGHT` vertical slice와 exact command/dispatch/trip order 확정

### R1 — 결정론적 실시간 Core vertical slice

범위:

- 별도 V3 clock, event scheduler와 timestamped command
- 한 chapter의 continuous construction, supply와 세 설비군 overload
- pure forecast와 canonical hash
- headless deterministic checks

제외:

- 새 Godot shell, production art, 전체 campaign migration

종료 gate:

- frame chunk·speed 독립, event order, overload·cooldown과 forecast=actual PASS
- v2 Core 무수정, 독립 P0/P1 0

### R2 — UX foundation과 사건 지평선

범위:

- code-native placeholder world 위의 새 shell
- simulation/tool/surface state reducer
- top HUD, horizontal event horizon, contextual inspector와 bottom command dock
- button tokens, modal policy, keyboard와 접근성 list
- FHD/4K responsive control hierarchy

제외:

- final asset, 전체 campaign

종료 gate:

- FHD/4K native에서 scroll 없는 핵심 flow와 actual realtime slice
- click priority·focus·pause reason checks, 독립 UX P0/P1 0

### R3 — 물리 world art spike

범위:

- 일반·보강 전신주, 소형 변전소, 한 수요시설
- 3상 conductor, orientation/attachment, 세 LOD
- planned/continuous/emergency/outage와 analysis overlay
- road·river·terrain의 작은 authored slice

종료 gate:

- label·analysis off에서도 city power-distribution scene로 읽힘
- 같은 selection ID·배치 결과, crossing과 depth 정확성
- FHD/4K 성능과 independent visual/layout review; 사람 vertical-slice는 §13.3에 별도 기록

### R4 — 전체 설비·도시·날씨 production pass

범위:

- 모든 현재 class와 terminal/facility mapping
- city prop, roads, riverbank, weather·lighting와 sound refresh
- asset manifest, source, license, hash와 import preset

종료 gate:

- visual catalog completeness, missing asset 0
- 모든 상태·LOD·날씨 대표 native matrix
- temp/reference asset package 포함 0, 독립 art/release P0/P1 0

### R5 — 밸런스·캠페인 실시간 전환

범위:

- 여덟 chapter의 authored realtime event schedule
- promise/deadline/result를 실제 시간과 연결
- class dominance 제거와 세 제한 설비 first-bottleneck witness
- onboarding, event horizon disclosure와 auto-pause tuning

종료 gate:

- 모든 chapter에 장단점이 다른 유효 설계 둘과 대표 실패·복구
- turn approval command가 새 path에 없음
- 전체 headless run과 future softlock 0, 독립 Core/content P0/P1 0

### R6 — v4 save·settings와 v3 migration

범위:

- v4 save/settings, atomic store와 strict restore
- 안정 v3 checkpoint one-way migration
- 불안정 v3 선택 UI와 frozen-v2 continue
- autosave·fresh process resume

종료 gate:

- 원본 v3 보존과 모든 migration 반례 PASS
- 두 fresh process의 mid-event·mid-construction resume
- FHD/4K setting restore, data loss·softlock 0

### R7 — 전체 native·package 검증과 구현 완료 후보

범위:

- 전체 campaign native run, 에필로그와 replay
- FHD·4K layout/performance/accessibility matrix
- bounded internal interaction review와 한국어 정적 교정
- package audit와 새 설치

종료 gate:

- §15.1 구현 종료 조건
- independent exact-tree P0/P1 0
- Developer ID·공증은 별도 자격증명 승인 없으면 여전히 내부 candidate로만 기록

---

## 15. 완료 판정

### 15.1 `TotalRevisionImplementationComplete`

다음은 repository 안에서 구현·결정론 검사·native 자동 interaction·독립 검토로 닫을 수 있는 전면
개편의 완료 조건이다. 모두 참일 때 이 branch를 `TotalRevisionImplementationComplete = YES`로
표시할 수 있다. 이것은 사람 검증, 서명·공증 또는 공개 출시 승인이 아니다.

### 게임 규칙

- 새 기본 path가 decision-window/turn 승인 없이 계속 흐르는 fixed-tick simulation이다.
- pause·1×·2×·4×가 frame rate와 무관하게 같은 결과를 만든다.
- 선로, 변전소와 전신주 접속부 각각의 과부하→보호정지→복귀가 campaign에서 실제로 발생하고
  forecast·원인·결과가 일치한다.
- class dominance가 없고 작은/큰 설비의 authored 유효안이 존재한다.

### UX

- 수평 사건 지평선에서 다음 사건, 공사 완료와 예상 보호정지를 확인할 수 있다.
- selection, build, analysis, recovery와 story의 click/modal hierarchy가 하나의 state contract를 따른다.
- 핵심 flow가 긴 오른쪽 scroll, 숨은 button과 반복 modal을 요구하지 않는다.
- mouse와 keyboard가 동등하고 focus/pause reason이 복원된다.

### 아트·해상도

- 기본 세계가 graph가 아니라 물리 도시 전력망으로 읽힌다.
- 모든 current class가 silhouette, attachment, state와 세 LOD를 가진다.
- FHD·4K에서 blur, clipping, UI 침범과 중요 1 px cue 소실이 없다.
- worst-case FHD·4K가 선언한 reference hardware의 성능 gate를 통과한다.
- runtime asset의 prompt/source/파생 provenance·hash·내부 사용 권리 근거가 완전하다. 외부 법무·재배포
  승인은 §15.2 상태로 별도 표시한다.

### 저장·안전

- v4 save가 exact restore되고 v3는 원본을 보존한 채 안전한 상태만 이관한다.
- new install→save→fresh continue→전체 campaign→completion resume가 같은 package bytes에서 끝난다.
- crash, data loss, softlock, unresolved critical/P0/P1이 0이다.

### 구현 증거

- 새 deterministic checks와 영향받은 모든 동결 회귀 PASS
- Debug/Release/ExportRelease build 0 warnings / 0 errors
- 단계별 native marker, 동일 bytes fresh-process restore와 package audit PASS
- 독립 exact-tree Core·UX·art·release P0/P1 0
- README, roadmap, object catalog, visual spec, asset manifest와 install 문서 최신화

### 15.2 `HumanValidatedInternalCandidate`와 공개 출시

다음은 §15.1과 분리된 후속 validation gate다. 수집되지 않았다고 구현을 미완성이라 부르지 않고,
수집되지 않은 상태에서 재미·미감·전기설비 사실성·출시 준비를 승인하지도 않는다.

- 처음 보는 사람의 bounded vertical slice와 전체 campaign review
- FHD·4K의 사람 가독성·미감·click hierarchy 검토
- 전력설비 전문가의 silhouette·용어·과부하 abstraction 검토
- 한국어 전문 교정
- 재배포 권리·법무 확인, Developer ID 서명과 공증
- 공개 배포 owner 승인

이 항목들이 모두 끝나기 전 상태는
`HumanValidatedInternalCandidate = NO`, `PublicReleaseStatus = NOT_AUTHORIZED`다. 사람 관찰과 외부
proof는 별도 사용자 승인 아래 실행하며 자동 PASS 수치에 합산하지 않는다.

현재 이 문서 작성 시점의 상태는 다음과 같다.

```text
RealtimePhysicalRevisionContract = DEFINED
ActiveRevisionGate = R1_REALTIME_CORE_VERTICAL_SLICE
R0BaselineCommit = 5a9e465
R0IndependentReview = P0_0_P1_0
RealtimeCoreStatus = NOT_IMPLEMENTED
RealtimeUxStatus = NOT_IMPLEMENTED
HorizontalEventHorizonStatus = NOT_IMPLEMENTED
PhysicalWorldArtStatus = NOT_IMPLEMENTED
FhdSupportEvidence = NOT_COLLECTED_FOR_REVISION
FourKSupportEvidence = NOT_COLLECTED
RealtimeOverloadEvidence = NOT_COLLECTED
V3SaveMigrationStatus = NOT_IMPLEMENTED
RevisionHumanValidationStatus = NOT_COLLECTED
TotalRevisionImplementationComplete = NO
HumanValidatedInternalCandidate = NO
TotalRevisionStatus = NOT_COMPLETE
PublicReleaseStatus = NOT_AUTHORIZED
```

---

## 16. 명시적 제외

- full 3D, 회전 camera, photoreal simulation과 자유 camera
- AC 전력조류, 전압·무효전력·상불평형·상세 보호계전
- 실제 섭씨, 풍속·일사별 동적 정격, 설비 화재·영구 열화
- 수동 급전, 차단기·재폐로 조작과 작업반 dispatch
- 숨은 확률 고장, procedural city와 무한 sandbox
- 여러 동시 공사반, 자재·인력·차량 simulation
- 전력시장, 대출, 연료경제와 기술 tree
- 자동 route·정답 pole 배치와 범용 완공망 편집기
- 대화 선택지, 호감도, 긴 cinematic과 음성
- mobile·gamepad·multiplayer·live service 동시 개발
- reference game asset·UI·font·icon의 복제
- 반복 LLM 목표 튜닝과 사람이 승인하지 않은 자동 beauty score

이 제외 항목이 없으면 핵심 실시간 물리 세계가 성립하지 않는다는 재현 가능한 증거가 생길 때만
새 사용자 승인과 별도 계약으로 연다.
