# Realtime Commercial UX evaluator

이 디렉터리는 `Release.V3`의 연속 실시간 규칙과 R2 화면을 대상으로, 판매 가능한 게임 경험을
`gpt-5.6-sol` / `ultra`로 평가하는 내부 LLM-as-a-judge 도구를 소유한다. 자동 점수는 사람 사용성,
미감, 한국어 전문 교정이나 공개 출시 승인이 아니다.

## 현재 상태

현재 포팅된 권위는 **text plan → blinded text evaluation**, UX-R1의
**Debug R2 candidate·targeted route authority**, **non-score session/attempt authority**와
**finalized evaluation-chain parent claim**이다. evidence/actor/judge/verifier/oracle/aggregate artifact
chain은 아직 포팅하지 않았다.

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

## 현재 UX-R1 gate

현재 단위는 generic session/provenance machinery를 V3/R2 권위에 포팅하되, 실제 capture나 runtime
콘텐츠 구현 없이 다음 경계만 닫는다.

1. 완료 — R2 checkpoint와 full-flow 예외를 분리한 actor recipe (`379e980`)
2. 완료 — V3+필요 V2 의존성을 exact bytes로 닫은 replay/candidate authority (`379e980`)
3. 완료 — candidate/story snapshot, 34-part unit route와 append-only session/attempt authority (`5a31ff3`)
4. 완료 — finalized retry prefix와 future artifact path를 봉인한 non-score chain parent (`74ba725`)
5. evidence·actor·judge·verifier·oracle·aggregate를 같은 chain parent에 결속하는 artifact chain
6. model identity를 자기선언 JSON이 아닌 platform/API receipt 또는 동등한 transcript에 결속하는 권위

native presentation과 실제 입력·화면·audio capture는 이후 gate다. UX-R1에서도
`ScoreBearingCaptureAllowed=false`이며 `CommercialUXProxy >= 87`을 선언하지 않는다.
