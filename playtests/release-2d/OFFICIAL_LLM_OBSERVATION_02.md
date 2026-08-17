# 2D 내부 후보 — 공식 LLM 관찰 02

> 상태: `COMPLETED`
>
> 권한 근거: 2026-08-17 사용자 요청 “After Done, run the same llm text, then, aggregate the 개선할 점들”
>
> 표본 상한: 두 번째 cold LLM 관찰 1회. 사람 검증, 성공률, 사용성·재미·밸런스 판정이 아니다.

## 1. 목적과 범위

첫 관찰 뒤 게임·수치·prompt를 바꾸지 않고, 동일한 macOS 내부 후보와 동일한 참가자 prompt를 처음
보는 LLM 한 명에게 한 번 더 제공한다. 타이틀부터 terminal 결과까지의 독립 native 흐름과 화면에서
형성된 설명·혼란만 기록한다.

- 관찰 ID: `RELEASE-L02`
- 실행 횟수: 정확히 1회
- 재시작·reload·참가자 교체·follow-up: 없음
- facilitator help: 없음
- 참가자: `gpt-5.6-sol`, reasoning `ultra`, fresh subagent, fork `none`
- 조작 경계: Computer Use로 이미 열린 `Gridworks` 창만 사용
- 금지: 저장소, filesystem, shell, web, source, fixture, 로그, 이전 대화와 첫 관찰 결과 열람

## 2. 고정 실행물

- ZIP: `dist/Gridworks-macOS-0.1.0.zip`
- ZIP SHA-256: `045ed65f3be85d05417cfb838acaacd08aebccd516cad89aba0a9ca3bddd5771`
- 앱: `Gridworks 0.1.0`, bundle `com.gridworks.game`, local ad-hoc signature
- build SHA-256: `5dee2c8ac3bcba8652c2820a1ebecca32fcb55093f55a481627f8c8e3d6cba39`
- campaign SHA-256: `9e9ec5ea0ee1d8ab5780799f308e5ebd287ccd5da2c0916aa1ec4828a0ccdedb`
- fixture SHA-256: `b00b7fc9d657fd355b8741e4326d9a5297ae749de629c1763334bcca4df83f9c`

실행 전 기존 `user://Gridworks` 저장은 별도 임시 백업하고 빈 상태로 시작한다. 관찰 뒤 공식 run의
save, 생성된 경우의 settings, app JSONL과 engine log를 Git 제외
`playtests/release-2d/private/official-llm-02/`에 보존한 다음 원래 사용자 저장을 복구한다.

## 3. 참가자 prompt

첫 관찰과 byte-for-byte 같은 아래 한 메시지만 전달한다.

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

`RELEASE-L01`까지 그대로 유지하는 것은 사용자가 요청한 동일 text 조건을 보존하기 위한 것이다.
실제 app session과 공개 관찰 ID는 `RELEASE-L02`이며 platform session ID로 두 실행을 구분한다.

## 4. 결과 판독

두 관찰을 합쳐도 성공률이나 aggregate gate를 만들지 않는다. 관찰별 terminal 결과, native completion,
app evidence, 설명과 혼란만 보존한다. 공통 개선점은 두 참가자의 반복·비반복 관찰을 구분해 별도
요약하되, 사람 사용성·재미·밸런스 증거로 바꾸거나 자동으로 구현 권한을 만들지 않는다.

## 5. 결과

### 5.1 판정

- `TerminalResult = SUCCESS`
- `NativeCompletion = true`
- `AppEvidenceValid = true`
- `FacilitatorHelp = false`
- `OfficialObservationsExecutedThisRound = 1`
- `OfficialObservationsExecutedTotalAtClosure = 2`
- `AggregateDecision = NOT_APPLICABLE`
- `HumanValidationStatus = NOT_COLLECTED`

참가자는 타이틀에서 시작해 재시작·reload·교체 없이 app `FINAL.elapsedMs = 1,008,269`에
`22 · 폭염 복구·결산`에 도달했다. 화면은 폭염 대응 완료, 세 수요처 전량공급, 미공급 `0 MWh`,
현금 변화 `+0.840 M`, 기말 현금 `3.900 M`을 표시했다.

### 5.2 원본과 증거 anchor

- platform session ID: `01a00f6b-7b80-75a0-b280-1ce9402af9c8`
- platform original:
  `/Users/fred/.codex/sessions/2026/08/17/rollout-2026-08-17T20-11-37-01a00f6b-7b80-75a0-b280-1ce9402af9c8.jsonl`
- platform original SHA-256:
  `ea30fb0b12b3cef82e81e2d62e2a343ed643084cc9be43fe1d8da9b5e7f14738`
