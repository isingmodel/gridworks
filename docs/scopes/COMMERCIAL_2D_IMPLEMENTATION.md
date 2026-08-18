# Gridworks 상용 2D 게임 구현

> 상태: **단계 F 완료·단계 G 활성**
> 구현 권한: **단계 G와 관찰 기반 선행 보정 backlog**
> 현재 작업: **선행 보정 → 최종 표현·settings v3 → 네 화면·키보드 증거 → 내부 package**
> 승인 근거: 사용자는 2026-08-18 보이는 격자를 없앤 자유 배치와 전선·변전소·전신주 접속부의
> 열 한계를 채택했고 상용 재기획서 구현을 지시했다. 단계 F 전체 감사 뒤 사용자 종료 조건에 따라
> 목표 추구를 멈췄다. 사용자가 2026-08-19 단계 G와 관찰 기반 선행 보정 backlog 구현을 새로
> 명시적으로 승인했다. 단계 H 사람 검증과 공개 배포는 승인하지 않았다.

이 문서는 [상용 2D 게임 재기획서](../product/COMMERCIAL_2D_GAME_DESIGN_PLAN_KO.md)의 단계 B~F 완료
기록과 활성 단계 G 계약이다. 재기획서가 경험·콘텐츠·표현 원칙을,
이 문서가 실행 권위·단계 순서와 완료 증거를 소유한다.

기존 `release v1` 경로와 macOS 내부 후보는 기술 기준선으로 동결한다. 새 게임은 그 타입과 저장을
점진적으로 일반화하지 않고 별도 v2 규칙·데이터·기본 장면을 사용한다. 각 단계는 구현, 결정론적
검사, 한정된 native 확인, 독립 검토와 커밋을 끝낸 뒤 다음 단계로 넘어간다.

## 1. 최종 결과

전체 계획 완료 시 Gridworks는 한 도시를 끝까지 책임지는 소형 싱글 플레이 2D 전력망 전략·퍼즐
게임이다. 현재 증거는 아래 목표 가운데 단계 F까지의 캠페인·규칙·제품 흐름만 완료했음을 뜻한다.

- 보이는 셀과 격자 맞춤 없이 지형 위에 전신주와 변전소를 자유롭게 놓는다.
- 수면·건물·설비 점유영역을 피하면서 직선 선로로 분기·합류망을 만든다.
- 선로 도체, 변전소 주기기와 전신주 접속부의 연속·비상 열 한계를 읽는다.
- 작성된 국면마다 평상·사고·열 상태를 미리 보고, 비상 운전 뒤 보호정지와 냉각을 계획한다.
- 안전 의무와 선택 가능한 도시 약속, 공사기간과 결정 경계를 비교한다.
- 최근 공사를 되돌리거나 임무 시작으로 복구할 수 있다.
- 프롤로그 세 임무와 본편 다섯 장에서 같은 지도·망·자금·결과 기록을 이어 쓴다.
- 실제 망 상태에 맞는 브리핑·사건·결과·에필로그, 2D 도시 반응과 최종 사운드를 제공한다.
- Title, 저장·재개, 설정, 접근성과 저장소 밖 macOS 내부 패키지를 새 제품 경로에서 사용한다.

단계 F 완료는 재미·문체·시장성에 대한 사람 승인이나 공개 배포 승인을 뜻하지 않는다. 단계 G의
최종 표현·접근성·패키징과 단계 H의 사람 관찰, 한국어 전문 교정, Developer ID 서명·공증 및 공개
상점 절차는 아직 수행하지 않은 별도 게이트다.

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
| 화면·음량·움직임 설정 — 단계 G 활성 | `user://settings.json`, strict settings v3 |

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

### 4.5 단계 C 종료 기록 — 2026-08-18

- final world v2 SHA-256:
  `c838ce789b44f29eff916d909c6864d76ffe2e135d4635aa7b8e8c7bde1682c4`
- CommercialChecks: **13 suites / 350 assertions PASS**
- Core와 Game Debug·Release·ExportRelease build: **0 warnings / 0 errors**
- 명시 `CommercialMain.tscn` native 열 화면:
  `COMMERCIAL_THERMAL_SMOKE_PASS projections=3 asset=EDGE_WATER states=Emergency>ProtectiveOutage>Continuous`
