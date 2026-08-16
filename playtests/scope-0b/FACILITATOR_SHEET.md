# Scope 0B LLM UI-proxy facilitator sheet

> Status: **S0B-RUN-v2 REVIEWED — official v2 sessions authorized, not yet started**
>
> `BuildVersion = S0B-BUILD-v1`
>
> `PromptVersion = S0B-PROXY-v2`
>
> `RunProtocolVersion = S0B-RUN-v2`
>
> `DecisionRuleVersion = S0B-GATE-v1`
>
> `L00Status = PASS`

This sheet is an execution copy of the active
[Scope 0B contract](../../docs/scopes/SCOPE_0B_PLAYABLE.md). It does not add rules, hints, retries or
scoring discretion. The coordinator may replace only `<SESSION_ID>`, `<EVIDENCE_ID>` and `<VARIANT>` in the
frozen launch command and only `<SESSION_ID>` in the participant prompt. The evidence-export message has no
placeholder.

## 1. Frozen native target

- Godot binary:
  `/Users/fred/dev/electric_simulator/.tools/godot-4.7.1/Godot_mono.app/Contents/MacOS/Godot`
- project: `/Users/fred/dev/electric_simulator/game`
- window title after READY in this frozen Godot editor-build launch: `Gridworks — 강변 병원 회랑 (DEBUG)`
- app target: the one Godot process whose visible window has that exact title
- engine log:
  `/Users/fred/dev/electric_simulator/playtests/scope-0b/private/<EVIDENCE_ID>-godot.log`
- diagnostic JSONL:
  `/Users/fred/dev/electric_simulator/playtests/scope-0b/private/<EVIDENCE_ID>-app.jsonl`
- runner manifest JSONL:
  `/Users/fred/dev/electric_simulator/playtests/scope-0b/private/<EVIDENCE_ID>-runner.jsonl`
- retained tool trace:
  `/Users/fred/dev/electric_simulator/playtests/scope-0b/private/<EVIDENCE_ID>-tool-trace.md`
- participant task/final transcript:
  `/Users/fred/dev/electric_simulator/playtests/scope-0b/private/<EVIDENCE_ID>-transcript.md`

Run exactly one process and one session at a time:

```text
/Users/fred/dev/electric_simulator/.tools/godot-4.7.1/Godot_mono.app/Contents/MacOS/Godot --path /Users/fred/dev/electric_simulator/game --windowed --resolution 1280x720 --position 0,40 --single-window --accessibility always --rendering-method gl_compatibility --log-file /Users/fred/dev/electric_simulator/playtests/scope-0b/private/<EVIDENCE_ID>-godot.log -- --session-id <SESSION_ID> --variant <VARIANT> --diagnostic-log /Users/fred/dev/electric_simulator/playtests/scope-0b/private/<EVIDENCE_ID>-app.jsonl
```

Assignment is fixed:

| Session | Variant | Evidence role |
|---|---|---|
| `S0B-L00` | `ab` | reviewed native UI rehearsal retained from unchanged build |
| `S0B-V2-L01` | `ab` | official v2 slot 1 |
| `S0B-V2-L02` | `ba` | official v2 slot 2 |
| `S0B-V2-L03` | `ab` | official v2 slot 3 |
| `S0B-V2-L04` | `ba` | official v2 slot 4 |
| `S0B-V2-L05` | `ab` | official v2 slot 5 |

Every log path must not exist before launch; the app fails closed rather than appending. The coordinator
sets `EVIDENCE_ID=<SESSION_ID>-launch1` for the first launch and increments only the launch number for an
independently evidenced runner replacement. The logical session ID, variant and participant message hash do
not change. The runner manifest contains both IDs and the launch-specific trace/transcript paths and hashes.
The coordinator
records launch, READY and end with monotonic times and the exact `endReason`/`faultAttribution` pair from the
active contract. The one-row runner manifest also records the exact model, reasoning, fork and tool policy,
plus the path and SHA-256 of the independent participant-launch/tool evidence. A non-`none` fault attribution
must likewise point to a preserved engine, app, host or transport artifact and its SHA-256; a coordinator note
alone is not evidence.

The only allowed pairs are `final:none`, `participant_stop:none`, `timeout:none`, `timeout:app`,
`app_crash:app` and `runner_error:runner`. A frozen-app failure is scored. Only independently evidenced
`runner_error:runner` launches are replaceable, at most twice; L01–L05 and their replacements may total at
most seven launches.

## 2. Exact participant prompt

The following code block must be byte-identical to the active contract. Replace `<SESSION_ID>` once; add no
greeting, explanation, success criterion or recovery hint.

`PromptHash` means the lowercase SHA-256 of the exact UTF-8 participant message after that replacement. It
excludes the Markdown fences and the newline immediately before the closing fence.

