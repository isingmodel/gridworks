# Scope 0B LLM UI-proxy facilitator sheet

> Status: **FROZEN FOR L00 — official sessions remain closed until native Computer Use preflight passes**
>
> `BuildVersion = S0B-BUILD-v1`
>
> `PromptVersion = S0B-PROXY-v1`
>
> `DecisionRuleVersion = S0B-GATE-v1`
>
> `L00Status = PENDING`

This sheet is an execution copy of the active
[Scope 0B contract](../../docs/scopes/SCOPE_0B_PLAYABLE.md). It does not add rules, hints, retries or
scoring discretion. The coordinator may replace only `<SESSION_ID>` and `<VARIANT>` in the frozen command
and only `<SESSION_ID>` in the participant prompt.

## 1. Frozen native target

- Godot binary:
  `/Users/fred/dev/electric_simulator/.tools/godot-4.7.1/Godot_mono.app/Contents/MacOS/Godot`
- project: `/Users/fred/dev/electric_simulator/game`
- window title after READY: `Gridworks — 강변 병원 회랑`
- app target: the one Godot process whose visible window has that exact title
- engine log:
  `/Users/fred/dev/electric_simulator/playtests/scope-0b/private/<SESSION_ID>-godot.log`
- diagnostic JSONL:
  `/Users/fred/dev/electric_simulator/playtests/scope-0b/private/<SESSION_ID>-app.jsonl`
- runner manifest JSONL:
  `/Users/fred/dev/electric_simulator/playtests/scope-0b/private/<SESSION_ID>-runner.jsonl`

Run exactly one process and one session at a time:

```text
/Users/fred/dev/electric_simulator/.tools/godot-4.7.1/Godot_mono.app/Contents/MacOS/Godot --path /Users/fred/dev/electric_simulator/game --windowed --resolution 1280x720 --position 0,40 --single-window --accessibility always --rendering-method gl_compatibility --log-file /Users/fred/dev/electric_simulator/playtests/scope-0b/private/<SESSION_ID>-godot.log -- --session-id <SESSION_ID> --variant <VARIANT> --diagnostic-log /Users/fred/dev/electric_simulator/playtests/scope-0b/private/<SESSION_ID>-app.jsonl
```

Assignment is fixed:

| Session | Variant | Evidence role |
|---|---|---|
| `S0B-L00` | `ab` | unscored native tool rehearsal |
| `S0B-L01` | `ab` | official slot 1 |
| `S0B-L02` | `ba` | official slot 2 |
| `S0B-L03` | `ab` | official slot 3 |
| `S0B-L04` | `ba` | official slot 4 |
| `S0B-L05` | `ab` | official slot 5 |

Every log path must not exist before launch; the app fails closed rather than appending. The coordinator
records launch, READY and end with monotonic times and the exact `endReason`/`faultAttribution` pair from the
active contract. A frozen-app failure is scored; only independently evidenced runner faults are replaceable.

## 2. Exact participant prompt

The following code block must be byte-identical to the active contract. Replace `<SESSION_ID>` once; add no
greeting, explanation, success criterion or recovery hint.

```text
당신은 Gridworks를 처음 플레이하는 전력회사 운영자입니다. 세션 ID는 <SESSION_ID>입니다.
측정 시작 전에 coordinator가 지정한 computer-use SKILL.md만 한 번 읽고, 그 뒤에는 이미 열린
Gridworks 창을 Computer Use(node_repl + @oai/sky)로만 조작하세요. repository, shell, web,
정적 카드와 다른 세션은 보지 마세요.

화면에서 직접 읽을 수 있는 정보만 사용해 처음 상태부터 마지막 복구·결산 화면까지 도움 없이
진행하세요. 결과가 공개되기 전에 표의 네 병원 utility 공급경로 결과를 모두 예측해 잠그고,
그다음 건설할 회랑 하나를 선택하세요. 앱이 막히거나 끝까지 갈 수 없으면 실제로 시도한 마지막
상태와 막힌 이유를 남기세요.

마지막에는 짧게 보고하세요: (1) 서비스 권역이 가능하게 하는 것과 실제 공급에 더 필요한 것,
(2) 잠갔던 네 예측과 전기회로 사고/공간 통로 사고의 차이, (3) 선택한 회랑과 실제 사건 결과,
(4) 병원 UPS·디젤이 지킨 P0와 전력회사의 병원 인도·판매가 같은지 다른지,
(5) 예상 밖이거나 이해하기 어려웠던 조작.
```

## 3. L00 preflight

Do not start official sessions unless all are true:

1. The host UI is unlocked and exactly one target process is running.
2. `get_app_state` returns twice within 20 seconds and exposes the exact title, current stage and enabled
   action through the accessibility tree or screenshot.
3. Element-index interaction is tried first. Screenshot coordinates or Tab/Return are fallback only.
4. One complete run reaches `FINAL`; the diagnostic contains the frozen ten-event sequence and the runner
   manifest agrees with it.
5. The source-manifest, fixture and prompt hashes match the implementation checkpoint.

If both AX and screenshot are unavailable, or a real accepted command cannot be produced, record
`PROXY-RUN-BLOCKED`. Do not substitute static cards or a screenshot questionnaire.

## 4. Official run order and scoring boundary

- Spawn one cold `gpt-5.6-sol`, reasoning `medium`, `fork_turns=none` session at a time.
- Before measurement, permit only the exact computer-use skill read. During measurement, permit only
  Computer Use through `node_repl + @oai/sky`.
- Stop at 15 minutes. A participant stop or timeout on an otherwise working app is a valid scored failure.
- The four `PREDICTION_LOCKED` values are prediction authority; reveal-time prose cannot repair them.
- Fill [`record-template.csv`](record-template.csv) from transcript, diagnostic and runner evidence.
- Do not discuss earlier sessions, scores or expected answers with a later participant.
- Do not tune text, layout, values or controls between official sessions.

The gate is exactly the active contract: each of the four fields at least `4/5`, integrated at least `3/5`.
Selection ratio, completion time and click count are diagnostic only.
