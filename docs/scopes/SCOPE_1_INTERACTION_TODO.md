# Gridworks — Scope 1 TODO: 고정 terminal 사이 수동 선로 건설

> 상태: **미개방 후보 체크리스트**
>
> 개방 조건: Scope 0A의 활성 계약 판정 통과 → 별도 승인된 Scope 0B `GO` → 적응형 결과 점검에서
> `Interaction` 위험 선정 → 사용자의 별도 구현 승인

이 문서는 확정 roadmap이나 현재 backlog가 아니다. 체크박스는 “해당 gate를 열 때 닫아야 할 계약”을 미리 정리한 것이며 현재 구현 지시가 아니다. 선행 gate의 증거가 다른 위험을 가리키면 이 문서를 개방하지 않고 수정하거나 폐기한다. 현재 활성 scope는 루트 [README](../../README.md)가 지목한다.

## 1. 한 문장 가설

> 전력망 게임을 처음 접한 플레이어가 완공된 두 terminal 사이에 한 종류의
> 전신주·철탑을 하나씩 놓고, 모든 인접 span이 하나의 `MaxSpan` 이하인
> `LineProject`를 자동 배치 없이 완공하며, 거리 제한과 미완성 선로의 의미를
> 설명할 수 있는가?

이 gate는 수동 pole 배치와 거리 피드백이 읽히는지만 검사한다. 자유 배선의 장기 재미, 최저비용 경로, 발전소·변전소 입지, 철거와 완성 게임의 밸런스를 증명하지 않는다.

## 2. 개방 선행조건 TODO

- [ ] Scope 0A가 당시 활성 계약의 통과 판정으로 종료되었다.
- [ ] Scope 0B가 별도 승인되어 구현·자동검사·신규 참가자 검증을 마쳤다.
- [ ] Scope 0B 결과에서 서비스 권역·실제 공급과 공간 공통원인 이해가 회귀하지 않았다.
- [ ] 적응형 점검에서 수동 선로 상호작용이 다음 최대 미검증 위험으로 기록됐다.
- [ ] 현재 사용자에게 Scope 1 구현을 별도로 승인받았다.
- [ ] 같은 변경에서 루트 README가 이 문서를 활성 scope로 지목한다.
- [ ] 실제 Scope 0B에 존재하는 graph 도달성·원자 편입·단위·공사 상태와 제거할 authored 계층을 인계표로 기록한다.
- [ ] 신규 scope의 단일 machine-readable fixture 경로·schema·ID 권위를 활성화 변경에서 고정한다.
- [ ] 참가자 지시문, 기록 필드와 사전 rubric을 구현 전에 동결한다.
- [ ] 표본, 세션 상한, 통합 통과선과 bounded revision budget을 선행 증거에 맞춰 활성화 변경에서 고정한다.

한 항목이라도 충족되지 않으면 이 문서는 후보 상태로 남고 아래 TODO를 실행하지
않는다.

## 3. 단일 fixture TODO