- 독립 검토가 찾은 실패 진단 경로와 strict ID P1 두 건을 집중 반례로 수정한 뒤 재검토:
  **P0 0 / P1 0**
- 실제 온도·연속 축열·범용 scheduler·수동 급전과 campaign placeholder 없이 이산 열 기반만 닫고
  단계 D로 승격했다.

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

### 5.1 단계 D 종료 기록 — 2026-08-18

- final world v2 SHA-256:
  `c838ce789b44f29eff916d909c6864d76ffe2e135d4635aa7b8e8c7bde1682c4`
- core slice SHA-256:
  `8d09a0745cf560246ac54b5a9774dfb98a2c0aab61f7c19d7b3316140f700842`
- CommercialChecks: **19 suites / 1,312 assertions PASS**
- Game Debug·Release·ExportRelease build: **0 warnings / 0 errors**
- 서로 다른 Godot 프로세스가 같은 v3 저장을 사용한 1280×720·UI 125% native 흐름:
  `COMMERCIAL_CORE_SMOKE_LEG1_PASS segment=CHAPTER_FIVE_SEGMENT commands=9`,
  `COMMERCIAL_CORE_SMOKE_LEG2_PASS complete=True commands=17 outcome=WHOSE_MARGIN`
- 첫 프로세스는 자유 좌표 `첫 불빛`을 끝내고 본편 시작점에 저장했다. 두 번째 프로세스는 그 저장을
  복원해 산업 약속, 일반 회랑의 비상 운전, 다음 보호정지와 실제 결과 카드까지 완료했다.
- Core와 Game 독립 exact-tree 검토에서 입력·저장 실패 P0/P1을 수정한 뒤 **P0 0 / P1 0**이었다.
- 빠른 smoke 종료 뒤 Godot가 오디오 객체 4개의 종료 정리 경고를 남겼지만 두 프로세스 모두 exit 0,
  저장·복원 marker는 PASS였다. 정상 종료와 package 경계는 단계 G에서 다시 확인한다.
- `CommercialSliceHumanStatus = NOT_COLLECTED`. 재미·이해를 자동 증거로 주장하지 않는다.
- `project.godot` 기본 장면을 `CommercialMain.tscn`으로 전환하고 단계 E로 승격했다.

## 6. 단계 E·F — 여덟 임무 전체 캠페인

### 6.1 임무 구조

프롤로그 세 임무와 본편 다섯 장은 같은 v2 world, campaign runner와 UI를 사용한다.

| 순서 | 임무 | 새로운 핵심 질문 |
|---|---|---|
| 1 | 첫 불빛 | 자유 배치로 첫 생활권을 어떻게 연결할까? |
| 2 | 두 번째 심장 | 두 접속 회선을 갖추고 범람 차단시험 때 공급을 유지할 수 있는가? |
| 3 | 두 번째 전원 | 경로 전체의 연속 한도와 공유 접속부를 읽을 수 있는가? |
| 4 | 북안의 약속 | 서비스권역과 미래 분기 공간 중 무엇을 남길 것인가? |
| 5 | 누구의 여유인가 | 도시 약속을 위해 비상 열여유와 다음 보호정지를 감수할 것인가? |
| 6 | 물이 닿기 전에 | 기한 안에 범람 밖 회랑을 만들 수 있는가? |
| 7 | 꺼야 지킬 수 있다 | 계획정지 중 남은 경로의 열여유로 무엇을 지킬 수 있는가? |
| 8 | 가장 긴 밤 | 열·보호정지·범람이 이어질 때 앞선 망과 약속이 누구를 지키는가? |

1~4장은 단계 E에서, 5~8장은 단계 F에서 닫았다. 1~4장은 안내를 단계적으로 줄이고,
5장 뒤 새 건설·열 규칙을 추가하지 않았다. 6~8장은 같은 동사를
지형, 기한, 정지 순서와 이전 열 상태의 다른 조합으로 사용한다. 모든 본편 장은 장단점이 다른
checker-owned 유효 설계 원형 두 개와 대표 실패·복구 하나를 가진다.

