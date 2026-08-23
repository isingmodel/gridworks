# UX-R0 실시간 텍스트 기준선 증거

이 디렉터리는 `origin/main`의 Release.V3/R2 실시간 방향 위에서 만든 UX-R0 형성평가를 보존한다.
도구·계약 구현 기준 커밋은 `74827cc`다. 이 증거는 텍스트 계획의 완결성 우선순위를 정하지만 실제
native 플레이, 사람 사용성, 공개 출시 또는 공식 `CommercialUXProxy`를 증명하지 않는다.

## 입력 결속

```text
base campaign sha256       = 078df95f9f0c833be7e1a299088b4ab6e0de4ddf13426ce5b96a1abbeee70b7a
realtime campaign sha256   = ef962a272683bfd6761fbf10a0ca14cb6c8bf90cdfde810b468ad451088f2258
text context sha256        = b93a763a7cb660dcb78bab85632760f23ff5be87574733557b367832ffd513c5
story manifest sha256      = 9072eef635220de62bf998088f88ea0ba86b2676f28085f16195f39712953180
artifactSha256             = a294389b51e14767e7cfa584c4f1f370152b5ea5e8a414ffb37f22d7aabbf303
textPlanSha256             = 6c592b78fdf074bdbcc49125eb6af4054431d5b434884a0b629dcca225564dc7
text-plan file sha256      = 7553603ea409d2ed6de2db0a046b0dd0616f4278a8d8d1ea8a733fc442c7d4b7
```

동결 평가 권위는 다음과 같다.

```text
canonical rubric sha256    = b13672772ee2a8fb9e1467ae3ee3b6a4a445f560cfd8079a38227dbe55d934c2
prompt template sha256     = 23fc0428cd728bea35df22f5e202e5e1cc1e67e0a37d941d6068e630f3c2dc15
judgment schema sha256     = 8a30187170b081f7d5a4d3853917c9191e4719cc7bff7cbb59b726046b740f6f
contract source sha256     = ccf30ed32b4345af5aea9f7337854a544304dbdb9173e527a00b9f29842a8ec3
builder source sha256      = f1a7b548ca7ed9f5946d4292680e4b543f6699597c813bb3e6db7e835be88689
```

`story-manifest.json`은 8 briefing, 6 window, 11 result, 3 epilogue card, 6 promise branch line의
34개 authored atom을 포함한다. `text-plan.json`은 8장 16개 사건 각각의 ID, priority, 시작 offset,
duration과 forecast lead를 원본 네 종과 함께 hash-bind한다.

## 판정 패널

모든 판정 파일은 `model=gpt-5.6-sol`, `reasoningEffort=ultra`, `judgeSlot=SOL-ULTRA`와 같은
`textPlanSha256`을 선언한다. 각 run은 서로의 답이나 점수를 받지 않은 fresh task였다. 다만 현재
오케스트레이션 플랫폼은 저장소에 서명된 execution receipt를 내보내지 않으므로 이 model provenance는
repo 밖의 서명 증거가 아닌 형성평가 기록이다.

첫 INITIAL 패널은 덮어쓰지 않고 보존한다.

```text
judgments                  = judgment-01.json, judgment-02.json, judgment-03.json
aggregate                  = aggregate.json
panelInputSha256           = 1145fd03ce93bf9fbc0143267d00d5a70c55bda83e26fd5ff1014f0757623c20
status                     = RERUN_REQUIRED_JUDGE_INSTABILITY
unstable cell              = TP-A1
textPlanProxy              = null
raw pre-instability value  = 82.5875 (유효 점수 아님)
```

두 번째 패널은 replacement가 아니라 별도 세 fresh run으로 만든 새 INITIAL 패널이다.

```text
judgments                  = judgment-04.json, judgment-05.json, judgment-06.json
aggregate                  = aggregate-initial-02.json
panelInputSha256           = 9e37a3e33cc0a8360e5135c6e8cc2a3c1a61cb046447ab5c1a1275e7957f23a5
status                     = SCORED_FORMATIVE
unstable cells             = none
textRaw                    = 85.625
disagreementPenalty        = 2.1775
TextPlanProxy              = 83.4475
officialCommercialUX       = false
CommercialUXProxy          = null
```

범주 점수는 causality 94.0, tutorial 92.5, agency 92.5, pacing 81.25, Korean 74.5,
journey 70.0이다. `TextPlanProxy`는 목표 87보다 3.5525 낮으며 공식 점수로 승격할 수 없다.
원본 네 종에서 manifest와 text plan을 다시 만들면 저장본과 byte-identical이다. aggregate는 출력
디렉터리에 따라 달라지는 비점수 필드 `replacementReceiptPath`를 제외하면 다시 만든 결과와 동일하다.

## 안정적으로 드러난 개선 축

- 현재 native 범위가 `FIRST_LIGHT` R2 slice뿐이라 8장 진행·결과·에필로그의 실제 완결성이 없다.
- 결과에서 약속 원장, 다음 장 상태와 재플레이 선택으로 이어지는 closure가 구체적이지 않다.
- 세 번째 튜토리얼까지 판단은 확장되지만 안내와 개입이 언제 철회되는지 정의되지 않았다.
- 첫 경로 선택은 비교 가능한 비용·완공 시각·안전 여유가 브리핑에 충분히 노출되지 않는다.
- pause, countdown, forecast, duty와 future-event status bar의 플레이어용 한국어 명칭이 고정되지 않았다.
- 잘못된 배치, 미완공, 공급 실패의 원인·복구를 설명하는 한국어 오류 문구가 없다.

future-event status bar는 이후 native 구현에서도 `CURRENT_TIME`, `NEXT_EVENT_COUNTDOWN`,
`EVENT_START_END`, `CONSTRUCTION_COMPLETION`, `PROMISE_DECISION_DEADLINE`,
`THERMAL_TRIP_RECOVERY` 여섯 신호를 한 planning surface에 유지해야 한다. 코드 존재만으로는 native
가시성·동기화·이해를 통과한 것으로 보지 않는다.

## 증거 상한

UX-R0 종료 후보에서 다음 bounded check를 다시 실행했다.

```text
dotnet build game/Gridworks.Game.csproj -c Debug --no-restore
  PASS, 0 warnings, 0 errors
dotnet run --project tools/Gridworks.CommercialChecks -c Release
  PASS, 31 suites, 7084 assertions
dotnet run --project tools/Gridworks.RealtimeChecks -c Release
  PASS, 22 suites, 639 assertions
python3 tools/commercial-ux/test-realtime-text-plan-tools.py
  PASS, 34 parts, 16 mutations
manifest/text-plan regeneration
  PASS, byte-identical
aggregate regeneration
  PASS, output-local replacementReceiptPath를 제외하고 identical
```

독립 score-integrity 검증은 두 panel을 현재 동결 권위에서 다시 계산해 P0/P1 0으로 판정했다. 저장소에
서명된 model execution receipt가 없다는 P2 한계는 아래 상한에 반영한다. UX-R0 종료 커밋 자체의 최종
독립 검토는 아직 남아 있다.

```text
TextPlanProxy = 83.4475_FORMATIVE
TextJudgeExecutionReceipt = NOT_EXPORTED_FORMATIVE_ONLY
CommercialUXProxy = null
ScoreBearingCaptureAllowed = false
NativeCapture = BLOCKED_MAC_LOCKED
UXR0ClosureReview = PENDING
```
