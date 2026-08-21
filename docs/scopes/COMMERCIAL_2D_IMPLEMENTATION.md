# Gridworks 상용 2D 게임 구현

> 상태: **ACTIVE**
> 구현 권한: **GRANTED — 단계 B부터 G까지**
> 현재 작업: **단계 E — 첫 네 임무와 공통 UX**
> 승인 근거: 사용자는 2026-08-18 보이는 격자를 없앤 자유 배치와 전선·변전소·전신주 접속부의
> 열 한계를 채택했고, 이어서 상용 재기획서 전체를 개발 완료하라고 지시했다.

이 문서는 [상용 2D 게임 재기획서](../product/COMMERCIAL_2D_GAME_DESIGN_PLAN_KO.md)를 실제 제품으로
만드는 현재 구현 계약이다. 재기획서가 경험·콘텐츠·표현 원칙을, 이 문서가 실행 권위·단계 순서와
완료 증거를 소유한다.

기존 `release v1` 경로와 macOS 내부 후보는 기술 기준선으로 동결한다. 새 게임은 그 타입과 저장을
점진적으로 일반화하지 않고 별도 v2 규칙·데이터·기본 장면을 사용한다. 각 단계는 구현, 결정론적
검사, 한정된 native 확인, 독립 검토와 커밋을 끝낸 뒤 다음 단계로 넘어간다.

## 1. 최종 결과

완료 시 Gridworks는 한 도시를 끝까지 책임지는 소형 싱글 플레이 2D 전력망 전략·퍼즐 게임이다.

- 보이는 셀과 격자 맞춤 없이 지형 위에 전신주와 변전소를 자유롭게 놓는다.
- 수면·건물·설비 점유영역을 피하면서 직선 선로로 분기·합류망을 만든다.
- 선로 도체, 변전소 주기기와 전신주 접속부의 연속·비상 열 한계를 읽는다.
- 작성된 국면마다 평상·사고·열 상태를 미리 보고, 비상 운전 뒤 보호정지와 냉각을 계획한다.
- 안전 의무와 선택 가능한 도시 약속, 공사기간과 결정 경계를 비교한다.
- 최근 공사를 되돌리거나 임무 시작으로 복구할 수 있다.
- 프롤로그 세 임무와 본편 다섯 장에서 같은 지도·망·자금·결과 기록을 이어 쓴다.
- 실제 망 상태에 맞는 브리핑·사건·결과·에필로그, 2D 도시 반응과 최종 사운드를 제공한다.
- Title, 저장·재개, 설정, 접근성과 저장소 밖 macOS 내부 패키지를 새 제품 경로에서 사용한다.

구현 완료는 재미·문체·시장성에 대한 사람 승인이나 공개 배포 승인을 뜻하지 않는다. 단계 H의
사람 관찰, 한국어 전문 교정, Developer ID 서명·공증과 공개 상점 절차는 이 구현 뒤 별도 외부
게이트다.

## 2. 단일 권위와 동결 경계

### 2.1 새 제품 권위

| 책임 | 단일 권위 |
|---|---|
| 단계 B 자유 배치 slice | `data/commercial-free-placement-slice-v1.json` |
| 단계 C 이후 지도·지형·설비 형식·열 한계·초기 망 | `data/release-world-v2.json` |
| 단계 D 상용 핵심 흐름 | `data/commercial-core-slice-v1.json` |
| 단계 E·F 여덟 임무·국면·의무·약속·문구 | `data/release-campaign-v2.json` |
| 자유좌표·기하·건설·급전·열·캠페인·저장 규칙 | `src/Gridworks.Core/Release/V2/`, namespace `Gridworks.Core.Release.V2` |
| 실제 제품 장면과 화면 adapter | `game/CommercialMain.tscn`, `game/Commercial*.cs/.tscn` |
| 결정론적 독립 검사 | `tools/Gridworks.CommercialChecks/` |
| 캠페인 저장 | `user://release-campaign-save-v3.json` |
| 화면·음량·움직임 설정 | `user://settings.json`, strict settings v3 |

