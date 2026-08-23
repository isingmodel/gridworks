# Realtime Commercial UX evaluator

이 디렉터리는 `Release.V3`의 연속 실시간 규칙과 R2 화면을 대상으로, 판매 가능한 게임 경험을
`gpt-5.6-sol` / `ultra`로 평가하는 내부 LLM-as-a-judge 도구를 소유한다. 자동 점수는 사람 사용성,
미감, 한국어 전문 교정이나 공개 출시 승인이 아니다.

## 현재 상태

현재 포팅된 권위는 **text plan → blinded text evaluation**뿐이다.

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
- 전체 8장 native E2E: `NOT_IMPLEMENTED`
- 공식 `CommercialUXProxy`: 없음

이 상태에서 text 점수가 높아도 `TextPlanProxy`일 뿐이며 87 종료 조건을 충족하지 않는다. 이전
`codex/commercial-ux-87`의 V2 native evaluator·gold replay·candidate packager는 설계 참고 이력이다.
현재 V3/R2 authority에 맞게 다시 결속하기 전에는 실행 권위나 점수 근거가 아니다.

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

## 다음 gate

다음 단위는 generic session/provenance machinery를 포팅한 뒤 다음을 새로 만든다.

1. R2 checkpoint와 full-flow 예외를 분리한 actor recipe
2. V3+필요 V2 의존성을 exact bytes로 닫은 replay/candidate authority
3. FIRST_LIGHT뿐 아니라 tutorial 3장·본편 5장·결과·epilogue·save/resume의 native presentation
4. 실제 입력·화면·audio evidence를 보는 cold actor 3명, coverage, blind judge 3명

이 네 경계와 실제 macOS capture가 닫히기 전에는 `CommercialUXProxy >= 87`을 선언하지 않는다.
