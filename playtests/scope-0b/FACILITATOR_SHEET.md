# Scope 0B LLM UI-proxy facilitator sheet

> Status: **S0B-RUN-v6 DRAFT — official sessions closed**
>
> `BuildVersion = S0B-BUILD-v1`
>
> `PromptVersion = S0B-PROXY-v6`
>
> `RunProtocolVersion = S0B-RUN-v6`
>
> `DecisionRuleVersion = S0B-GATE-v1`

This is the execution copy of the active
[Scope 0B contract](../../docs/scopes/SCOPE_0B_PLAYABLE.md). It adds no gameplay rule or scoring discretion.
The v6 rule is deliberately small: preflight before participant dispatch, then one scored cold session with no
replacement. Evidence comes from the platform session JSONL and the app diagnostic JSONL. There is no runner
manifest, participant-written provenance export or copied transcript.

## 1. Native target and assignments

- Godot binary:
  `/Users/fred/dev/electric_simulator/.tools/godot-4.7.1/Godot_mono.app/Contents/MacOS/Godot`
- project: `/Users/fred/dev/electric_simulator/game`
- expected title: `Gridworks — 강변 병원 회랑 (DEBUG)`
- app target: `org.godotengine.godot`
- designated skill: `computer-use:computer-use`
- diagnostic path:
  `/Users/fred/dev/electric_simulator/playtests/scope-0b/private/<SESSION_ID>-app.jsonl`
- optional engine-log path:
  `/Users/fred/dev/electric_simulator/playtests/scope-0b/private/<SESSION_ID>-godot.log`

```text
/Users/fred/dev/electric_simulator/.tools/godot-4.7.1/Godot_mono.app/Contents/MacOS/Godot --path /Users/fred/dev/electric_simulator/game --windowed --resolution 1280x720 --position 0,40 --single-window --accessibility always --rendering-method gl_compatibility --log-file /Users/fred/dev/electric_simulator/playtests/scope-0b/private/<SESSION_ID>-godot.log -- --session-id <SESSION_ID> --variant <VARIANT> --diagnostic-log /Users/fred/dev/electric_simulator/playtests/scope-0b/private/<SESSION_ID>-app.jsonl
```

| Session | Variant |
|---|---|
| `S0B-V6-L01` | `ab` |
| `S0B-V6-L02` | `ba` |
| `S0B-V6-L03` | `ab` |
| `S0B-V6-L04` | `ba` |
| `S0B-V6-L05` | `ab` |

## 2. Preflight before each participant

Preflight is not an official session because no participant has received the task yet.

1. Confirm that no Godot process remains and both new log paths do not exist.
2. Launch the command above and confirm exactly one Godot process.
3. Confirm the diagnostic starts with one `READY` row containing the frozen build hash, fixture hash, session ID
   and variant.
4. Confirm the exact title is readable from the target UI. Do not click or advance the game.
5. If setup fails, close the whole round as `PROXY-RUN-BLOCKED`; do not dispatch or replace a participant.

Only after a slot's preflight passes may that session begin. All five slots use the same frozen build and run
serially. Preflight supplies no gameplay observation.

## 3. Exact participant prompt

Replace `<SESSION_ID>` once and add nothing. Render it with
`ruby playtests/scope-0b/verify_implementation.rb --render-prompt <SESSION_ID>`.
The prompt hash is lowercase SHA-256 of those exact UTF-8 bytes; no whitespace normalization is applied.

```text
당신은 Gridworks를 처음 플레이하는 전력회사 운영자입니다. 세션 ID는 <SESSION_ID>입니다.
먼저 이 task 환경에 등록된 `computer-use:computer-use` skill의 SKILL.md를 끝까지 읽으세요. Computer Use wrapper는
`tools.mcp__node_repl__js`, 앱 target은 `org.godotengine.godot`입니다. 전체 task에서 Gridworks 정보는
이 안내와 현재 target UI에서만 얻으세요. generic tool name/signature와 import·transport error는
read-only로 확인할 수 있지만 repository·source/data, web, 앱 목록·다른 앱 내용, 정적 카드,
oracle·rubric과 이전 세션은 보지 마세요. Gridworks 화면 읽기와 조작은 wrapper 안의 @oai/sky로만
하세요. 첫 readable 화면 전에는 UI action을 하지 말고, 각 action 뒤에는 fresh app state를 읽은
다음 현재 element index로 다음 행동을 정하세요.

화면에서 직접 읽을 수 있는 정보만 사용해 처음 상태부터 마지막 복구·결산 화면까지 도움 없이
진행하세요. 결과가 공개되기 전에 표의 네 병원 utility 공급경로 결과를 모두 예측해 잠그고,
그다음 건설할 회랑 하나를 선택하세요. 앱이 막히거나 끝까지 갈 수 없으면 실제로 시도한 마지막
상태와 막힌 이유를 남기세요.

마지막에는 짧게 보고하세요: (1) 서비스 권역이 가능하게 하는 것과 실제 공급에 더 필요한 것,
(2) 잠갔던 네 예측과 전기회로 사고/공간 통로 사고의 차이, (3) 선택한 회랑과 실제 사건 결과,
(4) 병원 UPS·디젤이 지킨 P0와 전력회사의 병원 인도·판매가 같은지 다른지,
(5) 예상 밖이거나 이해하기 어려웠던 조작.
```

## 4. Official session and evidence

- Run five new cold `gpt-5.6-sol`, reasoning `medium`, `fork_turns=none` sessions in table order.
- Once the exact participant message is dispatched, that slot is never replaced. Stop, timeout, app crash or
  incomplete play is a scored `InteractionCompletionPass = false`, not missing data.
- Allowed Gridworks content sources are the exact task message, designated skill and current target UI. Generic
  tool metadata and import/transport errors are allowed setup information. Repository, source/data, web, static
  cards, oracle/rubric, previous sessions, app inventory and other-app content are forbidden.
- The first readable Gridworks state must precede every UI action. Each action must be followed by a fresh state
  read before the next action. Use only `tools.mcp__node_repl__js` with `@oai/sky` for UI reads and actions.
- The platform-owned session JSONL is the authority for the exact dispatched prompt, model/task identity, tool
  calls, content sources, UI sequence and final report. Record its path and SHA-256 after completion; do not copy
  or rewrite it.
- The app diagnostic JSONL is the authority for `READY`, accepted commands, pre-reveal `PREDICTION_LOCKED`,
  selected corridor and `FINAL`. Record its SHA-256.
- A session is scorable when the platform log has the exact prompt and no observed forbidden source, and its
  diagnostic begins with the exact frozen identity. Completion is not a scorable-session condition.
- If those two original artifacts are absent or disagree on identity, close the round as `PROXY-RUN-BLOCKED`.
  Do not replace the participant and do not infer missing evidence from filesystem times or prose.
- The operational timeout is 15 minutes. It is not a scored field and requires no custom timestamp proof.
- Do not send a post-measurement export, create a runner manifest or reconstruct a transcript.
- Do not tune text, layout, values or controls between sessions.

The frozen gate remains: each of the four gameplay fields at least `4/5`, and their integrated conjunction at
least `3/5`. Selection ratio, completion time and click count are diagnostic only. Keep
`HumanValidationStatus = NOT_COLLECTED`.