world와 campaign의 실행 숫자·문구를 문서, scene 또는 Game 코드에 복제하지 않는다. 검사기의 대표
해법 좌표는 런타임 데이터에 넣지 않는다. Core는 Godot, 화면 픽셀, 카메라와 로케일을 참조하지
않고 Game은 기하·급전·열 상태를 재계산하지 않는다.

단계 B slice는 공간 규칙만 소유하는 임시 권위이며 열 placeholder를 갖지 않는다. 단계 C가 시작될
때 같은 검증된 공간 계약을 포함한 최종 world v2를 열고 slice 파일은 실행 권위에서 제거한다.

### 2.2 동결 기준선

다음은 수정 대신 회귀로 보존한다.

- `data/release-world-v1.json`, `data/release-campaign-v1.json`
- `src/Gridworks.Core/Release/`의 기존 v1 공개 계약
- `ReleaseMain`, 기존 `release-campaign-save-v2.json`과 이전 내부 ZIP 증거
- `ProductMain`, prototype 장면과 product fixture

기존 `Gridworks.Core.Release` 파일과 공개 타입을 새 규칙 때문에 수정하지 않는다. v1 저장을 v3로
자동 변환하지 않는다. 기존 파일은 덮어쓰거나 삭제하지 않고 새 제목 화면에서
호환되지 않는 이전 후보 저장이라고 알린다. v3 저장은 현재 실행 권위의 ID와 SHA-256을 exact로
묶는다. 단계 D에서는 v2 world와 core-slice ID/hash, 단계 E~G에서는 v2 world와 final campaign-v2
ID/hash를 저장한다. 단계 E 전환 때 D 개발 저장은 읽지 않고 원본을 보존한 채 비호환으로 알린다.

## 3. 단계 B — 자유 좌표 기반

### 3.1 좌표와 거리

- `MapPoint`는 signed 32-bit 정수 세계좌표이며 설계 거리 1단위는 내부 100단위다.
- Game의 포인터·키보드 위치는 Core 명령 직전 가장 가까운 내부 단위로 한 번만 양자화한다.
- 거리와 선로 길이는 `ceilIntegerSqrt(dx² + dy²)`를 사용한다. 합·제곱·비용·시간 계산은 checked
  64-bit 범위에서 수행하고 overflow를 typed rejection으로 반환한다.
- 비용, 공사기간과 경로 동률은 정수 세계좌표만 사용한다. 화면 픽셀, 줌과 부동소수점은 규칙에
  참여하지 않는다.
- 단계 B spatial fixture는 내부 단위의 닫힌 지도 bounds를 소유하고 보이는 격자·셀·major step을
  갖지 않는다. 같은 계약은 단계 C final world v2에 포함된다.

### 3.2 점유영역과 지형

- 신규 전신주와 변전소는 형식별 원형 `footprintRadiusUnit`을 가진다.
- 원 전체가 지도 안에 있어야 하며 수면, 건물 polygon, 기존·초안 설비의 원형 점유영역과 닿거나
  겹칠 수 없다. 경계 접촉도 거부한다.
- 강 위에는 새 설비를 놓을 수 없다. 육지 접속점 사이의 선로는 강을 건널 수 있다. 처음부터
  교량·보호기초 위에 authored된 고정 접속점은 연결 endpoint로 쓸 수 있으나 같은 예외를 새로
  만들 수 없다.
- 위험구역은 배치를 막지 않고 점유영역·구간 접촉을 예고 사고 노출로 기록한다.
- 모든 polygon, 원·구간, 구간·구간 판정은 정수 기하 한 구현을 preview·명령·사고 평가가 공유한다.

### 3.3 선로 기하

