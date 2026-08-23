# Realtime Commercial UX evaluator

이 디렉터리는 `Release.V3`의 연속 실시간 규칙과 R2 화면을 대상으로, 판매 가능한 게임 경험을
`gpt-5.6-sol` / `ultra`로 평가하는 내부 LLM-as-a-judge 도구를 소유한다. 자동 점수는 사람 사용성,
미감, 한국어 전문 교정이나 공개 출시 승인이 아니다.

## 현재 상태

현재 포팅된 권위는 **text plan → blinded text evaluation**, UX-R1의
**Debug R2 candidate·targeted route authority**, **non-score session/attempt authority**와
**finalized evaluation-chain parent claim**, **finalized blocked current-route artifact chain**, 그리고
**local controlled Codex transcript authority**다. 마지막 항목은 요청한 `gpt-5.6-sol`/`ultra`와 fresh
local rollout이 일치했음을 결속하지만 platform/API attestation이나 model judgment는 아니다.

- 콘텐츠 권위: `data/release-campaign-v2.json`
- 실시간 일정 권위: `data/release-campaign-v3.json`
- 규칙 방향: pause·1×·2×·4×, timestamped construction, forecast/event, thermal duty
- 핵심 planning surface: 현재 시각·다음 사건 countdown·사건 구간·공사 완료·열 보호 경계를 함께
  보여 주는 수평 future-event status bar (`RealtimeEventRail`)
- 작성 narrative topology: 34개
  - briefing 8
  - window story 6
  - result card 11
  - epilogue card 3
  - epilogue promise branch line 6
- 현재 native 표현: `RealtimeSliceMain`의 FIRST_LIGHT targeted R2 slice만 확인됨
- UX-R2.1 product source: `e385707071e4ccfb34d5200e3401897db7f164ad`; shared release loader,
  FIRST_LIGHT controller/result, 단일 chronological rail과 actual-input-only checkpoint host의
  deterministic build·회귀 및 두 독립 review P0 0/P1 0
- UX-R2.1 actual-input 관찰: `PASS_THREE_NON_SCORE_RECORDS`; headless PASS와 actual record를 서로
  대신하거나 score evidence로 승격하지 않음
- 현재 candidate: 비기본 Debug/editor 39-file exact project tree, public package 아님
- 현재 실행 영수증: 두 checkpoint 성공 + missing/extra/FULL_FLOW selector 거부를 독립 verifier가
  fresh process로 재실행
- 전체 8장 native E2E: `NOT_IMPLEMENTED`
- 현재 text 형성평가: `TextPlanProxy = 83.4475`
- 공식 `CommercialUXProxy`: 없음

이 상태에서 text 점수가 높아도 `TextPlanProxy`일 뿐이며 87 종료 조건을 충족하지 않는다. 이전
`codex/commercial-ux-87`의 V2 native evaluator·gold replay·candidate packager는 설계 참고 이력이다.
현재 V3/R2 authority에 맞게 다시 결속하기 전에는 실행 권위나 점수 근거가 아니다.

## Debug R2 candidate authority

```sh
python3 tools/commercial-ux/native/test-realtime-candidate-authority.py

python3 tools/commercial-ux/native/build-realtime-candidate-authority.py \
  --source-revision 379e9800c81ca315976ab4c28d511664df6ab7ed \
  --output /tmp/gridworks-realtime-candidate-manifest.json
```

source revision `379e9800c81ca315976ab4c28d511664df6ab7ed`의 고정 증거는 candidate
`sha256:373785e45a4485dfeded43466a5bff0f66de4a0c106c972262686e7a432cbdd6`, 실행 영수증
`sha256:b28ebbfcca183fa5de6b2fd483b00d563906c731ff8696dc2e0c4d17861da4e1`다. 같은 source revision에서
두 fresh manifest의 raw SHA-256은 모두 `ca7826d38cae6e8a28e142e10e522e9c1425ba6abcec938182d9819ab0b2a816`이었고,
root 16개 적대적 테스트와 Draft 2020-12 strict AJV가 통과했다. 독립 재실행도 16/16, 같은 두
manifest/hash, AJV 8.20.0 strict PASS를 재현했고 최종 review는 P0 0/P1 0이다.
evaluator producer aggregate는
`sha256:c501608b8d47a35c32860ad1defe33a369cbc0313ab0f860952def52ef4e881c`, 고정 policy는
`sha256:96366d0bbc8ec4870b13b705595d036e5af949bab2e5623a07001bba16c48b08`다.

manifest는 다음을 결속한다.

