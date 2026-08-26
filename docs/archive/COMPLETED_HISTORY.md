# Gridworks 완료 이력

> 이 문서는 완료·중단된 task의 **유일한 요약**이다. 현재 구현 권한이나 남은 작업을 정의하지 않는다.
> 세부 scope, 실행 로그와 당시 문구는 Git 이력과 `playtests/`에 남아 있다.

## 1. 초기 prototype과 release v1

초기 Godot slice에서 다음 규칙을 분리해 검증했다.

- 변전소 service area는 연결 자격이며 발전원이 아니다.
- 전기적으로 다른 회로도 같은 공간 회랑 사고에 함께 영향을 받을 수 있다.
- 공사 중 설비는 무전압이고, 한 공사는 원자적으로 완공된다.

이후 33×21 격자 기반 `ReleaseMain`은 분기·합류, 공유 정격, 사건 projection, 8개 임무,
저장·재개와 ad-hoc macOS 내부 ZIP까지 연결했다. 이 빌드는 현재 제품이 아닌 동결 기술 기준선이다.

## 2. 상용 V2 단계 B–G

별도 `CommercialMain`과 V2 Core에서 다음을 완료했다.

- 자유 배치, 점유영역·수면·건물·선로 기하와 제한된 camera
- 선로·전신주 접속부·변전소의 연속/비상 열 한계, 보호정지·냉각
- 안전 의무·도시 약속·결정 기한·국면 preview와 save v3
- 동일한 망·자금·선택을 잇는 8개 임무, 결과와 epilogue
- 병목 설명, 발주 checklist, 설정·접근성·날씨·초상·음향
- 빈 user-data의 내부 macOS 후보에서 저장, fresh continue, 전체 캠페인과 완료 후 재개

규칙·wiring·내부 package 검증은 완료했지만 사람 전체 플레이, 한국어·전력설비 전문 교정,
Developer ID 서명·공증과 공개 출시는 승인되지 않았다. V2 저장과 패키지는 현재 R2의 저장·패키지가
아니다.

## 3. 실시간 전환 R0–R2

### R0 — 방향 전환

turn/승인 중심 흐름을 fixed-tick realtime으로 바꾸는 계약을 만들었다. pause·1×·2×·4×, 계속 흐르는
공사와 예고 사건을 제품의 중심으로 정했다.

### R1 — 실시간 Core

`FIRST_LIGHT` vertical slice에서 결정론적 시계, 공사, 사건 경계, 세 설비군의 과부하→보호정지→복귀,
forecast와 actual 상태를 구현했다. 이 단계는 Core 규칙 기준선이며 당시 Game UI, 제품 데이터와
persistence는 범위 밖이었다.

### R2 — 실시간 UX 기반

reducer, 상단 HUD, 수평 사건 시간축, 조건부 inspector/build/action UI와 code-native world를 만들었다.
초기 R2 종료 gate는 사용자 지시로 중단됐지만 이후 작업은 이 기반을 보존해 현재 live R2로 발전했다.
중단 당시의 “비기본 장면”이나 “전체 미완료” 문구는 현재 상태가 아니다.

## 4. 상용 UX 평가 기반

### UX-R0 — text-plan 형성평가

8장/16개 사건을 실시간 일정에 결속하고, briefing·window·result·epilogue를 34개 story part로 단독
실행할 수 있게 했다. 세 fresh judge의 두 번째 안정 panel은 `TextPlanProxy = 83.4475`였다. 이는 계획
평가이며 native 게임 점수나 공식 상용 UX 점수가 아니다.

### UX-R1 — 비점수 평가 도구

targeted checkpoint, story-part unit, session/attempt, evidence chain과 fail-closed 비점수 도구를
구축하고 독립 검토를 마쳤다. 로컬에서 요청 model/effort를 확인하는 controlled transcript도 만들었지만
platform attestation, 실제 judge 실행 또는 공식 점수를 주장하지 않았다.

### UX-R2.1 — 첫 장과 한 줄 future-event bar

실제 release `FIRST_LIGHT`의 briefing→live play→authored result를 R2에 연결했다. 사건·공사·결정·열
경계를 한 줄 chronological rail의 compact marker로 합치고 hover/선택 상세 구조를 만들었다. 첫 장과
두 targeted checkpoint는 실제 macOS 입력으로 비점수 관찰했다.

### UX-R2.2 — 튜토리얼 3장

`FIRST_LIGHT → SECOND_HEART → SECOND_SOURCE`를 동일한 망·현금·시계에서 이어지게 했다. 병원 2회선,
범람 안전 회랑과 전체 경로 용량을 장 전환과 authored result에 연결했다. fresh process의 production
mouse/keyboard 입력으로 3장 누적 경로를 끝까지 관찰했다.

### UX-R2.3 — 네 번째 장