- 각 구간은 양의 길이이고 선종의 `maxSpanUnit` 이하이며 두 접속점 또는 접속점·신규 전신주를 잇는다.
- 선로는 건물이나 endpoint가 아닌 제3 설비의 점유영역을 관통·접촉할 수 없다.
- 같은 unordered endpoint의 중복 구간과 양의 길이로 포개지는 공선 구간은 거부한다.
- 서로 교차하는 두 구간은 공통 node ID가 없으면 전기적으로 연결되지 않는다.
- 선로 몸체에는 맞춤하지 않는다. Game은 화면 거리로 접속점 후보를 정렬하고 `Q / E` 순환 뒤
  확정 node ID를 Core에 전달한다.
- 초안의 신규 전신주는 발주 전에 옮길 수 있고 마지막 점과 전체 초안을 되돌릴 수 있다. 완공 자산의
  임의 이동이나 범용 그래프 편집기는 만들지 않는다.

### 3.4 카메라와 입력

- 지도 변환은 한 순수 Game helper가 전체 bounds, viewport, zoom index와 center를 받아 계산한다.
- 줌은 `전체 보기 / 1.5배 / 2.25배` 세 단계뿐이다. 휠 또는 `+ / -`, 가운데 버튼 또는
  `Space+drag`, `Home`을 지원한다.
- 키보드 자유 커서는 방향키와 `Shift+방향키`로 이동하고 화면 가장자리에서 카메라가 따라간다.
- `Q / E`는 접속 후보, `Enter`는 확정, `Tab / Shift+Tab`은 표준 UI focus다.
- `Esc`는 현재 초안 취소 → story 유지 → 일시정지/뒤로의 전역 우선순위를 `CommercialMain` 한 곳이
  소유한다.
- 카메라, hover와 선택 후보 index는 저장·명령 journal에 들어가지 않는다.

### 3.5 단계 B 종료

- strict free-placement slice loader와 고정소수점 기하 검사가 통과한다.
- 같은 명령은 줌·해상도와 무관하게 같은 정규화 좌표, 견적과 snapshot을 만든다.
- 수면 경계, 건물, 설비 접촉, 제3 설비 관통, 중복·공선, 최대 경간, 교차 비접속을 대표 검사로
  고정한다.
- 1280×720·UI 125%의 실제 viewport smoke에서 자유 배치, 자석 맞춤, 세 줌, pan, Home, 키보드
  cursor-follow와 Tab focus를 한 흐름으로 확인한다.
- 열 schema, 열 UI와 전체 캠페인은 이 단계에 선제 구현하지 않는다.

### 3.6 단계 B 종료 기록 — 2026-08-18

- spatial fixture SHA-256:
  `34d9db83449b696c52a9548c6dffcd267bab51f936c86e89024a20fe441667cb`
- CommercialChecks: **7 suites / 238 assertions PASS**
- Game Debug·Release build: **0 warnings / 0 errors**
- 명시 `CommercialMain.tscn`, 1280×720·UI 125% native 흐름:
  `COMMERCIAL_PLACEMENT_SMOKE_PASS minute=192 nodes=10 edges=5 zoom=전체 보기`
- 독립 exact-tree 검토: **P0 0 / P1 0**
- 열 field·열 UI·campaign placeholder 없이 자유 좌표 기반만 닫고 단계 C로 승격했다.

## 4. 단계 C — 이산 열 국면 기반

### 4.1 열 대상과 상태

- 선로는 도체, 변전소는 변압기·모선·인출부 중 제한 요소, 전신주는 기둥이 아닌 단자·퓨즈·개폐기·
  분기 접속 장치의 계획 한계를 대표한다.
- 각 열 설비 형식은 `continuousLimitKw`와 `emergencyLimitKw` 두 값만 갖고 발전원은 기존 출력용량만
  사용한다. `0 < continuous <= emergency`를 loader가 강제한다.
- 공개 상태는 `Continuous`, `Emergency`, `ProtectiveOutage`, `OverLimit` 네 가지다.
- 정확히 연속 한계인 사용은 연속 운전이다. 연속 초과·비상 이하인 설비는 현재 국면을 공급하고
  다음 국면 전체에서 보호정지한다. 한 국면 무부하 냉각 뒤 그다음 국면부터 자동 복귀한다.