- [ ] 이미 완공된 시작 terminal 하나와 목표 terminal 하나의 좌표를 고정한다.
- [ ] 시작 terminal은 통전, 목표 terminal은 완공됐지만 무전압으로 시작한다.
- [ ] 두 terminal의 직접 span은 `MaxSpan`을 명확히 초과하게 한다.
- [ ] 중간 지지물을 수동으로 놓으면 충분한 여유를 두고 완성할 수 있는 witness path를 손검산한다.
- [ ] 최소 pole 수를 정답으로 만들지 않고, 모든 span이 유효하면 추가 pole을 허용한다.
- [ ] 배치 영역을 가로지르는 기존 회선 하나를 두되 교차점에 terminal을 두지 않는다.
- [ ] line class 하나, 회선 전용 degree-2 support type 하나, `MaxSpan` 하나만 둔다.
- [ ] 좌표 단위, 거리 metric, 배치 좌표 양자화·snap 순서와 `<=` 경계 처리를 고정한다.
- [ ] 화면 pick radius는 비권위 Presentation 값으로 두고, 겹친 후보의 deterministic tie-break를 고정한다.
- [ ] 시작 현금은 모든 유효한 경로를 발주할 수 있게 두어 현금 부족이 선택을 가리지 않게 한다.
- [ ] Scope 0B에 재사용 가능한 공사 상태전이가 실제로 있으면 인계표대로 쓴다. 없으면 이 gate의 최소 상태전이를 `Structural`로 정의한다.
- [ ] 비용·공기 값은 선택을 만들지 않는 `FrozenFixture`로 활성화 시 고정하며 경제 증거를 주장하지 않는다.
- [ ] pole 수·총 길이별 비용 최적화는 제거하고 현재 quote가 경로 선택을 유도하지 않게 한다.
- [ ] terrain, 장애물, 공간 위험, 사건, 복수 부하와 대체 발전원은 fixture에 두지 않는다.
- [ ] validator가 직접 span의 실패, witness path의 성공, 모든 정격의 여유를 검사한다.

fixture의 exact 숫자는 활성화 변경의 기계 데이터에만 둔다. 이 후보 문서에는
임시 `MaxSpan`, 좌표, 비용과 기간을 하드코딩하지 않는다.

## 4. 포함 범위 TODO

### Draft 상호작

- [ ] 완공 terminal을 선택해 `LineProject` draft를 시작한다.
- [ ] 마지막 endpoint에서 빈 위치로 지지물을 하나씩 순서대로 배치한다.
- [ ] 마지막 배치를 되돌리는 `Undo`를 제공한다.
- [ ] 전체 draft를 버리되 graph·현금·게임시간을 바꾸지 않는 `Cancel`을 제공한다.
- [ ] 완공된 목표 terminal을 선택해 경로를 닫는다.
- [ ] 한 번에 하나의 draft만 편집한다.
- [ ] drag 이동·복수선택 대신 배치·undo·cancel로만 수정한다.

발주를 확정하는 주 경로는 다음 한 방향으로 진행한다.

```text
Idle → DraftOpen → Quoted → Ordered → Building → Commissioned
```

- [ ] `Back`은 발주 전 `Quoted → DraftOpen`으로 돌아가며 graph·현금·시간을 바꾸지 않는다.
- [ ] `Cancel`은 `DraftOpen` 또는 `Quoted`에서 draft를 버리고 `Idle`로 돌아간다.
- [ ] `Ordered` 이후에는 `Back`·`Cancel`을 허용하지 않는다. 발주취소는 이 gate의 제외 기능이다.
- [ ] `AddSupport`의 prospective span이 무효면 오류만 반환하고 support를 추가하지 않는다.
- [ ] `CloseAtTerminal`의 prospective span이 무효면 경로를 닫지 않고 `DraftOpen`을 유지한다.
- [ ] 유효한 terminal 종단만 `Quoted`로 전이하고, 발주 직전 전체 ordered path를 다시 검증한다.
- [ ] 실패 명령은 권위 상태를 바꾸지 않으며 플레이어는 support 추가·`Undo`·`Cancel`로 계속 조작할 수 있다.

### 거리와 topology

- [ ] 각 span을 다음 한 규칙으로 판정한다.

```text
SpanValid = distance(EndpointA, EndpointB) <= MaxSpan
```

- [ ] 한 span이라도 실패하면 전체 project 발주를 거부한다.
- [ ] 실패한 span 중 경로 순서상 첫 번째의 code·index·실제/허용거리만 반환한다.
- [ ] 정확히 `MaxSpan`인 span은 유효하게 처리한다.
- [ ] 같은 좌표를 이은 zero-length span, self-loop와 중복 span을 구조 오류로 거부한다.
- [ ] 각 지지물은 한 `LineProject`·한 회선 전용 degree-2 waypoint로 유지한다.
- [ ] 지지물에서 분기·합류하지 않는다.
- [ ] 다른 선로와 기하학적으로 교차해도 명시 terminal이 없으면 graph node를 만들지 않는다.
- [ ] 총 선로 길이와 pole 수에 별도 hard limit을 추가하지 않는다.

