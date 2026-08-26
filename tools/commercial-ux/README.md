# Gridworks 상용 UX 평가 도구

이 디렉터리는 작성된 story/text plan을 단독 검사하고, 나중의 native LLM-as-a-judge evidence를
결정론적으로 다루기 위한 도구를 보존한다. 현재 공식 점수는 없으며, 이 도구의 존재가 평가 실행
권한을 만들지 않는다.

## 현재 사실

- 작성 콘텐츠: 8장, 16개 사건, 34개 story part
- R2 native 구현: `LONGEST_NIGHT`까지 누적 8장
- 실제 직접 플레이 관찰: `NORTH_BANK_PROMISE`까지 누적 4장 Keep·명시적 Defer 결과
- 누적 8장 product의 모든 장 stable 진행, story-idle active event·duty와 exact-minute active
  `EventStory | DecisionWindowStory` v2 save/Continue, exact prior standalone `FIRST_LIGHT` v1 Continue:
  deterministic wiring 완료
- text 형성평가: `TextPlanProxy = 83.4475`
- 공식 native 평가: 미실행, `CommercialUXProxy` 없음
- current R2 evaluation candidate package: 없음
- score-bearing execution authority: 없음, `ScoreBearingCaptureAllowed = false`
- 고정 judge: `gpt-5.6-sol`, reasoning effort `ultra`

전체 평가 계약과 87점 gate는
[상용 UX 평가 프로토콜](../../docs/product/COMMERCIAL_UX_EVALUATION_PROTOCOL_KO.md)을 따른다.

## 1. story part 단독 실행

전체 캠페인을 재생하지 않고 narrative atom 하나를 unit 수준에서 검사할 수 있다.

```sh
./dev story manifest
./dev story FIRST_LIGHT/briefing
./dev story SWITCH_OFF_TO_PROTECT/result/standard
./dev story campaign/epilogue/promise/NORTH_BANK_PROMISE/defer
```

manifest와 part 출력은 base content와 실시간 일정·사건 ID를 함께 결속한다. `authoredReachable=true`는
콘텐츠가 작성됐다는 뜻이며 R2 UI에서 실제로 보인다는 뜻이 아니다. 잘못된 selector는 exit code 2와
다음 typed error 중 하나를 반환한다.

- `INVALID_SELECTOR`
- `UNKNOWN_CHAPTER`
- `UNREACHABLE_STORY_PART`

새 story 구간을 만들 때도 이 selector 단독 실행을 유지한다. 단독 unit이 production controller와
누적 장 상태를 대신하지는 않는다.

## 2. text-plan 생성과 회귀

도구 전체 회귀:

```sh
python3 tools/commercial-ux/test-realtime-text-plan-tools.py
```

현재 source에서 text-plan 입력을 만드는 예:

```sh
./dev story manifest > /tmp/gridworks-realtime-story-manifest.json

python3 tools/commercial-ux/build-text-plan-input.py \
  --story-manifest /tmp/gridworks-realtime-story-manifest.json \
  --campaign data/release-campaign-v2.json \
  --realtime-campaign data/release-campaign-v3.json \
  --output /tmp/gridworks-realtime-text-plan.json
```

builder는 8장/16개 사건의 priority, 시작 offset, duration, forecast lead와 34개 story part,
text-plan context가 기록한 coverage 상한을 하나의 입력에 묶는다. 현재 context의
`FIRST_LIGHT_TARGETED_R2_SLICE_ONLY`는 UX-R0 형성평가 baseline을 보존한 값이며 현재 8장 구현 상태가
아니다. 다만 현재 기본 장면 사실은 갱신됐으므로 UX-R0의 byte-exact context는
`playtests/commercial-ux-87-realtime/text-plan-r0/text-plan-context.json`에 따로 보존한다. 역사 panel은
그 파일과 해당 README가 지정한 동결 도구 기준으로만 재검증한다. text-plan은 구현 화면을 보지 않으므로
언제나 형성평가다.

세 fresh judgment를 집계하는 형식:

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

모든 judgment는 서로 다른 fresh context에서 `gpt-5.6-sol`/`ultra`를 사용한다. 이전 점수나 다른
judge 결과를 보여 주지 않으며 실패 panel을 덮어쓰지 않는다.

## 3. native 이전의 가장 작은 검증

### Core suite 하나

```sh
dotnet run --project tools/Gridworks.RealtimeChecks -c Release -- \
  --suite frame-speed-canonical-hash
```