- 실제 섭씨, 연속 축열 수치, 가변 냉각·지속시간, 화재·열화와 수동 차단 조작은 만들지 않는다.

### 4.2 국면과 급전

단계 C의 typed thermal interval fixture/request가 활성 수요, 사고 사용불가, 현재 연속·비상 한계와
의무 등급을 소유한다. 캠페인·저장 명령 없이 순수 interval sequence를 다음 순서로 평가한다.

1. 이전 보호정지, authored 사용불가와 현재 한계를 적용한다.
2. 현재까지 예약된 사용량을 포함해 모든 발전원·단순 경로 후보를 비교하고 수요를 순서대로 배정한다.
3. 모든 수요 배정 뒤 공유 선로·변압기·전신주 접속부의 합산 사용량을 한 번 확정한다.
4. 비상 운전 설비를 다음 국면 보호정지로, 이번 국면을 쉰 설비를 다음 국면 복귀로 기록한다.

한 수요는 한 발전원·한 단순 경로로 전량 공급하거나 0이다. 후보 경로 총순서는 다음과 같다.

1. 허용 열 등급
2. 비상 운전 설비 수 오름차순
3. 경로 최소 남은 적용 한도 내림차순
4. authored 발전 순서
5. 정수 경로 길이
6. 구간 수
7. edge ID 열의 사전식 순서
8. 끝 node ID

연속 설비의 남은 적용 한도는 `continuous-use`, 비상 설비는 `emergency-use`다. 모든 source·endpoint·
simple path를 비교하며 먼저 본 발전원의 실패에서 멈추지 않는다.

### 4.3 비상 한계 사용 권한

| 의무 | 허용 |
|---|---|
| 평상시 안전 의무와 모든 국면의 운영 기록 | 연속 운전만 |
| 플레이어가 지키기로 한 도시 약속 | 연속 → 비상, 승인 전 직접 확인 |
| 이름 붙은 비상 국면의 안전 의무 | 연속 → 비상, 뒤 공개 안전 의무를 깨지 않을 때만 허용 |
| 미루기로 한 도시 약속 | 해당 국면 수요 후보에서 제외 |

thermal evaluator는 작성된 고정망과 interval sequence를 순수 계산한다. 추천 해법은 만들지 않고
공급, 공급원, 첫 병목, 경로 최소 열여유, 비상 운전과 다음 보호정지를 typed 결과로 반환한다.

### 4.4 단계 C 종료

- 연속·비상 경계값, 긴 연속 경로 대 짧은 비상 경로, 공유 접속부 합산, permission, 보호정지,
  한 국면 냉각과 재개를 대표 검사로 고정한다.
- 같은 interval sequence를 반복 평가한 결과와 다음 열 상태가 값으로 같다.
- 작은 작성망 native smoke에서 선택 설비의 사용/연속/비상/다음 상태, 열 overlay와 projection 변경을
  색 외 패턴·아이콘·문장으로 확인한다.
- 0~100 열 게이지, 범용 scheduler와 수동 급전은 열지 않는다.

### 4.5 단계 C 종료 기록 — 2026-08-21

- `release-world-v2.json` SHA-256:
  `1b3485ca3aa0ae6302d0c245d13b8947d314507eeaa9e2d56561c07fbe6113ed`
- CommercialChecks: **12 suites / 283 assertions PASS**
- Game Debug·Release rebuild: **0 warnings / 0 errors**
- 명시 `CommercialMain.tscn` native 회귀:
  `COMMERCIAL_PLACEMENT_SMOKE_PASS minute=228 nodes=19 edges=19 zoom=전체 보기`
- 1280×720·UI 125% 열 UI:
  `COMMERCIAL_THERMAL_SMOKE_PASS phases=3 selected=SOUTH_SUBSTATION patterns=continuous|emergency|protective-outage`
- 독립 exact-tree 검토: **P0 0 / P1 3**. 도시 약속 외 의무의 임의 제외, 비열 endpoint·접속점의
  사용불가 누락, 100,000 simple-path 임의 중단을 모두 수정하고 전용 반례를 재실행해 **open P0/P1
  0**으로 닫았다.