### 6.2 콘텐츠와 결과

- 장별 데이터는 briefing, objective, operating phases, deadline, active loads, contingency,
  obligations, optional promise와 result fact templates까지만 가진다. visual/audio cue key와 실제 자산
  연결은 단계 G에서 연다.
- 네 명의 고정 인물과 시작·사건·결과 카드만 사용한다. 대화 선택지·호감도·분기 결말 엔진은 없다.
- 결과 fact는 실제 공급·경로·열·약속 상태에서만 채운다. 판정하지 않은 `모두`, `완전히`, `안전하게`를
  쓰지 않는다.
- 긴 장간 시간경과는 데이터에 명시될 때만 모든 열 설비를 복귀시키고 UI가 그 사실을 알린다.
- 장 시작·최근 공사·결정 경계 checkpoint를 journal command count로 파생한다. 실패 저장도 같은
  실패를 restore하며 New Game은 항상 가능하다.
- 캠페인 완료 뒤 에필로그와 장 시작 상태 선택을 제공한다. 종합 점수나 별점은 만들지 않고 각 장의
  지킨 의무, 약속, 사용한 비상 운전과 남은 자금을 사실 기록으로 비교한다.

### 6.3 단계 E 종료 기록 — 2026-08-18

- final world v2 SHA-256:
  `c4923f752205c193efa78ddb4ca9e5431801731e6087be3ba3796abf9117ac14`
- final campaign v2 SHA-256:
  `d8290bb41ef0b9f284e75c951cbb259f41d39d12cc550f8281dfc6b14025815f`
- `첫 불빛`부터 `북안의 약속`까지 같은 world·network·현금·시간을 이어 쓴다. 실제 변전소를 놓고
  서비스 권역 안의 수요를 발전원 경로로 공급하며, 2장은 병원 접속 수 2와 범람 차단시험 중 공급을
  검사한다. 이 gate는 두 회선의 완전한 전기·공간 독립성을 증명하지 않는다.
- 4장 진입 직전 6개월을 경과시키고 열 상태를 초기화하며, 카드가 두 사실을 알린다. 결과는 typed
  공급·미공급·남은 자금과 4장 약속 지킴/미룸만 렌더링한다.
- 최근 완공 project, 현재 장과 이전 장 복구, 실패 상태 재개, final campaign save v3 fresh restore를
  닫았다. 같은 저장 URI의 단계 D save는 첫 final write 전에 SHA-12가 붙은 결정론적 백업으로 한 번만
  보존하며 백업 실패 시 final save를 덮어쓰지 않는다.
- CommercialChecks: **26 suites / 2,402 assertions PASS**
- Game Debug·Release·ExportRelease build: **0 warnings / 0 errors**
- 서로 다른 fresh Godot 프로세스 marker:
  `COMMERCIAL_CAMPAIGN_SMOKE_LEG1_PASS chapter=SECOND_SOURCE commands=48`,
  `COMMERCIAL_CAMPAIGN_SMOKE_LEG2_PASS complete=True chapters=4 commands=80`
- exact-tree Game 검토: **P0 0 / P1 0**. 사람 검토: `NOT_COLLECTED`.

### 6.4 단계 F 종료 조건

- 대표 full run은 모든 장에서 한 번 이상 의미 있는 공사를 하고 여덟 결과를 거쳐 에필로그에 도달한다.
- 각 본편 장의 두 원형이 hard obligation을 만족하고 전면 우월한 하나의 해법이 아님을 bounded
  witness로 확인한다.
- 장별 failure→recent rollback 또는 chapter restart가 복구 가능하며 이전 성공이 미래 장을 영구
  softlock하지 않는다.
- 두 process native 흐름에서 4장 뒤 저장·종료, 새 실행 재개, 5~8장·에필로그, 두 번째 process의
  제목 화면에서 완료 저장 재개와 장 선택을 확인한다. 완료 bytes의 별도 load/restore도 검사한다.
- 임무별 Game 코드 분기와 임무별 별도 native runner는 만들지 않는다.