`NORTH_BANK_PROMISE`까지의 누적 4장 경로를 구현했다. 6개월 달력 전환이 이전 망·공사·자금을
보존하며, 약속 마감이 같은 한 줄 rail에 표시되고 Keep/Defer가 Core 결과로 이어진다. 자동검사와
독립 review는 완료했지만 사용자 중단 지시에 따라 4장의 native 직접 플레이는 수행하지 않았다.

### UX-R2.4 — 제품 title과 새 게임 진입

인자 없는 기본 장면이 session을 만들기 전에 제품 title을 표시하도록 했다. 저장 권위가 없으므로
`이어하기`는 이유와 함께 비활성이고, production `새 게임` button 입력은 canonical `FIRST_LIGHT`와
authored briefing을 연다. `RealtimeLaunchCatalog`가 이 product boot를 명시적 DEBUG fixture,
checkpoint와 세 native 개발 route에서 분리한다.

작은 headless smoke가 실제 default scene의 pointer 입력, title input ownership, briefing wiring과
fixture/native resource 경계를 확인했다. 이는 자동 wiring 증거이며 사람 미감·사용성, fresh-install,
save/resume 또는 전체 캠페인 완결성의 증거가 아니다.

### UX-R2.5 — 누적 4장 production-input 직접 플레이

`./dev play through NORTH_BANK_PROMISE`를 서로 독립된 fresh process에서 Keep과 명시적 Defer로 각각
결과까지 진행했다. 첫 3장의 실제 망·완공/진행 공사·자금·시계가 네 번째 장에 보존됐고, 6개월 달력
전환, 약속 기한과 주변 사건 상세, 두 authored 결과를 production mouse/keyboard 입력으로 확인했다.

Keep은 canonical formative chapter·full-flow evidence를 기록했다. Defer는 authored Defer 결과로
도달성을 관찰했으며 설계상 Keep 전용 PASS evidence를 만들지 않는다. 재현된 결함이 없어 gameplay
code와 regression은 바꾸지 않았다. 이는 사람 참가자의 미감·사용성, 남은 4장, save/resume, 전체
캠페인 또는 공식 UX 점수의 증거가 아니다.

### UX-R2.6 — 다섯 번째 장 native 구현

`WHOSE_MARGIN`까지 앞선 망·공사·자금·시계를 잇는 누적 5장 경로를 구현했다. briefing, 두 planning
window와 세 사건을 authored reveal 순서로 진행하며, 산업 야간 증산 약속은 실제 duty가 있는
`NIGHT_SHIFT`에만 표시된다. 보강 회랑 Keep과 명시적 Defer가 각각 exact authored result로 이어지고,
Keep만 5장 full-flow evidence를 만든다.

일반 회랑에서는 Core가 기록한 비상 노출·보호정지·복귀를 자산별 stable rail marker와 detail history로
투영해 약속 실패가 성공 결과로 보이지 않게 했다. 이 연결은 chapter ID별 Session/Main/UI 분기 없이
typed promise fact와 Core transition history를 사용하는 공통 presentation 경로에 놓였다. Release
build, `./dev check`, WHOSE_MARGIN story selector, 누적 Godot UI harness와 독립 review를 통과했다.
production-input 직접 플레이의 관찰 상한은 여전히 `NORTH_BANK_PROMISE`까지 4장이며, 남은 3장,
save/resume, 전체 캠페인 또는 사람 UX 품질의 증거는 아니다.

### UX-R2.7 — 여섯 번째 장 native 구현

`BEFORE_WATER_RISE`까지 앞선 망·공사·자금·시계와 결과를 잇는 누적 6장 경로를 구현했다. 이미 상속된
동부 접속 2회를 새 공사로 세지 않고, 범람 구역을 피하는 남부 고지대 보완 회랑을 완공했다. Keep은
의료원·정수장·동부 생활권을, 명시적 Defer는 필수시설을 공급한 exact authored result로 이어지며
Keep만 6장 full-flow evidence를 만든다.

forecast와 active flood는 `RIVER_FLOOD_ZONE` 및 `WEST_SOURCE_NODE` 사용 불가를 보이고, active thermal
결과에서 실제 수요가 남부 발전원으로 공급되는 것까지 확인했다. Release build, `./dev check`,
BEFORE_WATER_RISE story selector, 누적 Godot UI harness와 두 독립 review를 통과했으며 canonical native
route는 3개를 유지했다. production-input 직접 플레이의 관찰 상한은 여전히 `NORTH_BANK_PROMISE`까지
4장이고, 남은 2장이나 전체 캠페인·사람 UX 품질의 증거로 확대하지 않는다.

### UX-R2.8 — 일곱 번째 장 native 구현

`SWITCH_OFF_TO_PROTECT`까지 앞선 망·공사·자금·시계와 결과를 잇는 누적 7장 경로를 구현했다. 267270분에
이전 종료 현금을 상속하고 1,600,000원 grant를 더한 뒤, 기존 정수장 player corridor 1/2에서 범람 장의
`(1950, 850)` 보강 전신주→새 `(2300, 900)` 소형 변전소→정수장 보강 회선을 267417분까지 완공해
첫 사건 시작인 267690분에 접속 2/2를 고정했다.