- 범용 scheduler, 0~100 게이지와 수동 급전을 열지 않고 단계 D로 승격했다.

## 5. 단계 D — 상용 핵심 흐름

`첫 불빛`의 짧은 무벌점 자유 배치 prelude와 4장 완료 상태의 본편 5장 `누구의 여유인가`를 같은
실제 제품 경로에 연결한다.

단계 D는 `commercial-core-slice-v1.json` 하나가 prelude, 4장 완료 시작 seed와 5장 국면을 소유한다.
여덟 장 placeholder나 부분 `release-campaign-v2.json`을 만들지 않는다. 단계 E에서 최종 campaign
v2를 열고 단계 F 종료 시 strict loader가 정확히 여덟 임무를 강제한다.

- 한 장은 `브리핑 → 예고 확인 → 공사 경계 1 → 운영 확인 → 공사 경계 2 → 사건 승인 → 결과`의
  최대 세 결정 경계만 사용한다.
- campaign은 `SafetyDuty`, `CityPromise`, `OperatingRecord`를 typed obligation으로 소유한다.
- 도시 약속은 지킴/미룸 중 하나를 명시적으로 선택한다. 지원금·필수 진행 조건을 바꾸지 않고 실제
  공급 후보, 결과 사실과 후일 기록만 바꿔 softlock을 만들지 않는다.
- 공사기간은 authored deadline과 비교한다. 하나의 공사만 active일 수 있고 공사반·큐·배속은 없다.
- 최근 공사 복구는 현재 장에서 마지막으로 완공한 한 project bundle 직전 journal checkpoint로
  되돌린다. 좌표·현금·시각·국면·약속·열 상태를 fresh replay로 복원한다.
- 결과 카드는 실제 발전원, 경로, 비상 운전, 보호정지, 지킨 안전 의무·약속과 영향을 받은 시설에서
  제한된 사실 문장을 만든다. 임의 칭찬과 generic story engine은 금지한다.
- 같은 핵심 흐름에 지도·패널·story·환경음·통전·보호정지 cue의 최종 표현 경계를 적용한다.

campaign v2는 범용 조건식 대신 다음 닫힌 모양만 허용한다.

- chapter: briefing, objective, grant, optional city promise, ordered decision windows, ordered operating
  phases, standard 또는 kept/deferred result card
- decision window: 다음 phase ID, optional story, optional positive build-minutes allowance
- operating phase: ordered load bundles, `ContinuousOnly` 또는 `SafetyEmergencyAllowed`, authored
  unavailable assets/risk areas, 낮아지는 class thermal-limit overrides
- load bundle: `MustSupply`, `CityPromise`, `OperatingRecord` 중 하나

V2 운영 명령은 건설 명령 외 `SetPromiseDecision(Keep|Defer)`와 `ApproveDecisionWindow`만 추가한다.
승인은 현재 window부터 다음 window 직전까지의 phase를 순서대로 평가·커밋하고, 마지막 window는 장
끝까지 처리한다. safety duty 실패나 미래 공개 safety duty를 깨는 열 사용이 있으면 world·자금·열
상태를 바꾸지 않고 거부한다.

단계 D 자동·native 증거는 두 유효 설계 원형, 대표 병목, 약속 두 선택, deadline 경계, rollback과
결과 사실, 모든 공개 phase preview=승인 결과, save→fresh restore와 rollback의 좌표·자금·국면·약속·
열 상태 동등성을 고정한다. 재미·이해 여부를 PASS라고 주장하지 않는다. 사용자의 전체 구현 지시에 따라
단계 E 이후 제작을 진행하되 `CommercialSliceHumanStatus = NOT_COLLECTED`로 남긴다.

단계 B·C에서는 `CommercialMain.tscn`을 명시 scene으로만 실행하고 `project.godot`의 기본 장면은
동결 기준선 `ReleaseMain.tscn`으로 유지한다. 단계 D 핵심 흐름의 자동·native 증거가 닫히는 커밋에서
기본 장면을 `CommercialMain.tscn`으로 전환하고, 이후 `ReleaseMain`은 명시 scene 회귀로만 실행한다.

