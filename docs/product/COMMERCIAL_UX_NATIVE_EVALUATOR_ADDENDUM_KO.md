# Gridworks 상용 UX native evaluator 실행 부록

> 상태: **FROZEN v1.1 — 첫 score-bearing native evidence capture 전 동결**
> 적용 대상: `COLD-JOURNEY`, `COVERAGE-JOURNEY`, qualification, native judge/verifier/oracle
> 변경하지 않는 것: v1의 model·effort, label, cell/category weight, floor, cap, 수식과 목표

이 문서는
[`COMMERCIAL_UX_EVALUATION_PROTOCOL_KO.md`](COMMERCIAL_UX_EVALUATION_PROTOCOL_KO.md)의 native
실행에서 결정론적으로 해석할 수 없던 부분만 닫는다. TEXT-PLAN v1 계약과 이미 보존한 텍스트
판정은 바꾸지 않는다.

## 1. 동결 시점과 기존 Gate C의 지위

v1에는 exact `coverage-recipe.json`과 holdout queue가 없었으므로 “첫 게임 후보 수정 전”이라는 문구를
소급 충족했다고 주장할 수 없다. Gate C의 copy·presentation 수정과 developer pilot은 점수를 만들지
않은 개발 단계이며 official cold/coverage evidence로 사용하지 않는다.

native v1.1에서는 다음 순서를 권위로 삼는다.

1. concept probe, qualification anchor, actor/judge/verifier 계약, formative recipe와 전체 holdout queue를
   commit하고 SHA를 고정한다.
2. 그 뒤에만 qualification 또는 candidate를 보여 주는 native evidence capture를 시작한다.
3. 동결 뒤 제품을 고치면 현재 recipe를 재사용하거나 바꾸지 않고 queue의 다음 미사용 holdout을 쓴다.
4. 이미 연 holdout, 실패한 실행과 invalid transport를 삭제하거나 다른 candidate에 재사용하지 않는다.

즉 v1의 누락을 숨기지 않고, **첫 native 판정 전에 candidate-independent 계약을 고정**하는 것으로
교정한다. 이 부록이 commit되기 전에 얻은 native 화면·pilot·smoke는 hard-gate 개발 증거일 수는 있어도
LLM 점수 입력은 아니다.

## 2. score-bearing 호출 단위

- 세 cold actor는 각각 하나의 fresh process tree와 user-data에서 한 전체 chronological artifact를
  만든다. actor 사이에 save, 화면, 입력 또는 대화를 공유하지 않는다.
- coverage runner는 한 recipe의 모든 E00~E11 artifact를 한 envelope로 묶는다.
- **blind judge 한 명은 익명 cold artifact 3개와 coverage envelope 1개를 포함한 동일한 evidence-set
  envelope 전체를 한 번에 평가한다.** 따라서 candidate panel은 정확히 세 fresh judge call이다.
- judge output은 `artifactJudgments[]` 안에 actor artifact 3개와 coverage artifact 1개를 각각 한 번
  포함한다. 각 artifact는 rubric의 lane ownership에 해당하는 cell만 가진다.
- judge 간 actor 익명 ID, artifact 순서, frame/audio ID와 evidence-set SHA는 동일하다. judge ID만 다르다.
- verifier 한 명은 세 judge의 label·confidence·cell·score·threshold를 제거한 deduplicated observation과
  원본 artifact를 한 call로 확인한다.

schema 또는 transport failure만 같은 run slot에서 최대 두 번 고칠 수 있다. valid한 불리한 label,
`PARTIAL` 또는 `UNSUPPORTED`는 재시도하지 않는다.

## 3. first-use probe와 trace 완결성

`tools/commercial-ux/native/concept-exposure-manifest.json`이 각 probe의 첫 episode/checkpoint,
필수 관찰 문장과 해당 cell을 소유한다.

- actor는 first-use checkpoint에서 행동 전에 `currentGoal`, `expectedVisibleConsequence`,
  `citedVisibleSource`를 기록한다.
- approval 직전에는 별도의 prediction, 직후에는 observed result와 짧은 causal account를 기록한다.
- manifest의 `requiredForCold=true` probe가 terminal state 이전에 하나라도 없으면 actor artifact는
  `INVALID`다. 제품의 검증된 stall 뒤 probe만 `NOT_REACHED_BY_PRODUCT`가 될 수 있다.
- source 인용은 frame에서 읽을 수 있는 제목·목표·다음 행동·설명·상태 cue의 artifact ID를 가리켜야
  한다. README, source, log, save JSON, terminal과 web은 actor source가 될 수 없다.
- app-active action index는 앱에 전달된 서로 다른 합리적 행동만 증가한다. focus 탐색, tool 대기와
  inference 시간은 증가시키지 않는다.

## 4. qualification

`qualification-anchors.json`은 candidate-independent anchor 20개를 사용한다. 다섯 label마다 정확히
네 anchor가 있으며 동일 native judge prompt/schema로 판정한다.