### 발주·공사·통전

- [ ] 유효한 전체 ordered path만 하나의 `LineProject`로 발주한다.
- [ ] 발주 전 지지물 수, 총 도체 길이, 고정 quote와 완공시각을 확인한다.
- [ ] 발주 시 인계표 또는 이 gate가 정의한 최소 비용·공사 상태전이를 한 번만 적용한다.
- [ ] draft와 공사 중 project는 graph·공급에 편입하지 않는다.
- [ ] 완공 분에 지지물과 span 전체를 graph에 원자적으로 한 번만 편입한다.
- [ ] graph 재계산 뒤에만 목표 terminal의 통전 표시를 바꾼다.
- [ ] 실패·undo·cancel은 현금, 승인된 공사 상태, ID allocator와 기존 graph를 바꾸지 않는다.

### 화면 피드백

- [ ] 현재 마지막 endpoint 중심의 `MaxSpan` 경계를 표시한다.
- [ ] cursor까지의 span을 유효·초과 상태로 즉시 미리 보여준다.
- [ ] 색뿐 아니라 pattern·선 형태·짧은 문장으로 상태를 구분한다.
- [ ] 첫 실패 span에 `실제 거리 / MaxSpan`과 `중간 지지물 필요`만 보여준다.
- [ ] 시스템이 중간 pole 위치를 추천·자동배치·자동보정하지 않는다.
- [ ] 교차점에 junction처럼 보이는 node·점·발광을 그리지 않는다.
- [ ] draft, 공사 중, 완공·통전 상태를 색 외의 pattern으로 구분한다.
- [ ] `왜?` 패널은 현재 첫 실패 원인만 설명하고 정답 경로를 제안하지 않는다.

## 5. 명시적 제외

- 발전소·변전소 자유 부지배치
- 발전소·변전소·지지물·선로 철거와 발주취소
- 완공 pole 이동, 부분 line 편집과 기존 경로 연장
- 분기 pole, 공유철탑, 다회선·메시와 세 개 이상 terminal
- 복수 support type, line class, 전압등급과 복수 `MaxSpan`
- pole별 공사 queue, 작업반 종류, 병렬공사와 자재
- terrain별 비용, 장력·처짐·풍하중·기초와 충돌회피
- 자동 pole 배치, 최단경로, 경로 추천과 optimizer
- 경제 선택, 수치 튜닝과 Static Balance Lab
- 폭염, 고장, 정비, 수리, 복구와 예고 타임라인
- DC/AC 전력조류, 손실·열·주파수와 확률사건
- 저장·replay, 3D, 범용 map editor와 미래 schema placeholder

## 6. 구현 TODO

### 계약과 데이터

- [ ] 활성 scope에 한 줄 가설, 포함·제외, 단일 fixture, 권위 경로와 완료조건을 확정한다.
- [ ] terminal, 기존 회선, line class와 support의 stable ID를 고정한다.
- [ ] 필드를 terminal, support, span, ordered draft와 `LineProject`의 현재 사용분만으로 제한한다.
- [ ] validation 결과를 `Valid` 또는 첫 실패의 code·span index·actual/max로 제한한다.
- [ ] fixture·Core·scene의 값 복제를 막는 단일 로드 경로를 둔다.
- [ ] 현재 scope의 parameter inventory와 각 family의 증거 상태를 기록한다.

### 순수 Core

