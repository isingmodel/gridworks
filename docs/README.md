# Gridworks 문서 안내

이 디렉터리는 **현재 `./assets` 스타일 실시간 게임 방향과 그 위의 상용 UX 87 활성 작업에 필요한
문서만 전면에 둔다.** 완료·중단된 과거 상세 계약은 현재 권한과 혼동되지 않도록 Git 이력으로
돌리고, 핵심 사실만 `archive/`에 압축했다.

## 현재 문서 구조

```text
docs/
├── README.md
├── ROADMAP_2D.md
├── ROADMAP_2D_CHECKLIST.md
├── product/
│   ├── GAME_DESIGN_KO.md
│   ├── COMMERCIAL_UX_EVALUATION_PROTOCOL_KO.md
│   ├── OBJECT_CATALOG.md
│   └── VISUAL_PRODUCTION_SPEC.md
├── scopes/
│   ├── ASSET_STYLE_REALTIME_GAME.md
│   └── COMMERCIAL_UX_87.md
└── archive/
    ├── README.md
    └── COMPLETED_HISTORY.md
```

## 질문별 소유권

| 질문 | 소유 문서 | 경계 |
|---|---|---|
| 지금 무엇을 할 수 있는가? | [루트 README](../README.md) | 목표와 코드 구현 권한을 구분 |
| 현재 단일 작업 scope와 gate는 무엇인가? | [에셋 스타일 실시간 게임 계약](scopes/ASSET_STYLE_REALTIME_GAME.md#a1-g3--기존-g3-시각-적용-carve-out--활성) | 기존 G3 visual layer만 R2에 적용; V2 gameplay 병합 금지 |
| LLM 점수와 증거를 어떻게 만드는가? | [상용 UX 평가 프로토콜](product/COMMERCIAL_UX_EVALUATION_PROTOCOL_KO.md) | 사람 검토·공식 출시 승인을 대신하지 않음 |
| 스토리 파트 하나만 어떻게 검사하는가? | [평가 도구 안내](../tools/commercial-ux/README.md) | authored reachability를 native reachability로 주장하지 않음 |
| 최종적으로 어떤 게임을 만드는가? | [게임 기획서](product/GAME_DESIGN_KO.md) | 단계별 파일·절차는 만들지 않음 |
| `./assets` 스타일을 게임에서 어떻게 재현하는가? | [비주얼 제작 명세](product/VISUAL_PRODUCTION_SPEC.md) | 규칙·수치를 새로 계산하지 않음 |
| 현재 전체 목표와 금지 범위는 무엇인가? | [에셋 스타일 실시간 게임 계약](scopes/ASSET_STYLE_REALTIME_GAME.md) | 활성 코드 gate가 없으면 구현하지 않음 |
| 라이브 테스트를 어디서 시작하는가? | [현재 목표 계약 §8](scopes/ASSET_STYLE_REALTIME_GAME.md#8-구간-직접-진입형-라이브-테스트) | 필요한 checkpoint가 기본, 전체 시작은 예외 |
| 어떤 순서로 전환하는가? | [로드맵](ROADMAP_2D.md) | 다음 단계 후보는 자동 승인 아님 |
| 현재 단계와 증거 상태는 무엇인가? | [체크리스트](ROADMAP_2D_CHECKLIST.md) | 긴 로그·규칙을 복제하지 않음 |
| 설비와 상태는 무엇인가? | [오브젝트 카탈로그](product/OBJECT_CATALOG.md) | 현재 규칙과 시각 준비 상태를 분리 |
| 과거에 무엇을 완료·중단했는가? | [완료 이력](archive/COMPLETED_HISTORY.md) | 현재 구현 권한이나 현재 증거가 아님 |

충돌하면 다음 순서를 따른다.

```text
현재 사용자 지시
→ 루트 README가 지목한 단일 active scope와 gate
→ 제품 방향 계약
→ 질문별 소유 문서
→ 압축 과거 이력
```

## 현재 경계

- `CurrentGoal = ASSET_STYLE_REALTIME_GAME`
- `ActiveScope = A1_G3_EXISTING_VISUAL_APPLICATION`
- `ActiveEvaluationGate = SUSPENDED_AT_USER_REQUEST_AFTER_UXR23_SOURCE_REVIEW`
- `NextEvaluationGate = NONE_USER_REQUESTED_STOP_AFTER_G3_APPLICATION`
- `UserAuthorization = EXPLICIT_APPLY_EXISTING_G3_DESIGN_THEN_STOP`
- `ProductArtImplementationGate = A1_G3_EXISTING_VISUAL_APPLICATION_ACTIVE`
- `DocumentationBaseline = A0_COMPLETE`
- `ArchitecturePreparation = COMPLETE`
- `NextCandidate = G3_R2_VISUAL_LAYER_ONLY`
- `RealtimeRuleAuthority = RELEASE_V3`
- `RealtimeUxAuthority = R2_TUTORIAL_PREFIX_THROUGH_SECOND_SOURCE_PLUS_REVIEWED_UX_R2_3`
- `FutureEventStatusBar = PASS_DETERMINISTIC_SINGLE_CHRONOLOGICAL_TRACK_COMPACT_MARKERS_CUSTOM_HOVER_DETAIL`
- `FullCampaignNativeE2E = NOT_IMPLEMENTED_THREE_CHAPTER_PREFIX_ONLY`
- `TextPlanProxy = 83.4475_FORMATIVE`
- `TextJudgeExecutionReceipt = NOT_EXPORTED_FORMATIVE_ONLY`
- `CommercialUXProxy = null`
- `ScoreBearingCaptureAllowed = false`
- `NativeCapturePolicy = NOT_REQUESTED_USER_STOP_AFTER_G3_APPLICATION`
- `NativeCaptureEnvironment = MAC_CONSOLE_UNLOCKED_NOT_AUTHORIZATION`
- `UXR21GateStatus = COMPLETE_NON_SCORE`
- `UXR21ProductSourceAuthority = PASS_SOURCE_REVISION_E385707071E4CCFB34D5200E3401897DB7F164AD`
- `UXR21SourceReview = PASS_FOR_UX_R2_1_SOURCE_MAJOR_UNIT_P0_0_P1_0_SOURCE_EC265999`
- `UXR21SingleRailReview = PASS_FOR_SINGLE_RAIL_MAJOR_UNIT_P0_0_P1_0_SOURCE_E385707`
- `UXR21ClosureReview = PASS_FOR_UX_R2_1_CLOSURE_MAJOR_UNIT_P0_0_P1_0_SOURCE_F2839D1`
- `UXR21ActualInputObservation = PASS_THREE_NON_SCORE_RECORDS`
- `InteractiveCheckpointHost = ACTUAL_INPUT_PASS_A1_NORMAL_READY_AND_A1_CONSTRUCTION_DUE_1M`
- `FirstLightNativeStoryReachability = FORMATIVE_DIRECT_PLAY_PASS_AUTHORED_STANDARD_RESULT`
- `UXR21DeterministicEvidence = BUILD_0_WARNINGS_REALTIME_23_673_COMMERCIAL_31_7084_UI_MATRIX_PASS_STORY_34`
- `UXR22GateStatus = COMPLETE_NON_SCORE`
- `UXR22GateOpeningReview = PASS_FOR_UX_R2_2_GATE_OPENING_P0_0_P1_0`
- `UXR22ProductSourceAuthority = PASS_SOURCE_REVISION_40ED3FAB92A7054D6BC40D609AB6C5D1E1F801CC`
- `UXR22MajorUnitSource = 659709DE2F654908DEE3E5FBC72D4106DF61E6CA`
- `UXR22SourceReview = PASS_FOR_UX_R2_2_SOURCE_COMMIT_P0_0_P1_0_SOURCE_659709D`
- `UXR22SourceFixReview = PASS_FOR_UX_R2_2_SOURCE_FIX_COMMIT_P0_0_P1_0_SOURCE_40ED3FA`
- `UXR22DeterministicEvidence = BUILD_0_WARNINGS_REALTIME_24_778_COMMERCIAL_31_7084_TEXT_TOOLS_34_PARTS_16_MUTATIONS_STORY_34_UI_MATRIX_PASS_CHECKPOINT_HASHES_PRESERVED`
- `UXR22ActualInputObservation = PASS_THREE_CHAPTER_RESULTS_PLUS_FULL_FLOW_NON_SCORE`
- `UXR22MarkerNativeObservation = PASS_CLICK_SELECTION_CUSTOM_HOVER_ONLY_POPUP_NOT_OBSERVED`
- `UXR22KeyboardCandidateObservation = PASS`
- `UXR22ActiveFloodSolidFillObservation = PASS`
- `UXR22ClosureReview = PASS_FOR_UX_R2_2_CLOSURE_MAJOR_UNIT_P0_0_P1_0_SOURCE_CF6398A`
- `TutorialThreeChapterReachability = FORMATIVE_DIRECT_PLAY_PASS_THROUGH_SECOND_SOURCE`
- `UXR23GateStatus = IMPLEMENTED_REVIEWED_NATIVE_OBSERVATION_DEFERRED`
- `UXR23GateOpeningReview = PASS_FOR_UX_R2_3_GATE_OPENING_P0_0_P1_0_SOURCE_B0383D6`
- `UXR23ProductSourceAuthority = PASS_SOURCE_FIX_D85BB3F`
- `UXR23SourceReview = PASS_FOR_UX_R2_3_SOURCE_FIX_COMMIT_P0_0_P1_0`
- `NorthBankPromiseNativeReachability = NOT_OBSERVED_USER_STOP`
- `NorthBankPromiseDeadlineRail = IMPLEMENTED_REVIEWED`
- `InterchapterCalendarTransition = IMPLEMENTED_REVIEWED`
- `UXR0ClosureReview = PASS_P0_0_P1_0_COMMIT_746C0AA`
- `NativeCandidateAuthority = PASS_SOURCE_REVISION_379E980_SHA256_373785E4`
- `EvaluatorProducerAuthority = FOUR_GIT_BLOBS_MATCH_CLT_GIT_REPLACE_AND_LAZY_FETCH_DISABLED`
- `TargetedCheckpointAuthority = TWO_POSITIVE_THREE_REJECTION_INDEPENDENT_REPLAY_PASS`
- `UXR1CandidateRouteReview = PASS_P0_0_P1_0_SOURCE_379E980`
- `SessionAttemptAuthority = PASS_SOURCE_REVISION_5A31FF3_PRODUCER_SHA256_FAEA99AC`
- `UXR1SessionAttemptReview = PASS_P0_0_P1_0_SOURCE_5A31FF3`
- `EvaluationChainParentAuthority = PASS_SOURCE_REVISION_74BA725_PRODUCER_SHA256_D87E6054`
- `UXR1ChainParentReview = PASS_P0_0_P1_0_SOURCE_74BA725`
- `CurrentRouteArtifactAuthority = PASS_SOURCE_REVISION_A270339_PRODUCER_SHA256_225696AD`
- `UXR1CurrentRouteArtifactReview = PASS_P0_0_P1_0_SOURCE_A270339`
- `ControlledCodexTranscriptAuthority = PASS_LOCAL_NON_PLATFORM_SOURCE_2B0B6EE_RECEIPT_SHA256_F7C17C4A`
- `UXR1ControlledTranscriptReview = PASS_SUBUNIT_P0_0_P1_0_SOURCE_2B0B6EE`
- `UXR1ClosureReview = PASS_P0_0_P1_0_SOURCE_2B0B6EE`
- `NativeEvaluatorAuthority = COMPLETE_CANDIDATE_ROUTE_SESSION_CHAIN_PARENT_BLOCKED_ARTIFACT_AND_CONTROLLED_TRANSCRIPT`
- `UXR21GateOpeningReview = PASS_P0_0_P1_0`
- `LiveTestDefault = TARGETED_DETERMINISTIC_CHECKPOINT`
- `TargetedCheckpointRuntime = A1_NORMAL_READY_AND_A1_CONSTRUCTION_DUE_1M_READY`
- `FullFlowE2EPolicy = EXCEPTION_ONLY`
- 기본 장면은 `CommercialMain`이다.
- R1/R2 기반과 완료된 UX-R2.1·UX-R2.2 source 및 실제 입력 record는 보존한다. UX-R2.3은 exact
  cumulative 4장 route, calendar transition과 promise deadline/Keep/Defer를 source `aee4932`와
  fix `d85bb3f`에 구현했고 두 bounded review는 P0 0/P1 0이다. native observation은 사용자 지시로
  보류하며, 현재는 local `main`의 provenanced G3 draw layer만 `RealtimePlaceholderMap`에 적용한다.
  이 작업도 `CommercialUXProxy`나 사람 미감 증거를 만들지 않는다.
- `./assets` 네 이미지는 visual reference authority이며 runtime·규칙·숫자 authority가 아니다.
- 이전 HTML/CSS 목표 화면은 현재 스타일 목표에서 폐기했다. 파일은 Git commit `9aceaf7`로 복구할 수
  있고 현재 증거로 사용하지 않는다.

## 작업 읽기 순서

1. 루트 [README](../README.md)를 읽는다.
2. 루트가 지목한 [활성 A1-G3 scope](scopes/ASSET_STYLE_REALTIME_GAME.md#a1-g3--기존-g3-시각-적용-carve-out--활성)를
   처음부터 끝까지 읽는다.
3. UX 평가 사실을 다루면 [실시간 상용 UX 87 계약](scopes/COMMERCIAL_UX_87.md)을 추가로 읽는다.
4. 작업 질문의 소유 문서 하나만 추가로 읽는다.
5. 승인된 gate 밖 코드·data·asset·scene은 만들지 않는다.
6. 작업이 끝나면 체크리스트와 현재 상태를 같은 변경에서 갱신한다.

과거 상세 내용이 필요하면 새 현재 문서로 되살리지 말고
[아카이브 안내](archive/README.md)의 Git 조회 방법을 사용한다.