### 5.1 단계 D 종료 기록 — 2026-08-21

- `release-world-v2.json` SHA-256:
  `f7af0a74c6819839eee542e11d7eabf49c298fbab5bd262c9bcd8f9672bddde0`
- `commercial-core-slice-v1.json` SHA-256:
  `d607cb6a8d2a637fc5fa88b560c45690cdf271cecb89050b4bcb21c0acf29632`
- CommercialChecks: **17 suites / 682 assertions PASS**. 두 유효 설계, 병목, 약속 두 선택, deadline,
  preview=approval, recent-project fresh replay, save v3 복원을 포함한다.
- Game Debug·Release rebuild: **0 warnings / 0 errors**. v1 release·product·Scope 0B·Scope 1 동결
  회귀도 모두 통과했다.
- 기본 `CommercialMain.tscn`, 1280×720·UI 125% 실제 입력 흐름:
  `COMMERCIAL_CORE_SMOKE_PASS chapters=2 choice=Keep path=7 emergency=1 rollback=fresh-replay preview=approval`
- 자유 배치·열 native 회귀:
  `COMMERCIAL_PLACEMENT_SMOKE_PASS minute=228 nodes=21 edges=22 zoom=전체 보기`,
  `COMMERCIAL_THERMAL_SMOKE_PASS phases=3 selected=SOUTH_SUBSTATION patterns=continuous|emergency|protective-outage`
- 독립 exact-tree 검토: **P0 0 / P1 4**. 전용 수요 terminal strict 검증, prelude 결과와 사건 story
  표시, 실제 시설·발전원·경로·약속·비상·보호정지 결과 문장, 환경음·발주·완공·통전·보호정지 cue를
  모두 수정하고 관련 자동·native 흐름을 재실행해 **open P0/P1 0**으로 닫았다.
- `project.godot`의 기본 장면을 `CommercialMain.tscn`으로 전환했다. `ReleaseMain`은 명시 scene
  회귀로만 남는다.
- `CommercialSliceHumanStatus = NOT_COLLECTED`. 이해·재미를 기계 통과로 주장하지 않고 단계 E로
  승격했다.

## 6. 단계 E·F — 여덟 임무 전체 캠페인

### 6.1 임무 구조

프롤로그 세 임무와 본편 다섯 장은 같은 v2 world, campaign runner와 UI를 사용한다.

| 순서 | 임무 | 새로운 핵심 질문 |
|---|---|---|
| 1 | 첫 불빛 | 자유 배치로 첫 생활권을 어떻게 연결할까? |
| 2 | 두 번째 심장 | 두 회선을 실제로 다른 위험 회랑에 놓을 수 있는가? |
| 3 | 두 번째 전원 | 경로 전체의 연속 한도와 공유 접속부를 읽을 수 있는가? |
| 4 | 북안의 약속 | 서비스권역과 미래 분기 공간 중 무엇을 남길 것인가? |
| 5 | 누구의 여유인가 | 도시 약속을 위해 비상 열여유와 다음 보호정지를 감수할 것인가? |
| 6 | 물이 닿기 전에 | 기한 안에 범람 밖 회랑을 만들 수 있는가? |
| 7 | 꺼야 지킬 수 있다 | 계획정지 중 남은 경로의 열여유로 무엇을 지킬 수 있는가? |
| 8 | 가장 긴 밤 | 열·보호정지·범람이 이어질 때 앞선 망과 약속이 누구를 지키는가? |

1~4장은 안내를 단계적으로 줄이고, 5장 뒤 새 건설·열 규칙을 추가하지 않는다. 6~8장은 같은 동사를
지형, 기한, 정지 순서와 이전 열 상태의 다른 조합으로 사용한다. 모든 본편 장은 장단점이 다른
checker-owned 유효 설계 원형 두 개와 대표 실패·복구 하나를 가진다.