- [ ] ordered endpoint 목록에서 인접 span을 결정론적으로 생성한다.
- [ ] 좌표를 권위 단위로 양자화한 뒤 거리를 판정한다.
- [ ] terminal 완공상태와 시작·종료 endpoint 적격성을 검증한다.
- [ ] support의 line 전용성, degree-2, 유일성과 유한 좌표를 검증한다.
- [ ] 선분 교차를 graph 접속으로 변환하지 않는다.
- [ ] invalid `AddSupport`·`CloseAtTerminal`의 권위 상태 불변과 `Undo`·`Back`·`Cancel`의 명시 전이를 보장한다.
- [ ] valid project 전체를 공사 완료 시 한 transaction으로 graph에 추가한다.
- [ ] 통전은 기존 graph의 온라인 발전원 도달 규칙으로만 재계산한다.
- [ ] 결정론적 ID 생성과 stable 순회 순서를 유지한다.

### 2D 표현과 입력

- [ ] terminal 선택 → support 배치 → terminal 종단 → quote → 발주의 한 흐름을 연결한다.
- [ ] `MaxSpan` 경계, cursor span, first-failure 피드백을 구현한다.
- [ ] 잘못된 클릭이 draft를 파괴하거나 자동 확정하지 않게 한다.
- [ ] camera zoom과 무관한 world-space 배치 결과와, 겹친 pick 후보의 deterministic tie-break를 확인한다.
- [ ] 발주 전·공사 중·시운전 후의 표시를 분리한다.
- [ ] 시각 표현은 [비주얼 명세](../product/VISUAL_PRODUCTION_SPEC.md)의 Interaction QA를 따른다.

## 7. 자동검사 TODO

### Fixture·단위

- [ ] 모든 ID가 유일하고 좌표·`MaxSpan`이 유한하며 권위 단위인지 검사한다.
- [ ] 시작·목표 terminal이 완공상태인지 검사한다.
- [ ] 직접 terminal span이 실패하고 witness path의 모든 span이 성공하는지 검사한다.
- [ ] `MaxSpan` 미만·동일·초과의 세 경계를 검사한다.
- [ ] 중간 span 하나만 초과해도 전체가 실패하는지 검사한다.
- [ ] 복수 실패에서 first-failure index가 안정적인지 검사한다.
- [ ] 중복 좌표, zero-length, self-loop와 중복 span을 거부하는지 검사한다.
- [ ] 미완공 terminal, 임의 지면점과 다른 회선 support를 시작·종료로 거부한다.
- [ ] committed support가 정확히 두 인접 span과 한 project만 갖는지 검사한다.
- [ ] 기하학적 교차가 graph node·adjacency를 추가하지 않는지 검사한다.
- [ ] support count와 총 conductor length 파생값이 재현 가능한지 검사한다.

### 상태전이·통합

- [ ] invalid 명령 전후의 권위 projection `(draft, GameMinute, graph, cash, 승인된 공사 상태, ID allocator)`이 동일한지 검사한다. command result와 진단 log는 제외한다.
- [ ] `Undo`·`Back`·`Cancel`이 허용된 draft 전이만 만들고 graph·현금·시간을 바꾸지 않는지 검사한다.
- [ ] draft와 공사 중 project가 전력을 전달하지 않는지 검사한다.
- [ ] 완료 분에 support·span 전체가 원자 편입되고 부분 통전 frame이 없는지 검사한다.
- [ ] 편입 전 무전압 target이 편입 뒤에만 발전원에 도달 가능해지는지 검사한다.
- [ ] 같은 fixture·명령열이 같은 validation·graph·현금 결과를 만드는지 검사한다.
- [ ] 실제로 인계한 Scope 0B의 서비스 권역·실제 공급 회귀검사를 유지한다.
- [ ] 교차 비접속은 Scope 1의 신규 invariant로 별도 검사한다.
- [ ] 부팅 → draft → invalid 시도 → 수동 support 추가 → 발주 → 완공 → target 통전 smoke test를 실행한다.

### 수동 시각 QA

- [ ] 초과 span이 색 없이도 식별된다.
- [ ] actual/max의 단위와 의미가 모호하지 않다.
- [ ] 교차점이 junction처럼 보이지 않다.
- [ ] 시스템이 추천 위치나 자동 pole을 암시하지 않다.
- [ ] draft·공사 중 선로가 통전선처럼 빛나지 않는다.
- [ ] 유효 경로가 최소 pole 수 퍼즐처럼 표현되지 않는다.