### 6.5 단계 F 종료 기록 — 2026-08-18

- final world v2 SHA-256:
  `c4923f752205c193efa78ddb4ca9e5431801731e6087be3ba3796abf9117ac14`
- final campaign v2 SHA-256:
  `078df95f9f0c833be7e1a299088b4ab6e0de4ddf13426ce5b96a1abbeee70b7a`
- 5~8장은 같은 world·network·현금·시간·열 상태를 이어 쓴다. 산업·동부 생활권 약속, 범람 밖 공사,
  서부 전원 계획정지, 최대수요·폭염·계산형 보호정지와 범람을 실제 typed 결과로 처리하고 세 약속을
  에필로그에서 회수한다.
- 각 장의 의미 있는 공사, 두 설계 원형, 대표 실패와 최근 공사·현재 장 복구를 bounded witness로
  고정했다. M6의 실제 보강 원형 witness를 바로잡은 뒤 전체 run에서 미래 softlock이 없음을 확인했다.
- CommercialChecks: **29 suites / 4,486 assertions PASS**
- Game Debug·Release·ExportRelease build: **0 warnings / 0 errors**
- 1280×720·UI 125%의 서로 다른 fresh Godot 프로세스 marker:
  `COMMERCIAL_CAMPAIGN_SMOKE_LEG1_PASS chapter=WHOSE_MARGIN commands=82`(뒤에는 실행별 저장 경로),
  `COMMERCIAL_CAMPAIGN_SMOKE_LEG2_PASS completedResume=True replay=WHOSE_MARGIN completedCommands=136 replayCommands=82`
- 두 번째 프로세스는 5~8장과 에필로그를 완료하고 같은 프로세스의 제목 화면에서 완료 저장을
  재개한 뒤 `누구의 여유인가` 시작 checkpoint를 선택했다. 별도 store load/restore assertion으로
  디스크에 기록된 완료 저장 bytes도 확인했다.
- M6 witness 수정 뒤 독립 exact-tree 최종 감사: **P0 0 / P1 0**
- `FullCampaignHumanStatus = NOT_COLLECTED`, `KoreanProfessionalProofStatus = NOT_COLLECTED`.
  자동증거는 사람의 이해·재미·문체 승인이나 상용 출시 준비 완료를 뜻하지 않는다.
- 사용자 종료 조건에 따라 전체 감사 뒤 목표 추구를 멈췄고 당시 단계 G를 자동 활성화하지 않았다.
  이후 2026-08-19의 별도 사용자 지시가 단계 G를 열었다.

### 6.6 사용자 중단 공식 LLM 관찰과 사후 리뷰 — 2026-08-18

사용자 요청으로 commit `36038a90d74708a4bebd9dbc5b2a5ea6907d44aa`의 기본 제품 장면을
빈 사용자 저장, 1280×720 native 창에서 처음 보는 LLM 한 명이 한 번 조작했다. 참가자는 저장소·
source·data·로그·web과 기존 대화를 보지 않고 Computer Use만 사용했다. 약 62분 동안 1~7장을
통과했으며, 8장 `가장 긴 밤`의 `폭염 정점 · 2/3`에서 화면에 표시된 일반 오류
`비상 운전 여유도 500 kW 부족`의 구체적인 병목을 찾던 중 사용자가 실행을 중단했다. 에필로그와
완료 화면에는 도달하지 않았고, 앱 재실행·두 번째 새 게임·장 되감기는 사용하지 않았다. 초안 취소
두 번과 도시 약속 변경 한 번은 게임 안의 정상 복구로 사용했다.

이 candidate에는 단계 F 뒤 사용자가 직접 보고한 오른쪽 패널 축소 문제의 bounded 수정이 포함된다.
정보 scroll은 최소 200 px을 유지하고 약속·국면·도구·편집 조작은 keyboard focus를 따라 드러나며,
`운영안 승인`과 `공사 발주`만 고정 footer에 남는다. exact-tree 검토의 P0/P1은 0이었지만 네 Stage G
화면 조합 증거는 아직 수집하지 않았다.

