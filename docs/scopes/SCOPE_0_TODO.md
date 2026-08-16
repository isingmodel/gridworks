# Gridworks — Scope 0 TODO: 핵심 인과 카드 → authored playable

> 상태: **Scope 0B official v6 `GO` — `Scope0State = REVIEWED`; Scope 1 준비 완료·구현 닫힘**
>
> 실행 권위: 루트 [README](../../README.md)가 지목한 활성 scope
>
> 구성: 종료된 [R1 카드 계약](SCOPE_0A_CARD_TEST.md) → 종료된 [R2 계약](SCOPE_0A_R2_CARD_TEST.md) → 완료된 [Scope 0B 계약](SCOPE_0B_PLAYABLE.md)

이 문서는 Scope 0 전체의 **순서·산출물·checkpoint**를 추적하는 비권위 실행 색인이다. 숫자, topology, oracle, 참가자 기준과 실행 절차는 루트 README가 지목한 활성 scope만 정한다. 이 문서를 단독 절차서로 사용하지 않으며, 누락·충돌 시 활성 scope를 따른다.

R1은 `PROXY-FAIL`로 끝났지만 [Scope 0A R2](SCOPE_0A_R2_CARD_TEST.md)는 네 field와 integrated
모두 `5/5`로 `PROXY-PASS`했다. [R2 결과](../../playtests/scope-0a-r2/RESULT.md)와 완료된 checkpoint는
Scope 0B 계약을 열 수 있게 한 역사적 승인 증거다. 완료된 실행 권위는
[Scope 0B 계약](SCOPE_0B_PLAYABLE.md)에 보존된다. 계약 checkpoint 뒤 구현·자동검사·독립 코드 review와
구현 checkpoint까지 완료했다. [L00 결과](../../playtests/scope-0b/L00_RESULT.md)는 실제 full run으로
`PASS`했다. 공식 v1~v5는 실행 증거 규칙 문제로 모두 판정 없이 `PROXY-RUN-BLOCKED`이며 상세 이력은
[evidence package](../../playtests/scope-0b/README.md)와 각 checkpoint가 소유한다. reviewed
[checkpoint 1F](../../playtests/scope-0b/CHECKPOINT_1F_RUN_PROTOCOL_V6.md)의 v6는 별도 runner evidence
schema 없이 다섯 고정 row를 실행했고, [공식 결과](../../playtests/scope-0b/RESULT.md)는 모든 row
`COMPLETED`와 전 항목 `5/5`로 `GO`를 기록한다.

## 1. Scope 0의 목적과 종료상태

Scope 0이 묻는 질문은 하나다.

> 서비스 권역·실제 공급, 전기적 분리·공간 독립, 병원 연속성·전력회사 공급을
> 비전문가 이해의 임시 proxy인 cold LLM이 카드와 최소 직접 조작에서 일관되게 구분하는가?

```text
0B_ACTIVE → 고정 다섯 row GO → REVIEWED
                               └─ 적응형 점검 → 다음 gate 선정 또는 terminal 유지
```

`Scope0State = REVIEWED`는 Scope 1 구현 승인이 아니다. 결과 점검에서 다음 위험을 다시 선정하고,
사용자가 별도로 승인해야 다음 gate를 열 수 있다.

종료된 R1·R2의 경로와 판정은 각각의 계약과 checkpoint가 소유한다. R2는 네 field와 integrated
모두 `5/5`로 `PROXY-PASS`했으며 Scope 0B의 역사적 승인 근거일 뿐 현재 실행 상태가 아니다.

아래 §2~§6의 완료 체크는 R1 당시의 불변 기록이다. 종료된 R2의 자료·절차·판정은
[R2 계약](SCOPE_0A_R2_CARD_TEST.md)과 [R2 checkpoint](../../playtests/scope-0a-r2/CHECKPOINT_1_MATERIALS_FREEZE.md)가
소유하며, R1 체크박스를 R2 완료로 재사용하지 않는다.