- candidate를 보기 전에 세 fresh qualification judge를 실행한다.
- 각 judge는 exact expected band **19/20 이상**, 모든 `EXCELLENT`·`BROKEN` anchor exact, schema
  20/20을 만족해야 한다.
- panel 평균이나 다수결로 한 judge의 실패를 덮지 않는다.
- 한 judge라도 실패하면 세 명 전체를 한 번 fresh replacement한다. replacement도 같은 조건을
  충족하지 못하면 `BLOCKED_JUDGE_QUALIFICATION`이다.

qualification output은 candidate label을 보정하거나 calibration 값으로 산술에 넣지 않는다.

## 5. incident, cap과 terminal state

stable incident key는 `chapterId/windowId/screenId/incidentType` 네 요소다.

- `severe single-run incident`: `UX_STALL`, 외부 hint 시도, recovery action 3회 실패, actual/projection·
  must/promise·thermal/result 경계의 같은 오독이 서로 다른 checkpoint에서 두 번 발생한 경우다.
- `unresolved critical incident`: 두 actor 이상에서 verifier-supported `UX_STALL`, 또는 hard-gate FAIL이다.
- 제품 P0는 data loss/crash/softlock/필수 진행 불가, P1은 cap 49·69·79 조건 또는 required cell 70 미만의
  반복 supported 원인, P2는 floor를 깨지 않는 국소 마찰이다.
- cap이 없으면 `ActiveCap=100`. 여러 cap이 성립하면 가장 낮은 값을 쓴다.
- cap 79의 “반복 혼동”은 같은 stable incident key가 한 actor의 서로 다른 두 approval checkpoint에서
  발생하고, 세 actor 중 둘 이상이 기록한 경우다.
- cap 69의 recovery 행동 3개와 cap 49의 stall 행동 12개는 action ledger의 서로 다른 action kind와
  post-state hash 불변으로 oracle이 확인해야 한다.

한 actor만 severe이면 cold lane 전체를 한 번 교체한다. 두 actor 이상이 같은 verified stall이면 교체
조건이 아니라 제품 결과이며, manifest가 그 checkpoint 뒤로 매핑한 required cell만
`NOT_REACHED_BY_PRODUCT=0`이 된다.

## 6. hard-gate predicate

각 gate는 stable ID, producer, input hash와 판정을 `hard-gates.json`에 기록한다.

| ID | 결정론적 PASS predicate |
|---|---|
| HG01-AUTHORITY | strict world/campaign load, 정확히 8장·3 tutorial·5 main·epilogue |
| HG02-STORY | 26 canonical selector, 12 result branch, selector↔native presentation identity exact |
| HG03-REACHABILITY | recipe의 accepted journal prefix와 모든 required checkpoint snapshot hash exact |
| HG04-BUILD | CommercialChecks와 영향 회귀, clean Debug·Release rebuild 0 warning/error |
| HG05-TYPED-DISPLAY | source/path/bottleneck/thermal/cash/result/save typed fact와 evidence emitter fact exact |
| HG06-VIEWPORT | 1920×1080 UI 100/125의 manifest `requiredVisibleControl`이 visible, nonzero, panel/window bounds 안 |
| HG07-KEYBOARD | keyboard recipe의 모든 required action postcondition 도달, focus cycle은 visible focusable 수+2 이내 |
| HG08-NONCOLOR | required state마다 색 외 text/icon/shape cue ID가 하나 이상 존재하고 glyph 누락 0 |
| HG09-AUDIO | typed transition마다 지정 cue가 존재하고 action timestamp와 cue start 차이가 100 ms 이하 |
| HG10-REPLAY | mid-save·completed-save의 canonical snapshot/journal hash가 process restart 전후 exact |
| HG11-LIVENESS | 모든 동결 recipe와 alternate future witness가 bounded command count 안에 완료, crash/exception/data loss 0 |
| HG12-RAW-ID | visible/accessibility text에 world/campaign machine ID, enum token, raw exception allowlist 외 노출 0 |
| HG13-PROVENANCE | commit/model/effort/prompt/schema/recipe/input/output/execution artifact SHA exact |

actor의 12-action UX stall은 HG11의 engine softlock이 아니다. HG11은 accepted deterministic witness가
더 진행할 수 없는 상태만 판정한다.

## 7. verifier와 oracle

- verifier input은 관찰 문장과 그것이 인용한 frame/audio/action artifact만 남긴다. actor ID는 익명,
  judge ID는 opaque panel hash 하나로 바꾸며 label, polarity, confidence, cell, category, score, cap,
  threshold와 추천 변경을 제거한다.
- valid `PARTIAL`·`UNSUPPORTED`는 즉시 `BLOCKED_EVIDENCE_VERIFICATION`이다. schema/transport failure만
  최대 두 번 고칠 수 있다.
- source/path/bottleneck/thermal/cash/result/save, state hash, action count와 audio transition은 verifier가
  아니라 canonical Core/evidence oracle이 판정한다.