- 실제 참가자 설정: `gpt-5.6-sol`, reasoning `ultra`, fresh subagent, fork `none`
- app JSONL: `playtests/release-2d/private/official-llm-02/app.jsonl`, SHA-256
  `c164b928bfd2d948958b8f9571a9aed9ba752deec145afef49018d994879276f`
- engine log: `playtests/release-2d/private/official-llm-02/engine.log`, SHA-256
  `678be1a5c713f54beb463daf16a33bc57ef6b439cdf48326e1db931eb7842dc0`
- 최종 campaign save:
  `playtests/release-2d/private/official-llm-02/official-userdata/campaign-save.json`, SHA-256
  `ab1adac7211dd4c2569ceb504358405cd8be5287d1203d52381c3b87a38daa7e`

platform original에는 coordinator task 한 건과 follow-up 0건이 있다. 참가자는 허용된 Computer Use
skill 설명을 두 번에 나눠 읽고 tool entry를 찾은 뒤, 게임 관찰·조작은 Computer Use로만 수행했다.
Computer Use screenshot 표시 외에 저장소·source·fixture·app log·web 또는 첫 관찰 결과를 열람하지
않았다. 전달 task 본문은 platform original에서 암호화되어 있으므로 prompt plaintext는 실행 전에
동결한 이 문서 §3가 보존하며, protocol commit은 `a8261f8`이다.

coordinator는 참가자 시작 전에 title의 빈 저장 상태를 read-only로 확인했지만 click이나 command를
보내지 않았다. 공식 app log의 첫 participant action은 `NEW_GAME`이다. 관찰 뒤 공식 save를 private
경로로 옮기고 기존 사용자 저장을 원래 위치에 복구했다. 참가자가 설정을 바꾸지 않아 공식 settings
파일은 생성되지 않았다.

### 5.3 앱 기록

공식 app JSONL은 sequence `1..44`가 빠짐없이 이어진다.

```text
READY → NEW_GAME → COMMAND × 41 → FINAL
```

- `READY`의 build·campaign·fixture SHA-256은 §2의 고정값과 일치한다.
- command 41개가 모두 accepted이며 final save journal의 41개 command와 일치한다.
- `FINAL.outcome = SUCCESS`; single-line removal, spatial incident utility, hospital P0,
  all-loads-fully-supplied 네 hard condition이 모두 `true`다.
- 폭염 중 병원 `1.0 MW`, 마을 `1.5 MW`, 공장 `2.0 MW`를 전량 공급했다. 미공급과 LostSales는 모두
  `0`이고 기말 현금은 `3.900 M`이다.
- 예방정비는 `ORDERED / COMMISSIONED`, 비용 `2.000 M`; 폭염은 minute `2015..2255`에 진행됐다.

### 5.4 참가자가 이해하고 선택한 것

참가자는 화면만으로 span 제약 `distanceSquared ≤ 16`, 미완공 설비·선로의 무전압, 변전소
권역과 source 경로의 결합, support 비공유 병원 이중회선, 공간사건 노출, 기존 발전원 우선 급전,
발전소 부지가격과 접속거리의 교환, 예방정비와 폭염 derating, 실제 공급 에너지 기준 결산을
설명했다.

실제 선택은 다음과 같다.

- 변전소 `(15,5)`
- 마을 회선 `(5,4) → (7,1) → (11,1) → (14,3)`
- 병원 주회선 `(4,3) → (6,0) → (10,0) → (14,0) → (17,2)`
- 병원 예비회선 `(5,8) → (7,11) → (10,12) → (14,12) → (17,10) → (17,6)`
- `FAR_CHEAP_SITE` `(18,11)`과 접속 support `(15,10) → (12,9) → (9,8) → (6,6)`
- 예방정비 발주

### 5.5 관찰된 혼란과 해석 상한

참가자를 막은 문제는 없었다. 보고한 혼란은 두 종류다.

1. `FAR_CHEAP_SITE` marker는 row 10 근처처럼 보였지만 실제 selectable anchor는 `(18,11)`이었다.
2. `SPAN_TOO_LONG`, `P0`, `FAR_CHEAP_SITE` 같은 혼합 언어·내부 식별자형 표현은 이해할 수는
   있었지만 제품 문구로 덜 다듬어져 보였다.

이 결과는 두 번째 LLM 한 명이 동일 build와 prompt에서 독립적으로 성공 종료하고 핵심 인과를
설명했다는 사실만 지지한다. 두 관찰을 성공률이나 사람 사용성·접근성·재미·밸런스 증거로 바꾸지
않는다. 두 관찰의 개선점 통합은 [별도 요약](OFFICIAL_LLM_OBSERVATIONS_SUMMARY.md)이 소유한다.