- 제품 source 170개 Git blob, evaluator producer/policy/schema/test 4개 Git blob과 실제 실행 바이트
- 실제 실행하는 Command Line Tools Git 본체의 bytes/version/object format, explicit per-worktree Git dir,
  fresh allowlist 환경, CLI+환경 양쪽의 replace-object·lazy-fetch 차단
- Godot 4.7.1 mono regular-file tree 153개와 Debug package 39개
- `A1_NORMAL_READY`, `A1_CONSTRUCTION_DUE_1M`의 exact replay/start/end와 두 성공 probe
- missing/extra/FULL_FLOW-as-checkpoint의 세 typed 거부 probe
- 기본 장면은 V2 `CommercialMain`, 평가 장면은 명시적 비기본 `RealtimeSliceMain`
- future-event status bar의 6-signal 계약과 두 checkpoint scene-load wiring

이는 `EDITOR_HEADLESS_DIAGNOSTIC_ONLY`다. `.app`/fresh install, 실제 입력, 화면·audio, 가독성, 전체
8장, save/resume, finale·epilogue, model `ultra` execution receipt를 증명하지 않는다. schema는 구조
검사용일 뿐이며 candidate 권위에는 `verify_manifest_against_reconstructed_authority`가 내부에서 다섯
probe를 다시 실행해야 한다. .NET 권위도 resolved wrapper+host 두 파일에 한정되며 transitive
SDK/runtime closure는 결속하지 않았다. Git도 직접 실행 파일만 결속하며 system dylib transitive
closure는 결속하지 않았고, 이 경계에서 score-bearing capture는 계속 금지된다.

## Non-score session/attempt authority

```sh
python3 tools/commercial-ux/native/test-realtime-session-authority.py
python3 tools/commercial-ux/native/realtime-session-authority.py --help
```

source revision `5a31ff35a6e2d293c2f1800e4297945ecf3a5584`의 session evaluator는 tool, policy, 세
structural schema, candidate verifier dependency와 adversarial test를 포함한 7개 Git blob을 결속한다.
aggregate는 `sha256:faea99acbc3a09334ccc8fbb9140a894f99fbfb0340ce224e9f12cba9b54b3e9`다.

- `create-session targeted-checkpoint`는 `A1_NORMAL_READY` 또는
  `A1_CONSTRUCTION_DUE_1M` 하나와 future-event bar 6-signal receipt를 고정한다.
- `create-session story-part-unit`는 canonical selector 하나와 exact 34-part row를 고정하며 native
  reachability를 주장하지 않는다.
- `create-session full-flow-exception`은 `UNAVAILABLE_NOT_IMPLEMENTED`를 기록하고 attempt나 output을
  만들지 않는다.
- executable route의 최대 세 attempt는 이전 terminal raw/self-hash를 연결한다. empty/malformed
  transport만 retry 가능하고, 유효하지만 다른 JSON은 즉시 nonretryable integrity failure다.
- claim은 candidate/story snapshot 뒤 마지막 `O_EXCL + fsync` commit marker이며 session/input/attempt
  root에 evidence·judge·score 파일을 미리 둘 수 없다.

root suite는 12/12 PASS, 세 schema는 AJV 8.20.0 Draft 2020-12 strict PASS이고 독립 재실행도 12/12,
P0 0/P1 0이다. 이 authority는 producer 실행을 외부 attestation하지 않으며 capture·judge·점수를 만들지
않는다.

## Finalized evaluation-chain parent claim

```sh
python3 tools/commercial-ux/native/test-realtime-evaluation-chain-authority.py
python3 tools/commercial-ux/native/realtime-evaluation-chain-authority.py --help
```

source revision `74ba7256766f41c1398fba98f59c1c942a4cb96e`의 chain authority는 finalized session을
수정하지 않고 deterministic sibling `<session-root>.evaluation-chain-v1`에 다음 parent prefix를 봉인한다.

- session claim과 bound candidate/story raw bytes
- executable route의 ordinal 1부터 마지막 유일 `SUCCESS`까지 모든 start/output/terminal bytes
- full-flow의 경우 attempt/output 없이 session claim에서 canonical 추출한 unavailable terminal
- targeted route의 future-event 6 signal, headless wiring PASS와 native quality `NOT_OBSERVED`
- 다음 단위가 사용할 정확히 일곱 artifact path; 이 단위는 `artifacts/`나 placeholder를 만들지 않음

