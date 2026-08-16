# Scope 0B LLM UI-proxy facilitator sheet

> Status: **S0B-RUN-v3 REVIEWED — official sessions authorized, not yet started**
>
> `BuildVersion = S0B-BUILD-v1`
>
> `PromptVersion = S0B-PROXY-v3`
>
> `RunProtocolVersion = S0B-RUN-v3`
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
| `S0B-V3-L01` | `ab` | official v3 slot 1 |
| `S0B-V3-L02` | `ba` | official v3 slot 2 |
| `S0B-V3-L03` | `ab` | official v3 slot 3 |
| `S0B-V3-L04` | `ba` | official v3 slot 4 |
| `S0B-V3-L05` | `ab` | official v3 slot 5 |

Every log path must not exist before launch; the app fails closed rather than appending. The coordinator
sets `EVIDENCE_ID=<SESSION_ID>-launch1` for the first launch and increments the launch number for every
authorized `TechnicalValid=false` replacement. The logical session ID, variant and participant message hash
do not change. The runner manifest contains both IDs and the launch-specific trace/transcript paths and hashes.
The coordinator
records launch, READY and end with monotonic times and the exact `endReason`/`faultAttribution` pair from the
active contract. The one-row runner manifest also records the exact model, reasoning, fork and tool policy,
plus the path and SHA-256 of the independent participant-launch/tool evidence. A non-`none` fault attribution
must likewise point to a preserved engine, app, host or transport artifact and its SHA-256; a coordinator note
alone is not evidence.

The only allowed pairs are `final:none`, `participant_stop:none`, `timeout:none`, `timeout:app`,
`app_crash:app` and `runner_error:runner`. A frozen-app or participant completion failure on a working target
is scored. Any `TechnicalValid=false` launch discards its gameplay answer and may be replaced, at most twice;
L01–L05 and their replacements may total at most seven launches. After the evidence export, the coordinator
locks TechnicalValid and replacement status from provenance evidence in launch order before scoring or
recording any gameplay fields; gameplay answer quality must not affect that classification.

## 2. Exact participant prompt

The following code block must be byte-identical to the active contract. Replace `<SESSION_ID>` once; add no
greeting, explanation, success criterion or recovery hint.

`PromptHash` means the lowercase SHA-256 of the exact UTF-8 participant message after that replacement. It
excludes the Markdown fences and the newline immediately before the closing fence.

```text
당신은 Gridworks를 처음 플레이하는 전력회사 운영자입니다. 세션 ID는 <SESSION_ID>입니다.
먼저 coordinator가 지정한 computer-use SKILL.md를 끝까지 읽으세요. Computer Use wrapper는
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

## 3. Native preflight inherited by v3

The reviewed `S0B-L00` remains valid because v3 changes no build, UI, app target, fixture or Computer Use
action path. Do not start official v3 sessions unless all are true:

1. The host UI is unlocked and exactly one target process is running.
2. `get_app_state` returns twice within 20 seconds and exposes the exact title, current stage and enabled
   action through the accessibility tree or screenshot.
3. Element-index interaction is tried first. Screenshot coordinates or Tab/Return are fallback only.
4. One complete run reaches `FINAL`; the diagnostic contains the frozen ten-event sequence and the runner
   manifest agrees with it.
5. The source-manifest build and fixture hashes match the implementation checkpoint; prompt, facilitator and
   assignment hashes match the current v3 run-protocol checkpoint.
6. An independent protocol review confirms the setup/scored-interaction boundary and that no game context is
   available through bootstrap metadata.

If both AX and screenshot are unavailable, or a real accepted command cannot be produced, record
`PROXY-RUN-BLOCKED`. Do not substitute static cards or a screenshot questionnaire.

## 4. Official run order and scoring boundary

- Spawn one cold `gpt-5.6-sol`, reasoning `medium`, `fork_turns=none` session at a time.
- Across the whole task, allowed Gridworks information sources are only the exact message, designated skill
  and current frozen target UI. Generic environment-owned tool name/signature metadata, import/dispatch/
  transport errors and exact-target metadata are non-game diagnostics and may be read-only. Repository,
  source/data, diagnostics/logs, web, stored screenshots/static cards, oracle/rubric, prior sessions, app
  inventory and other-app contents are forbidden. Literal wrapper spelling and first-call success are not
  validity criteria.
- The first readable Gridworks state starts scored interaction. No UI action may precede it. Every Gridworks
  read/action must use the frozen target through `tools.mcp__node_repl__js` and `@oai/sky`.
- After each Sky UI action, fetch fresh app state in the same dispatch or the next read-only direct-wrapper
  dispatch before deciding the next action. If no readable target state appears, use independent evidence:
  host/process/transport failure is `runner_error:runner`, while a working target and transport followed by
  participant stop/timeout is a TechnicalValid scored failure. Do not inspect repository or unrelated apps.
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

다음을 실제 순서대로 기록하세요: (1) task 시작부터 final report까지의 모든 tool call 이름·목적·
content source, (2) skill read와 첫 readable Gridworks state 전 metadata/error의 exact text,
(3) 각 Gridworks call의 request code 또는 method/action, result status/content type과 fresh-state 여부,
(4) 첫 readable Gridworks state를 scored interaction start로, final app-state read를 UI interaction end로,
final report 제출을 measurement end로 표시, (5) 금지 source 접근이 있었다면 그 call과 반환 내용을
명시하고 없었다면 없다고 명시. 전체 AX 본문은 복제하지 마세요. repository나 다른 파일을 읽지
말고 앱을 조작하지 마세요.
```
