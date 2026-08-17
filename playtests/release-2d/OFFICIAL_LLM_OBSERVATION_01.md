# 2D 내부 후보 — 공식 LLM 관찰 01

> 상태: `COMPLETED`
>
> 권한 근거: 2026-08-17 사용자 요청 “공식 LLM test를 한 번 진행해봐.”
>
> 표본 상한: cold LLM 1회. 사람 검증, 성공률, 사용성·재미·밸런스 판정이 아니다.

## 1. 목적과 범위

최종 macOS 내부 후보를 처음 보는 LLM 한 명이 실제 native 창만 사용해 타이틀부터 캠페인 종료까지
진행할 수 있는지 관찰한다. 자동검사가 이미 증명한 전력·경제 계산을 다시 채점하지 않으며, 화면
조작과 화면에서 형성된 설명만 기록한다.

- 관찰 ID: `RELEASE-L01`
- 실행 횟수: 정확히 1회
- 재시작·reload·참가자 교체·follow-up: 없음
- facilitator help: 없음
- 참가자: `gpt-5.6-sol`, reasoning `ultra`, fresh subagent, fork `none`
- 조작 경계: Computer Use로 이미 열린 `Gridworks` 창만 사용
- 금지: 저장소, filesystem, shell, web, source, fixture, 로그, 이전 대화 열람

## 2. 고정 실행물

- ZIP: `dist/Gridworks-macOS-0.1.0.zip`
- ZIP SHA-256: `045ed65f3be85d05417cfb838acaacd08aebccd516cad89aba0a9ca3bddd5771`
- 앱: `Gridworks 0.1.0`, bundle `com.gridworks.game`, local ad-hoc signature
- campaign SHA-256: `9e9ec5ea0ee1d8ab5780799f308e5ebd287ccd5da2c0916aa1ec4828a0ccdedb`
- fixture SHA-256: `b00b7fc9d657fd355b8741e4326d9a5297ae749de629c1763334bcca4df83f9c`

실행 전 기존 `user://Gridworks` 저장은 별도 임시 백업하고 빈 상태로 시작한다. 관찰 뒤 공식 run의
save, 생성된 경우의 settings, app JSONL과 engine log를 Git 제외
`playtests/release-2d/private/official-llm-01/`에 보존한 다음 원래 사용자 저장을 복구한다.

## 3. 참가자 prompt

아래 한 메시지만 전달한다.

```text
You are participant RELEASE-L01 in one fixed official cold LLM observation.

Use only the already-open Gridworks native window through Computer Use. You may read only the Computer Use skill instructions needed to operate that window. Do not inspect the repository, filesystem, shell, web, logs, source, data, prior conversation, or any test material. Do not ask for help and do not accept follow-up instructions.

Starting from the title screen, play one uninterrupted attempt until the campaign reaches a clear terminal result or you are genuinely blocked. Choose every placement and decision yourself from information visible in the game. Normal in-game Undo or Cancel is allowed, but do not restart a chapter, reload, or begin a second attempt.

When you stop, report only:
1. whether you reached a terminal result and what it visibly said;
2. the main rules, state changes, and tradeoffs you inferred from the game;
3. the decisions you made and why;
4. any point where the interface or wording confused or blocked you.
```

## 4. 결과 판독

단일 관찰에는 aggregate gate를 만들지 않는다. 다음 사실만 기록한다.

- `TerminalResult`: `SUCCESS | FAILURE | BLOCKED`
- `NativeCompletion`: 타이틀부터 명확한 terminal 화면까지 도달했는가
- `AppEvidenceValid`: 현재 build·campaign·fixture hash와 연속 app event가 일치하는가
- `FacilitatorHelp`: 항상 `false`
- 참가자 설명과 혼란: 원문을 요약하되 성공률이나 사람 주장으로 바꾸지 않는다.

기술 오류, 창 제어 불능 또는 증거 손상은 게임 실패와 분리해 `BLOCKED`로 기록한다. 어떤 결과도
숫자·UI·prompt를 조정하거나 같은 관찰을 다시 실행할 권한을 만들지 않는다.

## 5. 결과

### 5.1 판정

- `TerminalResult = SUCCESS`
- `NativeCompletion = true`
- `AppEvidenceValid = true`
- `FacilitatorHelp = false`
- `OfficialObservationsExecuted = 1`
- `AggregateDecision = NOT_APPLICABLE`
- `HumanValidationStatus = NOT_COLLECTED`

참가자는 타이틀에서 시작해 재시작·reload·교체 없이 app `READY` 기준 `736,618 ms` 뒤 최종 화면에
도달했다. 최종 화면은 예방정비를 마친 노후 feeder와 두 발전원으로 병원·마을·공장을 모두
공급했다고 알렸고, 더 실행할 action control은 남지 않았다.

### 5.2 원본과 증거 anchor

- platform session ID: `01a00f4f-4ce6-7953-8b1f-e76e25866155`
- platform original:
  `/Users/fred/.codex/sessions/2026/08/17/rollout-2026-08-17T19-40-50-01a00f4f-4ce6-7953-8b1f-e76e25866155.jsonl`
- platform original SHA-256:
  `5dd1438d98026edde508fa917df2d0e02600a5208abf1c22a83a575e68627b22`
- 실제 참가자 설정: `gpt-5.6-sol`, reasoning `ultra`, fresh subagent, fork `none`
- app JSONL: `playtests/release-2d/private/official-llm-01/app.jsonl`, SHA-256
  `cc8602febd550eac7adb3aa9f38f545667e53f41ab857ea392ad8d39d185f638`
- engine log: `playtests/release-2d/private/official-llm-01/engine.log`, SHA-256
  `678be1a5c713f54beb463daf16a33bc57ef6b439cdf48326e1db931eb7842dc0`