claim은 session claim flock을 유지한 채 두 번 원본을 읽고 snapshot byte/inventory를 다시 확인한 뒤 마지막
`O_EXCL + fsync` marker로 쓴다. caller는 chain root, nonce, attempt, outcome, route, model이나 score를
공급할 수 없다. 5개 Git blob aggregate는
`sha256:d87e605449e558d5debd2652f3cf0282f851da45eb19b85e1b0d811af18d218f`다. root와 독립 suite는
각각 14/14 PASS, AJV 8.20.0 strict PASS, 독립 review P0 0/P1 0이다. 이 claim은 원 session의 absolute
authority와 함께 검증되며 relocatable bundle, evidence, model receipt, native 관찰이나 점수가 아니다.

## Finalized blocked current-route artifact chain

```sh
python3 tools/commercial-ux/native/test-realtime-current-route-artifact-authority.py
python3 tools/commercial-ux/native/realtime-current-route-artifact-authority.py --help
```

source revision `a270339a778e49ce0458c61cef383fc96283a596`의 authority는 parent가 미리 고정한
일곱 path에 다음 append-only prefix를 쓴다.

1. `evidence-index.json` — 현재 route와 전체 finalized input snapshot의 non-score index
2. `actor-terminal.json` — `BLOCKED_NO_NATIVE_CAPTURE`
3. `judge-input.json` — `BLOCKED_NO_EXECUTABLE_JUDGE_INPUT`
4. `judge-terminal.json` — `BLOCKED_MODEL_EXECUTION_UNAUTHORIZED`
5. `verifier-result.json` — `BLOCKED_NO_JUDGE_OUTPUT`
6. `oracle-ledger.json` — `BLOCKED_NO_NATIVE_ORACLE_INPUT`
7. `aggregate.json` — 마지막 commit marker `FINALIZED_BLOCKED_NON_SCORE`

각 파일은 parent claim, source-bound policy/schema/producer, 모든 앞선 artifact의 raw/self hash와 canonical
path를 결속한다. 같은 session claim flock 아래에서 `O_EXCL + fsync`로 순서대로 쓰고 전체 prefix,
parent snapshot과 producer bytes를 반환 직전 다시 확인한다. partial prefix는 resume·삭제하지 않으며
aggregate semantic verifier는 aggregate object와 raw SHA-256만 반환한다. 저장소에는 실행 artifact를
보존하지 않는다.

targeted route는 future-event status bar의 6 signal, headless wiring PASS와 native quality
`NOT_OBSERVED`를 유지한다. story part는 authored-only이며 native reachability를 주장하지 않고,
full-flow는 0-attempt `UNAVAILABLE_NOT_IMPLEMENTED`를 유지한다. 이 authority는 native capture, model,
judgment, evidence verifier, product oracle, hard gate나 score aggregation을 실행하지 않는다.
`officialCommercialUX=false`, `ScoreBearingCaptureAllowed=false`, `CommercialUXProxy=null`이다.

11개 Git blob과 running bytes aggregate는
`sha256:225696ad11902e33213693e75e9576368a091b1a16ba32a3c0a449e6179dea1d`, policy raw는
`sha256:f27c3c49c00d547ee55ab5b0719fda1729ee13322dff6caccc48b2fea6297960`다. 최종 source-bound root
suite는 11/11 PASS(353.480초)했고, 네 route fixture의 genuine artifact 28개가 AJV 8.20.0 Draft 2020-12
strict instance 검증을 통과했다. write/fsync interruption, concurrent creator, late inventory,
parent/producer race, symlink/hardlink, cross-chain swap, downstream rehash와 false execution/score mutation은
fail-closed다. 독립 재실행도 11/11 PASS(374.887초), duplicate-key JSON·7 schema AJV strict·AST PASS와
P0 0/P1 0을 재현했다.

## Local controlled Codex transcript authority

```sh
python3 tools/commercial-ux/native/test-realtime-controlled-codex-transcript-authority.py
python3 tools/commercial-ux/native/realtime-controlled-codex-transcript-authority.py --help
```

source revision `2b0b6ee355790b73cc47eb17c17bd737bdcf8d9a`의 authority는 finalized blocked
aggregate가 고정한 candidate/session/route projection에 비점수 probe를 결속한다. prompt는 그 projection의
canonical hash를 그대로 되돌리는 고정 semantic echo이고, 실제 blocked `judge-input.json`은 실행하지 않는다.
직접 결속한 native Codex CLI를 fresh allowlist 환경에서 한 번 실행하고, 시작 전후 rollout inventory의 정확히
한 항목 추가, raw JSONL hash, 요청·보고 model/effort와 exact output을 마지막 `O_EXCL + fsync` receipt에
봉인한다. app shell에 `CODEX_INTERNAL_ORIGINATOR_OVERRIDE`가 있으면 create와 public verify 모두
`env -u CODEX_INTERNAL_ORIGINATOR_OVERRIDE`로 실행해야 한다.