### 6.2 콘텐츠와 결과

- 장별 데이터는 briefing, objective, operating phases, deadline, active loads, contingency,
  obligations, optional promise, result fact templates와 visual/audio cue key만 가진다.
- 네 명의 고정 인물과 시작·사건·결과 카드만 사용한다. 대화 선택지·호감도·분기 결말 엔진은 없다.
- 결과 fact는 실제 공급·경로·열·약속 상태에서만 채운다. 판정하지 않은 `모두`, `완전히`, `안전하게`를
  쓰지 않는다.
- 긴 장간 시간경과는 데이터에 명시될 때만 모든 열 설비를 복귀시키고 UI가 그 사실을 알린다.
- 장 시작·최근 공사·결정 경계 checkpoint를 journal command count로 파생한다. 실패 저장도 같은
  실패를 restore하며 New Game은 항상 가능하다.
- 캠페인 완료 뒤 에필로그와 장 시작 상태 선택을 제공한다. 종합 점수나 별점은 만들지 않고 각 장의
  지킨 의무, 약속, 사용한 비상 운전과 남은 자금을 사실 기록으로 비교한다.

### 6.3 단계 E·F 종료

- 대표 full run은 모든 장에서 한 번 이상 의미 있는 공사를 하고 여덟 결과를 거쳐 에필로그에 도달한다.
- 각 본편 장의 두 원형이 hard obligation을 만족하고 전면 우월한 하나의 해법이 아님을 bounded
  witness로 확인한다.
- 장별 failure→recent rollback 또는 chapter restart가 복구 가능하며 이전 성공이 미래 장을 영구
  softlock하지 않는다.
- 두 fresh process native 흐름에서 4장 뒤 저장·종료, 새 실행 재개, 5~8장·에필로그, 완료 저장 재개와
  장 선택을 확인한다.
- 임무별 Game 코드 분기와 임무별 별도 native runner는 만들지 않는다.

## 7. 단계 G — 시청각·접근성·패키징 마감

### 7.1 화면과 접근성

- 상단은 장·다음 경계·현금·필수 공급, 중앙은 도시와 망, 오른쪽은 의무·약속·선택 경로·열여유,
  하단 고정 영역은 현재 도구와 행동만 표시한다.
- 선택 수요의 발전원→전체 경로→최소 용량·열여유→수요처를 지도에서 강조한다.
- `평상/작성 국면` projection을 전환하면 사용불가, 실제 경로, 보호정지와 지킨 의무가 함께 바뀐다.
- 연속/비상/정지/복귀는 색 외에 선 모양, 패턴, 아이콘과 접근성 문장을 사용한다.
- 최소 지원 해상도는 1920×1080이다. UI 100/125%, 마우스·키보드 동등성과 한국어 glyph를
  지원한다. 핵심 행동은 패널 스크롤 밖에 숨지 않는다. 1280×720은 지원·검수 대상이 아니다.

### 7.2 시각·사운드

- 한 도시 지도에 주거·의료원·정수장·산업단지의 두세 상태, 강 수위·통제선·정비 표식과 장별
  조명·날씨를 적용한다.
- 네 인물은 작은 고정 초상과 카드 색을 사용한다. 컷신·립싱크·음성은 만들지 않는다.
- 도시 환경음, 장별 날씨 층, 발주·완공·통전·차단·경고·결과 cue와 첫 점등·마지막 우회의 짧은
  motif만 사용한다.
- 최종 runtime 자산은 자체 제작 또는 재배포 권리가 명확해야 하고 manifest·credits·notice에
  출처와 license를 기록한다. 필수 상태를 소리로만 전달하지 않는다.

### 7.3 저장과 패키지

- strict settings v3는 기존 화면·Master/Ambient/SFX 값을 보존해 v2를 한 번만 승격하고
  `ReduceMotion`만 추가한다. write는 v3만 한다.
- campaign/save/settings는 atomic same-directory temp→flush→replace를 사용하며 실패를 현재 화면에
  즉시 알린다. Save & Quit은 성공 뒤에만 제목으로 간다.