## 2. R1 역사적 계약·권위 TODO

- [x] Scope 0A의 완전한 실행 계약이 존재한다.
- [x] Scope 0B의 최대 후보 경계와 개방 조건이 분리돼 있다.
- [x] 각 작업단위 시작 전에 루트 README가 지목한 활성 scope를 다시 확인한다.
- [x] 활성 scope 전체를 읽고 이 색인의 해당 묶음이 빠뜨린 요구를 보충한다.
- [x] 카드·진행자 시트·기록표의 `CardSetVersion`을 `S0A-CARD-v1`으로 동결한다.
- [x] Scope 0A 실행 전에 문서의 동결 fixture·oracle·rubric과 제작물을 대조한다.
- [x] 카드 내부에 stable ID·개발자 용어·숨은 숫자가 노출되지 않는지 검사한다.
- [x] 새로 닫은 proxy·카드 상태 값은 실행 당시 활성 Scope 0A에서만 정의했다.

## 3. R1 역사적 Scope 0A 제작 TODO

이 절부터 §6까지는 R1 실행 당시 완료한 불변 기록이다. 세부 의미와 완료조건은 종료된
[R1 Scope 0A 계약](SCOPE_0A_CARD_TEST.md)이 소유하며 R2 또는 Scope 0B 절차로 사용하지 않는다.

### 산출물 준비

