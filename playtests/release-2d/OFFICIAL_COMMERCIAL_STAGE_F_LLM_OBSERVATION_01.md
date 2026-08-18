# 상용 2D 단계 F — 공식 cold LLM 관찰 01

> 상태: `RUNNING`
>
> 권한 근거: 2026-08-18 사용자 요청 “llm agent가 처음부터 끝까지 게임을 돌려보게 할래?
> 이것은 공식 검증이야.”
>
> 해석 상한: 현재 단계 F native 후보에 대한 cold LLM 1회다. 사람 검증, 성공률, 재미·밸런스
> 판정, 상용 출시 준비나 단계 G 완료 증거가 아니다.

## 1. 목적과 고정 조건

현재 상용 2D 단계 F 후보를 처음 보는 LLM 한 명이 실제 native 창만 사용해 빈 저장의 타이틀부터
여덟 장과 에필로그까지 진행할 수 있는지 관찰한다. 자동검사가 이미 증명한 규칙 계산을 다시
채점하지 않고, 참가자가 화면에서 이해한 규칙·선택·복구와 실제로 막힌 지점을 기록한다.

- 관찰 ID: `COMMERCIAL-F-L01`
- 실행 횟수: 정확히 1회
- 참가자: `gpt-5.6-sol`, reasoning `ultra`, fresh subagent, fork `none`
- 시작 시각: `2026-08-18T12:08:31Z`
- 시작 화면: 빈 사용자 저장의 `Gridworks` 타이틀
- 조작 경계: Computer Use로 이미 열린 native 앱만 사용
- 허용: 화면에 공개된 Undo, Cancel, 최근 공사 복구, 현재 장 재시작, 이전 장 되감기
- 금지: 앱 reload·재실행, 두 번째 새 게임, 참가자 교체, facilitator help, follow-up
- 금지된 정보: 저장소, filesystem, shell, web, 로그, source, data, 이전 대화와 검사 자료
- 종료: visible campaign completion/epilogue, genuine block, 또는 180분 timebox

## 2. 고정 실행물

- 후보 코드 Git commit: `36038a90d74708a4bebd9dbc5b2a5ea6907d44aa`
- Godot: `4.7.1.stable.mono.official.a13da4feb`
- Godot executable SHA-256:
  `d11dc4a241ec29a347e13c8c7706e49433379ae1f9fc6a6e6819efb3891fce97`
- world SHA-256:
  `c4923f752205c193efa78ddb4ca9e5431801731e6087be3ba3796abf9117ac14`
- campaign SHA-256:
  `078df95f9f0c833be7e1a299088b4ab6e0de4ddf13426ce5b96a1abbeee70b7a`
- Debug `Gridworks.Game.dll` SHA-256:
  `a61862062c9b66a3c8fbbd84a7cf085817c50213a21499b5f2204db2290ccfd6`
- Debug `Gridworks.Core.dll` SHA-256:
  `14c11aa32a9feaf7f6a93137576461ca564d78e3b90971cefdf1efde46f5df02`
- `CommercialMain.tscn` SHA-256:
  `4600aeae1e2ec886b177d466edc60360edaf4b8b51197c8d48d3ad90c97f5660`
- `CommercialTaskPanel.tscn` SHA-256:
  `1b17f05f1be2ae0a07820738d77063aec37704c2214482a7302d94672959eb28`

실행 명령은 저장소의 기본 `CommercialMain.tscn`을 1280×720 native 창으로 열며 smoke 인자나
대표 해법을 사용하지 않는다. 이는 현재 source-run Debug 후보 관찰이며 패키징·서명·새 설치 후보
검증은 아니다.

원래 `user://Gridworks` 전체는 실행 전에 Git 제외
`playtests/release-2d/private/official-commercial-f-llm-01/original-userdata/`로 옮겼다. 공식 실행은
빈 사용자 디렉터리에서 시작하며, 프로세스 종료 trap이 공식 user data를 같은 private root에
보존하고 원래 사용자 데이터를 복원한다.

## 3. 참가자에게 전달한 단일 prompt

아래 메시지 한 건만 전달했으며 follow-up은 보내지 않는다.

```text
You are participant COMMERCIAL-F-L01 in one fixed official cold LLM observation.

Use only the already-open native window titled “Gridworks” through Computer Use. Read only `/Users/fred/.codex/plugins/cache/openai-bundled/computer-use/1.0.1000717/skills/computer-use/SKILL.md` as needed to operate that window. Do not inspect the repository, filesystem, shell, web, logs, source, data, prior conversation, test material, or any other app. Do not ask for help and do not accept follow-up instructions.

Starting from the title screen with no prior save, play one continuous campaign from New Game until you reach the campaign’s visible terminal epilogue/completion or are genuinely blocked. Choose every construction placement, route, promise, projection, and recovery decision yourself using only information visible in the game. You may use ordinary recovery controls exposed by the game (Undo, Cancel, recent-project recovery, current-chapter restart, or previous-chapter rewind), but do not reload the app, start a second New Game, or use a second participant. Do not repeatedly brute-force identical rejected actions; if you cannot make meaningful progress using visible information and in-game recovery, declare BLOCKED. Leave the app open on the final or blocked screen when you report.

Do not send progress updates. When you stop, report only:
1. `TerminalResult = SUCCESS | FAILURE | BLOCKED` and whether you reached the visible campaign epilogue/completion;
2. the exact chapter/screen and visible state where you stopped;
3. the main rules, state changes, and tradeoffs you inferred;
4. the important decisions and in-game recoveries you used and why;
5. every interface, wording, or feedback issue that confused or blocked you.

This is one official observation, not a request to inspect or modify the game.
```

## 4. 판정 경계

다음 기계적 사실을 서로 분리해 기록한다.

- `TerminalResult = SUCCESS | FAILURE | BLOCKED | TIMEBOX_EXPIRED | INVALID`
- `NativeCompletion`: 참가자가 visible campaign completion/epilogue까지 도달했는가
- `CandidateIdentityValid`: §2의 candidate bytes와 실행물이 일치하는가
- `ColdStartValid`: 기존 저장 없이 새 게임 하나로 시작했는가
- `ProtocolValid`: forbidden information, help, follow-up, reload, second New Game이 없었는가
- `SaveEvidencePresent`: 공식 v3 save가 보존되고 현재 content hash로 strict restore되는가

기술 crash나 Computer Use 상실은 게임 규칙 실패와 분리한다. 첫 참가자 행동 뒤 발생한 기술
중단도 이 한 번의 관찰을 소비하며, 새 사용자 승인 없이 참가자를 교체하거나 다시 실행하지 않는다.
어떤 결과도 이번 표본만으로 게임 수치·UI를 자동 변경하거나 사람 검증·성공률·상용 출시 준비를
주장할 권한을 만들지 않는다.

## 5. 결과

실행 종료 뒤 기록한다.