suite 이름에 `hash`가 포함돼도 문서에서 고정 digest를 관리하지 않는다. 검사는 현재 fixture identity와
state transition이 변하지 않았는지 코드에서 판정한다.

save codec의 누적 8장 stable strict replay, v2 `closedStoryCount` strict shape, v1 read-only 호환과 pending
fail-closed를 확인할 때:

```sh
dotnet run --project tools/Gridworks.RealtimeChecks -c Release -- \
  --suite campaign-save-strict-replay
```

### R2 named checkpoint

```sh
./dev checkpoint A1_NORMAL_READY
./dev checkpoint A1_CONSTRUCTION_DUE_1M
```

checkpoint는 production controller·HUD signal·clock·presentation·world draw를 거쳐 정확한 근처 상태를
검사한다. `./dev check`는 Core의 누적 8장 stable replay와 pending fail-closed, isolated two-process의 active
`FLOOD_ISOLATION_TEST` story product save-create→same-story Continue, exact prior standalone `FIRST_LIGHT` v1
Continue와 invalid/unsupported/I/O-failure title 상태를 함께 검사한다. queued story·initial/result/briefing
handoff·완료를 포함한 전체 save/resume와 전체 8장 production-input 여정은 별도 fresh-process E2E가
필요하다.

## 4. `native/` 도구의 경계

`native/`에는 candidate, session/attempt, evaluation chain, evidence artifact와 controlled transcript를
fail-closed로 다루는 비점수 도구가 있다. 이들은 UX-R1에서 구조와 거부 경로를 검증했지만, current R2의
finale·epilogue를 포함한 전체 제품 여정 candidate와 실제 화면/audio evidence를 만들지 않는다.
그 안의 pinned candidate는 historical editor-native/비기본 First Light 기준선이다. 설치 가능한 current
R2 package가 아니며 title/이어하기/settings/audio/finale·epilogue evidence나 score-bearing model call을
만들지 않는다.

따라서 다음 주장은 금지한다.

- headless checkpoint를 native cold play라고 부르기
- authored story part를 native reachability로 부르기
- story/modal continue를 title의 `이어하기`로 부르기
- 과거 V2 title/save/settings/audio를 current R2 기능으로 부르기
- 과거 V2/비기본 candidate를 current R2 package로 부르기
- local controlled transcript를 platform attestation이나 judge 결과로 부르기
- repository JSON의 model 이름이나 local transcript를 platform/API execution authority로 부르기
- package gate 전에 수집한 artifact를 나중에 official session으로 승격하기
- text score를 `CommercialUXProxy`로 승격하기

공식 native 평가를 열 때는 [남은 작업](../../docs/NEXT_TASKS.md)의 finale·epilogue 포함 전체 제품 여정,
undelivered Core transition·queued story·initial/result/briefing handoff·완료를 포함한 전체 save/resume와
완료 후 선택, audio/settings와 fresh-install candidate gate를 먼저 닫는다. 이 package gate가 current R2 candidate
packager, finalized manifest와
verifier를 소유한다.
이어서 그 finalized candidate를 소비하는
versioned evaluation-session authority, capture, evidence verifier, hard-gate oracle과 score aggregator를
별도 gate로 구현한다. 이 전환은
[평가 프로토콜](../../docs/product/COMMERCIAL_UX_EVALUATION_PROTOCOL_KO.md)의
rubric·hard gate·model receipt를 함께 결속하고 누락에서 fail-closed해야 한다. 기존 UX-R0 context와
panel은 덮어쓰지 않으며, 새 version에서 current coverage와 evidence 상한을 다시 정의한다.

## 5. 사건 지평선(future-event bar) 평가 요구

한 줄 rail은 다음 정보를 같은 시간 권위에서 보여 줘야 한다.

- 현재 시각과 다음 중요 경계까지 countdown
- 사건 시작과 종료 interval
- actual/draft 공사 완료
- promise decision deadline
- 열 노출 종료, 보호정지와 복귀

각 항목은 compact marker를 사용하고 hover 또는 선택 시 상세 overlay를 연다. 코드 존재와 headless
wiring은 품질 증거가 아니다. cold/coverage journey에서 처음 보는 플레이어가 marker를 발견하고,
서로 비교하고, 상세를 연 뒤 올바른 행동을 결정하는지를 별도로 관찰한다.