7개 Git blob과 running bytes aggregate는
`sha256:1a9c24ff253374cb05a0b5854aeb7d7379329a0d0481656065f987e0f99c8751`, policy raw는
`sha256:ff77b3f3b95958b4813efb2a2a91ac3533faefd99e208f4d098640d1bc739cf6`다. source-bound suite는
13/13 PASS이고 start/final/output 세 schema가 AJV 8.20.0 Draft 2020-12 strict compile·instance 검증을
통과했다. 독립 public verifier가 확정한 genuine receipt raw SHA-256은
`sha256:f7c17c4a9bcf29bfe1dc77d1d638d755e32517e375f418b90356ebaba456891f`, transcript authority ID는
`sha256:d79bcb78d0f18426f7019f0959dbb3742d719154654b8716c298e0edaa840927`다. 요청과 local rollout은
`gpt-5.6-sol`/`ultra`, originator `codex_exec`, 1 turn·0 tool call로 일치했고, 독립 review는 P0 0/P1 0으로
`PASS_FOR_UX_R1_LOCAL_CONTROLLED_TRANSCRIPT_RECEIPT_SUBUNIT_ONLY`를 판정했다.

성공 전 만들어진 별도 poisoned root의 final receipt는 의도대로 zero-byte인 채 보존하며 resume·삭제·승격하지
않는다. 성공 receipt는 raw rollout, developer/system/auth/tool content를 root에 복사하지 않는다. 또한
platform model/effort/freshness attestation은 모두 false, server-signed receipt는 null이다. 현재 route의
`boundJudgeInputExecuted=false`, judge model call count는 0이고, future-event status bar는 여섯 signal과
headless wiring PASS만 유지하며 native quality는 `NOT_OBSERVED`다. 따라서
`officialCommercialUX=false`, `ScoreBearingCaptureAllowed=false`, `CommercialUXProxy=null`은 바뀌지 않는다.

## Story part 단독 실행

전체 캠페인을 재생하지 않고 작성된 narrative atom 하나만 검사할 수 있다.

```sh
dotnet run --project tools/Gridworks.CommercialChecks -c Release -- \
  --story-manifest

dotnet run --project tools/Gridworks.CommercialChecks -c Release -- \
  --story-part FIRST_LIGHT/briefing

dotnet run --project tools/Gridworks.CommercialChecks -c Release -- \
  --story-part campaign/epilogue/promise/NORTH_BANK_PROMISE/keep
```

manifest와 part는 base-content binding과 해당 장의 실시간 준비시간·결정기한·event ID를 함께 출력한다.
`authoredReachable=true`는 콘텐츠가 작성·결속됐다는 뜻이다. R2 UI에서 실제로 보인다는 뜻이 아니며
native reachability 필드는 의도적으로 없다. 잘못된 selector는 exit 2와 typed JSON
`INVALID_SELECTOR`, `UNKNOWN_CHAPTER`, `UNREACHABLE_STORY_PART` 중 하나를 반환한다.

## Text artifact와 평가

```sh
python3 tools/commercial-ux/test-realtime-text-plan-tools.py

dotnet run --project tools/Gridworks.CommercialChecks -c Release -- \
  --story-manifest > /tmp/gridworks-realtime-story-manifest.json

python3 tools/commercial-ux/build-text-plan-input.py \
  --story-manifest /tmp/gridworks-realtime-story-manifest.json \
  --campaign data/release-campaign-v2.json \
  --realtime-campaign data/release-campaign-v3.json \
  --output /tmp/gridworks-realtime-text-plan.json
```

builder는 base campaign, realtime overlay, context, story manifest의 raw SHA-256, canonical artifact
SHA-256과 둘을 함께 덮는 `textPlanSha256`을 묶는다. 16개 event는 ID뿐 아니라 priority, 시작 offset,
duration과 forecast lead까지 포함한다. `realtime_text_contract.py`는 정확한 8장/16 event/34 part
topology, 현재 native coverage의 정직한 상한과 future-event status bar의 필수 signal 계약을 고정한다.

세 blinded judge 결과는 다음처럼 집계한다.

