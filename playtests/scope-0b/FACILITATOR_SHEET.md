# Scope 0B LLM UI-proxy facilitator sheet

> Status: **S0B-RUN-v6 FROZEN EXECUTION COPY — use only when checkpoint 1F is AUTHORIZED**
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
The v6 rule is deliberately small: one global preflight, then five fixed cold slots with no replacement.
Evidence comes from the platform's coordinator and participant session JSONL plus the app diagnostic JSONL.
There is no runner manifest, participant-written provenance export or copied transcript.

## 1. Native target and assignments

- Godot binary:
  `/Users/fred/dev/electric_simulator/.tools/godot-4.7.1/Godot_mono.app/Contents/MacOS/Godot`
- project: `/Users/fred/dev/electric_simulator/game`
- expected title: `Gridworks — 강변 병원 회랑 (DEBUG)`
- app target: `org.godotengine.godot`
- designated skill: `computer-use:computer-use`
- skill resource:
  `/Users/fred/.codex/plugins/cache/openai-bundled/computer-use/1.0.1000717/skills/computer-use/SKILL.md`
- skill SHA-256: `e0ec667e63fba01381eb889ddbfd44a05b8556b1e502428e8ff0a474750a08d6`
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

## 2. One global preflight

One dedicated coordinator task owns the whole round. Its platform session JSONL must become immutable when it
returns. Before `L01` is dispatched, it performs one non-scored `S0B-V6-PREFLIGHT/ab` launch:

1. Confirm that no Godot process remains and both new log paths do not exist.
2. Launch the command above, capture its exact PID and confirm exactly one Godot process.
3. Confirm the diagnostic starts with one `READY` row containing the frozen build hash, fixture hash, preflight ID
   and variant.
4. Confirm the exact title is readable from the target UI. Do not click or advance the game.
5. Terminate only the captured PID, wait for exit and log flush, then confirm no Godot process remains.

Failure here closes the round as `PROXY-RUN-BLOCKED` before any participant observation. After `L01` dispatch,
the round is irrevocable: later setup, participant or evidence failures cannot discard earlier slots.

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

- The same coordinator runs the five rows serially. For each row it confirms no Godot process or old row path,
  launches the exact command, captures the exact PID and checks one process plus exact `READY` identity.
- A row setup failure gets `SlotStatus = SETUP_FAILURE`; no participant is dispatched and all gameplay fields,
  conclusions and integrated are `false`. Continue with the next row. Never erase an earlier row.
- After successful row setup, dispatch the exact rendered prompt once to a new cold `gpt-5.6-sol`, reasoning
  `medium`, `fork_turns=none` task whose agent path maps to that session ID. Send no follow-up or help.
- When that single participant turn returns or reaches the 15-minute operational timeout, terminate only the
  captured PID, wait for exit and log flush, hash the closed app log, and only then start the next row.
- Participant stop, timeout, app crash or incomplete play gets `SlotStatus = PARTICIPANT_FAILURE` and
  `InteractionCompletionPass = false`; other fields use only evidence actually produced. The slot is not replaced.
- Allowed Gridworks content sources are the exact task message, designated skill and current target UI. Generic
  tool metadata and import/transport errors are allowed setup information. Repository, source/data, web, static
  cards, oracle/rubric, previous sessions, app inventory and other-app content are forbidden.
- The first readable Gridworks state must precede every UI action. Each action must be followed by a fresh state
  read before the next action. Use only `tools.mcp__node_repl__js` with `@oai/sky` for UI reads and actions.
- The immutable coordinator platform JSONL is the authority for session assignment, exact model, reasoning,
  `fork_turns`, dispatch order, zero post-dispatch help, PID lifecycle and child platform IDs.
- Each immutable participant platform JSONL is the authority for its `session_meta` parent/task mapping, model,
  tool calls, content sources, UI sequence and final report. Record both platform paths and SHA-256 values after
  their tasks return; do not copy or rewrite either original.
- The app diagnostic JSONL is the authority for `READY`, accepted commands, pre-reveal `PREDICTION_LOCKED`,
  selected corridor and `FINAL`. Record its SHA-256.
- Platform session files encrypt the spawn-message body. They can link the coordinator dispatch ciphertext to the
  child receipt but cannot prove the frozen prompt's plaintext bytes. The prompt hash is a reviewed execution
  procedure, not a post-run evidence predicate; record this limitation in the result.
- Exact model/reasoning/fork, zero help, frozen app identity, no observed forbidden source and native `FINAL` are
  required for `SlotStatus = COMPLETED`. A missing or conflicting original, or an observed forbidden source, gets
  `SlotStatus = EVIDENCE_FAILURE` and every gameplay field, conclusion and integrated is `false`; continue without
  replacement.
- If the coordinator itself ends early after `L01` dispatch, every unfinished row is `EVIDENCE_FAILURE/false`.
  This prevents a later infrastructure or evidence problem from selecting away earlier outcomes.
- The operational timeout is not a scored field and requires no custom timestamp proof.
- Do not send a post-measurement export, create a runner manifest or reconstruct a transcript.
- Do not tune text, layout, values or controls between sessions.

The frozen gate remains: each of the four gameplay fields at least `4/5`, and their integrated conjunction at
least `3/5`. Selection ratio, completion time and click count are diagnostic only. Keep
`HumanValidationStatus = NOT_COLLECTED`.