- score·cap·hard-gate에 쓰인 모든 관찰이 `SUPPORTED`이고 모든 typed fact가 oracle exact일 때만
  aggregation을 연다.

## 8. holdout queue

`tools/commercial-ux/native/holdout-recipes.json`은 `FORMATIVE-01`과 `HOLDOUT-01`~`HOLDOUT-08`을
동시에 고정한다. 각 holdout은 E00~E11 전체를 유지하고 다음만 미리 바꾼다.

- 임무 5·6·7의 검증된 두 원형 선택 bit
- keep/defer alternate branch 공개 순서
- actor 익명 permutation과 coverage artifact 순서

8개 prototype bit는 `000, 111, 010, 101, 001, 110, 011, 100` 순서다. 제품 결과를 본 뒤 recipe를
선택하지 않고 번호가 가장 낮은 미사용 holdout을 연다. 한 candidate당 하나만 소모한다.
`FORMATIVE-01`은 공개 개선용이며 official PASS를 소유하지 않는다. queue를 모두 소모하면 점수를
reroll하지 않고 `BLOCKED_HOLDOUT_EXHAUSTED`로 닫는다.

## 9. 실행 artifact provenance

공개 배포 package는 이 scope 밖이다. editor-native 실행은 다음을 모두 기록할 때 허용한다.

- source commit와 clean-tree 여부
- Godot executable SHA와 exact version
- Debug 또는 `COMMERCIAL_INTERNAL` managed assembly SHA
- PCK/imported resource manifest SHA
- world, campaign, rubric, prompts, schemas와 recipe SHA

이 경우 `packageSha256=null`, `packageStatus=EDITOR_NATIVE_NOT_PUBLIC_PACKAGE`로 기록하며 누락으로
간주하지 않는다. 내부 app bundle을 사용하면 bundle 전체 canonical manifest SHA를
`executionArtifactSha256`으로 기록한다. 어느 경우도 공개 판매·공증 증거로 승격하지 않는다.

## 10. 우선순위와 변경 규칙

이 부록은 native 실행 모호성에 한해 v1보다 우선한다. 서로 충돌하지 않는 v1 조항은 그대로다.
rubric, target, label anchor, category/cell weight, floor, cap 수치 또는 집계 수식을 바꾸려면 v1.1을
수정하지 않고 새 protocol major와 새 candidate sequence를 열어야 한다.

## 11. 첫 capture 전 session·attempt 단일성 명확화

첫 score-bearing capture 전 무결성 감사에서, holdout claim과 최종 panel seal 사이의 실행을 caller가
선택한 파일 목록만으로 증명할 수 없다는 공백을 닫았다. 이는 10절의 점수·rubric 계약을 바꾸지 않고
이미 금지한 reroll을 filesystem 수준에서 증명한다.

- canonical holdout receipt를 검증한 직후, gold capture나 actor/judge/verifier/oracle 노출보다 먼저
  evaluation-session claim 파일을 `O_EXCL`로 만들고 파일과 부모 디렉터리를 `fsync`한다. claim 파일은
  아직 존재하지 않는 session root 밖의 receipt별 singleton 경로를 사용한다.
- INITIAL 하나와 허용된 REPLACEMENT 하나는 서로 다른 canonical root를 쓴다. 각 root에는 역할이
  고정된 9개 opaque slot이 있고, attempt ordinal은 1~3의 연속 prefix만 허용한다.
- attempt root와 artifact root를 exclusive mkdir하고, output 0-byte 예약과 start receipt를 producer
  시작 전에 내구화한다. terminal receipt 경로는 output을 읽기 전에 0-byte `O_EXCL`로 예약한다.
  terminal 쓰기가 실패하면 그 0-byte 파일은 삭제·복구·재분류하지 않는 영구 tombstone이다.
- retry는 frozen role output의 strict JSON transport failure 또는 schema failure만 허용한다. valid하지만
  불리한 출력, `INPUT_UNREADABLE`, harness failure와 oracle failure는 다음 attempt를 열지 않는다.
- terminal은 output exact bytes와 두 번 동일하게 읽힌 전체 artifact tree의 locator/raw SHA/length
  content root를 결속한다. 집계기는 claim에서 모든 존재하는 attempt를 직접 발견하며 caller subset,
  symlink alias, 추가 root/file, 불완전 terminal과 변경 중인 artifact tree를 거부한다.
- REPLACEMENT claim은 INITIAL claim의 exact bytes와 함께 finalized scorecard·panel seal의 canonical
  path, raw/self SHA, rerun status, replacementRequiredLanes, attempt audit와 selected-attempt projection을
  결속한다. copied/resealed scorecard나 일부 field만 같은 initial은 replacement 권위를 만들 수 없다.
- 최종 집계는 scorecard 경로를 먼저 0-byte로 선점하고, holdout finalization과 claimed fixed panel seal을
  `fsync`한 뒤 scorecard bytes를 마지막에 기록한다. 어느 단계에서든 실패하면 schema-valid PASS
  scorecard가 남지 않는다.