- [x] [Scope 0A §3](SCOPE_0A_CARD_TEST.md#3-네-장의-카드)에 맞는 저충실도 16:9 카드 네 장을 제작한다.
- [x] 카드 1은 서비스 권역과 상위 연결 부재 하나만 검사한다.
- [x] 카드 1 응답 기록 후 고정 접속공사 완료를 고지하고 카드 2로 넘어가는 진행자 문장을 고정한다.
- [x] 카드 2는 병원 주 회로, 내부전원, 의무와 읽기 전용 예고 타임라인을 보여준다.
- [x] 카드 3은 두 authored 회랑을 같은 순서·크기·시각 무게로 비교한다.
- [x] 카드 4는 선택 전에 두 계획×두 제거사건을 모두 예측하게 하고 응답 후에만 인과·정산 결과를 차례로 공개한다.
- [x] `SAFE/RISKY`, 추천색, 방패·해골과 총점을 사용하지 않는다.
- [x] 서비스 권역, 통전, 공사, 사용불가와 내부전원을 색 외의 pattern·선·label로 구분한다.
- [x] [비주얼 명세](../product/VISUAL_PRODUCTION_SPEC.md)의 Scope 0A 카드 QA를 통과한다.

### 진행자 시트·기록

- [x] 중립 지시문, 카드 전환 문장, 질문 순서와 추가 설명 금지를 한 장에 정리한다.
- [x] Scope 0A의 기록 필드와 사전 rubric을 그대로 사용한다.
- [x] 진행자 도움 후 고친 답을 통과로 세지 않는다.
- [x] 배치 순서 variant를 세션별로 번갈아 배정한 표를 준비한다.
- [x] 자유응답 원자료는 공개 저장소에 커밋하지 않고, 식별 불가능한 집계·오해 유형만 남기도록 `private/`를 Git에서 제외한다.
- [x] proxy 라운드는 이름·연락처·음성을 수집하지 않으며 model/session metadata만 로컬 보관한다.

## 4. R1 역사적 Scope 0A 사전 검증 TODO

- [x] 모든 카드·진행자 자료의 링크, 해상도, 폰트, 대비, 여백과 16:9 표시를 수동 검사한다.
- [x] 한국어 문구가 카드의 경로·상태·숫자를 가리지 않는지 확인한다.
- [x] Scope 0A topology 제거 행렬을 deterministic 검사로 확인한다.
- [x] 병원 내부전원 에너지·지속시간·잔여시간 oracle을 다시 검산한다.
- [x] 전력회사 인도, 미공급, 판매, 가스비, 보상과 기말현금에 중복·누락이 없는지 검산한다.
- [x] `M`, `CashUnit`, `MW`, `MWh/GWh`, `GameMinute`의 단위 변환이 닫히는지 확인한다.
- [x] 카드의 두 회랑·두 사건 결과가 동결 fixture와 일치하는지 확인한다.
- [x] 내부 dry run `L00`으로 절차·기록표 누락만 확인하고 본 집계에서 제외한다.
- [x] dry run은 카드 변경을 요구하지 않았으며 `S0A-CARD-v1`을 그대로 동결했다.
- [x] 카드·진행자 자료 동결 checkpoint를 마친 뒤 LLM proxy 테스트로 넘어갔다. 기록: [`CHECKPOINT_1_MATERIALS_FREEZE.md`](../../playtests/scope-0a/CHECKPOINT_1_MATERIALS_FREEZE.md)

## 5. R1 역사적 Scope 0A LLM proxy 테스트 TODO

`LLM-PROXY-R1`은 available runner evidence 기준 기술 유효 `5/5`, coverage `0/5`, 위험 인과 `4/5`, 내부전원 경계·trade-off 각 `5/5`, integrated `0/5`로 `PROXY-FAIL`이었다. L02가 coverage 외에도 전기/공간 인과를 섞어 revision의 단일결손 조건을 충족하지 못했다. 원문은 Git 제외 로컬 파일에, hash·집계·결정은 [`RESULT.md`](../../playtests/scope-0a/RESULT.md)에 보존한다.

- [x] 세션 수, cold-context 조건, 금지 도구, 배치 variant와 통과선은 [Scope 0A §2](SCOPE_0A_CARD_TEST.md#2-proxy-세션과-운영)와 [§6](SCOPE_0A_CARD_TEST.md#6-산출물과-완료조건)을 그대로 따랐다.
- [x] 다섯 세션 모두 같은 공개 model identifier·runner 설정을 쓰고 fork·memory 없이 시작하며 participant PNG와 고정 진행자 문장 외 context를 주지 않았다.
- [x] available runner evidence의 도구 보고를 확인했다. 금지 도구가 보고된 세션은 없었다.
- [x] 참가자별로 권역, 전기/공간 원인, 내부전원, trade-off와 진행자 도움을 사전 기록표에 남겼다.
- [x] 선택률·좋아하는 계획·응답속도를 통과 목표로 사용하지 않았다.
- [x] 사후에 정답 문구만 맞추지 않고 필수 이유까지 사전 rubric으로 판정했다.
- [x] 동일 참가자의 필수 항목을 통합해 `IntegratedCausalPass`를 계산했다.
- [x] 원답, 공개 model 설정, 카드 hash, 보고된 도구 사용과 round timestamp를 Git 제외 로컬 표에 보존했다.
- [x] 원답 SHA-256, 식별 불가능한 개수·오해 유형·예상 밖 응답과 `HumanValidationStatus = NOT_COLLECTED`를 한 페이지로 요약했다.

## 6. R1 역사적 Scope 0A 결정 TODO

R1 decision checkpoint: [`CHECKPOINT_2_R1_DECISION.md`](../../playtests/scope-0a/CHECKPOINT_2_R1_DECISION.md). `PROXY-FAIL`이므로 revision은 허용되지 않았고 `SCOPE_0_STOPPED`가 terminal state다.

- [x] 자동·수동 사전 검사 결과와 LLM proxy 원자료를 분리해 보관했다.
- [x] 사전 정의된 `PROXY-PASS / PROXY-REVISE / PROXY-FAIL`만 사용하고 결과를 본 뒤 기준을 바꾸지 않았다.
- [x] `PROXY-REVISE`의 단일결손 조건을 검사했고 L02의 두 번째 결손 때문에 거짓으로 판정했다.
- [x] revision 조건이 거짓이므로 카드·prompt·version을 바꾸거나 새 세션을 실행하지 않았다.
- [x] `PROXY-FAIL`을 자유 배선·경제·물리 부족으로 해석해 기능을 추가하지 않았다.
- [x] `PROXY-PASS` 분기는 해당 없음으로 기록하고 사람 증거를 주장하지 않았다.
- [x] Scope 0B를 시작하지 않고 `SCOPE_0_STOPPED`로 종료했다.
- [x] 판정과 익명 집계를 기록한 뒤 결과 checkpoint를 완료했다. 기록: [`CHECKPOINT_2_R1_DECISION.md`](../../playtests/scope-0a/CHECKPOINT_2_R1_DECISION.md)

## 7. Scope 0B 활성화 TODO — R2 `PROXY-PASS`와 결과 checkpoint 뒤

아래 항목은 backlog가 아니라 조건부 gate다. 현재 사용자 목표는 R2 통과와 결과 checkpoint를
조건으로 Scope 0B 계약·구현을 승인했으며, 조건 충족 전에는 실행하지 않는다.

- [x] 사용자의 조건부 구현 승인과 발효 조건을 R2 계약에 기록했다.
- [x] Scope 0A의 식별 불가능한 집계·예상 밖 결과를 연결했다. R2 scored 오해는 없으므로 새 오해를 만들지 않았다.
- [x] playable에서 다시 검사할 잔여 transfer risk 하나만 선정했다.
- [x] [Scope 0B 계약](SCOPE_0B_PLAYABLE.md)을 증거에 맞는 완전한 실행 계약으로 작성했다.
- [x] 한 문장 가설, 포함·제외, 산출물과 중단조건을 고정했다.
- [x] 참가자 조건, 기록 필드, 사전 rubric, 통과선과 bounded revision budget을 고정했다.
- [x] 단일 machine-readable fixture의 경로, schema, stable ID, 좌표·시간·화폐 단위를 고정했다.
- [x] Scope 0A의 값·ID·제거행렬·oracle이 새 fixture와 일치하는 인계검사를 통과했다.
- [x] reviewed contract commit `01c3c279edfcd3b5b5c743bad5476b1b87ce3dbc` 뒤 새 fixture가
  **Scope 0B의** 기계 숫자 권위가 된 시점을 기록했다. Scope 0A 문서와 결과는 불변 권위로 보존한다.
- [x] 공사 완료·편입·의무·계량·사건·복구·절체의 같은 분 적용 순서를 고정했다.
- [x] 최소 명령, 실패 무상태변경, LLM 조작 proxy 질문과 exact 통과선을 구현 전에 닫았다.
- [x] 구현 TODO, 자동검사, build·smoke와 LLM 조작 proxy 절차를 활성 Scope 0B 문서 안에 작성했다.
- [x] 공식 지원 상태를 확인하고 Godot .NET·.NET SDK exact patch를 고정했다.
- [x] 자유 배치·일반 BFS·범용 scheduler 등 통과에 필요하지 않은 항목을 제거했다.
- [x] 같은 변경에서 루트 README를 Scope 0B 계약 review로 전환하고 Scope 0A 결과를 링크했다.
- [x] 계약 작업단위 checkpoint를 완료한 뒤에만 구현을 열었다. 기록:
  [`CHECKPOINT_0_CONTRACT_FREEZE.md`](../../playtests/scope-0b/CHECKPOINT_0_CONTRACT_FREEZE.md)

## 8. Scope 0B 실행 TODO — 활성 계약과 checkpoint 완료 후에만

이 절은 미래 구현 내용을 선결하지 않는다. 정확한 class, command, UI와 test 목록은
Scope 0A 증거를 반영해 활성화된 Scope 0B가 소유한다.

### 구현·자동 증거 단위

- [x] 활성 Scope 0B의 TODO만 구현하고 이 색인에서 누락된 기능을 추론해 추가하지 않는다.
- [x] 권위 규칙·상태전이·정산과 표현·입력 계층의 경계를 활성 계약대로 지킨다.
- [x] fixture 인계검사, 단위·상태전이·보존식·결정론 검사와 대표 smoke를 모두 통과한다.
- [x] 자유 배치, 상세 물리, 저장·replay, 미래 schema와 placeholder UI가 artifact에 없는지 확인한다.
- [x] 실행한 명령, 결과와 예상 밖 기술 관찰을 재현 가능하게 기록한다.
- [x] 자동검사 전체 통과 뒤 구현 checkpoint를 마쳤다. 기록:
  [`CHECKPOINT_1_IMPLEMENTATION_FREEZE.md`](../../playtests/scope-0b/CHECKPOINT_1_IMPLEMENTATION_FREEZE.md)

### LLM 조작 proxy·판정 단위

- [x] 공식 v1의 six-launch evidence를 감사했고, 다섯 완료 세션 모두 허용되지 않은 bootstrap
  tool-catalog 조회 때문에 `TechnicalValid = 0/5`임을
  [`protocol reset checkpoint`](../../playtests/scope-0b/CHECKPOINT_1B_RUN_PROTOCOL_V2.md)에 기록했다.
- [x] v1을 gameplay 판정 없이 `PROXY-RUN-BLOCKED`로 닫고 응답·launch를 v2와 합산하지 않았다.
- [x] 게임·fixture·UI·rubric·gate를 그대로 둔 v2 direct-wrapper 실행 계약의 bounded review를 닫았다.
- [x] v2를 gameplay 판정 없이 `PROXY-RUN-BLOCKED`로 닫고 응답·launch를 다른 version과 합산하지
  않았다. 기록: [`v2 result / v3 reset`](../../playtests/scope-0b/CHECKPOINT_1C_RUN_PROTOCOL_V3.md)
- [x] 같은 build·fixture·UI·rubric·gate를 유지한 v3 content-source 계약의 bounded review를 닫았다.
- [x] 검토를 마친 동일 build·fixture·rubric을 한 라운드 동안 동결했다.
- [x] v3의 prompt identity 불일치와 `TechnicalValid = 2/5`를 판정 없이
  [`v4 reset checkpoint`](../../playtests/scope-0b/CHECKPOINT_1D_RUN_PROTOCOL_V4.md)에 보존했다.
- [x] 같은 build·fixture·UI·rubric·gate를 유지한 v4 single-source prompt 계약의 bounded review를 닫았다.
- [x] v4의 evidence-format 불일치와 `TechnicalValid = 2/5`를 판정 없이
  [`v5 준비 checkpoint`](../../playtests/scope-0b/CHECKPOINT_1E_RUN_PROTOCOL_V5.md)에 보존했다.
- [x] 같은 build·fixture·UI·rubric·gate를 유지한 v5 evidence contract를 동결하고 bounded review를 닫았다.
- [x] v5의 필수 runner evidence 미기록을 판정 없이 닫고 filesystem 시각으로 사후 복구하지 않았다.
- [x] 같은 build·fixture·UI·rubric·수치 gate에서 별도 runner wrapper를 제거한 v6 계약과 rehearsal을
  정의했으며 review·승인 상태는 checkpoint 1F가 소유한다.
- [x] checkpoint 1F가 승인한 protocol의 신규 cold LLM session·실제 화면 조작·무도움 절차만 따랐다.
- [x] 조작 결과를 서비스 권역·상위 연결, 전기·공간 원인과 내부전원·전력회사 공급에 귀속해 기록했다.
- [x] 계획 선택률이나 미세 조정된 성공률을 통과 목표로 사용하지 않았다.
- [x] 원자료는 공개 저장소 밖에 두고 비식별 집계와 예상 밖 행동만 보존했다.
- [x] `HumanValidationStatus = NOT_COLLECTED`를 유지하고 사람 사용성 증거로 표현하지 않았다.
- [x] `SubGateDecision = GO`와 `Scope0State = REVIEWED`를 기록했다.
- [x] 판정 뒤 결과 작업단위의 독립 review와 문서 최신성 점검을 닫았다.

## 9. 종료된 파라미터 정책

- Scope 0A 수치는 각 카드 계약, Scope 0B 수치는 승인된
  [`scope-0b-v1.json`](../../data/scope-0b-v1.json)이 소유한다.
- 선택률·성공률을 목표로 수치를 조정하지 않았고 v1~v6 결과를 합산하지 않았다.
- 자동 sweep과 목표 점수용 LLM 튜닝은 사용하지 않았다.

## 10. 완료 checkpoint 색인

| 작업단위 | 완료 증거 |
|---|---|
| R1 카드 자료 | [`CHECKPOINT_1_MATERIALS_FREEZE.md`](../../playtests/scope-0a/CHECKPOINT_1_MATERIALS_FREEZE.md) |
| R1 판정 | [`CHECKPOINT_2_R1_DECISION.md`](../../playtests/scope-0a/CHECKPOINT_2_R1_DECISION.md) |
| R2 카드 자료 | [`CHECKPOINT_1_MATERIALS_FREEZE.md`](../../playtests/scope-0a-r2/CHECKPOINT_1_MATERIALS_FREEZE.md) |
| R2 판정 | [`CHECKPOINT_2_R2_DECISION.md`](../../playtests/scope-0a-r2/CHECKPOINT_2_R2_DECISION.md) |
| Scope 0B 계약 | [`CHECKPOINT_0_CONTRACT_FREEZE.md`](../../playtests/scope-0b/CHECKPOINT_0_CONTRACT_FREEZE.md) |
| Scope 0B 구현 | [`CHECKPOINT_1_IMPLEMENTATION_FREEZE.md`](../../playtests/scope-0b/CHECKPOINT_1_IMPLEMENTATION_FREEZE.md) |
| Scope 0B 판정 | [`CHECKPOINT_2_DECISION.md`](../../playtests/scope-0b/CHECKPOINT_2_DECISION.md) |

반복 작업 규칙은 루트 [`AGENTS.md`](../../AGENTS.md)가 소유한다. 이 종료 색인은 새 checkpoint
절차나 미래 실행 권한을 만들지 않는다.

## 11. 다음 gate 선정과 Scope 1 준비

Scope 0 뒤의 다음 gate는 번호 순서가 아니라 새 증거로 선정한다. 적응형 점검은 제품의 직접
건설 약속 중 증거가 없고 독립 가능한 가장 작은 위험인 `Interaction`을 선택했다. 결과는
[Scope 1 준비 계약](SCOPE_1_INTERACTION.md)에 있다.

- [x] Scope 0B가 조건부 승인·구현됐고 자동검사와 LLM 조작 proxy 기준을 `GO`로 통과했다.
- [x] Scope 0 결과 문서의 독립 review와 최신성 점검을 완료했다.
- [x] Scope 0 후 적응형 점검에서 수동 pole·`MaxSpan` 상호작용을 다음 최대 미검증 위험으로 선정했다.
- [x] 실제 Scope 0B의 공사·실패 불변·원자 편입 원칙만 인계하고 범용 lifecycle을 가정하지 않았다.
- [x] Scope 1이 묻지 않을 서비스 권역·경제·사건 시스템을 제외했다.
- [x] Scope 0 증거에 맞게 Scope 1의 임시 표본·rubric·범위와 pre-code fixture 표를 다시 썼다.
- [ ] 사용자가 Scope 1 구현을 별도로 승인했다.
- [x] 같은 변경에서 README·Scope 0 종료 색인·Scope 1 준비 계약·문서 링크를 갱신했다.

하나라도 미충족이면 Scope 1을 구현하지 않는다.
