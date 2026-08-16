# Scope 1 공식 단일 관찰 — L01

> `ObservationStatus = COMPLETED`
>
> `IntegratedPlacementPass = true`
>
> `OfficialSampleProgress = 1/1`
>
> `Scope1Status = COMPLETED`
>
> `CompletionBasis = USER_ACCEPTED_SINGLE_OBSERVATION`
>
> `HumanValidationStatus = NOT_COLLECTED`

## 실행 경계

2026-08-17 사용자는 공식 LLM 실행을 정확히 한 번만 승인했고, 결과를 확인한 뒤 이 한 번의 pass를
충분한 Scope 1 종료 근거로 수용했다. 따라서 공식 관찰은 `1/1`로 닫고 Scope 1을 완료로 기록한다.
추가 row, 재시도, 교체와 수정 라운드는 실행하지 않는다.

직전에 완료한 비공식 전체 실행은 동일 build의 기술 사전점검으로만 사용했다. 그 실행은 build·fixture
identity, 실제 native 입력, `READY → SUPPORT_ADDED → SUPPORT_ADDED → ORDERED → COMPLETED → FINAL`,
오류 없는 로그와 프로세스 종료를 확인했지만 공식 row에 합산하지 않았다.

L01은 이전 참가자를 재사용하지 않은 새 cold agent였다. 좌표나 정답 경로를 제공하지 않았고, 저장소,
웹, 로그, source·data와 이전 대화를 게임 증거로 보지 못하게 했다. 필수 Computer Use 지침을 읽은 것
외에는 shell 명령을 게임 증거에 사용하지 않았다. 실행 중 후속 메시지나 도움, 재시작과 재시도는
없었다. 참가자는 이미 열린 Godot 창을 Computer Use로만 조작했다. 플랫폼이 보존한 원본은 복사하지
않았고, 앱과 엔진의 원본 로그만 ignored `private/`에 보존한다.

## 관찰 결과

참가자는 도움 없이 처음 화면부터 최종 완공까지 도달했다.

- 선택한 support: `(4,4) → (7,4)`
- 설명한 거리 규칙: source, 각 support와 target 사이의 모든 연속 span이 직선 grid 거리
  `4 GridUnit` 이하여야 하며 정확히 `4`도 허용된다.
- 설명한 상태 규칙: target은 drafting과 building 동안 무전압이며, `완공까지 진행`으로 시간이
  `60 GameMinute`가 되어 commissioned 상태가 된 뒤에만 통전된다.
- 관찰된 혼란: 발주가 자동으로 시간을 진행하지 않고 별도의 `완공까지 진행` 버튼을 요구한다는 점이
  약간 예상 밖이었다. 범위 원, 좌표 목록, 남은 거리 문구와 button enabled 상태는 이해에 충분했다고
  보고했다.

따라서 이 row의 유일한 conjunction인 `IntegratedPlacementPass`는 true다. 사용자는 이를 Scope 1
완료 근거로 수용했다. 이는 한 동일-model LLM이 한 화면에서 과제를 수행했다는 관찰일 뿐, 과거의
3-row aggregate를 소급 통과한 결과가 아니며 사람 사용성·재미·성공률·경로 최적화나 다음 gate를
지지하지 않는다.

## 앱 증거

- session: `S1-OFFICIAL-L01`
- reviewed build hash: `6322218c7ad0396fbe0e3c4f435f35f584f85c0ee999dc559e686a25590d5899`
- fixture hash: `f308a739f9e4fcaf9d6f07aacba65af6fdd9ae3600a1e5569254fcb749bb2edc`
- initial snapshot: `928a92efde792d1c40a6452424785f181a060bbce6a12cf02010a47c754ab34d`
- final snapshot for `(4,4) → (7,4)`: `e01c466d6e9fff1aef1f34aaff9fe1b57746506faba8edb20ae5bf916f2c15e1`
- accepted events: `READY → SUPPORT_ADDED → SUPPORT_ADDED → ORDERED → COMPLETED → FINAL`
- `ORDERED`: completion minute `60`, target not energized
- `COMPLETED` and `FINAL`: target energized
- app log SHA-256: `46d3be214b2911ceded5faf5a0d30aaebac5a2a9433744ec5d070418f1407453`
- engine log SHA-256: `678be1a5c713f54beb463daf16a33bc57ef6b439cdf48326e1db931eb7842dc0`
- engine/app error scan: none

## 플랫폼·사전점검 원본

- participant session ID: `01a00ccd-a8ef-77a0-9d03-2e7c10bd52fd`
- participant original:
  `/Users/fred/.codex/sessions/2026/08/17/rollout-2026-08-17T07-59-59-01a00ccd-a8ef-77a0-9d03-2e7c10bd52fd.jsonl`
- participant original SHA-256: `627e9cc8b53dfbdb57f08af8ae972fc4f08b93cd15e1c6d1d85eda494db4cf78`
- participant config: `gpt-5.6-sol`, reasoning `ultra`, fresh subagent with `fork_turns=none`
- facilitator messages: initial task `1`, follow-up `0`; `FacilitatorHelp = false`
- preflight app SHA-256: `9e0413864176cff44953cad29def97a154b9387fdc47f8d7f53537bc5d561190`
- preflight engine SHA-256: `678be1a5c713f54beb463daf16a33bc57ef6b439cdf48326e1db931eb7842dc0`

플랫폼 원본은 participant final, tool 사용과 무후속 실행을 보존한다. 다만 dispatch prompt 본문은 플랫폼
원본에서 암호화되므로 “좌표·정답 경로를 주지 않았다”는 문구는 coordinator가 실제 dispatch와 대조한
attestation이며 독립적으로 평문을 복원한 주장이 아니다.

Private log paths:

- `playtests/scope-1/private/S1-OFFICIAL-L01-app.jsonl`
- `playtests/scope-1/private/S1-OFFICIAL-L01-engine.log`
- `playtests/scope-1/private/S1-PREFLIGHT-app.jsonl`
- `playtests/scope-1/private/S1-PREFLIGHT-engine.log`

앱 진단은 support 좌표 자체를 기록하지 않는다. 위 final snapshot은 참가자가 보고한 `(4,4) → (7,4)`와
minute `60`, commissioned, target energized view를 권위 직렬화한 SHA-256과 일치한다.

## 독립 검토

- initial evidence commit: `f2ca4f929063976389c126f914a4e79dc54e3b93`
- bounded reviewer: `scope1_single_official_guard`
- initial findings: `P0=0`, `P1=1`, `P2=1`; 원본 anchor 누락과 shell 표현을 수정했다.
- final recheck: `P0=0`, `P1=0`, `P2=0`; blocker 없음