```sh
python3 tools/commercial-ux/aggregate-text-plan.py \
  judgment-1.json judgment-2.json judgment-3.json \
  --text-plan /tmp/gridworks-realtime-text-plan.json \
  --story-manifest /tmp/gridworks-realtime-story-manifest.json \
  --campaign data/release-campaign-v2.json \
  --realtime-campaign data/release-campaign-v3.json \
  --context tools/commercial-ux/text-plan-context.json \
  --output /tmp/gridworks-realtime-text-aggregate.json
```

집계기는 네 원본을 다시 읽고 source hash, 전체 event timing, authored content와 envelope를 결정론적으로
재생성해 byte-equivalent인지 확인한다. binding이나 파생 artifact를 다시 hash한 위조도 거부한다.

모든 judgment는 서로 다른 fresh run이어야 하고 `judgeSlot=SOL-ULTRA`, `model=gpt-5.6-sol`,
`reasoningEffort=ultra`를 사용한다. 출력은 `SCORED_FORMATIVE` 또는 불안정성에 따른 재평가 상태이며
`officialCommercialUX=false`다. 불안정 panel은 보존하고 별도 이름의 새 INITIAL panel을 만든다.
현재 replacement 입력은 fail-closed로 비활성화돼 있다.

보존된 UX-R0 실행과 두 INITIAL panel의 상태·hash·증거 상한은
`playtests/commercial-ux-87-realtime/text-plan-r0/README.md`가 소유한다. 첫 panel의 raw 값은 불안정성으로
무효이며, 두 번째 panel의 83.4475도 native 관찰이 아닌 형성평가다.

## 완료된 UX-R1 gate

이 gate는 generic session/provenance machinery를 V3/R2 권위에 포팅하되, 실제 capture나 runtime
콘텐츠 구현 없이 다음 경계를 닫았다.

1. 완료 — R2 checkpoint와 full-flow 예외를 분리한 actor recipe (`379e980`)
2. 완료 — V3+필요 V2 의존성을 exact bytes로 닫은 replay/candidate authority (`379e980`)
3. 완료 — candidate/story snapshot, 34-part unit route와 append-only session/attempt authority (`5a31ff3`)
4. 완료 — finalized retry prefix와 future artifact path를 봉인한 non-score chain parent (`74ba725`)
5. 완료 — evidence·actor·judge·verifier·oracle·aggregate를 같은 parent에 결속한 blocked artifact chain
   (`a270339`)
6. 완료 — model identity를 자기선언 JSON이 아닌 동등한 local controlled transcript에 결속하는 권위
   (`2b0b6ee`; platform attestation 아님)

native presentation과 실제 입력·화면·audio capture는 이후 gate다. UX-R1에서도
`ScoreBearingCaptureAllowed=false`이며 `CommercialUXProxy >= 87`을 선언하지 않는다.
전체 독립 검토는 source boundary `2b0b6ee`에서
`PASS_FOR_UX_R1_WHOLE_GATE_CLOSURE`, P0 0/P1 0이다. UX-R2.1은 actual release `FIRST_LIGHT` 장의
briefing→live→authored result, 단일 chronological future-event rail과 interactive checkpoint host를
product source `e385707071e4ccfb34d5200e3401897db7f164ad`에서 구현했다. first-light review
`PASS_FOR_UX_R2_1_SOURCE_MAJOR_UNIT`과 single-rail review `PASS_FOR_SINGLE_RAIL_MAJOR_UNIT`은 모두
P0 0/P1 0이며 세 actual-input record도 PASS했다. package/candidate manifest는 이 carve-out의 증거가
아니므로 `N/A`다. 현재 UX-R2.2는 기존 text-plan 입력·34-part grammar를 바꾸지 않고 누적 tutorial
3장 prefix를 native 구현한다. unit/content 회귀는 다음 네 selector를 각각 실행한다.

```sh
dotnet run --project tools/Gridworks.CommercialChecks -c Release -- --story-part SECOND_HEART/briefing
dotnet run --project tools/Gridworks.CommercialChecks -c Release -- --story-part SECOND_HEART/result/standard
dotnet run --project tools/Gridworks.CommercialChecks -c Release -- --story-part SECOND_SOURCE/briefing
dotnet run --project tools/Gridworks.CommercialChecks -c Release -- --story-part SECOND_SOURCE/result/standard
```

이 selector PASS는 native reachability가 아니다. UX-R2.2는 source commit·build·독립 review 뒤 exact
`--release-through=SECOND_SOURCE` fresh-process 한 경로만 비점수 actual-input으로 관찰한다. A1 art,
4–8장, persistence, default/package와 score-bearing capture는 계속 미개방이다.