따라서 플레이 관찰의 종료 상태는 `USER_STOPPED`다. 캠페인 `SUCCESS`, 게임 규칙 `FAILURE`, 자력
진행 `BLOCKED` 중 어느 것으로도 재분류하지 않는다. 중단 뒤 사용자가 같은 참가자에게 별도 사후
리뷰를 요청했으므로, 무후속을 요구한 원래 공식 cold **완료 검증 protocol은 `INVALIDATED`** 됐다.
중단 전 플레이 구간과 중단 후 리뷰의 출처는 보존하되 둘을 완료 검증으로 합산하지 않는다. 아래
증거는 특정 build의 비인간 관찰이며 사람 사용성·재미·밸런스, 성공률, 출시 준비 또는 단계 G 완료
증거가 아니다.

- candidate, world와 campaign hash는 §6.5의 동결 실행물과 일치했다.
- 공식 v3 save SHA-256:
  `30c65457d5552816dfe1437664cfe538d40d0c14872d365a4b0089a10c7d249e`
  저장 전후 bytes가 동일한 상태에서 exact Core API로 strict restore했다. 복원 결과는 111 commands,
  7개 장 완료, `LONGEST_NIGHT`의 `FINAL_OPERATING_PLAN_WINDOW`, `CampaignComplete = false`,
  `CanApprove = false`, 현금 12,505,000원이었다. 화면에서 선택돼 있던 `폭염 정점 · 2/3` tab은
  저장하지 않는 view 상태이므로 복원된 Core 상태와 구분한다.
- engine log SHA-256:
  `678be1a5c713f54beb463daf16a33bc57ef6b439cdf48326e1db931eb7842dc0`.
  Godot 4.7.1/OpenGL 시작 기록만 있고 오류·crash는 없었다.
- 참가자 platform session ID: `01a014c6-66e5-7073-bdbc-a33e737b4446`, transcript SHA-256:
  `a250255c6ca59a8397010fb4351cb9361bef1747e5810aaeab459727d61dcd6c`.
- 격리한 공식 user data를 보존한 뒤 원래 user data를 복원했다. 사본과 session transcript는 Git에
  넣지 않는 기존 private evidence 위치에 남긴다.