- 최종 campaign save:
  `playtests/release-2d/private/official-llm-01/official-userdata/campaign-save.json`, SHA-256
  `c92ef25fc2bc8670bf25316a6403eacd48acb361f893c1b1d8af095b923d0cce`

platform original에는 coordinator의 최초 task 한 건과 follow-up 0건이 있다. 참가자는 허용된 Computer
Use skill 설명을 shell call 한 번으로 읽었고, 이후 게임 관찰·조작은 Computer Use로만 수행했다.
Computer Use가 반환한 screenshot을 같은 도구 안에서 표시한 것 외에 저장소·source·fixture·app log·
web을 열람하지 않았다. platform original의 전달 task 본문은 암호화되어 있으므로 prompt plaintext의
근거는 실행 전에 고정한 이 문서 §3이며, session 원본은 그 task의 단일 전달과 실행 provenance를
보존한다.

Mac 잠금 때문에 참가자를 시작하기 전에 중단한 첫 기술 launch는 `READY` 한 행만 남겼고 화면 조작이
없었다. 이 launch는 official observation을 소비하지 않았으며
`playtests/release-2d/private/official-llm-01/preflight-locked-app.jsonl`에 SHA-256
`13155dfa1fe8aab86fc8cf239a57c58848013d43e89b751d7f4e69b329f0091b`로 별도 보존했다. 공식 참가자는
잠금 해제 뒤 빈 사용자 저장으로 한 번만 시작했다. 관찰 뒤 공식 save를 위 private 경로로 옮기고
기존 사용자 저장을 원래 위치에 복구했다. 참가자가 설정을 바꾸지 않아 공식 settings 파일은
생성되지 않았다.

### 5.3 앱 기록

공식 app JSONL은 sequence `1..46`이 빠짐없이 이어진다.

```text
READY → NEW_GAME → COMMAND × 43 → FINAL
```

- `READY`의 build SHA-256:
  `5dee2c8ac3bcba8652c2820a1ebecca32fcb55093f55a481627f8c8e3d6cba39`
- `READY`의 campaign·fixture SHA-256은 §2의 고정값과 일치한다.
- 저장된 accepted command는 41개다.
- `SPAN_TOO_LONG`으로 거부된 support 입력은 2개이며, 각각 첫 점등과 병원 주회선 계획 중 정상적으로
  수정됐다. 거부된 입력은 save에 들어가지 않았고 재시작이나 facilitator 개입도 만들지 않았다.
- `FINAL.outcome = SUCCESS`; single-line removal, spatial incident utility, hospital P0,
  all-loads-fully-supplied 네 hard condition이 모두 `true`다.
- 폭염 중 병원 `1.0 MW`, 마을 `1.5 MW`, 공장 `2.0 MW`를 전량 공급했다. 미공급과 LostSales는 모두
  `0`이고 기말 현금은 `3.700 M`이다.
- 예방정비는 `ORDERED / COMMISSIONED`, 비용 `2.000 M`; 폭염은 minute `1845..2085`에 진행됐다.

### 5.4 참가자가 이해하고 선택한 것

참가자는 화면만으로 다음을 설명했다.

- 설비와 선로는 초안·발주·시간 진행·완공 순서이며 미완공 자산은 전기를 전달하지 않는다.
- 선로 span에는 최대 길이가 있고 support를 늘리면 비용과 공기가 함께 증가한다.
- 변전소 서비스 권역은 접속 자격일 뿐이며, 발전원에서 변전소까지 선로가 완공돼야 공급된다.
- 병원 이중회선은 support를 공유하지 않아야 하고 전기 단일회선 제거와 공간사건을 모두 견뎌야 한다.
- 가스발전소는 접속 전 출력이 0이며, 두 부지는 높은 부지가격·짧은 접속과 낮은 부지가격·긴 접속을
  맞바꾼다.
- 폭염은 마을 수요를 `1.0 → 1.5 MW`, 노후 feeder 유효정격을 `2.5 → 2.0 MW`로 바꾸며,
  `120분` 예방정비가 `180분` 예고 안에 끝나 feeder를 지킨다.
- 결산은 실제 공급 에너지와 발전비를 반영하고 미공급·LostSales를 따로 보여준다.

실제 선택은 변전소 `(16,8)`, 공간적으로 분리한 병원 북쪽·남쪽 회선, 비싸지만 접속이 짧은
`NEAR_EXPENSIVE_SITE`의 가스발전소와 예방정비였다. 정확한 accepted 위치 열은 위 campaign save가
보존한다.

### 5.5 관찰된 혼란과 해석 상한

참가자는 완료를 막지는 않았지만 다음 세 지점을 혼란으로 보고했다.

1. `새 게임` 직후 조작 도움말 overlay가 먼저 열렸다.
2. 발전소 부지 이름이 내부 식별자처럼 보이는 영어였고, preview는 부지가격만 보여 전체
   부지+접속 비용은 발주 전에 한눈에 알기 어려웠다.
3. 마지막 화면이 성공적인 폭염 대응을 설명하고 action을 제거했지만 `승리` 또는 `캠페인 완료`라고
   직접 쓰지는 않았다.

이 한 번의 관찰은 **이 LLM이 이 build에서 도움 없이 성공 종료하고 핵심 인과를 설명했다**는
사실만 지지한다. 성공률, 평균 플레이 시간, 사람 사용성·접근성·재미·밸런스, 다른 전략의 실행
가능성은 증명하지 않는다. 혼란은 후속 외부 관찰의 질문 후보일 뿐, 이번 결과만으로 UI나 수치를
조정하지 않는다.