## 8. 검증 행위 TODO — 활성화 때 재작성

이 미개방 후보의 `사람/비전문가` 문구는 이전 초안이며 현재 실행 권위가 아니다. 현재 사용자
지시대로 첫 검증은 새 cold LLM proxy로 대체하고 `HumanValidationStatus = NOT_COLLECTED`를 유지해야
한다. Scope 1을 실제로 열 때 표본·runner·rubric을 한 번만 다시 쓰며, 아래 역사적 사람 절차를
그대로 실행하지 않는다.

### 운영

- [ ] Scope 0A/0B에 참여하지 않은 비전문가를 활성 계약의 표본만큼 모집한다.
- [ ] 활성 계약의 세션 상한과 동일 build·fixture·중립 지시문을 사용한다.
- [ ] 익명 참가자 ID, build/fixture version, 완료시간, undo 횟수, invalid 시도와 진행자 도움만 기록한다.
- [ ] 지시문은 “완공된 두 terminal을 새 회선으로 연결하세요” 이외의 전기·조작 설명을 하지 않는다.
- [ ] 진행자 도움 뒤의 성공은 통과로 세지 않는다.
- [ ] build·정보구조·fixture 기하가 바뀌면 새 version과 신규 참가자로 다시 시작한다.

### 사후 질문과 통과 판정

- [ ] 두 terminal을 바로 잇는 안은 왜 발주할 수 없는가?
- [ ] `MaxSpan` 원은 무엇을 뜻하는가?
- [ ] 어떤 단위 사이의 거리를 모두 만족해야 하는가?
- [ ] 기존 선로와 교차한 지점에서 두 회선은 접속되었는가?
- [ ] draft와 공사 중 선로는 언제부터 전기를 전달하는가?

교차 비접속 질문은 표현 문제를 찾는 진단값이다. 이 fixture에서는 잘못 접속해도 목표 terminal의
최종 통전 여부가 달라지지 않으므로 `IntegratedInteractionPass`에는 넣지 않는다.

같은 참가자가 아래를 모두 만족해야 `IntegratedInteractionPass`다.

- [ ] 진행자 도움 없이 유효한 경로를 발주·완공한다.
- [ ] 모든 인접 span이 `MaxSpan` 이하여야 함을 설명한다.
- [ ] 초과한 경로에는 중간 지지물을 직접 추가해야 함을 설명한다.
- [ ] draft·공사 중 project는 통전되지 않고 전체 완료 후에만 통전된다고 답한다.
- [ ] `FacilitatorHelp = false`다.

## 9. GO / REVISE / NO-GO

- [ ] 자동검사나 수동 시각 QA 미통과는 사람 테스트 전 preflight blocker이며 revision budget과 사람 증거의 `NO-GO`를 소비하지 않는다.
- [ ] preflight 통과 뒤 활성 계약의 사람 통합 통과선을 만족하면 `GO`다.
- [ ] `GO` 미달 원인이 한 표현·입력 규칙·parameter family에 귀속되면 남은 budget 안에서 `REVISE`다.
- [ ] `GO` 미달인데 단일 수정 원인이 없으면 즉시 `NO-GO`다.
- [ ] 한 라운드에서는 Presentation, 구조 규칙 또는 parameter family 하나만 바꾼다.
- [ ] 수정 시 새 build/version과 신규 참가자로 다시 검사한다.
- [ ] 활성 계약의 revision budget을 소진했는데 통과선 미달이면 `NO-GO`다.
- [ ] 자동 경로·복수 시스템 없이는 상호작용이 읽히지 않으면 `NO-GO`다.
- [ ] 통과를 위해 `MaxSpan`이나 fixture 좌표를 미세 조정하지 않는다.
- [ ] 최소 pole 수, 최단시간, 최저비용과 경로 모양은 통과 기준이 아니다.
- [ ] Scope 1 `GO`는 변전소·발전소·철거 또는 다음 번호 scope 구현을 자동 승인하지 않는다.

