# 2D 내부 후보 — 공식 LLM 관찰 01

> 상태: `FROZEN_NOT_RUN`
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
save·settings·app JSONL·engine log를 Git 제외 `playtests/release-2d/private/official-llm-01/`에
보존한 다음 원래 사용자 저장을 복구한다.

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

`NOT_RUN`