- v2 world·campaign byte와 build identity는 assembly resource로 포함하고 runtime repo path를 읽지 않는다.
- macOS 내부 후보는 필요한 새 제품 scene/resource와 license만 포함하고 prototype·v1 fixture·PDB·
  로컬 절대경로를 제외한다.
- Developer ID와 notarization 자격증명이 없으면 ad-hoc 내부 후보로만 기록한다. 공개 배포 가능이라고
  표현하지 않는다.

## 8. 전체 완료 증거

검증은 간결하게 유지하고 같은 사실을 여러 runner에서 반복하지 않는다.

1. v1 release·product 회귀는 기존 frozen runner로 한 번 통과한다.
2. CommercialChecks 한 번이 strict v2 loader, 자유 기하, 건설 원자성, 열 경계·routing·상태전이,
   preview=실행, save prefix·rollback, 여덟 임무의 두 원형과 대표 실패를 검사한다.
3. Commercial Game Debug/Release rebuild가 warning/error 0이다.
4. 자유 배치 native smoke 1회, 열 UI smoke 1회, two-process 전체 캠페인 smoke 1회를 실행한다.
5. 1920×1080 × UI 100/125 두 화면과 keyboard/focus/reduce-motion을 bounded evidence로 남긴다.
6. clean committed checkout에서 내부 macOS ZIP을 만들고 저장소 밖에서 새 게임→저장→fresh continue→
   전체 완료를 한 번 실행한다. archive hash, embedded data hash, Universal binary, signature 경계,
   license, PDB·로컬경로·prototype 부재를 기록한다.
7. exact commit을 대상으로 독립 검토에서 P0/P1이 0이고 worktree가 clean하다.

종료 상태는 다음과 같다.

```text
ImplementationStatus = COMPLETE
CommercialSliceHumanStatus = NOT_COLLECTED
FullCampaignHumanStatus = NOT_COLLECTED
KoreanProfessionalProofStatus = NOT_COLLECTED
NewCandidateLlmObservationStatus = NOT_REQUESTED
PublicDistributionStatus = BLOCKED_UNTIL_SIGNING_NOTARIZATION_AND_OWNER_RELEASE_DECISION
```

사람 관찰은 단계 H의 외부 증거이고 LLM 관찰은 사용자가 별도로 요청할 때만 수행한다. 어느 쪽도
자동 완료 수치에 합산하지 않는다.

## 9. 명시적 제외

- 두 번째 도시, 샌드박스, 절차 생성, 사용자 제작 지도
- 실시간 수동급전, 확률 고장, AC 전력조류, 전압·무효전력·보호계전
- 부하 분할, 여러 공사반, 공사 queue·배속, 자재·인력 시뮬레이션
- 실제 섭씨, 풍속·일사별 동적 정격, 0~100 열 수치, 가변 냉각·열화·화재
- 곡선 선로, 자동 경로·전신주, 선로 몸체 snap, 도로 추종, 범용 완공망 편집기
- 연속 줌·회전·관성·미니맵
- 대화 선택지, 호감도, 범용 분기 스토리 엔진, 긴 컷신·음성
- 온라인, 업적, 순위표, 게임패드·모바일·다국어 동시 출시
- 반복 LLM 플레이, 사람을 대체하는 agent 평가와 자동 밸런스 튜닝

## 10. 단계 승격 규칙

단계 B→C→D→E/F→G 순서를 지킨다. 한 단계가 끝날 때 다음을 같은 단위에서 수행한다.

1. 구현과 필요한 최소 문서 갱신
2. 해당 단계 deterministic checks와 bounded native smoke
3. 독립 검토 한 번
4. 범위 안 P0/P1 수정과 관련 검사 재실행
5. exact evidence와 상태 기록
6. clean commit

검사 수를 늘려 불확실성을 숨기지 않는다. 기계 규칙·상태·빌드·저장·wiring은 자동화하고,
이해·재미·문체·가치는 단계 H 외부 증거로 정직하게 남긴다.