```text
당신은 Gridworks를 처음 플레이하는 전력회사 운영자입니다. 세션 ID는 <SESSION_ID>입니다.
측정 시작 전에 coordinator가 지정한 computer-use SKILL.md만 한 번 읽으세요. 측정용 Computer Use
wrapper는 `tools.mcp__node_repl__js`, 앱 target은 `org.godotengine.godot`입니다. 이 transport 정보는
도구 목록을 찾지 않고 바로 사용하세요. 첫 wrapper 호출부터 측정이 시작되며, 그 뒤에는 wrapper
안의 @oai/sky로만 이미 열린 Gridworks 창을 조작하세요. ALL_TOOLS, list_apps, repository, shell,
web, 정적 카드와 다른 세션은 보지 마세요. 각 UI action 뒤에는 fresh app state를 읽은 다음
현재 element index로 다음 행동을 정하세요.

화면에서 직접 읽을 수 있는 정보만 사용해 처음 상태부터 마지막 복구·결산 화면까지 도움 없이
진행하세요. 결과가 공개되기 전에 표의 네 병원 utility 공급경로 결과를 모두 예측해 잠그고,
그다음 건설할 회랑 하나를 선택하세요. 앱이 막히거나 끝까지 갈 수 없으면 실제로 시도한 마지막
상태와 막힌 이유를 남기세요.

마지막에는 짧게 보고하세요: (1) 서비스 권역이 가능하게 하는 것과 실제 공급에 더 필요한 것,
(2) 잠갔던 네 예측과 전기회로 사고/공간 통로 사고의 차이, (3) 선택한 회랑과 실제 사건 결과,
(4) 병원 UPS·디젤이 지킨 P0와 전력회사의 병원 인도·판매가 같은지 다른지,
(5) 예상 밖이거나 이해하기 어려웠던 조작.
```

## 3. Native preflight inherited by v2

The reviewed `S0B-L00` remains valid because v2 changes no build, UI, app target, fixture or Computer Use
action path. Do not start official v2 sessions unless all are true:

1. The host UI is unlocked and exactly one target process is running.
2. `get_app_state` returns twice within 20 seconds and exposes the exact title, current stage and enabled
   action through the accessibility tree or screenshot.
3. Element-index interaction is tried first. Screenshot coordinates or Tab/Return are fallback only.
4. One complete run reaches `FINAL`; the diagnostic contains the frozen ten-event sequence and the runner
   manifest agrees with it.
5. The source-manifest, fixture and prompt hashes match the implementation checkpoint.
6. An independent protocol review directly invokes `tools.mcp__node_repl__js` without `ALL_TOOLS` and confirms
   that the callable is available. The participant must not discover a fallback tool or app identifier.

If both AX and screenshot are unavailable, or a real accepted command cannot be produced, record
`PROXY-RUN-BLOCKED`. Do not substitute static cards or a screenshot questionnaire.

## 4. Official run order and scoring boundary

- Spawn one cold `gpt-5.6-sol`, reasoning `medium`, `fork_turns=none` session at a time.
- Before measurement, permit only the exact computer-use skill read. The task message itself supplies the
  direct wrapper and app target; `ALL_TOOLS`, `list_apps` and every discovery fallback are forbidden.
- The first `tools.mcp__node_repl__js` dispatch starts measurement. During measurement, the outer
  `functions.exec` may only dispatch that tool and UI access inside it may only use `@oai/sky`.
- After each Sky UI action, fetch fresh app state in the same dispatch or the next read-only direct-wrapper
  dispatch before deciding the next action. If the direct wrapper or exact app target fails, stop as
  `runner_error:runner`; do not search for another tool or app.
- Stop at 15 minutes. A participant stop or timeout on an otherwise working app is a valid scored failure.
- The final app-state read ends UI interaction, not the evidence boundary. The same tool policy remains in
  force through the scored final report; measurement ends only when that report is submitted.
- The four `PREDICTION_LOCKED` values are prediction authority; reveal-time prose cannot repair them.
- Fill [`record-template.csv`](record-template.csv) from transcript, diagnostic and runner evidence.
- After the final participant report, send the exact export message in §5. Hash its output and verify the exact
  preparation and measured-tool boundary before starting the next session.
- Do not discuss earlier sessions, scores or expected answers with a later participant.
- Do not tune text, layout, values or controls between official sessions.

The gate is exactly the active contract: each of the four fields at least `4/5`, integrated at least `3/5`.
Selection ratio, completion time and click count are diagnostic only.

## 5. Exact post-measurement evidence export

This is a separate, unscored turn to the same participant after its final report. Send it byte-identically and
add nothing else. The participant returns Markdown in its response without calling a tool. The coordinator,
who already satisfied the repository reading rules, stores that response verbatim at the frozen
`<EVIDENCE_ID>-tool-trace.md` path.

```text
측정은 종료됐습니다. Gridworks, @oai/sky, node_repl 또는 다른 도구를 다시 호출하지 마세요. 이번
세션에 보존된 task/tool history만 사용해 이 응답에 Markdown 본문만 반환하세요.

다음을 실제 순서대로 기록하세요: (1) 측정 전 skill read의 exact wrapper call과 결과, (2) 첫 direct
wrapper 호출을 measurement start로 표시, (3) 측정 중 모든 `tools.mcp__node_repl__js` request의 code,
timeout_ms, title과 반환 text 원문 및 image 유무, (4) final app-state read를 UI interaction end로,
final report 제출을 measurement end로 표시, (5) task 시작부터 final report 제출까지 사용한 다른
tool call의 전체 목록. ALL_TOOLS, list_apps 또는 금지 도구가 없었다면
없다고 명시하세요. repository나 다른 파일을 읽지 말고 앱을 조작하지 마세요.
```