## 10. 파라미터 제한

- `ActiveKnob`: 0개
- 자동 sweep과 Static Balance Lab: 없음
- 목표 성공률을 위한 `MaxSpan`·좌표·tolerance 튜닝: 금지
- `Structural`:

  - terminal-only 접속
  - ordered polyline과 회선 전용 degree-2 support
  - 교차 비접속
  - 전체 경로 검증과 원자 편입
  - 실제로 인계했거나 이 gate에서 정의한 공사 상태전이

- `Derived`:

  - 각 span 거리
  - 총 conductor 길이
  - support 수

- `FrozenFixture` family 상한:

  1. 지도·terminal·기존 교차선 geometry
  2. support type·line class·`MaxSpan`
  3. 배치 좌표 양자화·snap
  4. 비판정 fixed quote·duration 값

결론을 뒤집을 수 있는 `FrozenFixture + ActiveKnob`은 최대 4개 family다. geometry나
`MaxSpan`을 바꾸면 기존 참가자 결과와 합산하지 않는다.

화면 pick radius는 `Presentation`이며 위 family에 넣지 않는다. 이를 바꿔 통과선을 맞추지 않는다.

## 11. 즉시 중단 조건

- [ ] 유효 경로를 만들기 위해 terrain, optimizer, 복수 support class가 필요하다.
- [ ] 기본 조작을 설명하기 위해 변전소·발전소 배치나 철거까지 함께 열어야 한다.
- [ ] invalid draft가 graph·현금·승인된 공사 상태를 변경하거나 부분 통전을 만든다.
- [ ] 교차 비접속을 최소 표현으로 설명할 수 없다.
- [ ] Scope 0의 서비스 권역·실제 공급 인과가 직접 배치 UI 때문에 다시 흐려진다.
- [ ] 하나의 fixture와 최대 4개 parameter family로 가설을 닫을 수 없다.

중단 조건을 만족하면 기능을 추가하지 않고 당시 증거와 실패 원인을 기록한 뒤
scope를 종료한다.

## 12. 완료 산출물·저장소 checkpoint

- [ ] 승인된 Scope 1 실행 계약과 단일 machine-readable fixture
- [ ] fixture validator와 witness path·경계값 oracle
- [ ] 고정 terminal 사이 manual line-placement 2D prototype
- [ ] 순수 Core 단위·상태전이·통합·smoke 회귀검사
- [ ] 중립 지시문·사전 rubric·익명 기록표
- [ ] 한 페이지 `GO / REVISE / NO-GO` 결과와 예상 밖 관찰
- [ ] parameter inventory와 각 family의 증거 상태
- [ ] 구현·자동검사·사람 증거를 이 문서에 기록한다.
- [ ] 루트 README의 현재 개발 상태를 실제 상태와 일치시킨다.
- [ ] [오브젝트 카탈로그](../product/OBJECT_CATALOG.md)의 pole·line 가능 상태와 확인된 제한을 갱신한다.
- [ ] 관찰로 표현 규칙이 바뀐 경우에만 비주얼 명세를 갱신한다.
- [ ] 모든 문서에서 legacy 경로, stale 상태, 중복 권위와 모순을 검사한다.
- [ ] 큰 작업단위의 첫 커밋을 만든다.
- [ ] 독립 subagent에게 scope 경계·oracle·문서 일관성을 bounded review 받는다.
- [ ] scope-valid 지적만 수정하고 모든 검사를 다시 실행한다.
- [ ] 검토 반영 커밋을 만든다.
- [ ] 사용자의 명시적 승인 없이 push·PR·다음 gate를 실행하지 않는다.

Scope 1 `GO`는 수동 선로 상호작 가설만 지지한다. 완성 게임의 재미, 변전소·발전소·철거·경제 또는 다음 번호 gate를 자동 승인하지 않는다.