267690–267810분 계획정지에는 서부 전원을 제외하고 남부 3,900 kW로 의료원 1,800 kW·정수장
1,400 kW·동부 700 kW를 연속 공급했으며 비상·보호정지를 만들지 않았다. 267870–267990분 복귀에는
서부 전원을 다시 사용해 의료원·정수장 각 900 kW와 동부 700 kW를 연속 공급했다. exact standard
result, 7장의 ordered result와 full-flow evidence, canonical 3-route cap을 Release build, story selector,
`./dev check`, 누적 Godot UI harness와 두 독립 review로 확인했다. production-input 직접 플레이의 관찰
상한은 여전히 `NORTH_BANK_PROMISE`까지 4장이며 남은 native 장은 1개다.

## 5. G3 아트와 main 통합

루트 `assets/`의 네 이미지를 시각 방향으로 삼아 회화적 아이소메트릭 도시·설비·UI 자산을 제작했다.
중간의 지도 35개 부분 적용은 최종 작업으로 대체됐다. 현재 기준선은 G3 PNG 57개 전부를 live R2에
연결한다.

- 지도 50개: 지형, 강·제방, 도로, 주거·병원·산업 시설, 발전·전신주·변전소 구조물
- UI 7개: panel, HUD metric, inspector, tool slot과 버튼 chrome
- clear·heat·rain·storm draw, UI scale, hit/focus와 기존 상태 표현 회귀 검사
- `RealtimeSliceMain`을 저장소 기본 Godot 장면으로 전환
- 작업 이력을 local `main` 한 branch로 정리

이 완료는 runtime 연결과 자동검사의 증거다. 사람 미감·사용성, 전체 상태별 production polish,
R2 save/package 또는 출시 승인을 뜻하지 않는다.

## 6. 문서 기준선 정리

현재 사실, 남은 일, 제품 기준과 완료 이력을 분리했다. 완료된 scope와 체크리스트는 현재 문서
트리에서 제거하고 이 파일에 압축했다. README에는 실행 가능한 현재 사실만, `NEXT_TASKS.md`에는
미완료 항목만 남겼으며 SHA·커밋 영수증은 현재-facing 기획 문서에서 제거했다.

후속 재감사에서는 fresh-install 후보와 공식 평가의 순서, audio/settings와 score-bearing 도구 gate,
UX-R0 context 보존, 설비 catalog·용어·역사 링크와 source 주석을 바로잡았다. 이는 문서와 동결 입력의
정합성 보완이며 새로운 gameplay, package 또는 UX 점수 완료를 뜻하지 않는다.

## 7. R2 개발 구조 단순화

current R2를 새 gameplay 없이 더 적은 권위·분기·파일 fan-out으로 변경할 수 있게 정리했다.

- root `Gridworks.sln`과 `./dev`를 current Core/Game/check의 단일 개발 진입점으로 만들고 historical
  Product/V1/check graph와 동결 V2 `ExportRelease`를 분리했다.
- release route와 native 4장 cap을 `RealtimeNativeRouteCatalog` 하나로 묶고, strict loader가 V2 base와
  V3 overlay를 합성한 뒤 generic story flow가 Core transition에서 modal timing/request를 만들게 했다.
- Godot main에서 plain C# `RealtimeSession`을 분리해 main을 441줄 scene/input/publication adapter로
  줄였다. raw input은 `RealtimeInputRouter`가 typed request로 바꾸고 Main이 명시적 capability로
  검증·routing한다.
- 한 full projection은 하나의 `RealtimePresentationSource`에서 조립한다. modal의 이중 projection을 제거하고,
  1,968줄 presenter를 158줄 facade와 world/timeline/context/construction/shell/modal component로 나눴다.
  component끼리는 호출하지 않고 facade만 최종 immutable presentation을 조립한다.
- stable ID, 문구, timeline 정책과 target resolution을 leaf authority로 분리하고 full ID 형식을 독립
  assertion으로 고정했다.
- [개발 구조](../ARCHITECTURE.md)에 규칙·application·presentation·Godot ownership과 chapter/mechanic/
  presentation 변경 경로를 기록했다.

Debug/Release build, current check suites, Python 회귀, 두 named checkpoint의 기존 canonical hash와 전체
Godot UI harness를 유지했다. 이 완료는 compile 시간 개선, 새 chapter/title/save/package 또는 사람 UX
품질을 주장하지 않는다.

## 8. 이 문서가 소유하지 않는 항목

현재 질문의 소유 문서는 [문서 지도](../README.md)가 지정한다. 제품 구현 상태는
[루트 README](../../README.md), current 개발 구조는 [개발 구조](../ARCHITECTURE.md), 미완료 항목·순서는
[남은 작업](../NEXT_TASKS.md)이 소유한다. 이 완료 이력의 경계 문장을 current 문서로 복사해 갱신하지
않는다.