참가자는 한 도시와 망을 계속 쓰는 구조, 도시 약속과 전기 규칙의 결합, 다국면 예고, 자유 배치,
서비스 권역과 실제 전력 경로의 구분, 복구 수단과 실제 결과 fact를 강점으로 보았다. 반면 최종 종합
문제에서는 일반 부족량만으로 병목을 찾아야 했고, 배치 입력의 성공·거부 상태, 승인 전 필수 조건과
조밀한 지도 선택, 누적 공사 기한을 읽는 비용이 컸다고 보고했다. 이는 한 참가자의 관찰·추론이며
독립 재현된 결함이나 수치 조정 근거가 아니다. 상세 후속 항목과 수용 경계는
[로드맵의 관찰 기반 backlog](../ROADMAP_2D.md#관찰-기반-선행-보정-backlog--단계-g-활성)가 소유하고,
[상용 2D 게임 재기획서](../product/COMMERCIAL_2D_GAME_DESIGN_PLAN_KO.md)는 제품 원칙만 소유한다.
모든 항목은 `OBSERVATION_INFORMED / AUTHORIZED / VALIDATION_REQUIRED`이며, 재현 결과와 구현 증거를
분리해 기록한다. 이것은 사람 사용성·재미 증거나 Stage H 활성화를 뜻하지 않는다.

## 7. 단계 G — 시청각·접근성·패키징 마감 — 활성

단계 G는 현재 활성 단계다. §6.6 관찰 backlog를 먼저 재현·보정하고 아래 항목을 구현·검증한다.
단계 F 규칙·콘텐츠를 다시 설계하거나 Stage H 사람 검증을 시작하지 않는다.

- 최종 도시 아트, 장별 날씨, 네 인물 초상과 audio cue 자산
- settings v3와 움직임 줄이기
- 1280×720·1920×1080 × UI 100/125 네 조합과 키보드 동등성 evidence
- 패키징·서명·법적·license 정리와 새 설치 user-data에서의 전체 캠페인 실행

### 7.1 화면과 접근성

- 상단은 장·다음 경계·현금·필수 공급, 중앙은 도시와 망, 오른쪽은 의무·약속·선택 경로·열여유,
  오른쪽 하단 고정 우선 행동 영역은 현재 결정 경계의 승인·완공만 표시한다. 약속·국면·도구·편집
  같은 보조 조작은 정보와 함께 하나의 focus-follow scroll에 두고, 정보 viewport 높이를 최소
  200 px로 유지한다.
- 선택 수요의 발전원→전체 경로→최소 용량·열여유→수요처를 지도에서 강조한다.
- `평상/작성 국면` projection을 전환하면 사용불가, 실제 경로, 보호정지와 지킨 의무가 함께 바뀐다.
- 연속/비상/정지/복귀는 색 외에 선 모양, 패턴, 아이콘과 접근성 문장을 사용한다.
- 움직임 줄이기, UI 100/125%, 1280×720·1920×1080, 마우스·키보드 동등성과 한국어 glyph를
  지원한다. 핵심 행동은 패널 스크롤 밖에 숨지 않는다.

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

## 8. 전체 완료 증거 — 단계 G gate

다음은 단계 G에서 전체 내부 출시 후보를 만들 때 필요한 gate다. 단계 F 종료 시점에는 6.5의
캠페인 검사·build·native·감사 증거만 닫혔고, 네 화면 evidence·패키지·새 설치 전체 실행은 수집하지
않았다. 검증은 간결하게 유지하고 같은 사실을 여러 runner에서 반복하지 않는다.

1. v1 release·product 회귀는 기존 frozen runner로 한 번 통과한다.
2. CommercialChecks 한 번이 strict v2 loader, 자유 기하, 건설 원자성, 열 경계·routing·상태전이,
   preview=실행, save prefix·rollback, 여덟 임무의 두 원형과 대표 실패를 검사한다.
3. Commercial Game Debug/Release rebuild가 warning/error 0이다.
4. 자유 배치 native smoke 1회, 열 UI smoke 1회, two-process 전체 캠페인 smoke 1회를 실행한다.
5. 1280×720·1920×1080 × UI 100/125 네 화면과 keyboard/focus/reduce-motion을 bounded evidence로 남긴다.
6. clean committed checkout에서 내부 macOS ZIP을 만들고 저장소 밖에서 새 게임→저장→fresh continue→
   전체 완료를 한 번 실행한다. archive hash, embedded data hash, Universal binary, signature 경계,
   license, PDB·로컬경로·prototype 부재를 기록한다.
7. exact commit을 대상으로 독립 검토에서 P0/P1이 0이고 worktree가 clean하다.

현재 종료 상태는 다음과 같다.

```text
StageFImplementationStatus = COMPLETE
GoalSeekingStatus = ACTIVE_STAGE_G
StageGStatus = ACTIVE
CommercialSliceHumanStatus = NOT_COLLECTED
FullCampaignHumanStatus = NOT_COLLECTED
KoreanProfessionalProofStatus = NOT_COLLECTED
OfficialCommercialLlmObservationStatus = USER_STOPPED_REVIEW_COLLECTED
OfficialCommercialCompletionProtocolStatus = INVALIDATED_BY_USER_FOLLOWUP
OfficialCommercialNativeCompletion = NOT_CONFIRMED
OfficialCommercialFollowUpCount = 1
OfficialCommercialHumanEvidence = NOT_APPLICABLE
CommercialReleaseReadyStatus = NO
PublicDistributionStatus = BLOCKED_UNTIL_SIGNING_NOTARIZATION_AND_OWNER_RELEASE_DECISION
```

사람 관찰은 단계 H의 외부 증거다. §6.6의 사용자 요청 LLM 관찰과 사후 리뷰는 그 사람 증거를
대체하지 않으며 자동 완료 수치에 합산하지 않는다. 추가 LLM 실행도 새 사용자 지시 없이는 수행하지
않는다.

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

단계 G는 2026-08-19 사용자 지시로 열렸다. 단계 G gate와 clean commit이 닫히기 전에는 Stage H 사람
검증·전문 교정·공개 배포를 시작하지 않는다.
